using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CorpusSearch.Dependencies.Lucene;
using CorpusSearch.Services;
using NUnit.Framework;

namespace CorpusSearch.Test.LemmaAdjudication;

/// <summary>
/// Re-keys lemma.sidecar.tsv against the current corpus. The sidecar is keyed
/// by a hash of each line's normalized token stream, so load-time changes to
/// the stream (the verse-reference extraction stripped markers like
/// "Genesis:1:1." and "[MS 1 Thessalonians 2.16]") orphan rows: the
/// resolutions are still right, only their keys and token indexes moved.
///
/// Matching, per document: an unchanged key stays; an orphaned line is found
/// again through its English cell's hash (where only the Manx side changed),
/// or - when the English was trimmed too (the leading-number docs) - by a
/// unique forms-fingerprint among the document's unclaimed lines. Token
/// indexes shift by the constant the line's matchable rows agree on (markers
/// were leading, so the surviving stream is a suffix), and forms re-fold
/// through the current normalization (the combining-mark fold renamed
/// accented forms since the export). Rows whose token no longer exists (the
/// marker words themselves, reference-only rows) drop individually - that is
/// the correct outcome, not a loss.
///
/// Run: LEMMA_SIDECAR_DIR=~/Work/manx-lemma-data
///   dotnet test CorpusSearch.Test --filter FullyQualifiedName~SidecarRekeyer
/// </summary>
[TestFixture]
[Explicit("artifact maintenance over the full corpus, not a regression test")]
public class SidecarRekeyer
{
    private sealed record Row(string DocId, string Key, string EnglishHash, int TokenIndex,
        string Form, string LemmaIds, string Tier, string HumanVerified);

    private sealed record Line(string Key, string EnglishHash, List<string> Tokens, string? Reference);

    [Test]
    public void Rekey()
    {
        var dir = Environment.GetEnvironmentVariable("LEMMA_SIDECAR_DIR");
        Assert.That(dir, Is.Not.Null.And.Not.Empty, "set LEMMA_SIDECAR_DIR to the artifact directory");
        var path = Path.Combine(dir!, "lemma.sidecar.tsv");

        var header = new List<string>();
        var rows = new List<Row>();
        foreach (var line in File.ReadLines(path))
        {
            if (line.StartsWith('#') || line.StartsWith("docId\t"))
            {
                header.Add(line);
                continue;
            }
            var c = line.Split('\t');
            rows.Add(new Row(c[0], c[1], c[2], int.Parse(c[3]), c[4], c[5], c[6], c[7]));
        }

        // the corpus as the index sees it now (prepared lines)
        var linesByDoc = new Dictionary<string, List<Line>>();
        var documents = OpenDataLoader.LoadDocumentsFromFile(null)
            .Concat(ClosedDataLoader.LoadDocumentsFromFile())
            .ToList();
        AdjudicationCommon.ForEachManxLine(documents, (doc, line) =>
        {
            if (!linesByDoc.TryGetValue(doc, out var lines))
            {
                linesByDoc[doc] = lines = [];
            }
            lines.Add(new Line(
                AdjudicationCommon.LineKey(line.Manx ?? ""),
                AdjudicationCommon.Hash(line.English ?? ""),
                AdjudicationCommon.Tokenize(line.Manx ?? ""),
                line.Reference));
        });

        // the extraction moved each line's marker into Reference: the old key
        // is reconstructible as hash(tokens(reference) + tokens(manx)), and
        // the index shift is exactly the marker's token count
        var byOldKey = new Dictionary<(string Doc, string OldKey), (Line Line, int Shift)>();
        foreach (var (doc, lines) in linesByDoc)
        {
            foreach (var line in lines.Where(x => !string.IsNullOrWhiteSpace(x.Reference)))
            {
                var markerTokens = AdjudicationCommon.Tokenize(line.Reference!);
                if (markerTokens.Count == 0)
                {
                    continue;
                }
                var oldKey = CorpusSearch.Dependencies.Lucene.LemmaResolver.LineKey(
                    markerTokens.Concat(line.Tokens));
                byOldKey.TryAdd((doc, oldKey), (line, markerTokens.Count));
            }
        }

        long unchanged = 0, rekeyed = 0, dropped = 0;
        var droppedSample = new List<string>();
        var output = new List<Row>();
        var emitted = new HashSet<(string, string, int)>();

        var claimed = new HashSet<(string Doc, Line Line)>();
        foreach (var lineGroup in rows.GroupBy(x => (x.DocId, x.Key)))
        {
            var docLines = linesByDoc.GetValueOrDefault(lineGroup.Key.DocId, []);
            var group = lineGroup
                .Select(row => row with { Form = LemmaTable.NormalizeForm(row.Form) })
                .ToList();
            if (docLines.Any(x => x.Key == lineGroup.Key.Key))
            {
                // stream unchanged: refresh the staleness marker, keep the rest
                var current = docLines.First(x => x.Key == lineGroup.Key.Key);
                foreach (var row in group)
                {
                    if (emitted.Add((row.DocId, row.Key, row.TokenIndex)))
                    {
                        unchanged++;
                        output.Add(row with { EnglishHash = current.EnglishHash });
                    }
                }
                continue;
            }

            // reference-extracted: the reconstructed old key is deterministic
            if (byOldKey.TryGetValue((lineGroup.Key.DocId, lineGroup.Key.Key), out var viaReference))
            {
                claimed.Add((lineGroup.Key.DocId, viaReference.Line));
                foreach (var row in group)
                {
                    var index = row.TokenIndex - viaReference.Shift;
                    if (index < 0 || index >= viaReference.Line.Tokens.Count
                        || viaReference.Line.Tokens[index] != row.Form)
                    {
                        dropped++;
                        continue;
                    }
                    if (emitted.Add((row.DocId, viaReference.Line.Key, index)))
                    {
                        rekeyed++;
                        output.Add(row with
                        {
                            Key = viaReference.Line.Key,
                            EnglishHash = viaReference.Line.EnglishHash,
                            TokenIndex = index,
                        });
                    }
                }
                continue;
            }

            // orphaned: the unchanged translation finds the line again; when
            // the English was trimmed too, a unique forms-fingerprint does
            var byEnglish = docLines
                .Where(x => x.EnglishHash == group[0].EnglishHash)
                .Select(x => (Line: x, Shift: ConsistentShift(group, x.Tokens)))
                .Where(x => x.Shift != null)
                .ToList();
            var candidates = byEnglish;
            if (candidates.Count == 0)
            {
                var byFingerprint = docLines
                    .Where(x => !claimed.Contains((lineGroup.Key.DocId, x)))
                    .Select(x => (Line: x, Shift: ConsistentShift(group, x.Tokens)))
                    .Where(x => x.Shift != null)
                    .ToList();
                // only an unambiguous fingerprint is trustworthy
                candidates = byFingerprint.Count == 1 ? byFingerprint : [];
            }
            if (candidates.Count == 0)
            {
                dropped += group.Count;
                if (droppedSample.Count < 12)
                {
                    droppedSample.Add($"  {lineGroup.Key.DocId} {lineGroup.Key.Key} ({group.Count} rows, forms: "
                                      + string.Join(",", group.Select(x => x.Form).Distinct().Take(5)) + ")");
                }
                continue;
            }
            var match = candidates[0];
            claimed.Add((lineGroup.Key.DocId, match.Line));
            foreach (var row in group)
            {
                var index = row.TokenIndex - match.Shift!.Value;
                // the row's own token must survive at the shifted position;
                // marker-word rows die here individually
                if (index < 0 || index >= match.Line.Tokens.Count
                    || match.Line.Tokens[index] != row.Form)
                {
                    dropped++;
                    continue;
                }
                if (emitted.Add((row.DocId, match.Line.Key, index)))
                {
                    rekeyed++;
                    output.Add(row with
                    {
                        Key = match.Line.Key,
                        EnglishHash = match.Line.EnglishHash,
                        TokenIndex = index,
                    });
                }
            }
        }

        using (var writer = new StreamWriter(path))
        {
            header.ForEach(writer.WriteLine);
            foreach (var row in output.OrderBy(x => x.DocId, StringComparer.Ordinal)
                         .ThenBy(x => x.Key, StringComparer.Ordinal).ThenBy(x => x.TokenIndex))
            {
                writer.WriteLine($"{row.DocId}\t{row.Key}\t{row.EnglishHash}\t{row.TokenIndex}"
                                 + $"\t{row.Form}\t{row.LemmaIds}\t{row.Tier}\t{row.HumanVerified}");
            }
        }

        var report = new StringBuilder();
        report.AppendLine($"sidecar rekey: {rows.Count:N0} rows in -> {output.Count:N0} out "
                          + $"({unchanged:N0} unchanged, {rekeyed:N0} rekeyed, {dropped:N0} dropped)");
        if (droppedSample.Count > 0)
        {
            report.AppendLine("dropped (sample):");
            droppedSample.ForEach(x => report.AppendLine(x));
        }
        TestContext.Progress.WriteLine(report.ToString());
    }

    /// <summary>The single non-negative index shift the line's matchable rows
    /// agree on (markers were leading, so the new stream is a suffix of the
    /// old). Rows whose form has no position at all (dead marker words) don't
    /// veto the rest; null only when no row can be placed.</summary>
    private static int? ConsistentShift(IEnumerable<Row> rows, List<string> tokens)
    {
        HashSet<int>? shifts = null;
        foreach (var row in rows)
        {
            var mine = new HashSet<int>();
            for (var j = 0; j < tokens.Count; j++)
            {
                if (tokens[j] == row.Form && row.TokenIndex - j >= 0)
                {
                    mine.Add(row.TokenIndex - j);
                }
            }
            if (mine.Count == 0)
            {
                continue;
            }
            if (shifts == null)
            {
                shifts = mine;
            }
            else
            {
                shifts.IntersectWith(mine);
                if (shifts.Count == 0)
                {
                    return null;
                }
            }
        }
        return shifts?.Min();
    }
}
