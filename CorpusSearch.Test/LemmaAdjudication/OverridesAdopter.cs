using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CorpusSearch.Dependencies.Lucene;
using NUnit.Framework;

namespace CorpusSearch.Test.LemmaAdjudication;

/// <summary>
/// Writes lemma.overrides.tsv — the first adopted form-level layer — from the
/// two staging files, applying <see cref="AdoptionGate"/> (DESIGN-disambiguation.md
/// Phase 2):
///
/// - lemma.overrides.seed.tsv (UD-treebank majorities): same-group narrowings
///   adopt as they stand; lexeme-dropping rows adopt only on unanimity at 10+
///   observations (98% at 50+). The refused stay behind for per-line
///   adjudication — they are hypotheses, not facts.
/// - lemma.overrides.candidates.tsv (unanimous per-occurrence LLM verdicts,
///   10+ each): adopt after this skim; each was judged with its line and
///   English translation in view.
///
/// A form both files claim must agree at the display-lemma level or neither
/// row adopts. Rows whose ids no longer strictly narrow the current table are
/// dropped here for the same reason LemmaResolver.Load would drop them.
///
/// Run: LEMMA_DATA_DIR=&lt;manx-lemma-data&gt;
///   dotnet test CorpusSearch.Test --filter FullyQualifiedName~OverridesAdopter
/// </summary>
[TestFixture]
[Explicit("artifact generator over the staging files, not a regression test")]
public class OverridesAdopter
{
    private sealed record Row(string Form, string[] Ids, string Evidence, string Source);

    [Test]
    public void Adopt()
    {
        var dataDir = Environment.GetEnvironmentVariable("LEMMA_DATA_DIR");
        Assert.That(dataDir, Is.Not.Null.And.Not.Empty, "set LEMMA_DATA_DIR to the manx-lemma-data checkout");

        var table = LemmaTable.Instance;
        var displayById = AdjudicationCommon.DisplayLemmaById();
        var equivalencesPath = Path.Combine(dataDir!, "lemma.equivalences.seed.tsv");
        var equivalenceRoot = File.Exists(equivalencesPath)
            ? AdjudicationCommon.EquivalenceGroups(equivalencesPath)
            : id => id;

        var rows = ReadRows(Path.Combine(dataDir!, "lemma.overrides.seed.tsv"), "UD")
            .Concat(ReadRows(Path.Combine(dataDir!, "lemma.overrides.candidates.tsv"), "LLM"))
            .ToList();

        var report = new StringBuilder();
        var adopted = new List<Row>();
        var refused = new List<string>();

        foreach (var group in rows.GroupBy(x => x.Form).OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var candidates = table.CandidatesFor(group.Key);
            var claims = group
                .Select(row => (Row: row, Displays: row.Ids
                    .Select(id => AdjudicationCommon.DisplayKey(displayById.GetValueOrDefault(id, id)))
                    .ToHashSet()))
                .ToList();
            if (claims.Count > 1 && claims.Select(x => x.Displays).Distinct(HashSet<string>.CreateSetComparer()).Count() > 1)
            {
                refused.Add($"{group.Key}: the seed and the candidates disagree "
                            + $"({string.Join(" vs ", claims.Select(x => string.Join(",", x.Row.Ids)))}) - kept per-line");
                continue;
            }

            // one claim (or two agreeing: the better-evidenced stands)
            var row = group.OrderByDescending(x => x.Source == "LLM").First();
            if (row.Ids.Length == 0 || row.Ids.Length >= candidates.Count || !row.Ids.All(candidates.Contains))
            {
                refused.Add($"{row.Form}: no longer strictly narrows the table's candidates - regenerate the staging file");
                continue;
            }
            var dropped = AdoptionGate.DroppedDisplays(candidates, row.Ids, equivalenceRoot, displayById);
            if (dropped.Count > 0)
            {
                var refusal = AdoptionGate.RefusalOf(AdoptionGate.Parse(row.Evidence));
                if (refusal != null)
                {
                    refused.Add($"{row.Form} -> {string.Join(",", row.Ids)} drops {string.Join(",", dropped)}: {refusal}");
                    continue;
                }
            }
            adopted.Add(row with
            {
                Evidence = row.Source == "LLM" ? row.Evidence : $"{row.Evidence} UD"
            });
        }

        var outPath = Path.Combine(dataDir!, "lemma.overrides.tsv");
        using (var writer = new StreamWriter(outPath))
        {
            writer.WriteLine("# adopted form-level resolutions: every occurrence of `form` resolves to `lemmaIds`, context-free");
            writer.WriteLine("# gated by CorpusSearch.Test/LemmaAdjudication/OverridesAdopter (AdoptionGate): same-lexeme");
            writer.WriteLine("# narrowings freely; a row dropping a lexeme group only on unanimous evidence at 10+");
            writer.WriteLine("# observations - the refused stay in the seed for per-line adjudication");
            writer.WriteLine("form\tlemmaIds\tevidence");
            foreach (var row in adopted.OrderBy(x => x.Form, StringComparer.Ordinal))
            {
                writer.WriteLine($"{row.Form}\t{string.Join(",", row.Ids)}\t{row.Evidence}");
            }
        }

        report.AppendLine($"adopted {adopted.Count} of {rows.GroupBy(x => x.Form).Count()} staged forms -> {outPath}");
        report.AppendLine($"refused {refused.Count}:");
        refused.ForEach(x => report.AppendLine($"  {x}"));
        TestContext.Progress.WriteLine(report.ToString());
    }

    private static IEnumerable<Row> ReadRows(string path, string source)
    {
        if (!File.Exists(path))
        {
            yield break;
        }
        foreach (var line in File.ReadLines(path))
        {
            if (line.StartsWith('#') || line.StartsWith("form\t") || string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            var columns = line.Split('\t');
            if (columns.Length >= 2)
            {
                yield return new Row(columns[0], columns[1].Split(','),
                    columns.Length > 2 ? columns[2] : "", source);
            }
        }
    }
}
