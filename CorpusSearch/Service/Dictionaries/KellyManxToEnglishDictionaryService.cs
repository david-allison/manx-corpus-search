using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CorpusSearch.Model.Dictionary;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace CorpusSearch.Service.Dictionaries;

public class KellyManxToEnglishDictionaryService(ISet<string> allWords, IList<KellyManxToEnglishEntry> entries)
    : ISearchDictionary
{
    public string Identifier => "J Kelly Manx to English";

    public List<string> QueryLanguages => ["gv"];
        
    public bool LinkToDictionary => false;

    public static KellyManxToEnglishDictionaryService Init(ILogger<KellyManxToEnglishDictionaryService> log)
    {
        HashSet<string> allWords;

        IList<KellyManxToEnglishEntry> entries = new List<KellyManxToEnglishEntry>();
        try
        {
            entries = GetEntries();
            var allEntries = entries.SelectMany(x => x.ChildrenRecursive).SelectMany(x => x.Words);
            allWords = new HashSet<string>(allEntries, StringComparer.InvariantCultureIgnoreCase);
        } 
        catch (Exception)
        {
            // TODO: Add to health check
            log.LogError("Failed to load Kelly");
            allWords = [];
        }
            
        return new KellyManxToEnglishDictionaryService(allWords, entries);
    }

    /// <summary>Loads the tree of entries from Cregeen</summary>
    public static IList<KellyManxToEnglishEntry> GetEntries()
    {
        // TODO: This shouldn't be static

        var path = Startup.GetLocalFile("Resources", "kellym2e.json");
            
        var text = File.ReadAllText(path);

        var entries = JsonConvert.DeserializeObject<List<KellyManxToEnglishEntry>>(text) ?? [];

        entries.ForEach(EnrichCedillaEntries);
        return entries;
    }

    /// <summary>If an entry has 'ç', allow 'c'</summary>
    /// <remarks>Allows us to search for ymmyrçhagh. Shouldn't be done in the dictionary JSON. Best done here</remarks>
    private static void EnrichCedillaEntries(KellyManxToEnglishEntry entry)
    {
        // TODO: needs test
        entry.Words = entry.Words.Concat(entry.Words.Select(x => x.Replace('ç', 'c'))).ToHashSet().ToList();
        entry.SafeChildren.ForEach(EnrichCedillaEntries);
    }

    /// <summary>Whether the dictionary contains the provided word (no fuzziness)</summary>
    public bool ContainsWordExact(string s)
    {
        return allWords.Contains(s);
    }

    public IEnumerable<DictionarySummary> GetSummaries(string query, bool basic)
    {
        if (!ContainsWordExact(query)) { yield break; }

        // PERF: extract to member
        var entries1 = entries.SelectMany(x => x.ChildrenRecursive).ToList();

        foreach (var validEntry in entries1.Where(e => e.Words.Contains(query, StringComparer.InvariantCultureIgnoreCase)))
        {
            yield return GetDictionarySummary(validEntry);
        }

        yield break;

        DictionarySummary GetDictionarySummary(KellyManxToEnglishEntry entry)
        {
            return new DictionarySummary
            {
                PrimaryWord = entry.Words.First(),
                Summary = entry.Definition
            };
        }
    }
}