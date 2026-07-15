using System;
using System.Collections.Generic;
using System.Linq;

namespace CorpusSearch.Service;

/// <summary>
/// The dictionary as a book you can open at a letter: A|B|C across the top, a
/// prefix bar under the letter, and the headwords under the prefix.
/// </summary>
public class DictionaryBrowseService(IEnumerable<ISearchDictionary> dictionaryServices)
{
    private readonly ISearchDictionary[] dictionaries = dictionaryServices.ToArray();

    /// <summary>How much of a definition an index line carries before it stops
    /// being a glance and starts being the entry</summary>
    private const int MaxGloss = 60;

    public ISearchDictionary? Find(string slug) =>
        dictionaries.FirstOrDefault(x =>
            string.Equals(x.Slug, slug, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// One page of the index. <paramref name="at"/> is a letter ("a") or a prefix
    /// ("aal"); the first letter when it names neither, so the URL always opens
    /// somewhere.
    /// </summary>
    public DictionaryBrowsePage? Page(string slug, string? at)
    {
        var dictionary = Find(slug);
        if (dictionary == null)
        {
            return null;
        }

        var headwords = dictionary.Headwords;
        var letters = DictionaryBrowse.LettersOf(headwords);
        var empty = new DictionaryBrowsePage
        {
            Dictionary = dictionary.Identifier,
            Slug = dictionary.Slug,
            Letters = letters.Select(c => c.ToString()).ToList(),
            Prefixes = [],
            Headwords = [],
        };
        if (letters.Count == 0)
        {
            // the JSON is downloaded on deployment: without it the dictionary is
            // empty rather than broken, and so is its index
            return empty;
        }

        // 'at' may be a letter or a prefix, and its first character says which
        // letter to open either way
        var asked = at == null ? "" : DictionaryBrowse.CollationKey(at);
        var letter = asked.Length > 0 && letters.Contains(asked[0]) ? asked[0] : letters[0];

        var ofLetter = headwords.Where(x => DictionaryBrowse.LetterOf(x) == letter).ToList();
        var depth = DictionaryBrowse.DepthFor(ofLetter);
        var prefixes = ofLetter
            .Select(x => DictionaryBrowse.PrefixOf(x, depth))
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();

        // a letter opens at its first prefix; a prefix deeper or shallower than
        // this letter's bar (a stale link, or 'a' for the whole letter) opens at
        // the nearest one at or after it
        var prefix = asked.Length > 1
            ? prefixes.FirstOrDefault(
                p => string.CompareOrdinal(p, DictionaryBrowse.PrefixOf(asked, depth)) >= 0)
              ?? prefixes[0]
            : prefixes[0];

        empty.Letter = letter.ToString();
        empty.Prefixes = prefixes;
        empty.Prefix = prefix;
        empty.Headwords = ofLetter
            .Where(x => DictionaryBrowse.PrefixOf(x, depth) == prefix)
            .Select(x => new BrowseHeadword { Word = x, Gloss = GlossOf(dictionary, x) })
            .ToList();
        return empty;
    }

    /// <summary>The opening of the headword's own entry. Basic summaries, because
    /// an index line is a glance: the full text belongs to the word's page.</summary>
    private static string? GlossOf(ISearchDictionary dictionary, string headword)
    {
        var summary = dictionary.GetSummaries(headword, basic: true).FirstOrDefault()?.Summary;
        if (string.IsNullOrWhiteSpace(summary))
        {
            return null;
        }
        summary = summary.Trim();
        if (summary.Length <= MaxGloss)
        {
            return summary;
        }
        var cut = summary[..MaxGloss];
        var lastSpace = cut.LastIndexOf(' ');
        return (lastSpace > MaxGloss / 2 ? cut[..lastSpace] : cut).TrimEnd() + "…";
    }
}
