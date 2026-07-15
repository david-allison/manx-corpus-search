using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CorpusSearch.Model;
using CorpusSearch.Model.Dictionary;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace CorpusSearch.Service.Dictionaries;

public class KellyManxToEnglishDictionaryService(ISet<string> allWords, IList<KellyManxToEnglishEntry> entries)
    : ISearchDictionary
{
    public string Identifier => "J Kelly Manx to English";

    /// <summary>matches the data repo's own name (kelly-m2e-manx-dictionary-data):
    /// bare 'kelly' would not say which Kelly, with Phil Kelly's beside it</summary>
    public string Slug => "kelly-m2e";

    public List<string> QueryLanguages => ["gv"];
        
    public bool LinkToDictionary => false;

    public static KellyManxToEnglishDictionaryService Init(ILogger<KellyManxToEnglishDictionaryService> log)
    {
        HashSet<string> allWords;

        IList<KellyManxToEnglishEntry> entries = new List<KellyManxToEnglishEntry>();
        try
        {
            entries = GetEntries();
            var allEntries = entries.SelectMany(x => x.ChildrenRecursive)
                .SelectMany(x => x.Words.Concat(
                    (x.Plurals ?? []).SelectMany(p => new[] { p, p.Replace('ç', 'c').Replace('Ç', 'C') })));
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

        entries.ForEach(RemoveArticleFromSpain);
        entries.ForEach(EnrichCedillaEntries);
        return entries;
    }

    /// <summary>The heading for Spain is 'SPAINEY, YN': the 'yn' records that the noun takes the
    /// article ('yn Spainey', as Irish 'An Spáin'). It is not a form of the word, so looking up
    /// the article 'yn' should not return Spain.</summary>
    internal static void RemoveArticleFromSpain(KellyManxToEnglishEntry entry)
    {
        if (entry.Words is ["SPAINEY", "YN"])
        {
            entry.Words = ["SPAINEY"];
        }
        entry.SafeChildren.ForEach(RemoveArticleFromSpain);
    }

    /// <summary>If an entry has 'ç', allow 'c'</summary>
    /// <remarks>Allows us to search for ymmyrçhagh. Shouldn't be done in the dictionary JSON. Best done here</remarks>
    private static void EnrichCedillaEntries(KellyManxToEnglishEntry entry)
    {
        // TODO: needs test
        entry.Words = entry.Words.Concat(entry.Words.Select(x => x.Replace('ç', 'c'))).ToHashSet().ToList();
        entry.SafeChildren.ForEach(EnrichCedillaEntries);
    }

    /// <summary>The plural list answers for its ç-respellings at match time
    /// (ÇHENTYN matches chentyn) so the display list stays as printed</summary>
    private static bool PluralMatches(KellyManxToEnglishEntry entry, string query)
    {
        return (entry.Plurals ?? []).Any(p =>
            p.Equals(query, StringComparison.InvariantCultureIgnoreCase)
            || p.Replace('ç', 'c').Replace('Ç', 'C')
                .Equals(query, StringComparison.InvariantCultureIgnoreCase));
    }

    /// <summary>Whether the dictionary contains the provided word (no fuzziness)</summary>
    public bool ContainsWordExact(string s)
    {
        return allWords.Contains(s);
    }

    public bool ContainsWord(string word)
    {
        return ContainsWordExact(word);
    }

    public IEnumerable<string> AllWords => allWords;

    /// <summary>The printed headwords, top-level entries only and in the file's
    /// order, which is Kelly's own</summary>
    public IReadOnlyList<string> Headwords { get; } =
        entries.Select(x => x.Words.FirstOrDefault()).OfType<string>().ToList();

    /// <summary>
    /// Kelly's 1866 definitions open with the printed word-class abbreviation
    /// ("s. pl. EE. a dog."): recovered for sense filtering. Null when the
    /// definition doesn't declare one.
    /// </summary>
    /// <remarks>
    /// The alternation is longest-first only for readability: every branch is
    /// anchored by the '.', so "adv." cannot be read as "a.".
    ///
    /// 'part.' is deliberately absent. A participle is a form of a verb, not a
    /// class beside it, so labelling one would filter it out of its own verb's
    /// root chain (<see cref="DictionaryLookupService"/> keeps only the classes
    /// the lemma id means). Unlabelled, it stays — which is the right answer.
    /// </remarks>
    internal static List<string>? PartsOfSpeechOf(string definition)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            definition ?? "", @"^\s*(interj|prep|pron|conj|adv|adj|pre|pro|int|s|v|a)\.");
        return match.Success
            ? [match.Groups[1].Value switch
            {
                "s" => "Noun",
                "v" => "Verb",
                "a" or "adj" => "Adjective",
                "adv" => "Adverb",
                "pre" or "prep" => "Preposition",
                "conj" => "Conjunction",
                "int" or "interj" => "Interjection",
                _ => "Pronoun",
            }]
            : null;
    }

    public IEnumerable<DictionarySummary> GetSummaries(string query, bool basic)
    {
        if (!ContainsWordExact(query)) { yield break; }

        // PERF: extract to member
        var entries1 = entries.SelectMany(x => x.ChildrenRecursive).ToList();

        foreach (var validEntry in entries1.Where(e =>
                     e.Words.Contains(query, StringComparer.InvariantCultureIgnoreCase)
                     || PluralMatches(e, query)))
        {
            yield return GetDictionarySummary(validEntry);
        }

        yield break;

        static DictionarySummary GetDictionarySummary(KellyManxToEnglishEntry entry)
        {
            return new DictionarySummary
            {
                PrimaryWord = entry.Words.First(),
                Summary = entry.Definition,
                PartsOfSpeech = PartsOfSpeechOf(entry.Definition),
                Words = entry.Words.Count > 1 ? entry.Words : null,
                Plurals = entry.Plurals is { Count: > 0 } ? entry.Plurals : null,
                Citations = VerseCitations.FindAll(entry.Definition),
            };
        }
    }
}