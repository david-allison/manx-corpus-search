using System;
using System.Collections.Generic;
using System.Linq;
using CorpusSearch.Dependencies.Lucene;
using CorpusSearch.Model;

namespace CorpusSearch.Service;

/// <summary>
/// Every Manx word the corpus actually says.
///
/// A dictionary lists what the language can say; this lists what it has said,
/// and the gap between them is worth showing. Phil Kelly is a 66,000-word
/// revival vocabulary and half of it appears in no text we hold — a reader
/// browsing it is owed the difference between a word Manx uses and a word
/// somebody once proposed.
///
/// The index's whole term list is read at startup already, for the statistics
/// page, so this costs nothing to build; and it answers in a hash lookup, which
/// is what a browse page of 10,773 headwords needs it to do.
/// </summary>
public class CorpusVocabulary(LemmaTable lemmaTable)
{
    /// <summary>Set by <see cref="Init"/> before the server starts serving. Until
    /// then nothing is attested, which is the honest answer for an empty index —
    /// and the browse page would rather say nothing than grey the language out.</summary>
    private HashSet<string> terms = [];

    /// <summary>Every lemma the corpus has a word for, worked out once. A term the
    /// corpus says attests each lemma it is a form of, so reading the 52,000 terms
    /// this way settles the whole table in one pass — where asking a word at a time
    /// would walk its paradigm again for each of a browse page's ten thousand.</summary>
    private HashSet<string> attestedLemmas = [];

    /// <summary>How often the corpus says each term, keyed by the lemma table's
    /// fold: the table spaces a hyphenated form ('aa-vioghey' is 'aa vioghey'
    /// there), so asking about a *form* has to fold the corpus to match. Two
    /// terms folding together ('aa-vioghey'/'Aa-vioghey') sum.</summary>
    private Dictionary<string, long> formAttestations = [];

    /// <summary>How often the corpus says each distinct word, by the word's own
    /// spelling rather than the table's fold: what the dictionary's front page
    /// counts its coverage over, a word at a time with its weight</summary>
    private Dictionary<string, long> frequencies = [];

    /// <summary>How often the corpus says each multiword headword, once
    /// <see cref="ScanPhrases"/> has read it for them: a phrase it read for and
    /// never met is 0, one it has not read for is absent. Null until the scan
    /// lands, and a phrase is met a word at a time in the meantime — the guess
    /// that was the only answer there was before this existed.</summary>
    private Dictionary<string, long>? attestedPhrases;

    private bool loaded;

    public void Init(IEnumerable<(string Term, long Frequency)> termFrequency)
    {
        terms = [];
        formAttestations = [];
        frequencies = [];
        foreach (var (term, frequency) in termFrequency)
        {
            var normalized = DocumentLine.NormalizeManx(term);
            terms.Add(normalized);
            frequencies[normalized] = frequencies.GetValueOrDefault(normalized) + frequency;
            var folded = LemmaTable.NormalizeForm(normalized);
            formAttestations[folded] = formAttestations.GetValueOrDefault(folded) + frequency;
        }
        attestedLemmas = terms.SelectMany(lemmaTable.DisplayLemmasFor).ToHashSet();
        loaded = true;
    }

    /// <summary>The longest a headword phrase runs: Phil Kelly's longest is twelve
    /// words, and a line offers no n-gram longer than the phrase looked for</summary>
    private const int LongestPhrase = 16;

    /// <summary>
    /// Reads the corpus for the books' multiword headwords, and remembers which of
    /// them it actually says.
    ///
    /// A phrase cannot be met a word at a time — the index holds tokens, and
    /// 'aachummey eddin' is not said by a text saying 'aachummey' in one place and
    /// 'eddin' in another. But nor can it be asked one phrase at a time: 6,395 of
    /// the 10,804 headwords on Phil Kelly's 'c' page are phrases, and a phrase
    /// query each would take a minute to paint one page. So the corpus is read
    /// once, here, and every phrase is answered from the result in a hash lookup.
    ///
    /// Deliberately off the startup path (Startup runs it behind the server): the
    /// pass walks two million tokens, and nobody should wait to read a page for an
    /// answer only a browse index greys by.
    /// </summary>
    /// <param name="headwords">every headword the books print; the single words are
    /// ignored, being answered by <see cref="terms"/> already</param>
    /// <param name="corpusLines">the Manx of every line, as the statistics count it</param>
    public void ScanPhrases(IEnumerable<string> headwords, IEnumerable<string> corpusLines)
    {
        var wanted = new HashSet<string>();
        var opening = new HashSet<string>();
        foreach (var headword in headwords)
        {
            var words = Words(headword);
            if (words.Length is > 1 and <= LongestPhrase)
            {
                wanted.Add(string.Join(' ', words));
                // most tokens open no headword at all, so this is what keeps the
                // pass to one hash lookup for nearly every word of the corpus
                opening.Add(words[0]);
            }
        }
        var found = new Dictionary<string, long>();
        foreach (var line in corpusLines)
        {
            var words = Words(line);
            for (var i = 0; i < words.Length; i++)
            {
                if (!opening.Contains(words[i]))
                {
                    continue;
                }
                var last = Math.Min(words.Length, i + LongestPhrase);
                for (var end = i + 2; end <= last; end++)
                {
                    var phrase = string.Join(' ', words, i, end - i);
                    if (wanted.Contains(phrase))
                    {
                        found[phrase] = found.GetValueOrDefault(phrase) + 1;
                    }
                }
            }
        }
        // only what was found is kept: a phrase asked about came from `headwords`,
        // so one that is not here is one the corpus does not say
        attestedPhrases = found;
    }

    private static string[] Words(string text) =>
        DocumentLine.NormalizeManx(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>How many distinct words the corpus uses</summary>
    public int Count => terms.Count;

    /// <summary>
    /// Whether the corpus uses the word — by that spelling, or by one the lemma
    /// table calls the same word.
    ///
    /// A phrase is answered from the read <see cref="ScanPhrases"/> made of the
    /// corpus, and only met a word at a time until that lands: 'aachummey eddin'
    /// is not said by a text saying 'aachummey' in one place and 'eddin' in
    /// another, and claiming so left a browse index un-greying 4,444 phrases on
    /// one page whose word pages then had nothing to show.
    ///
    /// The lemma hop is what saves a headword the corpus only ever writes
    /// mutated: 'yaagh' is 'jaagh' after a lenition, and a text that says the one
    /// attests the other. It reaches 16% of Phil Kelly — the table is built from
    /// Cregeen and J Kelly, which is most of what it knows.
    /// </summary>
    public bool? Attestation(string word)
    {
        // an index that never loaded would grey out the whole language
        if (!loaded)
        {
            return true;
        }
        // an affix is attested by the words carrying it and by nothing else, and
        // it is asked first because everything below drops the hyphen that marks
        // it bound: 'an-' would otherwise be attested by the 126 uses of 'an'
        // meaning *their*. Costs a walk of the term list, but only the sixteen
        // affix headwords in the books ever take this path.
        if (Affix.Is(word))
        {
            return CarriedByATerm(word);
        }
        var words = Words(word);
        if (words.Length > 1)
        {
            // the corpus is read for a phrase, never guessed at: no lemma hop
            // either — a phrase's mutations are not a paradigm the table holds,
            // and the word page's own scan is as literal as this
            return attestedPhrases == null
                ? null
                : attestedPhrases.GetValueOrDefault(string.Join(' ', words)) > 0;
        }
        if (words.Length > 0 && Array.TrueForAll(words, terms.Contains))
        {
            return true;
        }
        return lemmaTable.DisplayLemmasFor(word).Any(attestedLemmas.Contains);
    }

    /// <summary>Whether the corpus uses the word, for a caller with nowhere to put
    /// "not yet known" — a browse page of ten thousand headwords cannot ask each of
    /// them to say so. An unread phrase is left un-greyed: the index claims nothing
    /// it has not read, and greying is a claim.</summary>
    public bool IsAttested(string word) => Attestation(word) ?? true;

    /// <summary>
    /// How often the corpus says the form by this spelling itself — no lemma
    /// hop: the lemma tree asks which of a lexeme's spellings the texts use, and
    /// the hop would answer for the whole paradigm at once. The corpus is folded
    /// the table's way, so a spaced form is said by its hyphenated token ('aa
    /// vioghey' by 'aa-vioghey'), and a spaced form of several words is a phrase,
    /// counted by the read <see cref="ScanPhrases"/> makes. Null while not yet
    /// known — before the index loads, or for a phrase before the read lands (a
    /// hyphenated token's count stands in meanwhile: an undercount beats a
    /// claim of silence).
    /// </summary>
    public long? AttestationsOf(string form)
    {
        if (!loaded)
        {
            return null;
        }
        // an affix is said only by the words carrying it, and the fold below
        // loses the hyphen that marks it bound: 'an-' must not count the 126
        // uses of 'an' meaning *their* (see Affix)
        if (Affix.Is(form))
        {
            return AffixAttestations(form);
        }
        var folded = LemmaTable.NormalizeForm(form);
        if (folded.Length == 0)
        {
            return 0;
        }
        var count = formAttestations.GetValueOrDefault(folded);
        if (!folded.Contains(' '))
        {
            return count;
        }
        if (attestedPhrases == null)
        {
            return count > 0 ? count : null;
        }
        return count + attestedPhrases.GetValueOrDefault(folded);
    }

    /// <summary>How often the corpus says the words carrying the affix — its
    /// only sayings. A carrier's hyphens fold to spaces in the count table, so
    /// the prefix's family is the keys opening "aa " ('aa-vioghey' files as
    /// 'aa vioghey'), and a suffix's the keys closing " ys".</summary>
    private long AffixAttestations(string affix)
    {
        var trimmed = affix.Trim().Replace('‑', '-');
        var folded = LemmaTable.NormalizeForm(trimmed);
        if (folded.Length == 0)
        {
            return 0;
        }
        var prefix = trimmed[^1] == '-';
        long count = 0;
        foreach (var (key, uses) in formAttestations)
        {
            if (prefix
                    ? key.StartsWith(folded + " ", StringComparison.Ordinal)
                    : key.EndsWith(" " + folded, StringComparison.Ordinal))
            {
                count += uses;
            }
        }
        return count;
    }

    /// <summary>Every distinct word the corpus says with how often it says it,
    /// as <see cref="Init"/> read them in</summary>
    public IEnumerable<(string Term, long Frequency)> TermFrequencies =>
        frequencies.Select(x => (x.Key, x.Value));

    /// <summary>How many of the table's lemmas some text attests</summary>
    public int AttestedLemmaCount => attestedLemmas.Count;

    /// <summary>Every distinct word the corpus says spelled with the prefix
    /// ('aa-' → aa-chroo): a compound needs no book to be real — the texts
    /// coin them freely, and a prefix's family is whatever is spelled with
    /// it</summary>
    public IEnumerable<string> TermsStartingWith(string prefix) =>
        terms.Where(t => t.Length > prefix.Length
                         && t.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether any word the corpus says carries the affix: 'aa-' is
    /// attested by 'aa-vioghey', which is the only way an affix ever is</summary>
    private bool CarriedByATerm(string affix)
    {
        var trimmed = affix.Trim().Replace('‑', '-');
        return trimmed[^1] == '-'
            // longer than the affix itself: 'aa-' is not carried by a bare 'aa-'
            ? terms.Any(t => t.Length > trimmed.Length && t.StartsWith(trimmed, StringComparison.Ordinal))
            : terms.Any(t => t.Length > trimmed.Length && t.EndsWith(trimmed, StringComparison.Ordinal));
    }
}
