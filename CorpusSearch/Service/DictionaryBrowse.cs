using System;
using System.Collections.Generic;
using System.Linq;

namespace CorpusSearch.Service;

/// <summary>
/// Browsing a dictionary the way its printed index works: A|B|C across the top,
/// then the whole letter, its headwords filed under the prefix each one starts
/// with.
/// </summary>
public static class DictionaryBrowse
{
    /// <summary>How many letters of a headword name the chapter it files under.
    /// Three is what a printed index uses, and it is what the reader scans down
    /// the margin: at two letters Phil Kelly's 'co' is 2,548 words, and past
    /// three a chapter is most of the word it is filing.</summary>
    public const int ChapterDepth = 3;

    /// <summary>
    /// How the books alphabetise, which is not how a computer does.
    ///
    /// Cregeen prints 'agh-markiagh' among the 'agh...' words and 'atçhim'
    /// before 'att': hyphens, spaces and apostrophes are not letters, and ç
    /// files under c. Kelly prints its headwords in capitals. Fold all of it
    /// away and the two books agree with each other and with the reader.
    /// </summary>
    public static string CollationKey(string headword)
    {
        var folded = new System.Text.StringBuilder(headword.Length);
        foreach (var c in headword)
        {
            switch (char.ToLowerInvariant(c))
            {
                case '-' or '\'' or '’' or ' ':
                    break;
                case 'ç':
                    folded.Append('c');
                    break;
                default:
                    folded.Append(char.ToLowerInvariant(c));
                    break;
            }
        }
        return folded.ToString();
    }

    /// <summary>The letter a headword files under; '\0' when it folds to nothing
    /// (Cregeen's suffix entries, '-al', file under the letter after the hyphen)</summary>
    public static char LetterOf(string headword)
    {
        var key = CollationKey(headword);
        return key.Length == 0 ? '\0' : key[0];
    }

    /// <summary>
    /// The letters a dictionary has headwords for, in order.
    /// </summary>
    /// <remarks>Derived rather than declared: the hardcoded
    /// <see cref="Dictionaries.CregeenDictionaryService.LetterLookup"/> is Cregeen's
    /// alone and has no ç, which 39 of its own headwords and 129 of Kelly's start
    /// with. A letter the data has is a letter the bar shows.</remarks>
    public static IReadOnlyList<char> LettersOf(IEnumerable<string> headwords) =>
        headwords.Select(LetterOf).Where(c => c != '\0').Distinct().Order().ToList();

    /// <summary>A headword's chapter at a depth; the whole word when it is shorter
    /// ('ad' is its own chapter at three letters, 'a' its own at one)</summary>
    public static string PrefixOf(string headword, int depth)
    {
        var key = CollationKey(headword);
        return key.Length <= depth ? key : key[..depth];
    }

    /// <summary>
    /// A letter's headwords in chapters: a new one each time the prefix changes.
    ///
    /// Chunked in the order given rather than grouped, because the order given is
    /// the book's and grouping would leave it. Cregeen files 'faar-y-chaagh'
    /// among the 'caa' words, so its 'f' opens with an FAA of that one word and
    /// meets FAA again where the F section proper begins. A chapter key appearing
    /// twice is the book being honest — 11 times in Cregeen, 23 in Kelly —
    /// where gathering the two would move a word out of the place it is printed.
    ///
    /// A headword printed more than once in a row is another matter: the book
    /// heads five entries 'A' because it has five senses to define, but the index
    /// is a way in and not the book, and five identical links to the one page ('A'
    /// resolves by its spelling, and its page carries all five senses) are one
    /// entry to a reader. So a run of the same spelling folds to one. Only against
    /// the word beside it — the double-back opens a fresh chapter, so a spelling
    /// repeating across that seam is left alone.
    /// </summary>
    /// <param name="attested">Whether the corpus uses a word; everything is taken
    /// as used when nothing is passed, so a caller with no index greys nothing</param>
    /// <param name="sourceOf">names the file whose print attests a word the
    /// corpus never says ("cregeen"): the lemma index's voucher for its greyed
    /// rows. Only asked about the greyed — an attested word needs none.</param>
    public static IReadOnlyList<BrowseChapter> Chapters(
        IEnumerable<string> headwords, Func<string, bool>? attested = null,
        Func<string, string?>? sourceOf = null)
    {
        var chapters = new List<BrowseChapter>();
        foreach (var headword in headwords)
        {
            var key = PrefixOf(headword, ChapterDepth).ToUpperInvariant();
            if (chapters.Count == 0 || chapters[^1].Key != key)
            {
                chapters.Add(new BrowseChapter { Key = key, Words = [] });
            }
            var words = chapters[^1].Words;
            // the same spelling twice over is one link twice over: fold it
            if (words.Count > 0 && words[^1].Word == headword)
            {
                continue;
            }
            var isAttested = attested?.Invoke(headword) ?? true;
            words.Add(new BrowseWord
            {
                Word = headword,
                Attested = isAttested,
                Source = isAttested ? null : sourceOf?.Invoke(headword),
            });
        }
        return chapters;
    }
}

/// <summary>A dictionary's index: the letters, and one letter's headwords in
/// their chapters</summary>
public class DictionaryBrowsePage
{
    public required string Dictionary { get; set; }
    public required string Slug { get; set; }
    /// <summary>Every letter the dictionary has headwords for, in capitals as a
    /// printed index has them</summary>
    public required List<string> Letters { get; set; }
    /// <summary>The letter being shown, in capitals; null when the dictionary is
    /// empty</summary>
    public string? Letter { get; set; }
    /// <summary>The whole letter, chapter by chapter</summary>
    public required List<BrowseChapter> Chapters { get; set; }
}

/// <summary>One prefix and the headwords filed under it</summary>
public class BrowseChapter
{
    /// <summary>The prefix in capitals, as a printed index heads its column:
    /// 'AAL', or 'AD' where the word is shorter than the chapter is deep</summary>
    public required string Key { get; set; }
    /// <summary>The chapter's headwords, each spelling once: the book prints five
    /// entries 'A', but they are one link and the index shows one (see
    /// <see cref="DictionaryBrowse.Chapters"/>)</summary>
    public required List<BrowseWord> Words { get; set; }
}

/// <summary>A headword in the index, and whether the corpus ever says it</summary>
public class BrowseWord
{
    /// <summary>As the dictionary prints it: Kelly capitalises, Cregeen does not</summary>
    public required string Word { get; set; }
    /// <summary>False where no text we hold uses the word: a dictionary lists what
    /// the language can say, and this is what it has said</summary>
    public required bool Attested { get; set; }

    /// <summary>The file whose print attests a word the corpus never says
    /// ("cregeen"): the lemma index's voucher for its greyed rows, which
    /// without it read as phantoms when in fact a book prints them. Null
    /// where the corpus speaks for the word, and on the book indexes, whose
    /// every word is the book's own.</summary>
    public string? Source { get; set; }
}

/// <summary>One entry of the browse sampler: a way into the book that is not
/// the letter A</summary>
public class DictionarySample
{
    public required string Word { get; set; }
    /// <summary>The entry's short gloss, as the popups use it; null where the
    /// book has none to give</summary>
    public string? Summary { get; set; }
    /// <summary>How often the corpus says the word; null while not yet known</summary>
    public long? Attestations { get; set; }
    /// <summary>False only at a known 0: the dictionary-only word the sampler
    /// deals in on purpose</summary>
    public bool Attested { get; set; }
}

/// <summary>The headwords either side of a word, for stepping through a
/// dictionary the way you turn a page</summary>
public class DictionaryNeighbours
{
    public required string Word { get; set; }
    /// <summary>Null at the dictionary's first headword, or when it has none</summary>
    public string? Previous { get; set; }
    public string? Next { get; set; }
    /// <summary>Whether the corpus uses the word itself</summary>
    public bool Attested { get; set; }
    /// <summary>Whether the corpus uses <see cref="Previous"/>; false when there
    /// is none</summary>
    public bool PreviousAttested { get; set; }
    public bool NextAttested { get; set; }
    /// <summary>The nearest headword either side the corpus actually uses, which
    /// is not usually the one next door: half of Phil Kelly is unattested, so
    /// stepping one word at a time can walk a long way through words no text
    /// says. Null when there is none left in that direction.</summary>
    public string? PreviousUsed { get; set; }
    public string? NextUsed { get; set; }

    /// <summary>The complete windows of the nearest pages either side, when a
    /// span was asked for: previous pages nearest-first, then next pages
    /// nearest-first, each carrying its own arrows and skips. The walk's
    /// client steps through these without asking again, so a reader tapping
    /// faster than a round trip is never stopped. Empty without a span, and
    /// for a word that is not a headword (its first step lands on one, whose
    /// own answer brings a span).</summary>
    public List<DictionaryNeighbours> Nearby { get; set; } = [];
}
