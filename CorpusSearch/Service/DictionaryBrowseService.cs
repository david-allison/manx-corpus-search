using System;
using System.Collections.Generic;
using System.Linq;

namespace CorpusSearch.Service;

/// <summary>
/// The dictionary as a book you can open at a letter: A|B|C across the top, and
/// the letter's headwords under the prefixes they file under.
/// </summary>
public class DictionaryBrowseService(
    IEnumerable<ISearchDictionary> dictionaryServices, CorpusVocabulary vocabulary)
{
    private readonly ISearchDictionary[] dictionaries = dictionaryServices.ToArray();

    public ISearchDictionary? Find(string slug) =>
        dictionaries.FirstOrDefault(x =>
            string.Equals(x.Slug, slug, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// One letter of the index, whole. <paramref name="at"/> is a letter ("a"),
    /// or a prefix ("aal") from a link made when a prefix was a page of its own;
    /// the first letter when it names neither, so the URL always opens somewhere.
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
        var page = new DictionaryBrowsePage
        {
            Dictionary = dictionary.Identifier,
            Slug = dictionary.Slug,
            Letters = letters.Select(c => char.ToUpperInvariant(c).ToString()).ToList(),
            Chapters = [],
        };
        if (letters.Count == 0)
        {
            // the JSON is downloaded on deployment: without it the dictionary is
            // empty rather than broken, and so is its index
            return page;
        }

        // 'at' may be a letter or a prefix, and its first character says which
        // letter to open either way
        var asked = at == null ? "" : DictionaryBrowse.CollationKey(at);
        var letter = asked.Length > 0 && letters.Contains(asked[0]) ? asked[0] : letters[0];

        page.Letter = char.ToUpperInvariant(letter).ToString();
        page.Chapters = DictionaryBrowse
            .Chapters(
                headwords.Where(x => DictionaryBrowse.LetterOf(x) == letter),
                vocabulary.IsAttested)
            .ToList();
        return page;
    }

    /// <summary>
    /// A handful of a dictionary's entries spanning the range of corpus use — a
    /// couple of common words, the middling, the rare, and one no text says (a
    /// dictionary word): the letter bar invites A-and-onward reading, and this
    /// invites opening the book anywhere. Random each visit, and unordered.
    /// Null for an unknown dictionary.
    /// </summary>
    /// <param name="random">seedable for tests; the site rolls fresh</param>
    public List<DictionarySample>? Samples(string slug, int count, Random? random = null)
    {
        var dictionary = Find(slug);
        if (dictionary == null)
        {
            return null;
        }
        var headwords = dictionary.Headwords;
        var rng = random ?? Random.Shared;
        count = Math.Clamp(count, 1, 12);
        if (headwords.Count == 0)
        {
            return [];
        }

        // bands by corpus use: never said, rare, middling, common
        var bands = new[] { new List<string>(), [], [], [] };
        var wanted = Quota(count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // probe rather than scan: Phil Kelly is 66,000 headwords, and a page
        // of six needs no census. A band the probes never fill stays short.
        for (var probe = 0; probe < count * 50; probe++)
        {
            if (bands.Select((band, i) => band.Count >= wanted[i]).All(full => full))
            {
                break;
            }
            var word = headwords[rng.Next(headwords.Count)];
            if (!seen.Add(word) || !Sampleable(word))
            {
                continue;
            }
            var uses = vocabulary.AttestationsOf(word);
            if (uses == null)
            {
                continue; // a phrase the corpus is still being read for
            }
            var band = uses == 0 ? 0 : uses < 10 ? 1 : uses < 100 ? 2 : 3;
            if (bands[band].Count < wanted[band])
            {
                bands[band].Add(word);
            }
        }

        var samples = bands.SelectMany(x => x)
            .Select(word => new DictionarySample
            {
                Word = word,
                Summary = dictionary.GetSummaries(word, basic: true)
                    .FirstOrDefault()?.Summary,
                Attestations = vocabulary.AttestationsOf(word),
                Attested = (vocabulary.AttestationsOf(word) ?? 1) > 0,
            })
            .ToArray();
        rng.Shuffle(samples);
        return [.. samples];
    }

    /// <summary>How many of each band a page of <paramref name="count"/> wants:
    /// one word no text says, the rest spread from common down</summary>
    private static int[] Quota(int count)
    {
        var quota = new int[4];
        quota[0] = count > 1 ? 1 : 0;
        var order = new[] { 3, 2, 1 }; // common, middling, rare
        for (var i = 0; i < count - quota[0]; i++)
        {
            quota[order[i % order.Length]]++;
        }
        return quota;
    }

    /// <summary>A headword the word page opens cleanly: letters at both ends —
    /// no affixes ('-al'), no trailing-dot keys (Phil Kelly's 'a.r.e.', which
    /// the lookup's punctuation trim still misses)</summary>
    private static bool Sampleable(string word) =>
        word.Length > 0 && char.IsLetter(word[0]) && char.IsLetter(word[^1]);

    /// <summary>
    /// The headwords either side of a word, for stepping through a dictionary the
    /// way you turn a page.
    ///
    /// A word that is not a headword ('gheiney', an inflection) still has
    /// neighbours: it is placed where it would be filed, between the headwords
    /// it falls between. Scoped to one dictionary the order is that book's own;
    /// across all of them it is the union in collation order, which is nobody's
    /// printed order but is the only one they can share.
    /// </summary>
    public DictionaryNeighbours Neighbours(string? slug, string word)
    {
        var scope = slug == null ? null : Find(slug);
        if (slug != null && scope == null)
        {
            return new DictionaryNeighbours { Word = word };
        }

        var ordered = scope != null
            ? scope.Headwords.ToList()
            // no book's order can be kept across books, so the union takes the
            // reader's: a word in several dictionaries is one step, not three
            : dictionaries
                .Where(x => x.QueryLanguages.Contains("gv"))
                .SelectMany(x => x.Headwords)
                .Distinct(StringComparer.InvariantCultureIgnoreCase)
                .OrderBy(DictionaryBrowse.CollationKey, StringComparer.Ordinal)
                .ToList();
        if (ordered.Count == 0)
        {
            return new DictionaryNeighbours { Word = word };
        }

        var at = ordered.FindIndex(x =>
            string.Equals(x, word, StringComparison.InvariantCultureIgnoreCase));
        if (at >= 0)
        {
            // a step to a headword spelled as this one is a step to where you
            // already are: the URL is the spelling, so Cregeen's two 'baare' and
            // Kelly's five 'A' are each one page, however many entries they are
            var back = Step(ordered, at, -1, word, used: false);
            var forward = Step(ordered, at, +1, word, used: false);
            return new DictionaryNeighbours
            {
                Word = word,
                Attested = vocabulary.IsAttested(word),
                Previous = back,
                PreviousAttested = back != null && vocabulary.IsAttested(back),
                Next = forward,
                NextAttested = forward != null && vocabulary.IsAttested(forward),
                // the nearest word either side the corpus uses is rarely the one
                // next door: stepping through Phil Kelly one headword at a time
                // walks a long way through words no text says
                PreviousUsed = Step(ordered, at, -1, word, used: true),
                NextUsed = Step(ordered, at, +1, word, used: true),
            };
        }

        // not a headword: file it, and take the entries it would sit between.
        // The scoped list is the book's order, which is not the collation's, so
        // this is where it would go rather than where a binary search says.
        var key = DictionaryBrowse.CollationKey(word);
        string? previous = null;
        string? next = null;
        string? previousUsed = null;
        string? nextUsed = null;
        foreach (var headword in ordered)
        {
            var compared = string.CompareOrdinal(DictionaryBrowse.CollationKey(headword), key);
            if (compared < 0)
            {
                if (Nearer(headword, previous, after: false))
                {
                    previous = headword;
                }
                if (Nearer(headword, previousUsed, after: false) && vocabulary.IsAttested(headword))
                {
                    previousUsed = headword;
                }
            }
            if (compared > 0)
            {
                if (Nearer(headword, next, after: true))
                {
                    next = headword;
                }
                if (Nearer(headword, nextUsed, after: true) && vocabulary.IsAttested(headword))
                {
                    nextUsed = headword;
                }
            }
        }
        return new DictionaryNeighbours
        {
            Word = word,
            Attested = vocabulary.IsAttested(word),
            Previous = previous,
            PreviousAttested = previous != null && vocabulary.IsAttested(previous),
            Next = next,
            NextAttested = next != null && vocabulary.IsAttested(next),
            PreviousUsed = previousUsed,
            NextUsed = nextUsed,
        };
    }

    /// <summary>Whether a headword sits closer to the word being filed than the
    /// best found so far, on the given side of it</summary>
    private static bool Nearer(string headword, string? best, bool after)
    {
        if (best == null)
        {
            return true;
        }
        var compared = string.CompareOrdinal(
            DictionaryBrowse.CollationKey(headword), DictionaryBrowse.CollationKey(best));
        return after ? compared < 0 : compared > 0;
    }

    /// <summary>
    /// The nearest headword from <paramref name="at"/> in the given direction
    /// that is a page of its own: one spelled differently from
    /// <paramref name="word"/>, and — when <paramref name="used"/> — one the
    /// corpus actually says.
    /// </summary>
    private string? Step(List<string> ordered, int at, int step, string word, bool used)
    {
        for (var i = at + step; i >= 0 && i < ordered.Count; i += step)
        {
            if (string.Equals(ordered[i], word, StringComparison.InvariantCultureIgnoreCase))
            {
                continue;
            }
            if (!used || vocabulary.IsAttested(ordered[i]))
            {
                return ordered[i];
            }
        }
        return null;
    }
}
