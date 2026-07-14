using System;
using System.Collections.Generic;
using System.Linq;

namespace CorpusSearch.Service.Dictionaries;

/// <summary>
/// Phil Kelly's Manx-English dictionary as a lookup dictionary: the
/// modern/revival-era vocabulary the print dictionaries predate. Wraps the
/// word -> translations map the Translations feature already loads
/// (Resources/manx.json; canonical source: cregeen-nvh phil-kelly/
/// manx-to-english.nvh, a lossless conversion).
/// </summary>
public class PhilKellyDictionaryService(IReadOnlyDictionary<string, IList<string>> entries)
    : ISearchDictionary
{
    public const string Name = "Phil Kelly Manx to English";

    public string Identifier => Name;

    public List<string> QueryLanguages => ["gv"];

    public bool LinkToDictionary => false;

    public static PhilKellyDictionaryService Init()
    {
        // the source spells ç inconsistently (chellveeish beside çhellveeish
        // lhoob jeighit): alias the folded spelling so either form answers
        var entries = new Dictionary<string, IList<string>>(
            Startup.ManxToEnglishDictionary, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in Startup.ManxToEnglishDictionary)
        {
            var folded = FoldCedilla(key);
            if (folded != key)
            {
                entries.TryAdd(folded, value);
            }
        }
        return new PhilKellyDictionaryService(entries);
    }

    private static string FoldCedilla(string s) => s.Replace('ç', 'c').Replace('Ç', 'C');

    public bool ContainsWord(string word) =>
        entries.ContainsKey(word.Trim()) || entries.ContainsKey(FoldCedilla(word.Trim()));

    public IEnumerable<string> AllWords => entries.Keys;

    public IEnumerable<DictionarySummary> GetSummaries(string query, bool basic = false)
    {
        if (!entries.TryGetValue(query.Trim(), out var glosses))
        {
            entries.TryGetValue(FoldCedilla(query.Trim()), out glosses);
        }
        if (glosses == null || glosses.Count == 0)
        {
            yield break;
        }
        yield return new DictionarySummary
        {
            // the map's keys are the dictionary's lowercase spellings; the
            // case-insensitive hit answers for the tapped casing
            PrimaryWord = query.Trim().ToLowerInvariant(),
            Summary = string.Join("; ", glosses.Where(x => !string.IsNullOrWhiteSpace(x))),
        };
    }
}
