using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CorpusSearch.Services;
using NUnit.Framework;

namespace CorpusSearch.Test.LemmaAdjudication;

/// <summary>
/// A form-level override adopted on corpus-wide LLM unanimity is a claim about
/// every occurrence THEN IN THE CORPUS — its adoption premise is that each
/// line was read. The AdoptionGate re-tests rows against the table on every
/// build, but nothing re-tests them against the corpus: a document added
/// later can use the suppressed reading and be silently mis-resolved.
///
/// This check closes that gap for the LLM-unanimous lane: every corpus
/// occurrence of such a form must be covered by a sidecar verdict line. New
/// lines fail the check and are emitted as a re-vote worklist — the rule
/// stays a standing hypothesis that new evidence automatically re-tests.
///
/// UD-lane and same-lexeme rows are out of scope by design: same-lexeme
/// narrowings are naming conventions no future line can falsify, and
/// UD-evidence rows never claimed corpus coverage.
///
/// Run (needs the real corpus, so gated like the exporter):
///   LEMMA_DATA_DIR=&lt;manx-lemma-data&gt; [LEMMA_FRESHNESS_OUT=&lt;dir&gt;]
///   dotnet test CorpusSearch.Test --filter FullyQualifiedName~OverridesFreshness
/// </summary>
[TestFixture]
[Explicit("full-corpus scan over the data checkout, not a regression test")]
public class OverridesFreshnessTest
{
    [Test]
    public void EveryLlmLaneOccurrenceIsCovered()
    {
        var dataDir = Environment.GetEnvironmentVariable("LEMMA_DATA_DIR");
        Assert.That(dataDir, Is.Not.Null.And.Not.Empty, "set LEMMA_DATA_DIR to the manx-lemma-data checkout");

        // the LLM-unanimous lane only: rows whose adoption premise was
        // "every line was read"
        var llmForms = new HashSet<string>();
        foreach (var line in File.ReadLines(Path.Combine(dataDir!, "lemma.overrides.tsv")))
        {
            if (line.StartsWith('#') || line.StartsWith("form\t") || string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            var columns = line.Split('\t');
            if (columns.Length >= 3 && columns[2].Contains("LLM-unanimous", StringComparison.Ordinal))
            {
                llmForms.Add(columns[0]);
            }
        }

        // (key, form) pairs the sidecar has verdicts for
        var covered = new HashSet<(string Key, string Form)>();
        foreach (var line in File.ReadLines(Path.Combine(dataDir!, "lemma.sidecar.tsv")))
        {
            if (line.StartsWith('#') || line.StartsWith("docId\t"))
            {
                continue;
            }
            var columns = line.Split('\t');
            if (columns.Length > 4)
            {
                covered.Add((columns[1], columns[4].ToLowerInvariant()));
            }
        }

        var documents = OpenDataLoader.LoadDocumentsFromFile(null)
            .Concat(ClosedDataLoader.LoadDocumentsFromFile())
            .ToList();
        var uncovered = new List<string>();
        var seenKeys = new HashSet<string>();
        AdjudicationCommon.ForEachManxLine(documents, (docId, line) =>
        {
            // untranslated lines were never adjudicable and are outside the
            // adoption premise (METHOD.md: the coverage asymmetry is by design)
            if (string.IsNullOrWhiteSpace(line.English))
            {
                return;
            }
            var key = AdjudicationCommon.LineKey(line.NormalizedManx);
            if (!seenKeys.Add(key))
            {
                return;
            }
            foreach (var form in AdjudicationCommon.Tokenize(line.NormalizedManx).Distinct())
            {
                if (llmForms.Contains(form) && !covered.Contains((key, form)))
                {
                    uncovered.Add($"{form}\t{docId}\t{key}\t{line.NormalizedManx.Trim()}");
                }
            }
        });

        var outDir = Environment.GetEnvironmentVariable("LEMMA_FRESHNESS_OUT");
        if (!string.IsNullOrEmpty(outDir))
        {
            Directory.CreateDirectory(outDir);
            var sb = new StringBuilder("form\tdocId\tkey\tmanx\n");
            foreach (var row in uncovered.OrderBy(x => x, StringComparer.Ordinal))
            {
                sb.Append(row).Append('\n');
            }
            File.WriteAllText(Path.Combine(outDir, "overrides-freshness-worklist.tsv"), sb.ToString());
        }

        var byForm = uncovered
            .Select(x => x.Split('\t')[0])
            .GroupBy(x => x)
            .OrderByDescending(g => g.Count())
            .Select(g => $"{g.Key} ×{g.Count()}")
            .ToList();
        Assert.That(uncovered, Is.Empty,
            $"{uncovered.Count} corpus occurrences of LLM-lane override forms have no sidecar verdict "
            + $"(new or changed lines since adoption — re-vote them or demote the rows): "
            + string.Join(", ", byForm.Take(20)));
    }
}
