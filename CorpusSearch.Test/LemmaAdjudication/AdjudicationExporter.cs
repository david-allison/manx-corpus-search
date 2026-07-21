using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CorpusSearch.Dependencies.Lucene;
using CorpusSearch.Services;
using Newtonsoft.Json;
using NUnit.Framework;

namespace CorpusSearch.Test.LemmaAdjudication;

/// <summary>
/// Exports LLM adjudication requests for translation-based lemma
/// disambiguation (plan: multi-agent translate+lemmatize; the LLM runs are
/// Claude Code workflow subagents, not API calls — JSONL files are the
/// contract between this harness and the agents).
///
/// - eval-requests-*.jsonl / eval-gold.jsonl: UD_Manx-Cadhan sentences with
///   text_en — the labelled pilot set that gates the corpus run.
/// - corpus-requests-*.jsonl: corpus lines (deduplicated by content key) with
///   >=1 ambiguous token left unresolved by the form-level overrides layer.
///
/// A request line: {key, docId, manx, english, tokens: [{i, form,
/// candidates: [{id, lemma, gloss, link}]}]}. `key` is a hash of the
/// normalized Manx token stream (punctuation/case edits can't orphan rows;
/// token changes correctly invalidate). Verdicts come back as
/// verdicts-*.jsonl: {key, i, chosenIds, confidence}.
///
/// Run: LEMMA_ADJUDICATION_DIR=... [LEMMA_OVERRIDES_TSV=...]
///   dotnet test CorpusSearch.Test --filter FullyQualifiedName~AdjudicationExporter
///
/// LEMMA_OVERRIDES_TSV is the ADOPTED overrides layer (lemma.overrides.tsv):
/// forms it owns are settled and leave the pool. Never pass the seed — a seed
/// row is a hypothesis this run exists to test, and passing it once excluded
/// voddey from the adjudication that would have disproved its 3/3 dog reading
/// (DESIGN-disambiguation.md Phase 0b). Absent means nothing is adopted.
/// </summary>
[TestFixture]
[Explicit("artifact generator over the full corpus, not a regression test")]
public class AdjudicationExporter
{
    private const int EvalSentencesPerFile = 60;
    private const int CorpusLinesPerFile = 120;

    /// <summary>Any UD-attested reading missing from the candidates marks a table
    /// gap (ching -> kione, single-obs faagail/cronk): un-adjudicable, because
    /// the veto cannot protect a reading the table doesn't offer</summary>
    private const int GapMinObservations = 1;

    /// <summary>UD attesting two readings each above this share (with enough
    /// observations) marks a convention split (ny: DET is lemmatized ny 185x
    /// and yn 179x): ungradeable, so un-adjudicable</summary>
    private const double SplitShare = 0.3;

    private const int SplitMinObservations = 20;

    [Test]
    public void Export()
    {
        var outDir = Environment.GetEnvironmentVariable("LEMMA_ADJUDICATION_DIR");
        Assert.That(outDir, Is.Not.Null.And.Not.Empty, "set LEMMA_ADJUDICATION_DIR to the work directory");
        var overridesPath = Environment.GetEnvironmentVariable("LEMMA_OVERRIDES_TSV");
        Directory.CreateDirectory(outDir!);

        var table = LemmaTable.Instance;
        var displayById = AdjudicationCommon.DisplayLemmaById();
        var glossTexts = AdjudicationCommon.LoadGlossTexts();
        var linkTypes = AdjudicationCommon.LinkTypesByFormId();
        Dictionary<string, string[]> overrides = string.IsNullOrEmpty(overridesPath)
            ? []
            : AdjudicationCommon.LoadOverrides(overridesPath);

        // the equivalence layer over Cregeen: ids classified as one lexeme
        // collapse into a group; ids sharing a display headword always do.
        // Adjudication chooses between groups, so within-lexeme direction
        // (aarkey/faarkey, jeeg/jeeig) never reaches a per-line verdict
        var equivalencesPath = Environment.GetEnvironmentVariable("LEMMA_EQUIVALENCES_TSV");
        var equivalenceRoot = equivalencesPath != null
            ? AdjudicationCommon.EquivalenceGroups(equivalencesPath)
            : id => id;

        var groupCache = new Dictionary<string, (object[] Candidates, int Groups)>();
        (object[] Candidates, int Groups) Grouped(string form)
        {
            if (groupCache.TryGetValue(form, out var cached))
            {
                return cached;
            }
            var ids = table.CandidatesFor(form);
            var groupByKey = new Dictionary<string, string>();
            var displayAlias = new Dictionary<string, string>();
            var labels = new List<string>();
            var candidates = ids.Select(id =>
            {
                var lemma = displayById.GetValueOrDefault(id, id);
                var rootKey = equivalenceRoot(id);
                var displayKey = AdjudicationCommon.DisplayKey(lemma);
                // same display headword => same lexeme group, regardless of ids
                if (displayAlias.TryGetValue(displayKey, out var aliased))
                {
                    rootKey = aliased;
                }
                else
                {
                    displayAlias[displayKey] = rootKey;
                }
                if (!groupByKey.TryGetValue(rootKey, out var label))
                {
                    label = $"g{groupByKey.Count + 1}";
                    groupByKey[rootKey] = label;
                    labels.Add(label);
                }
                var glossKey = LemmaTable.NormalizeForm(lemma);
                var gloss = glossTexts.TryGetValue(glossKey, out var texts)
                    ? string.Join("; ", texts.Take(3))
                    : "";
                return (object)new
                {
                    id,
                    lemma,
                    gloss = gloss.Length > 200 ? gloss[..200] : gloss,
                    link = linkTypes.GetValueOrDefault((form, id), "self"),
                    group = label,
                };
            }).ToArray();
            var result = (candidates, labels.Count);
            groupCache[form] = result;
            return result;
        }

        object[] Candidates(string form)
        {
            return Grouped(form).Candidates;
        }

        var sentences = AdjudicationCommon.TreebankSentences();
        var attestedCounts = AdjudicationCommon.AttestedCounts(sentences);
        var unAdjudicableForms = new HashSet<string>();

        // an adjudicable token: ambiguous across >=2 lexeme groups, not already
        // resolved by the overrides layer, and gradeable - forms whose
        // candidates miss a UD-attested reading are table gaps, and forms UD
        // itself lemmatizes inconsistently are convention calls; both stay
        // fully ambiguous
        bool Adjudicable(string form)
        {
            if (table.CandidatesFor(form).Count < 2 || overrides.ContainsKey(form)
                || Grouped(form).Groups < 2)
            {
                return false;
            }
            if (!attestedCounts.TryGetValue(form, out var counts))
            {
                return true;
            }
            var candidateDisplays = table.CandidatesFor(form)
                .Select(id => AdjudicationCommon.DisplayKey(displayById.GetValueOrDefault(id, id)))
                .ToHashSet();
            var gap = counts.Any(x => x.Value >= GapMinObservations && !candidateDisplays.Contains(x.Key));
            var total = counts.Values.Sum();
            var split = total >= SplitMinObservations
                        && counts.Values.Count(x => x / (double)total >= SplitShare) >= 2;
            if (gap || split)
            {
                unAdjudicableForms.Add(form);
                return false;
            }
            return true;
        }

        // ---- eval set: UD sentences with an English translation ----
        var evalRequests = new List<string>();
        var evalGold = new List<string>();
        var evalTokens = 0;
        var sentenceIndex = 0;
        foreach (var sentence in sentences)
        {
            sentenceIndex++;
            if (string.IsNullOrWhiteSpace(sentence.TextEn))
            {
                continue;
            }
            var key = $"ud:{sentenceIndex}";
            var tokens = new List<object>();
            foreach (var (word, i) in sentence.Words.Select((w, i) => (w, i)))
            {
                var form = LemmaTable.NormalizeForm(word.Form);
                if (!Adjudicable(form))
                {
                    continue;
                }
                tokens.Add(new { i, form, candidates = Candidates(form) });
                evalGold.Add(JsonConvert.SerializeObject(new
                {
                    key,
                    i,
                    form,
                    gold = AdjudicationCommon.DisplayKey(word.Lemma),
                    isTest = sentence.IsTestFile,
                }));
                evalTokens++;
            }
            if (tokens.Count == 0)
            {
                continue;
            }
            evalRequests.Add(JsonConvert.SerializeObject(new
            {
                key,
                docId = "UD_Manx-Cadhan",
                manx = sentence.Text ?? string.Join(" ", sentence.Words.Select(x => x.Form)),
                english = sentence.TextEn,
                tokens,
            }));
        }

        WriteBatches(outDir!, "eval-requests", evalRequests, EvalSentencesPerFile);
        File.WriteAllLines(Path.Combine(outDir!, "eval-gold.jsonl"), evalGold);

        // ---- corpus set: translated lines, deduplicated by content key ----
        var documents = OpenDataLoader.LoadDocumentsFromFile(null)
            .Concat(ClosedDataLoader.LoadDocumentsFromFile())
            .ToList();
        var corpusRequests = new List<string>();
        var seenKeys = new HashSet<string>();
        long corpusTokens = 0, duplicateLines = 0;
        AdjudicationCommon.ForEachManxLine(documents, (docId, line) =>
        {
            if (string.IsNullOrWhiteSpace(line.English))
            {
                return;
            }
            var streamTokens = AdjudicationCommon.Tokenize(line.NormalizedManx);
            var tokens = new List<object>();
            for (var i = 0; i < streamTokens.Count; i++)
            {
                if (Adjudicable(streamTokens[i]))
                {
                    tokens.Add(new { i, form = streamTokens[i], candidates = Candidates(streamTokens[i]) });
                }
            }
            if (tokens.Count == 0)
            {
                return;
            }
            var key = AdjudicationCommon.Hash(string.Join(" ", streamTokens));
            if (!seenKeys.Add(key))
            {
                duplicateLines++;
                return;
            }
            corpusTokens += tokens.Count;
            corpusRequests.Add(JsonConvert.SerializeObject(new
            {
                key,
                docId,
                manx = line.NormalizedManx,
                english = line.English,
                englishHash = AdjudicationCommon.Hash(line.English),
                tokens,
            }));
        });

        WriteBatches(outDir!, "corpus-requests", corpusRequests, CorpusLinesPerFile);

        var report = new StringBuilder();
        report.AppendLine($"overrides layer: {overrides.Count} forms ({overridesPath ?? "none adopted"})");
        report.AppendLine($"un-adjudicable forms (table gap / UD convention split): "
                          + string.Join(", ", unAdjudicableForms.OrderBy(x => x, StringComparer.Ordinal)));
        report.AppendLine($"eval: {evalRequests.Count:N0} UD sentences with text_en carrying {evalTokens:N0} adjudicable tokens");
        report.AppendLine($"corpus: {corpusRequests.Count:N0} unique translated lines carrying {corpusTokens:N0} adjudicable tokens "
                          + $"({duplicateLines:N0} duplicate lines folded into their first occurrence)");
        report.AppendLine($"wrote {Directory.GetFiles(outDir!, "eval-requests-*.jsonl").Length} eval files, "
                          + $"{Directory.GetFiles(outDir!, "corpus-requests-*.jsonl").Length} corpus files -> {outDir}");
        TestContext.Progress.WriteLine(report.ToString());
    }

    private static void WriteBatches(string outDir, string prefix, List<string> lines, int perFile)
    {
        foreach (var stale in Directory.GetFiles(outDir, $"{prefix}-*.jsonl"))
        {
            File.Delete(stale);
        }
        var fileIndex = 0;
        foreach (var chunk in lines.Chunk(perFile))
        {
            File.WriteAllLines(Path.Combine(outDir, $"{prefix}-{fileIndex:D3}.jsonl"), chunk);
            fileIndex++;
        }
    }
}
