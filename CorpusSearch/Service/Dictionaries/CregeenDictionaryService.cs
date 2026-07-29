using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using CorpusSearch.Model;
using CorpusSearch.Model.Dictionary;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace CorpusSearch.Service.Dictionaries;

public class CregeenDictionaryService(ISet<string> allWords, IList<CregeenEntry> entries)
    : ISearchDictionary, IQuotingDictionary
{
    /// <summary>The letters of Cregeen's printed index: no X or Z, which head no
    /// words of the book's, and no ç, which files under c (see
    /// <see cref="DictionaryBrowse.CollationKey"/>). The legacy page's letter
    /// bar, and what makes a one-character query a letter page, not a search.</summary>
    public static readonly IReadOnlyList<char> Letters =
    [
        'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M',
        'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'Y',
    ];

    public string Identifier => "Cregeen";
    public string Slug => "cregeen";
    public bool LinkToDictionary => true;
    public List<string> QueryLanguages => ["gv"];

    public static CregeenDictionaryService Init(ILogger<CregeenDictionaryService> log)
    {
        HashSet<string> allWords;

        IList<CregeenEntry> entries = new List<CregeenEntry>();
        try
        {
            entries = GetEntries();
            var allEntries = entries.SelectMany(x => x.ChildrenRecursive).SelectMany(x => x.Words);
            allWords = new HashSet<string>(allEntries, StringComparer.InvariantCultureIgnoreCase);
        } 
        catch (Exception)
        {
            // TODO: Add to health check
            log.LogError("Failed to load Cregeen");
            allWords = [];
        }
            
        return new CregeenDictionaryService(allWords, entries);
    }

    /// <summary>Loads the tree of entries from Cregeen</summary>
    public static IList<CregeenEntry> GetEntries()
    {
        // TODO: This shouldn't be static

        var path = Startup.GetLocalFile("Resources", "cregeen.json");

        // the file is downloaded on deployment (tools/init.sh); without it
        // (dev checkouts, CI) the dictionary is empty, not a failing page
        if (!File.Exists(path))
        {
            return [];
        }

        var text = File.ReadAllText(path);

        var entries = JsonConvert.DeserializeObject<List<CregeenEntry>>(text) ?? [];

        entries.ForEach(EnrichCedillaEntries);
        return entries;
    }

    /// <summary>If an entry has 'ç', allow 'c'</summary>
    /// <remarks>Allows us to search for ymmyrçhagh. Shouldn't be done in the dictionary JSON. Best done here</remarks>
    private static void EnrichCedillaEntries(CregeenEntry entry)
    {
        // TODO: needs test
        entry.Words = entry.Words.Concat(entry.Words.Select(x => x.Replace('ç', 'c'))).ToHashSet().ToList();
        entry.SafeChildren.ForEach(EnrichCedillaEntries);
    }

    public static bool IsValidSearch(string query)
    {
        return !string.IsNullOrWhiteSpace(query) // invalid if whitespace
               && (query.Length > 1 // valid if longer than 1 char
                   || Letters.Contains(char.ToUpperInvariant(query[0])) // valid if 1 char and a letter of the index
               );
    }

    public static IList<CregeenEntry> FuzzySearch(string query, IEnumerable<CregeenEntry> entries)
    {
        return FuzzySearchInternal(query, entries).Distinct().ToList();
    }

    private static IEnumerable<CregeenEntry> FuzzySearchInternal(string query, IEnumerable<CregeenEntry> entryData)
    {
        var flatEntries = entryData.SelectMany(x => x.ChildrenRecursive).ToList();

        // exact match
        foreach (var e in flatEntries.Where(x => x.ContainsWordExact(query)))
        {
            yield return e;
        }

        // Prefix
        foreach (var e in flatEntries.Where(entry => entry.Words.Any(word => word.StartsWith(query))))
        {
            yield return e;
        }

        // Contains
        foreach (var e in flatEntries.Where(entry => entry.Words.Any(word => word.Contains(query))))
        {
            yield return e;
        }
    }

    /// <summary>One letter of the legacy browse page: the top-level entries
    /// whose headword files under it (<see cref="DictionaryBrowse.LetterOf"/>),
    /// in the book's order. Filed by each word's own letter rather than sliced
    /// between landmark entries, so a letter keeps its page when the data
    /// respells the entry that used to open it.</summary>
    public static IEnumerable<CregeenEntry> EntriesUnder(char letter, IEnumerable<CregeenEntry> entries) =>
        entries.Where(x =>
            DictionaryBrowse.LetterOf(x.Words.FirstOrDefault() ?? "") == char.ToLowerInvariant(letter));

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
    /// order, which is Cregeen's own: the browse keeps the book's order (see
    /// <see cref="DictionaryBrowse.Chapters"/>). Built once: GetSummaries
    /// re-flattens the tree per call (see the PERF note below), and the index
    /// must not pay that.</summary>
    public IReadOnlyList<string> Headwords { get; } =
        entries.Select(x => x.Words.FirstOrDefault()).OfType<string>().ToList();

    /// <summary>Every entry's decoded text (never the basic gloss, which drops
    /// the quotations): the reverse verse lookup's input</summary>
    public IEnumerable<(string Word, string Text)> QuotableEntries =>
        entries.SelectMany(x => x.ChildrenRecursive)
            .Where(e => e.Words.Count > 0 && !string.IsNullOrEmpty(e.EntryHtml))
            .Select(e =>
            {
                HtmlDocument doc = new();
                doc.LoadHtml(e.EntryHtml);
                return (e.Words[0], HttpUtility.HtmlDecode(doc.DocumentNode.InnerText));
            });

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

        string GetSummary(CregeenEntry entry)
        {
            if (basic && !string.IsNullOrWhiteSpace(entry.Definition))
            {
                return entry.Definition;
            }
                
            // decode the HTML
            HtmlDocument doc = new();
            doc.LoadHtml(entry.EntryHtml);

            return HttpUtility.HtmlDecode(doc.DocumentNode.InnerText);
        }

        DictionarySummary GetDictionarySummary(CregeenEntry entry)
        {
            var summary = GetSummary(entry);
            var label = GrammarLabelOf(entry.EntryHtml);
            // entries without a plain Definition fall back to the entry text,
            // which opens with the printed label ("a. id. Aashagh..."): the
            // label rides beside the headword, so it leaves the text
            if (label != null && summary.TrimStart().StartsWith(label, StringComparison.Ordinal))
            {
                summary = summary.TrimStart()[label.Length..].TrimStart();
            }
            return new DictionarySummary
            {
                PartsOfSpeech = entry.PartsOfSpeech,
                PrimaryWord = entry.Words.First(),
                Summary = summary,
                GrammarLabel = label,
                GenderNote = GenderNoteOf(entry.Notes),
                Citations = VerseCitations.FindAll(summary),
            };
        }
    }

    /// <summary>The "gender:" editorial note, reader-ready: the corpus
    /// evidence against the printed gender, without the tool stamp</summary>
    internal static string? GenderNoteOf(string? notes)
    {
        const string prefix = "gender: ";
        var at = notes?.IndexOf(prefix, StringComparison.Ordinal) ?? -1;
        if (at < 0)
        {
            return null;
        }
        var text = notes![(at + prefix.Length)..];
        return System.Text.RegularExpressions.Regex
            .Replace(text, @"\s*\[gender_check[^\]]*\]", "").Trim();
    }

    /// <summary>Cregeen's printed grammar label - the entry's leading italic
    /// run ("s. m.", "s. f.", "v."): word class and gender, absent from the
    /// basic summary text, surfaced so the client can show it beside the
    /// headword with the expansion on hover</summary>
    internal static string? GrammarLabelOf(string? entryHtml)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            entryHtml ?? "", @"^\s*<i>\s*([^<]{1,30}?)\s*</i>");
        return match.Success ? match.Groups[1].Value : null;
    }
}