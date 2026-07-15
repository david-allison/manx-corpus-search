using System;
using System.Collections.Generic;
using System.Linq;

namespace CorpusSearch.Service;

/// <summary>
/// Browsing a dictionary the way its printed index works: A|B|C across the top,
/// then a bar of prefixes under the letter, then the headwords themselves.
/// </summary>
public static class DictionaryBrowse
{
    /// <summary>Above this a prefix group is a wall of words rather than a place
    /// to look something up: it sets how deep <see cref="DepthFor"/> goes</summary>
    private const int MaxPerGroup = 60;

    /// <summary>Past four letters a prefix is most of the word it is filing</summary>
    private const int MaxDepth = 4;

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

    /// <summary>
    /// How many letters of prefix a letter's bar needs: the shallowest that
    /// leaves no group a wall.
    ///
    /// One depth for a whole dictionary fits none of it. Cregeen's 150 'a'
    /// headwords want two letters — at three they scatter into 79 groups of two —
    /// while its 376 'c' want three, and Kelly's 1,392 'c' still leave a group of
    /// 128 at three. So each letter gets its own.
    ///
    /// Some letters have no good depth at all. Phil Kelly is a 66,000-word
    /// translation list rather than a book with an index: its 'c' alone is 10,773
    /// headwords, which is 12 prefixes of 2,548 at two letters and 625 of 364 at
    /// four. Nothing here rescues that — it wants a browse of its own, or none.
    /// </summary>
    public static int DepthFor(IReadOnlyCollection<string> lettersHeadwords)
    {
        for (var depth = 2; depth < MaxDepth; depth++)
        {
            var biggest = lettersHeadwords.GroupBy(x => PrefixOf(x, depth)).Max(g => g.Count());
            if (biggest <= MaxPerGroup)
            {
                return depth;
            }
        }
        return MaxDepth;
    }

    /// <summary>A headword's group at a depth; the whole word when it is shorter
    /// ('ad' is its own group at three letters)</summary>
    public static string PrefixOf(string headword, int depth)
    {
        var key = CollationKey(headword);
        return key.Length <= depth ? key : key[..depth];
    }
}

/// <summary>A dictionary's index: the letters, one letter's prefixes, and one
/// prefix's headwords</summary>
public class DictionaryBrowsePage
{
    public required string Dictionary { get; set; }
    public required string Slug { get; set; }
    /// <summary>Every letter the dictionary has headwords for</summary>
    public required List<string> Letters { get; set; }
    /// <summary>The letter being shown; null when the dictionary is empty</summary>
    public string? Letter { get; set; }
    /// <summary>The letter's prefix bar, as deep as this letter needs</summary>
    public required List<string> Prefixes { get; set; }
    /// <summary>The prefix being shown</summary>
    public string? Prefix { get; set; }
    public required List<BrowseHeadword> Headwords { get; set; }
}

public class BrowseHeadword
{
    /// <summary>As the dictionary prints it: Kelly capitalises, Cregeen does not</summary>
    public required string Word { get; set; }
    /// <summary>The opening of its definition, for the index line</summary>
    public string? Gloss { get; set; }
}
