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
        // an affix is not a word a text can say, whatever the index holds: 'an-'
        // is only ever the front of a longer word, and what is built from it is
        // its own headword. Asked before anything below, because everything below
        // drops the hyphen that says it is bound — and would then answer for the
        // standalone 'an' the corpus really does say.
        if (LemmaTable.IsAffix(word))
        {
            return false;
        }
        // an index that never loaded would grey out the whole language
        if (!loaded)
        {
            return true;
        }
        if (UsesEveryWordOf(word))
        {
            return true;
        }
        return lemmaTable.DisplayLemmasFor(word).Any(attestedLemmas.Contains);
    }

    private bool UsesEveryWordOf(string headword)
    {
        var words = DocumentLine.NormalizeManx(headword)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length > 0 && Array.TrueForAll(words, terms.Contains);
    }
}
