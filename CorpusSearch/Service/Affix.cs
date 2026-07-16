using CorpusSearch.Dependencies.Lucene;

namespace CorpusSearch.Service;

/// <summary>
/// The dictionary headwords that are not words. Cregeen prints 'an-', 'neu-',
/// 'aa-' and '-agh' as headwords; Kelly prints '‑YS'. Each is only ever part of
/// a longer word.
///
/// No text says one on its own — but texts say them constantly, inside the words
/// they build: 'aa-vioghey' (revive) carries 'aa-' as plainly as any sentence
/// carries any word, 69 spellings of it across 85 documents. So an affix is
/// attested, and its evidence is the words carrying it.
///
/// What it is *not* attested by is the bare token it is spelled like, which is
/// somebody else entirely: the corpus says 'an' 126 times meaning *their*
/// (Phillips writes "’an"), and none of that is the prefix 'an-'. Reading the
/// two together made the prefix's page open on 130 lines of the Psalms saying
/// "their".
///
/// Both facts have to be read off the headword's spelling, and there is nowhere
/// else left to read them: <see cref="LemmaTable.NormalizeForm"/> folds a hyphen
/// to a space and trims it, so 'an-' and 'an' reach the lemma table as one key
/// and the lexeme cannot tell them apart afterwards.
/// </summary>
public static class Affix
{
    /// <summary>The books print the plain hyphen and the non-breaking one
    /// interchangeably (Kelly's "‑YS"), and neither is a different claim</summary>
    private static bool IsHyphen(char c) => c is '-' or '‑';

    /// <summary>Whether the headword is an affix rather than a word</summary>
    public static bool Is(string word)
    {
        var trimmed = word.Trim();
        // a lone hyphen is not an affix; it is not anything
        return trimmed.Length > 1 && (IsHyphen(trimmed[0]) || IsHyphen(trimmed[^1]));
    }

    /// <summary>
    /// The corpus query for the words carrying the affix: 'aa-' → "aa-*",
    /// '-ys' → "*-ys". What a page about a prefix should be showing.
    ///
    /// The wildcard is doing the work the hyphen cannot do anywhere else. It
    /// separates the two exactly: 'aa' matches 180 tokens, 'aa-*' matches the
    /// 169 that are the prefix, and the 11 left over are the bare word. For 'an'
    /// it is 252, 125 and 126.
    /// </summary>
    public static string CorpusQuery(string word)
    {
        var trimmed = word.Trim();
        // the tokenizer holds a hyphen inside the token it joins, so the affix's
        // own hyphen is part of what is being matched: only its outer edge is a
        // wildcard. Normalized to the plain hyphen, which is what is indexed.
        var prefix = IsHyphen(trimmed[^1]);
        var stem = (prefix ? trimmed[..^1] : trimmed[1..]).Replace('‑', '-');
        return prefix ? $"{stem}-*" : $"*-{stem}";
    }
}
