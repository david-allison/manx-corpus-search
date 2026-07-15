using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace CorpusSearch.Service.Dictionaries;

/// <summary>
/// The Culture Vannin spoken dictionary (learnmanx.com): word/phrase ->
/// translation, each with a pronunciation recording. The audio is streamed
/// from learnmanx.com, never rehosted, and every entry carries the source's
/// URL so the popup cites Culture Vannin. The artifact (spoken.json) is
/// generated from cregeen-nvh's spoken/spoken.nvh and vendored beside the
/// lemma tables.
/// </summary>
public class CultureVanninSpokenDictionaryService(CultureVanninSpokenDictionaryService.SpokenArtifact artifact)
    : ISearchDictionary
{
    /// <summary>
    /// While false the service is never registered as a lookup
    /// source and the relay (AudioController) serves nothing, so no entry and
    /// no recording reaches the UI.
    /// </summary>
    // not const: constant folding would make the call sites' dropped branches
    // unreachable code, which TreatWarningsAsErrors turns into build errors
    public static readonly bool Enabled = false;

    public class SpokenEntry
    {
        public string Word { get; set; } = "";
        public string Translation { get; set; } = "";
        public string AudioUrl { get; set; } = "";
        public string Topic { get; set; } = "";
        public List<string>? PartsOfSpeech { get; set; }
    }

    public class SpokenArtifact
    {
        public string Name { get; set; } = "";
        public string Credit { get; set; } = "";
        public string Url { get; set; } = "";
        public string Note { get; set; } = "";
        public List<SpokenEntry> Entries { get; set; } = [];
    }

    private readonly Dictionary<string, List<SpokenEntry>> byWord =
        artifact.Entries
            .GroupBy(e => Key(e.Word), StringComparer.InvariantCultureIgnoreCase)
            .Where(g => g.Key.Length > 0)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.InvariantCultureIgnoreCase);

    /// <summary>Phrases are recorded with their punctuation ("Kys t'ou?"); lookups
    /// arrive without it</summary>
    private static string Key(string word) => word.Trim().TrimEnd('?', '!', '.', ',');

    public string Identifier { get; } =
        string.IsNullOrWhiteSpace(artifact.Name) ? "LearnManx Spoken Dictionary" : artifact.Name;

    private readonly string sourceUrl = artifact.Url;
    private readonly string sourceCredit = artifact.Credit;

    public List<string> QueryLanguages => ["gv"];

    public bool LinkToDictionary => false;

    public static CultureVanninSpokenDictionaryService Init(ILogger<CultureVanninSpokenDictionaryService> log)
    {
        var path = Startup.GetLocalFile("Resources", "spoken.json");
        if (!File.Exists(path))
        {
            // optional until the artifact is vendored: the popup just has no recordings
            log.LogInformation("{Path} not found: the spoken dictionary is disabled", path);
            return new CultureVanninSpokenDictionaryService(new SpokenArtifact());
        }
        try
        {
            var artifact = JsonConvert.DeserializeObject<SpokenArtifact>(File.ReadAllText(path));
            return new CultureVanninSpokenDictionaryService(artifact ?? new SpokenArtifact());
        }
        catch (Exception)
        {
            // TODO: Add to health check
            log.LogError("Failed to load the spoken dictionary");
            return new CultureVanninSpokenDictionaryService(new SpokenArtifact());
        }
    }

    public IEnumerable<DictionarySummary> GetSummaries(string query, bool basic = false)
    {
        if (!byWord.TryGetValue(Key(query), out var entries))
        {
            yield break;
        }
        foreach (var entry in entries)
        {
            yield return new DictionarySummary
            {
                PrimaryWord = entry.Word,
                Summary = entry.Translation.Length > 0 ? entry.Translation : "pronunciation",
                // played through the same-origin relay: cross-site media fetches
                // are unreliable against the source's CDN rules (AudioController)
                AudioUrl = entry.AudioUrl.Length > 0
                    ? "/api/Audio?url=" + Uri.EscapeDataString(entry.AudioUrl)
                    : null,
                SourceUrl = sourceUrl.Length > 0 ? sourceUrl : null,
                SourceCredit = sourceCredit.Length > 0 ? sourceCredit : null,
                PartsOfSpeech = entry.PartsOfSpeech is { Count: > 0 } ? entry.PartsOfSpeech : null,
            };
        }
    }

    public bool ContainsWord(string word) => byWord.ContainsKey(Key(word));

    public IEnumerable<string> AllWords => byWord.Values.SelectMany(x => x).Select(x => x.Word);
}
