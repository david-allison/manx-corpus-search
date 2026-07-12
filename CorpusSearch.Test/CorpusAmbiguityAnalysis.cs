using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CorpusSearch.Dependencies.Lucene;
using CorpusSearch.Services;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Util;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>
/// One-off sizing analysis for translation-based lemma disambiguation
/// (HANDOFF-ud-eval.md follow-up): token-weighted ambiguity over the real corpus
/// through the shipped lookup (the manx_lemma pipeline: gv lines, NormalizedManx,
/// uncased analyzer, clitic fallback), plus UD_Manx-Cadhan reading distributions
/// for the most collision-prone forms.
///
/// Run: dotnet test CorpusSearch.Test --filter FullyQualifiedName~CorpusAmbiguityAnalysis --logger "console;verbosity=detailed"
/// </summary>
[TestFixture]
[Explicit("one-off analysis over the full corpus, not a regression test")]
public class CorpusAmbiguityAnalysis
{
    private sealed record FormStats(string Form, int CorpusTokens, IReadOnlyList<string> Candidates)
    {
        public Dictionary<string, int> UdReadings { get; } = [];
    }

    [Test]
    public void Report()
    {
        var table = LemmaTable.Instance;
        Assert.That(table.FormCount, Is.GreaterThan(39_000), "vendored table missing");

        var documents = OpenDataLoader.LoadDocumentsFromFile(null)
            .Concat(ClosedDataLoader.LoadDocumentsFromFile())
            .ToList();

        long tokens = 0, covered = 0, cliticCovered = 0, postings = 0, wrongPostings = 0;
        long ambiguousWithEnglish = 0, ambiguousWithoutEnglish = 0;
        var histogram = new long[7]; // candidate count 0..5, 6+ clamped
        var ambiguousFormTokens = new Dictionary<string, int>();
        var candidatesByAmbiguousForm = new Dictionary<string, IReadOnlyList<string>>();
        var coveredTypes = new HashSet<string>();
        var ambiguousTypes = new HashSet<string>();
        var failedDocuments = 0;

        foreach (var document in documents)
        {
            List<CorpusSearch.Model.DocumentLine> lines;
            try
            {
                lines = document.LoadPreparedLines();
            }
            catch (Exception)
            {
                failedDocuments++;
                continue;
            }
            foreach (var line in lines.Where(x => x.IsManxLanguage))
            {
                foreach (var token in Tokenize(line.NormalizedManx))
                {
                    tokens++;
                    var candidates = table.CandidatesFor(token);
                    var viaClitic = false;
                    if (candidates.Count == 0)
                    {
                        candidates = table.CliticCandidatesFor(token);
                        viaClitic = candidates.Count > 0;
                    }
                    histogram[Math.Min(candidates.Count, 6)]++;
                    if (candidates.Count == 0)
                    {
                        continue;
                    }
                    covered++;
                    if (viaClitic)
                    {
                        cliticCovered++;
                    }
                    postings += candidates.Count;
                    wrongPostings += candidates.Count - 1;
                    coveredTypes.Add(token);
                    if (candidates.Count >= 2)
                    {
                        ambiguousTypes.Add(token);
                        ambiguousFormTokens[token] = ambiguousFormTokens.GetValueOrDefault(token) + 1;
                        candidatesByAmbiguousForm.TryAdd(token, candidates);
                        // translation evidence available for this occurrence?
                        if (string.IsNullOrWhiteSpace(line.English))
                        {
                            ambiguousWithoutEnglish++;
                        }
                        else
                        {
                            ambiguousWithEnglish++;
                        }
                    }
                }
            }
        }

        // UD reading distributions for the ambiguous forms (ground truth where available)
        var udReadings = UdReadingsByForm();

        var ranked = ambiguousFormTokens
            .OrderByDescending(x => x.Value)
            .Select(x => new FormStats(x.Key, x.Value, candidatesByAmbiguousForm[x.Key]))
            .ToList();
        foreach (var form in ranked)
        {
            if (udReadings.TryGetValue(form.Form, out var readings))
            {
                foreach (var (lemma, count) in readings)
                {
                    form.UdReadings[lemma] = count;
                }
            }
        }

        var report = new StringBuilder();
        var ambiguousTokens = ambiguousFormTokens.Values.Sum(x => (long)x);
        report.AppendLine($"documents: {documents.Count} loaded ({failedDocuments} failed), gv tokens: {tokens:N0}");
        report.AppendLine($"covered: {covered:N0} ({covered / (double)tokens:P2}) [{cliticCovered:N0} via clitic fallback]");
        report.AppendLine($"ambiguous (>=2 candidates): {ambiguousTokens:N0} tokens = {ambiguousTokens / (double)covered:P2} of covered ({ambiguousTypes.Count:N0} of {coveredTypes.Count:N0} covered types)");
        report.AppendLine($"lemma-id postings: {postings:N0}; wrong-reading postings (<=1 right per token): {wrongPostings:N0} = {wrongPostings / (double)postings:P2}");
        report.AppendLine($"ambiguous occurrences on lines with an English translation: {ambiguousWithEnglish:N0} ({ambiguousWithEnglish / (double)(ambiguousWithEnglish + ambiguousWithoutEnglish):P1}); without: {ambiguousWithoutEnglish:N0}");
        report.AppendLine($"mean candidates per covered token: {postings / (double)covered:F3}");
        report.AppendLine();
        report.AppendLine("candidate-count histogram (token-weighted):");
        for (var i = 0; i < histogram.Length; i++)
        {
            var label = i == 6 ? "6+" : i.ToString();
            report.AppendLine($"  {label,2}: {histogram[i],12:N0}  ({histogram[i] / (double)tokens:P2})");
        }
        report.AppendLine();
        report.AppendLine("cumulative share of ambiguous-token mass (overrides-file sizing):");
        foreach (var top in new[] { 10, 25, 50, 100, 200, 500, 1000 })
        {
            var share = ranked.Take(top).Sum(x => (long)x.CorpusTokens) / (double)ambiguousTokens;
            report.AppendLine($"  top {top,4} forms: {share:P1}");
        }
        report.AppendLine();
        report.AppendLine($"total ambiguous forms: {ranked.Count:N0}");
        report.AppendLine();
        // how far a treebank-seeded overrides file gets on its own: forms whose UD
        // readings are decisive (>=3 observations, >=80% majority reading)
        var withUd = ranked.Where(x => x.UdReadings.Count > 0).ToList();
        var decisive = withUd.Where(x =>
            x.UdReadings.Values.Sum() >= 3 &&
            x.UdReadings.Values.Max() / (double)x.UdReadings.Values.Sum() >= 0.8).ToList();
        report.AppendLine($"forms with any UD evidence: {withUd.Count:N0} covering {withUd.Sum(x => (long)x.CorpusTokens) / (double)ambiguousTokens:P1} of ambiguous mass");
        report.AppendLine($"decisive by UD majority (>=3 obs, >=80%): {decisive.Count:N0} forms covering {decisive.Sum(x => (long)x.CorpusTokens) / (double)ambiguousTokens:P1} of ambiguous mass");
        report.AppendLine();
        report.AppendLine("top 30 ambiguous forms (corpus freq, candidates, UD readings):");
        foreach (var form in ranked.Take(30))
        {
            var readings = form.UdReadings.Count == 0
                ? "-"
                : string.Join(" ", form.UdReadings.OrderByDescending(x => x.Value).Select(x => $"{x.Key}x{x.Value}"));
            report.AppendLine($"  {form.CorpusTokens,7:N0}  {form.Form,-16} [{string.Join(", ", form.Candidates)}]  UD: {readings}");
        }

        TestContext.Progress.WriteLine(report.ToString());
    }

    /// <summary>form -> (normalized UD lemma -> count) over the treebank's syntactic
    /// words, same skip set as <see cref="UdLemmaAgreementTest"/></summary>
    private static Dictionary<string, Dictionary<string, int>> UdReadingsByForm()
    {
        var result = new Dictionary<string, Dictionary<string, int>>();
        var directory = Path.Combine(TestContext.CurrentContext.TestDirectory, "Resources", "UD_Manx-Cadhan");
        var files = Directory.Exists(directory) ? Directory.GetFiles(directory, "*.conllu") : [];
        foreach (var line in files.OrderBy(x => x).SelectMany(File.ReadLines))
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
            if (columns[3] is "PUNCT" or "NUM" or "X")
            {
                continue;
            }
            var form = LemmaTable.NormalizeForm(columns[1]);
            var lemma = LemmaTable.NormalizeForm(columns[2]);
            if (!result.TryGetValue(form, out var readings))
            {
                result[form] = readings = [];
            }
            readings[lemma] = readings.GetValueOrDefault(lemma) + 1;
        }
        return result;
    }

    /// <summary>The manx_lemma pipeline's tokens: uncased ManxTokenizer + ManxTokenFilter</summary>
    private static IEnumerable<string> Tokenize(string text)
    {
        var tokenizer = new ManxTokenizer(LuceneVersion.LUCENE_48, new StringReader(text));
        using var stream = new ManxTokenFilter(tokenizer);
        var term = stream.GetAttribute<ICharTermAttribute>();
        stream.Reset();
        while (stream.IncrementToken())
        {
            yield return term.ToString();
        }
        stream.End();
    }
}
