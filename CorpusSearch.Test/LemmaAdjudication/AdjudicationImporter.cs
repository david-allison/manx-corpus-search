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
/// eval-gold.jsonl and prints precision/coverage per config, the test-file
/// subset, and a wrong-verdict sample. This is the pilot gate: the corpus run
/// only proceeds on a config measuring >=97% precision on resolved tokens.
///
/// Corpus mode (when corpus-verdicts-*.jsonl exist and LEMMA_SIDECAR_OUT is
/// set): joins verdicts to corpus-requests-*.jsonl, applies the
/// UD-attestation veto (a resolution never drops an attested reading), and
/// emits lemma.sidecar.tsv v2 with tier (index/popup) and humanVerified
/// columns, plus lemma.overrides.candidates.tsv for unanimous forms.
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
            EmitSidecar(workDir!, sidecarOut, displayById, report);
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
            report.AppendLine($"eval [{config.Key}]: {Score(verdicts, gold, displayById, isTest: null)}");
            report.AppendLine($"  test files only: {Score(verdicts, gold, displayById, isTest: true)}");
            foreach (var line in WrongSample(verdicts, gold, displayById).Take(12))
            {
                report.AppendLine($"  WRONG {line}");
            }
        }
        if (!any)
        {
            report.AppendLine("eval: no eval-verdicts-*.jsonl yet (run the pilot workflow first)");
        }
    }

    private static string Score(List<Verdict> verdicts, Dictionary<(string, int), GoldToken> gold,
        Dictionary<string, string> displayById, bool? isTest)
    {
        long eligible = 0, resolved = 0, correct = 0, unsure = 0;
        foreach (var verdict in verdicts)
        {
            if (!gold.TryGetValue((verdict.Key, verdict.I), out var token)
                || (isTest != null && token.IsTest != isTest))
            {
                continue;
            }
            eligible++;
            if (!IsResolution(verdict))
            {
                unsure++;
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
               + $"precision {precision:P1} ({correct}/{resolved}), {unsure:N0} unresolved";
    }

    private static IEnumerable<string> WrongSample(List<Verdict> verdicts,
        Dictionary<(string, int), GoldToken> gold, Dictionary<string, string> displayById)
    {
        foreach (var verdict in verdicts)
        {
            if (gold.TryGetValue((verdict.Key, verdict.I), out var token)
                && IsResolution(verdict)
                && !ChosenDisplayKeys(verdict, displayById).Contains(token.Gold))
            {
                yield return $"{token.Form} -> [{string.Join(",", verdict.ChosenIds)}] but UD={token.Gold} ({verdict.Key})";
            }
        }
    }

    // ---- corpus emit ----

    private static void EmitSidecar(string workDir, string sidecarOut,
        Dictionary<string, string> displayById, StringBuilder report)
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

        var sentences = AdjudicationCommon.TreebankSentences();
        var attestedAll = AdjudicationCommon.AttestedReadings(sentences, trainOnly: false);

        var rows = new List<(RequestToken Token, Verdict Verdict, string Tier)>();
        long vetoed = 0, unsure = 0, invalid = 0;
        foreach (var path in Directory.GetFiles(workDir, "corpus-verdicts-*.jsonl").OrderBy(x => x))
        {
            foreach (var verdict in ReadVerdicts(path))
            {
                if (!requests.TryGetValue((verdict.Key, verdict.I), out var token))
                {
                    invalid++;
                    continue;
                }
                if (!IsResolution(verdict) || verdict.ChosenIds.Any(id => !token.CandidateIds.Contains(id))
                    || verdict.ChosenIds.Count >= token.CandidateIds.Count)
                {
                    unsure++;
                    continue;
                }
                // the veto: dropping a UD-attested reading is never allowed
                var attested = attestedAll.GetValueOrDefault(token.Form);
                var dropsAttested = attested != null && token.CandidateIds.Except(verdict.ChosenIds)
                    .Any(id => attested.Contains(AdjudicationCommon.DisplayKey(displayById.GetValueOrDefault(id, id))));
                if (dropsAttested)
                {
                    vetoed++;
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
                                 + $"\t{string.Join(",", verdict.ChosenIds)}\t{tier}\t0");
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
                          + $"{vetoed:N0} vetoed (attested reading), {unsure:N0} unresolved/invalid-shape, {invalid:N0} unmatched keys");
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
