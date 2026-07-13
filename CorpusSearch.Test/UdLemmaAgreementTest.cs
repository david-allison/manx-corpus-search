using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CorpusSearch.Dependencies.Lucene;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>
/// The consumer seatbelt on lemma-lookup quality (HANDOFF-ud-eval.md): the vendored
/// cregeen.tsv, read through the shipped lookup (<see cref="LemmaTable"/> normalization
/// and clitic fallback), must keep agreeing with the UD_Manx-Cadhan treebank. Pins a
/// floor so table re-vendors and normalization/filter changes can't silently regress.
///
/// Reference data: https://github.com/UniversalDependencies/UD_Manx-Cadhan
/// (CC BY-SA 4.0), pinned by the CorpusSearch.Test/external/UD_Manx-Cadhan submodule
/// commit. Bumping the treebank is a deliberate submodule-pointer commit that
/// re-baselines the floors.
/// </summary>
[TestFixture]
public class UdLemmaAgreementTest
{
    // Floors sit just below the values measured at the pinned treebank commit
    // (9069716) and table version: 89.60% coverage, 97.50% agreement with the
    // names.tsv supplement loaded (88.91/97.43 before it) — matching
    // cregeen-nvh's generation-side eval (89.6/97.5) exactly, so the C# lookup
    // reimplementation has no divergence from the F# one. The metric is
    // deterministic; the small slack only tolerates deliberate trade-offs.
    private const double CoverageFloor = 0.8950;
    private const double AgreementFloor = 0.9740;

    /// <summary>Not evaluated: punctuation and numerals aren't dictionary material,
    /// X marks foreign/unanalysable words</summary>
    private static readonly string[] SkippedUpos = ["PUNCT", "NUM", "X"];

    private sealed record Word(string Form, string Lemma, string Upos);

    [Test]
    public void VendoredTableAgreesWithTheTreebank()
    {
        var words = TreebankWords().Where(x => !SkippedUpos.Contains(x.Upos)).ToList();
        var table = LemmaTable.Instance;

        var covered = 0;
        var agreed = 0;
        var disagreementCounts = new Dictionary<(string Form, string UdLemma, string Candidates), int>();
        foreach (var word in words)
        {
            var displayLemmas = table.DisplayLemmasFor(word.Form);
            if (displayLemmas.Count == 0)
            {
                displayLemmas = table.CliticDisplayLemmasFor(word.Form);
            }
            if (displayLemmas.Count == 0)
            {
                continue;
            }
            covered++;

            var udLemma = LemmaTable.NormalizeForm(word.Lemma);
            if (displayLemmas.Any(x => LemmaTable.NormalizeForm(x) == udLemma))
            {
                agreed++;
                continue;
            }
            var key = (LemmaTable.NormalizeForm(word.Form), udLemma, string.Join(", ", displayLemmas));
            disagreementCounts[key] = disagreementCounts.GetValueOrDefault(key) + 1;
        }

        var coverage = covered / (double)words.Count;
        var agreement = agreed / (double)covered;
        var report = Report(words.Count, covered, agreed, coverage, agreement, disagreementCounts);
        TestContext.Progress.WriteLine(report);

        Assert.Multiple(() =>
        {
            Assert.That(coverage, Is.GreaterThanOrEqualTo(CoverageFloor), report);
            Assert.That(agreement, Is.GreaterThanOrEqualTo(AgreementFloor), report);
        });
    }

    private static string Report(int considered, int covered, int agreed,
        double coverage, double agreement, Dictionary<(string Form, string UdLemma, string Candidates), int> disagreementCounts)
    {
        var report = new StringBuilder();
        report.AppendLine($"UD_Manx-Cadhan lemma eval: {considered} words considered");
        report.AppendLine($"  coverage:  {coverage:P2} ({covered}/{considered}; floor {CoverageFloor:P2})");
        report.AppendLine($"  agreement: {agreement:P2} ({agreed}/{covered}; floor {AgreementFloor:P2})");
        report.AppendLine("Top disagreements (count, form, UD lemma, table's display lemmas):");
        foreach (var (key, count) in disagreementCounts.OrderByDescending(x => x.Value).Take(20))
        {
            report.AppendLine($"  {count,4}  {key.Form}  ->  {key.UdLemma}  (table: {key.Candidates})");
        }
        return report.ToString();
    }

    /// <summary>
    /// The treebank's syntactic words. Comments, multiword-token ranges ("3-4") and
    /// empty nodes ("3.1") are skipped: contractions are evaluated through the split
    /// words UD annotates (whose forms may be clitic parts like "v'").
    /// </summary>
    private static IEnumerable<Word> TreebankWords()
    {
        var directory = Path.Combine(TestContext.CurrentContext.TestDirectory, "Resources", "UD_Manx-Cadhan");
        var files = Directory.Exists(directory) ? Directory.GetFiles(directory, "*.conllu") : [];
        Assert.That(files, Is.Not.Empty,
            "UD_Manx-Cadhan treebank missing from test output. " +
            "Initialise the submodule ('git submodule update --init') and rebuild.");

        foreach (var file in files.OrderBy(x => x))
        {
            foreach (var line in File.ReadLines(file))
            {
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }
                var columns = line.Split('\t');
                if (columns.Length < 4 || columns[0].Contains('-') || columns[0].Contains('.'))
                {
                    continue;
                }
                yield return new Word(Form: columns[1], Lemma: columns[2], Upos: columns[3]);
            }
        }
    }
}
