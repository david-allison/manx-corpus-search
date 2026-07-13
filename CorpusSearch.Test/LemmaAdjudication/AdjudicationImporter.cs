using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CorpusSearch.Test.LemmaAdjudication;

/// <summary>
/// Scores and applies LLM adjudication verdicts (see AdjudicationExporter for
/// the request side and file shapes).
///
/// Eval mode (always runs): joins eval-verdicts-[config]-*.jsonl to
/// eval-gold.jsonl and prints precision/coverage per config using production
/// resolution semantics. The gate number is the "discriminating tokens" line:
/// single-display tokens (all candidates share one headword spelling)
/// auto-score correct whatever is chosen, so only multi-display tokens
/// measure real disambiguation.
///
/// Corpus mode (when corpus-verdicts-*.jsonl exist and LEMMA_SIDECAR_OUT is
/// set): joins verdicts to corpus-requests-*.jsonl and emits
/// lemma.sidecar.tsv v2 with tier (index/popup) and humanVerified columns,
/// plus lemma.overrides.candidates.tsv for unanimous forms.
///
/// Run: LEMMA_ADJUDICATION_DIR=... [LEMMA_SIDECAR_OUT=...]
///   dotnet test CorpusSearch.Test --filter FullyQualifiedName~AdjudicationImporter
/// </summary>
[TestFixture]
[Explicit("artifact generator over adjudication outputs, not a regression test")]
public class AdjudicationImporter
{
    /// <summary>Occurrences needed before a unanimous form becomes an overrides candidate</summary>
    private const int PromotionMinOccurrences = 10;

    private sealed record Verdict(string Key, int I, IReadOnlyList<string> ChosenIds, string Confidence);

    private sealed record GoldToken(string Key, int I, string Form, string Gold, bool IsTest);

    private sealed record RequestToken(string Key, string DocId, string EnglishHash, int I, string Form,
        IReadOnlyList<string> CandidateIds);

    [Test]
    public void Import()
    {
        var workDir = Environment.GetEnvironmentVariable("LEMMA_ADJUDICATION_DIR");
        Assert.That(workDir, Is.Not.Null.And.Not.Empty, "set LEMMA_ADJUDICATION_DIR to the work directory");

        var displayById = AdjudicationCommon.DisplayLemmaById();
        var report = new StringBuilder();

        EvaluatePilot(workDir!, displayById, report);

        var sidecarOut = Environment.GetEnvironmentVariable("LEMMA_SIDECAR_OUT");
        if (!string.IsNullOrEmpty(sidecarOut) && Directory.GetFiles(workDir!, "corpus-verdicts-*.jsonl").Length > 0)
        {
            EmitSidecar(workDir!, sidecarOut, report);
        }

        TestContext.Progress.WriteLine(report.ToString());
    }

    // ---- pilot eval ----

    private static void EvaluatePilot(string workDir, Dictionary<string, string> displayById, StringBuilder report)
    {
        var goldPath = Path.Combine(workDir, "eval-gold.jsonl");
        if (!File.Exists(goldPath))
        {
            report.AppendLine("eval: no eval-gold.jsonl, skipping");
            return;
        }
        var gold = File.ReadLines(goldPath)
            .Select(x => JObject.Parse(x))
            .ToDictionary(
                x => (x["key"]!.Value<string>()!, x["i"]!.Value<int>()),
                x => new GoldToken(x["key"]!.Value<string>()!, x["i"]!.Value<int>(),
                    x["form"]!.Value<string>()!, x["gold"]!.Value<string>()!, x["isTest"]!.Value<bool>()));

        // candidate ids per token, so scoring uses production resolution
        // semantics (subset of candidates, strictly narrowing)
        var candidates = new Dictionary<(string, int), IReadOnlyList<string>>();
        foreach (var path in Directory.GetFiles(workDir, "eval-requests-*.jsonl"))
        {
            foreach (var line in File.ReadLines(path))
            {
                var record = JObject.Parse(line);
                var key = record["key"]!.Value<string>()!;
                foreach (var token in (JArray)record["tokens"]!)
                {
                    candidates[(key, token["i"]!.Value<int>())] =
                        ((JArray)token["candidates"]!).Select(c => c["id"]!.Value<string>()!).ToList();
                }
            }
        }

        // eval-verdicts-<config>-NNN.jsonl, grouped by config
        var byConfig = Directory.GetFiles(workDir, "eval-verdicts-*.jsonl")
            .GroupBy(path =>
            {
                var stem = Path.GetFileNameWithoutExtension(path)["eval-verdicts-".Length..];
                var lastDash = stem.LastIndexOf('-');
                return lastDash > 0 ? stem[..lastDash] : stem;
            })
            .OrderBy(x => x.Key);

        var any = false;
        foreach (var config in byConfig)
        {
            any = true;
            var verdicts = config.SelectMany(ReadVerdicts).ToList();
            report.AppendLine($"eval [{config.Key}]: "
                + Score(verdicts, gold, candidates, displayById, isTest: null, discriminatingOnly: false, confidence: null));
            // single-display tokens auto-score correct whatever is chosen (the
            // gold is display-keyed), so the gate number is the multi-display
            // ("discriminating") precision
            report.AppendLine("  discriminating tokens (multi-display): "
                + Score(verdicts, gold, candidates, displayById, isTest: null, discriminatingOnly: true, confidence: null));
            report.AppendLine("  discriminating, high-confidence only:  "
                + Score(verdicts, gold, candidates, displayById, isTest: null, discriminatingOnly: true, confidence: "high"));
            report.AppendLine("  discriminating, test files only:       "
                + Score(verdicts, gold, candidates, displayById, isTest: true, discriminatingOnly: true, confidence: null));
            foreach (var line in WrongSample(verdicts, gold, candidates, displayById).Take(12))
            {
                report.AppendLine($"  WRONG {line}");
            }
        }
        if (!any)
        {
            report.AppendLine("eval: no eval-verdicts-*.jsonl yet (run the pilot workflow first)");
        }
    }

    /// <summary>True when the verdict narrows the candidate set the way production
    /// would apply it: a confident choice of a strict, known subset</summary>
    private static bool ResolvesInProduction(Verdict verdict, IReadOnlyList<string> candidateIds)
    {
        return IsResolution(verdict)
               && verdict.ChosenIds.All(candidateIds.Contains)
               && verdict.ChosenIds.Count < candidateIds.Count;
    }

    private static bool Discriminating(IReadOnlyList<string> candidateIds, Dictionary<string, string> displayById)
    {
        return candidateIds
            .Select(id => AdjudicationCommon.DisplayKey(displayById.GetValueOrDefault(id, id)))
            .Distinct()
            .Count() > 1;
    }

    private static string Score(List<Verdict> verdicts, Dictionary<(string, int), GoldToken> gold,
        Dictionary<(string, int), IReadOnlyList<string>> candidates,
        Dictionary<string, string> displayById, bool? isTest, bool discriminatingOnly, string? confidence)
    {
        long eligible = 0, resolved = 0, correct = 0;
        foreach (var verdict in verdicts)
        {
            if (!gold.TryGetValue((verdict.Key, verdict.I), out var token)
                || (isTest != null && token.IsTest != isTest)
                || !candidates.TryGetValue((verdict.Key, verdict.I), out var candidateIds)
                || (discriminatingOnly && !Discriminating(candidateIds, displayById)))
            {
                continue;
            }
            eligible++;
            if (!ResolvesInProduction(verdict, candidateIds)
                || (confidence != null && verdict.Confidence != confidence))
            {
                continue;
            }
            resolved++;
            if (ChosenDisplayKeys(verdict, displayById).Contains(token.Gold))
            {
                correct++;
            }
        }
        var precision = resolved > 0 ? correct / (double)resolved : 0;
        var coverage = eligible > 0 ? resolved / (double)eligible : 0;
        return $"{eligible:N0} adjudicated, {resolved:N0} resolved ({coverage:P1}), "
               + $"precision {precision:P1} ({correct}/{resolved})";
    }

    private static IEnumerable<string> WrongSample(List<Verdict> verdicts,
        Dictionary<(string, int), GoldToken> gold,
        Dictionary<(string, int), IReadOnlyList<string>> candidates,
        Dictionary<string, string> displayById)
    {
        foreach (var verdict in verdicts)
        {
            if (gold.TryGetValue((verdict.Key, verdict.I), out var token)
                && candidates.TryGetValue((verdict.Key, verdict.I), out var candidateIds)
                && ResolvesInProduction(verdict, candidateIds)
                && !ChosenDisplayKeys(verdict, displayById).Contains(token.Gold))
            {
                yield return $"{token.Form} -> [{string.Join(",", verdict.ChosenIds)}] but UD={token.Gold} ({verdict.Key})";
            }
        }
    }

    // ---- corpus emit ----

    private static void EmitSidecar(string workDir, string sidecarOut, StringBuilder report)
    {
        var requests = new Dictionary<(string Key, int I), RequestToken>();
        foreach (var path in Directory.GetFiles(workDir, "corpus-requests-*.jsonl").OrderBy(x => x))
        {
            foreach (var line in File.ReadLines(path))
            {
                var record = JObject.Parse(line);
                var key = record["key"]!.Value<string>()!;
                var docId = record["docId"]!.Value<string>()!;
                var englishHash = record["englishHash"]?.Value<string>() ?? "";
                foreach (var token in (JArray)record["tokens"]!)
                {
                    var i = token["i"]!.Value<int>();
                    requests[(key, i)] = new RequestToken(key, docId, englishHash, i,
                        token["form"]!.Value<string>()!,
                        ((JArray)token["candidates"]!).Select(c => c["id"]!.Value<string>()!).ToList());
                }
            }
        }

        // No attestation veto here, deliberately: the prototype's form-level
        // veto forbids dropping any UD-attested reading, which blocks exactly
        // the context-split forms per-occurrence resolution exists for; and a
        // per-form deterministic veto is redundant because UD-deterministic
        // forms already live in the overrides layer and never reach
        // adjudication. Safety = the measured pilot gate + the tier split.
        var rows = new List<(RequestToken Token, Verdict Verdict, string Tier)>();
        long unsure = 0, invalid = 0;
        foreach (var path in Directory.GetFiles(workDir, "corpus-verdicts-*.jsonl").OrderBy(x => x))
        {
            foreach (var verdict in ReadVerdicts(path))
            {
                if (!requests.TryGetValue((verdict.Key, verdict.I), out var token))
                {
                    invalid++;
                    continue;
                }
                if (!ResolvesInProduction(verdict, token.CandidateIds))
                {
                    unsure++;
                    continue;
                }
                rows.Add((token, verdict, verdict.Confidence == "high" ? "index" : "popup"));
            }
        }

        Directory.CreateDirectory(sidecarOut);
        var sidecarPath = Path.Combine(sidecarOut, "lemma.sidecar.tsv");
        using (var writer = new StreamWriter(sidecarPath))
        {
            writer.WriteLine("# generated by CorpusSearch.Test/LemmaAdjudication (LLM adjudication of Manx/English line pairs)");
            writer.WriteLine("# key = SHA-256[..16] of the normalized Manx token stream; englishHash is a staleness marker");
            writer.WriteLine("# tier: index = high-confidence (search index + popup); popup = display demotion only");
            writer.WriteLine("# humanVerified: 0 = LLM-only; 1 = human-checked");
            writer.WriteLine("docId\tkey\tenglishHash\ttokenIndex\tform\tlemmaIds\ttier\thumanVerified");
            foreach (var (token, verdict, tier) in rows.OrderBy(x => x.Token.DocId, StringComparer.Ordinal)
                         .ThenBy(x => x.Token.Key, StringComparer.Ordinal).ThenBy(x => x.Token.I))
            {
                writer.WriteLine($"{token.DocId}\t{token.Key}\t{token.EnglishHash}\t{token.I}\t{token.Form}"
                                 + $"\t{string.Join(",", verdict.ChosenIds.OrderBy(x => x, StringComparer.Ordinal))}\t{tier}\t0");
            }
        }

        // unanimous forms become overrides candidates (form-level rows are
        // cheaper and survive corpus edits entirely)
        var candidatesPath = Path.Combine(sidecarOut, "lemma.overrides.candidates.tsv");
        var promotions = rows
            .GroupBy(x => x.Token.Form)
            .Where(g => g.Count() >= PromotionMinOccurrences)
            .Select(g => (Form: g.Key,
                IdSets: g.Select(x => string.Join(",", x.Verdict.ChosenIds.OrderBy(id => id, StringComparer.Ordinal)))
                    .Distinct().ToList(),
                Count: g.Count()))
            .Where(x => x.IdSets.Count == 1)
            .OrderBy(x => x.Form, StringComparer.Ordinal)
            .ToList();
        using (var writer = new StreamWriter(candidatesPath))
        {
            writer.WriteLine("# unanimous LLM resolutions (>= " + PromotionMinOccurrences + " occurrences, one id-set) - human-skim, then adopt into lemma.overrides.tsv");
            writer.WriteLine("form\tlemmaIds\tevidence");
            foreach (var (form, idSets, count) in promotions)
            {
                writer.WriteLine($"{form}\t{idSets[0]}\t{count}/{count} LLM-unanimous");
            }
        }

        report.AppendLine($"sidecar: {rows.Count:N0} resolutions "
                          + $"({rows.Count(x => x.Tier == "index"):N0} index-tier, {rows.Count(x => x.Tier == "popup"):N0} popup-tier); "
                          + $"{unsure:N0} unresolved/invalid-shape, {invalid:N0} unmatched keys");
        report.AppendLine($"promotions: {promotions.Count:N0} unanimous forms -> {candidatesPath}");
        report.AppendLine($"wrote {sidecarPath}");
    }

    // ---- shared ----

    private static bool IsResolution(Verdict verdict)
    {
        return verdict.Confidence != "unsure" && verdict.ChosenIds.Count > 0;
    }

    private static HashSet<string> ChosenDisplayKeys(Verdict verdict, Dictionary<string, string> displayById)
    {
        return verdict.ChosenIds
            .Select(id => AdjudicationCommon.DisplayKey(displayById.GetValueOrDefault(id, id)))
            .ToHashSet();
    }

    private static IEnumerable<Verdict> ReadVerdicts(string path)
    {
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            JObject record;
            try
            {
                record = JObject.Parse(line);
            }
            catch (JsonReaderException)
            {
                continue; // a malformed agent line loses one verdict, not the file
            }
            var key = record["key"]?.Value<string>();
            var i = record["i"]?.Value<int?>();
            if (key == null || i == null)
            {
                continue;
            }
            var chosen = record["chosenIds"] is JArray ids
                ? ids.Select(x => x.Value<string>()!).ToList()
                : [];
            yield return new Verdict(key, i.Value, chosen,
                record["confidence"]?.Value<string>() ?? "low");
        }
    }
}
