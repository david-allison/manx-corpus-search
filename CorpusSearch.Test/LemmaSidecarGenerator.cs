using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CorpusSearch.Dependencies.Lucene;
using CorpusSearch.Services;
using CorpusSearch.Test.LemmaAdjudication;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>
/// Generates the lemma disambiguation artifacts for manx-lemma-data
/// (ANALYSIS-lemma-ambiguity.md; HANDOFF-ud-eval.md §Follow-up):
///
/// 1. lemma.overrides.seed.tsv — form-level resolutions seeded from decisive
///    UD_Manx-Cadhan majorities (>=3 observations, >=80% one reading), for
///    human review.
/// 2. lemma.sidecar.tsv — per-occurrence resolutions for the remaining
///    ambiguous forms, scored against each corpus line's English translation
///    via manx.json glosses. Keyed by (manifest Ident, Manx-cell hash, token
///    index) so corpus edits make entries miss (falling back to all
///    candidates) rather than mis-apply.
///
/// The gloss scorer's precision is measured against the treebank's text_en
/// sentences and printed; a resolution REMOVES readings from the index, so
/// precision matters more than coverage.
///
/// Run: dotnet test CorpusSearch.Test --filter FullyQualifiedName~LemmaSidecarGenerator --logger "console;verbosity=detailed"
/// Output dir: $LEMMA_SIDECAR_OUT (required).
/// </summary>
[TestFixture]
[Explicit("artifact generator over the full corpus, not a regression test")]
public class LemmaSidecarGenerator
{
    /// <summary>UD evidence needed before a form-level override is seeded</summary>
    private const int DecisiveMinObservations = 3;
    private const double DecisiveMajority = 0.8;

    /// <summary>English words in more than this share of translated corpus lines
    /// carry no evidence ("the" would otherwise resolve every article)</summary>
    private const double MaxEvidenceLineShare = 0.05;

    private sealed record Occurrence(string DocIdent, string LineHash, int TokenIndex, string Form,
        IReadOnlyList<string> Candidates, string English);

    private static string DisplayKey(string lemma)
    {
        return AdjudicationCommon.DisplayKey(lemma);
    }

    [Test]
    public void Generate()
    {
        var outDir = Environment.GetEnvironmentVariable("LEMMA_SIDECAR_OUT");
        Assert.That(outDir, Is.Not.Null.And.Not.Empty, "set LEMMA_SIDECAR_OUT to the output directory");
        Directory.CreateDirectory(outDir!);

        var table = LemmaTable.Instance;
        var displayById = AdjudicationCommon.DisplayLemmaById();
        var glosses = AdjudicationCommon.LoadGlosses();
        var sentences = AdjudicationCommon.TreebankSentences();

        // ---- form-level layer: decisive UD majorities -> overrides seed ----
        var readingsByForm = new Dictionary<string, Dictionary<string, int>>();
        foreach (var word in sentences.SelectMany(x => x.Words))
        {
            var form = LemmaTable.NormalizeForm(word.Form);
            if (table.CandidatesFor(form).Count < 2)
            {
                continue;
            }
            if (!readingsByForm.TryGetValue(form, out var readings))
            {
                readingsByForm[form] = readings = [];
            }
            var lemma = DisplayKey(word.Lemma);
            readings[lemma] = readings.GetValueOrDefault(lemma) + 1;
        }

        // every reading UD ever attests for a form must survive resolution; the
        // eval uses train-file attestations only, so the veto isn't graded on
        // its own labels
        var attestedTrain = AdjudicationCommon.AttestedReadings(sentences, trainOnly: true);
        var attestedAll = AdjudicationCommon.AttestedReadings(sentences, trainOnly: false);

        var overrides = new Dictionary<string, (IReadOnlyList<string> Ids, string Evidence)>();
        var undecidableDecisive = 0;
        foreach (var (form, readings) in readingsByForm)
        {
            var total = readings.Values.Sum();
            var (majority, count) = readings.OrderByDescending(x => x.Value).Select(x => (x.Key, x.Value)).First();
            if (total < DecisiveMinObservations || count / (double)total < DecisiveMajority)
            {
                continue;
            }
            var ids = table.CandidatesFor(form)
                .Where(id => DisplayKey(displayById.GetValueOrDefault(id, id)) == majority)
                .ToList();
            // the majority reading isn't among the candidates (an eval disagreement):
            // nothing to choose between, leave the form alone
            if (ids.Count == 0 || ids.Count == table.CandidatesFor(form).Count)
            {
                undecidableDecisive++;
                continue;
            }
            overrides[form] = (ids, $"{count}/{total}");
        }

        // ---- corpus pass 1: English-line document frequency ----
        var documents = OpenDataLoader.LoadDocumentsFromFile(null)
            .Concat(ClosedDataLoader.LoadDocumentsFromFile())
            .ToList();
        var lineFrequency = new Dictionary<string, int>();
        long translatedLines = 0;
        AdjudicationCommon.ForEachManxLine(documents, (_, line) =>
        {
            if (string.IsNullOrWhiteSpace(line.English))
            {
                return;
            }
            translatedLines++;
            foreach (var word in AdjudicationCommon.EnglishWords(line.English).Distinct())
            {
                lineFrequency[word] = lineFrequency.GetValueOrDefault(word) + 1;
            }
        });
        bool CarriesEvidence(string word) =>
            lineFrequency.GetValueOrDefault(word) / (double)translatedLines <= MaxEvidenceLineShare;

        // ---- scorer precision against the treebank's text_en ----
        List<string>? VetoedResolve(string form, IReadOnlyList<string> candidates, HashSet<string> english,
            Dictionary<string, HashSet<string>> attested)
        {
            var chosen = Resolve(candidates, english, glosses, displayById);
            if (chosen == null)
            {
                return null;
            }
            var attestedReadings = attested.GetValueOrDefault(form);
            var dropsAttested = attestedReadings != null && candidates.Except(chosen)
                .Any(id => attestedReadings.Contains(DisplayKey(displayById.GetValueOrDefault(id, id))));
            return dropsAttested ? null : chosen;
        }

        long evalEligible = 0, evalResolved = 0, evalCorrect = 0;
        var wrongSample = new List<string>();
        foreach (var sentence in sentences.Where(x => x.IsTestFile && !string.IsNullOrWhiteSpace(x.TextEn)))
        {
            var english = AdjudicationCommon.EnglishWords(sentence.TextEn!).Where(CarriesEvidence).ToHashSet();
            foreach (var word in sentence.Words)
            {
                var form = LemmaTable.NormalizeForm(word.Form);
                var candidates = table.CandidatesFor(form);
                if (candidates.Count < 2 || overrides.ContainsKey(form))
                {
                    continue;
                }
                evalEligible++;
                var chosen = VetoedResolve(form, candidates, english, attestedTrain);
                if (chosen == null)
                {
                    continue;
                }
                evalResolved++;
                var udLemma = DisplayKey(word.Lemma);
                if (chosen.Any(id => DisplayKey(displayById.GetValueOrDefault(id, id)) == udLemma))
                {
                    evalCorrect++;
                }
                else if (wrongSample.Count < 15)
                {
                    wrongSample.Add($"  {form} -> [{string.Join(",", chosen)}] but UD={udLemma} | {sentence.TextEn}");
                }
            }
        }

        // ---- corpus pass 2: per-occurrence sidecar rows ----
        long ambiguousTokens = 0, overrideTokens = 0, sidecarTokens = 0;
        var sidecar = new List<Occurrence>();
        var chosenByOccurrence = new List<string>();
        AdjudicationCommon.ForEachManxLine(documents, (document, line) =>
        {
            var tokenIndex = -1;
            HashSet<string>? english = null;
            string? lineHash = null;
            foreach (var token in AdjudicationCommon.Tokenize(line.NormalizedManx))
            {
                tokenIndex++;
                var candidates = table.CandidatesFor(token);
                if (candidates.Count < 2)
                {
                    continue;
                }
                ambiguousTokens++;
                if (overrides.ContainsKey(token))
                {
                    overrideTokens++;
                    continue;
                }
                if (string.IsNullOrWhiteSpace(line.English))
                {
                    continue;
                }
                english ??= AdjudicationCommon.EnglishWords(line.English).Where(CarriesEvidence).ToHashSet();
                var chosen = VetoedResolve(token, candidates, english, attestedAll);
                if (chosen == null)
                {
                    continue;
                }
                sidecarTokens++;
                lineHash ??= AdjudicationCommon.Hash(line.Manx ?? "");
                sidecar.Add(new Occurrence(document, lineHash, tokenIndex, token, candidates, line.English!));
                chosenByOccurrence.Add(string.Join(",", chosen));
            }
        });

        // ---- write artifacts ----
        var stamp = $"# generated by CorpusSearch.Test/LemmaSidecarGenerator.cs; table=manx-lemma-data, treebank=UD_Manx-Cadhan (pinned by the consumer's submodules)";
        var overridesPath = Path.Combine(outDir!, "lemma.overrides.seed.tsv");
        using (var writer = new StreamWriter(overridesPath))
        {
            writer.WriteLine(stamp);
            writer.WriteLine("# form-level: every occurrence of `form` resolves to `lemmaIds`; seeded from");
            writer.WriteLine($"# decisive UD majorities (>={DecisiveMinObservations} observations, >={DecisiveMajority:P0} one reading) - review before adopting");
            writer.WriteLine("form\tlemmaIds\tudEvidence");
            foreach (var (form, (ids, evidence)) in overrides.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                writer.WriteLine($"{form}\t{string.Join(",", ids)}\t{evidence}");
            }
        }

        var sidecarPath = Path.Combine(outDir!, "lemma.sidecar.tsv");
        using (var writer = new StreamWriter(sidecarPath))
        {
            writer.WriteLine(stamp);
            writer.WriteLine("# per-occurrence: gloss-scored against the line's English cell; lineHash =");
            writer.WriteLine("# first 16 hex of SHA-256(UTF-8 raw Manx cell) - a miss falls back to all candidates");
            writer.WriteLine($"# EXPERIMENTAL: measured {evalCorrect / (double)Math.Max(1, evalResolved):P1} precision on held-out UD text_en ({evalCorrect}/{evalResolved}); a wrong row hides the true reading from lemma search");
            writer.WriteLine("docId\tlineHash\ttokenIndex\tform\tlemmaIds");
            foreach (var (occurrence, chosen) in sidecar.Zip(chosenByOccurrence))
            {
                writer.WriteLine($"{occurrence.DocIdent}\t{occurrence.LineHash}\t{occurrence.TokenIndex}\t{occurrence.Form}\t{chosen}");
            }
        }

        var report = new StringBuilder();
        report.AppendLine($"overrides seed: {overrides.Count} forms ({undecidableDecisive} decisive forms skipped: majority reading not a candidate)");
        report.AppendLine($"gloss scorer on treebank text_en: resolved {evalResolved:N0}/{evalEligible:N0} eligible ({evalResolved / (double)evalEligible:P1}), precision {evalCorrect / (double)Math.Max(1, evalResolved):P1}");
        if (wrongSample.Count > 0)
        {
            report.AppendLine("wrong resolutions (sample):");
            wrongSample.ForEach(x => report.AppendLine(x));
        }
        report.AppendLine($"corpus: {ambiguousTokens:N0} ambiguous tokens; {overrideTokens:N0} ({overrideTokens / (double)ambiguousTokens:P1}) resolved by overrides; {sidecarTokens:N0} ({sidecarTokens / (double)ambiguousTokens:P1}) by sidecar");
        report.AppendLine($"unresolved (stay fully ambiguous): {ambiguousTokens - overrideTokens - sidecarTokens:N0} ({(ambiguousTokens - overrideTokens - sidecarTokens) / (double)ambiguousTokens:P1})");
        report.AppendLine($"wrote {overridesPath} ({new FileInfo(overridesPath).Length / 1024}KB) and {sidecarPath} ({new FileInfo(sidecarPath).Length / 1024 / 1024}MB, {sidecar.Count:N0} rows)");
        TestContext.Progress.WriteLine(report.ToString());
    }

    /// <summary>The candidates backed by discriminative gloss evidence in the line's
    /// English; null unless every unchosen candidate scored zero (conservative: a
    /// resolution removes readings from the index)</summary>
    private static List<string>? Resolve(IReadOnlyList<string> candidates, HashSet<string> english,
        Dictionary<string, HashSet<string>> glosses, Dictionary<string, string> displayById)
    {
        var glossSets = candidates
            .Select(id => glosses.GetValueOrDefault(LemmaTable.NormalizeForm(displayById.GetValueOrDefault(id, id)), []))
            .ToList();
        var scores = new int[candidates.Count];
        for (var i = 0; i < candidates.Count; i++)
        {
            foreach (var word in glossSets[i])
            {
                var discriminative = !Enumerable.Range(0, candidates.Count)
                    .Any(j => j != i && glossSets[j].Contains(word));
                if (discriminative && english.Contains(word))
                {
                    scores[i]++;
                }
            }
        }
        var chosen = Enumerable.Range(0, candidates.Count).Where(i => scores[i] > 0).ToList();
        if (chosen.Count == 0 || chosen.Count == candidates.Count)
        {
            return null;
        }
        // only adjudicate semantic homographs: when a kept and a dropped
        // candidate share any gloss word they are spelling/mutation variants of
        // one meaning (chrie/crie 'shake'), and evidence can only be lucky there
        if (chosen.Any(i => Enumerable.Range(0, candidates.Count)
                .Any(j => scores[j] == 0 && glossSets[i].Overlaps(glossSets[j]))))
        {
            return null;
        }
        return chosen.Select(i => candidates[i]).ToList();
    }

}
