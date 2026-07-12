using System;
using System.Collections.Generic;
using System.Linq;
using CorpusSearch.Dependencies.Lucene;

namespace CorpusSearch.Service;

/// <summary>
/// Resolves a user's selection to summaries from the dictionaries which handle the query language.
///
/// The selection alone is often not enough (#135): the surrounding text lets us expand a word to a
/// known phrase/idiom, and a compound such as 'goll-mygeayrt' can be broken into its parts.
/// </summary>
public class DictionaryLookupService(IEnumerable<ISearchDictionary> dictionaryServices, LemmaTable lemmaTable)
{
    /// <summary>The longest dictionary phrase (in words) we attempt to match around a selection</summary>
    private const int MaxPhraseWords = 4;

    /// <summary>Trimmed from the edges of each word; apostrophes/hyphens are word-internal in Manx</summary>
    private static readonly char[] Punctuation = ['.', ',', '?', ';', ':', '!', '(', ')', '[', ']', '{', '}', '"', '“', '”', '…'];

    /// <summary>Each marks a compound/contraction whose parts may have their own entries</summary>
    private static readonly char[] WordSeparators = ['-', '\'', '’'];

    private readonly ISearchDictionary[] dictionaryServices = dictionaryServices.ToArray();

    /// <param name="lang">the query language, for example 'gv'</param>
    /// <param name="selection">the word/phrase the user selected</param>
    /// <param name="context">the text surrounding the selection (typically the line it appears in)</param>
    public List<DictionarySummary> Lookup(string lang, string selection, string? context = null)
    {
        var dictionaries = dictionaryServices.Where(x => x.QueryLanguages.Contains(lang)).ToList();

        List<DictionarySummary> GetSummaries(IEnumerable<string> queries) =>
            queries.SelectMany(query =>
                dictionaries.SelectMany(d => d.GetSummaries(query, basic: true)
                        .Select(summary =>
                        {
                            // let the client attribute each entry to its dictionary (#51)
                            summary.DictionaryName = d.Identifier;
                            return summary;
                        }))
                    // an entry can list the query as a mere variant ('EEN, YN'): those headed by it come first
                    .OrderBy(x => string.Equals(x.PrimaryWord, query, StringComparison.InvariantCultureIgnoreCase) ? 0 : 1))
                .ToList();

        var candidates = GetCandidates(selection, context);
        var results = GetSummaries(candidates);
        if (lang == "gv")
        {
            // the root chain of an inflected/mutated selection follows the surface
            // candidates, each hop tagged with its depth so the client can nest it
            // ('gheiney' -> 'deiney' -> 'dooinney'): the reader always gets the
            // headwords a dictionary actually lists, without them posing as
            // entries for the selection
            var seen = new HashSet<string>(candidates, StringComparer.InvariantCultureIgnoreCase);
            var frontier = lemmaTable.DisplayLemmasFor(selection).Where(x => !seen.Contains(x)).ToList();
            for (var depth = 1; frontier.Count > 0 && depth <= 3; depth++)
            {
                seen.UnionWith(frontier);
                foreach (var summary in GetSummaries(frontier))
                {
                    summary.RootDepth = depth;
                    results.Add(summary);
                }
                // deeper hops follow paradigm links only: a mutation guess is a
                // candidate reading of the selection, not a root of the root
                frontier = frontier
                    .SelectMany(root => lemmaTable.RootDisplayLemmasFor(root))
                    .Where(x => !seen.Contains(x))
                    .Distinct(StringComparer.InvariantCultureIgnoreCase)
                    .ToList();
            }
        }

        if (results.Count == 0)
        {
            // 'goll-mygeayrt' has no entry, but 'goll' and 'mygeayrt' do
            results = GetSummaries(GetParts(selection));
        }

        return Deduplicate(results);
    }

    /// <summary>
    /// The queries to attempt, most specific first: phrases from the context containing the
    /// selection (longest first), then the selection itself. Compounds are written both
    /// hyphenated and as separate words, so each candidate is also tried with hyphens
    /// exchanged for spaces (and vice versa), and with each apostrophe style.
    /// </summary>
    private static List<string> GetCandidates(string selection, string? context)
    {
        var selectionWords = Tokenize(selection);
        if (selectionWords.Count == 0)
        {
            return [];
        }

        var phrases = new List<List<string>>();
        var contextWords = Tokenize(context);
        foreach (var occurrence in FindOccurrences(contextWords, selectionWords))
        {
            for (int length = selectionWords.Count + 1; length <= MaxPhraseWords; length++)
            {
                for (int start = occurrence + selectionWords.Count - length; start <= occurrence; start++)
                {
                    if (start < 0 || start + length > contextWords.Count)
                    {
                        continue;
                    }
                    phrases.Add(contextWords.GetRange(start, length));
                }
            }
        }

        var candidates = phrases
            .OrderByDescending(x => x.Count)
            .Append(selectionWords)
            .Select(words => string.Join(" ", words))
            .SelectMany(HyphenVariants)
            .SelectMany(ApostropheVariants);

        return candidates.Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();
    }

    /// <summary>'goll-mygeayrt' is also tried as 'goll mygeayrt', and vice versa</summary>
    private static IEnumerable<string> HyphenVariants(string candidate)
    {
        yield return candidate;
        yield return candidate.Replace('-', ' ');
        yield return candidate.Replace(' ', '-');
    }

    /// <summary>The texts and dictionaries mix typewriter (') and typographic (’) apostrophes</summary>
    private static IEnumerable<string> ApostropheVariants(string candidate)
    {
        yield return candidate;
        yield return candidate.Replace('’', '\'');
        yield return candidate.Replace('\'', '’');
    }

    /// <summary>The starting indexes at which the selection occurs within the context</summary>
    private static IEnumerable<int> FindOccurrences(List<string> contextWords, List<string> selectionWords)
    {
        for (int i = 0; i + selectionWords.Count <= contextWords.Count; i++)
        {
            if (selectionWords.Select((word, offset) => (word, offset))
                .All(x => string.Equals(contextWords[i + x.offset], x.word, StringComparison.InvariantCultureIgnoreCase)))
            {
                yield return i;
            }
        }
    }

    /// <summary>
    /// The component words of a compound/contraction: 'goll-mygeayrt' -> ['goll', 'mygeayrt'],
    /// "mooad's" (#337) -> ['mooad'].
    /// </summary>
    private static List<string> GetParts(string selection)
    {
        var queried = string.Join(" ", Tokenize(selection));

        return Tokenize(selection)
            .SelectMany(x => x.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries))
            .Where(x => x.Any(char.IsLetter))
            // a lone letter is the stub of a contraction ('t' from t'eh, the possessive 's'), not a word
            .Where(x => x.Length > 1)
            // the selection itself was already queried
            .Where(x => !x.Equals(queried, StringComparison.InvariantCultureIgnoreCase))
            .Distinct(StringComparer.InvariantCultureIgnoreCase)
            .ToList();
    }

    private static List<string> Tokenize(string? text) =>
        (text ?? "")
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim(Punctuation))
            .Where(x => x.Any(char.IsLetter))
            .ToList();

    /// <summary>An entry can match more than one candidate (e.g. both hyphen variants)</summary>
    private static List<DictionarySummary> Deduplicate(IEnumerable<DictionarySummary> summaries) =>
        summaries
            .GroupBy(x => (x.PrimaryWord, x.Summary))
            .Select(x => x.First())
            .ToList();
}
