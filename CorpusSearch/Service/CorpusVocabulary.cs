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

    private bool loaded;

    public void Init(IEnumerable<(string Term, long Frequency)> termFrequency)
    {
        terms = termFrequency.Select(x => DocumentLine.NormalizeManx(x.Term)).ToHashSet();
        attestedLemmas = terms.SelectMany(lemmaTable.DisplayLemmasFor).ToHashSet();
        loaded = true;
    }

    /// <summary>How many distinct words the corpus uses</summary>
    public int Count => terms.Count;

    /// <summary>
    /// Whether the corpus uses the word — by that spelling, or by one the lemma
    /// table calls the same word.
    ///
    /// The corpus indexes tokens rather than phrases, so a headword of several
    /// words ('cur my ner', half of Phil Kelly) can only be met one word at a
    /// time: every word of it being used is the most this can honestly claim.
    ///
    /// The lemma hop is what saves a headword the corpus only ever writes
    /// mutated: 'yaagh' is 'jaagh' after a lenition, and a text that says the one
    /// attests the other. It reaches 16% of Phil Kelly — the table is built from
    /// Cregeen and J Kelly, which is most of what it knows.
    /// </summary>
    public bool IsAttested(string word)
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
        if (UsesEveryWordOf(word))
        {
            return true;
        }
        return lemmaTable.DisplayLemmasFor(word).Any(attestedLemmas.Contains);
    }

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

    private bool UsesEveryWordOf(string headword)
    {
        var words = DocumentLine.NormalizeManx(headword)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length > 0 && Array.TrueForAll(words, terms.Contains);
    }
}
