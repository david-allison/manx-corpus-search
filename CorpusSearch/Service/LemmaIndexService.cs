using System;
using System.Collections.Generic;
using System.Linq;
using CorpusSearch.Dependencies.Lucene;

namespace CorpusSearch.Service;

/// <summary>
/// The lemma index at /dictionary/lemma: every lemma the tables link a form to,
/// as an index you can open at a letter, and — for one lemma — the tree of its
/// forms, grouped by how each hangs off it.
/// </summary>
public class LemmaIndexService(LemmaTable lemmaTable, CorpusVocabulary vocabulary)
{
    // ordered the reader's way (collation), not the accessor's (ordinal, which
    // files 'Aachummey' among the capitals) — the chapters chunk in given order,
    // so the order given has to be the one the index reads in
    private readonly Lazy<List<string>> ordered = new(() =>
        lemmaTable.AllDisplayLemmas
            // an index by letter can only file what starts with one: the two
            // transcription artifacts in the data ('≈', '[s'tammyltee') would
            // otherwise each put a punctuation "letter" in the bar. Their trees
            // stay reachable by URL; only the index has no shelf for them.
            .Where(x => char.IsLetter(DictionaryBrowse.LetterOf(x)))
            .OrderBy(DictionaryBrowse.CollationKey, StringComparer.Ordinal)
            .ThenBy(x => x, StringComparer.Ordinal)
            .ToList());

    /// <summary>
    /// One letter of the lemma index, whole, in the shape the dictionary browse
    /// serves: the same letters, chapters and corpus greying, over lemmas instead
    /// of one book's headwords.
    /// </summary>
    public DictionaryBrowsePage Index(string? at)
    {
        var lemmas = ordered.Value;
        var letters = DictionaryBrowse.LettersOf(lemmas);
        var page = new DictionaryBrowsePage
        {
            Dictionary = "Lemmas",
            Slug = "lemma",
            Letters = letters.Select(c => char.ToUpperInvariant(c).ToString()).ToList(),
            Chapters = [],
        };
        if (letters.Count == 0)
        {
            // an uninitialised submodule shouldn't take the page down: the index
            // is empty rather than broken, as the browse is without its JSON
            return page;
        }

        var asked = at == null ? "" : DictionaryBrowse.CollationKey(at);
        var letter = asked.Length > 0 && letters.Contains(asked[0]) ? asked[0] : letters[0];
        page.Letter = char.ToUpperInvariant(letter).ToString();
        page.Chapters = DictionaryBrowse
            .Chapters(
                lemmas.Where(x => DictionaryBrowse.LetterOf(x) == letter),
                vocabulary.IsAttested)
            .ToList();
        return page;
    }

    /// <summary>The order the groups read in: the lexeme's own headwords, then the
    /// paradigm, then the rule-made and historical links. A link type the data
    /// grows that is not named here files after these, under its own name.</summary>
    private static readonly string[] GroupOrder =
    [
        "self", "inflected", "plural", "compSup", "irregular", "emphatic",
        "contraction", "variant", "mutation", "demutated", "particle",
        "univerbated", "phillips", "undecided", "override", "typo",
    ];

    /// <summary>
    /// One lemma's form tree: every form the tables link to it, grouped by link
    /// type, each marked for whether the corpus says it and whether the link
    /// rests on a rule or hand-assertion alone. One level deep on purpose — the
    /// link graph carries book-true cycles (fee inflects to feeagh, feeagh
    /// pluralizes to fee; see LemmaLinkCycleTest), and a tree of leaves cannot
    /// be walked in a circle. Null when the tables name no such lemma.
    /// </summary>
    public LemmaTreePage? Tree(string lemma)
    {
        var links = lemmaTable.LinksOf(lemma);
        if (links == null)
        {
            return null;
        }
        var groups = links.Links
            .GroupBy(x => x.LinkType)
            .OrderBy(g =>
            {
                var known = Array.IndexOf(GroupOrder, g.Key);
                return known < 0 ? GroupOrder.Length : known;
            })
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new LemmaTreeGroup
            {
                LinkType = g.Key,
                Forms = g
                    .OrderBy(x => DictionaryBrowse.CollationKey(x.Form), StringComparer.Ordinal)
                    .ThenBy(x => x.Form, StringComparer.Ordinal)
                    .Select(x => new LemmaTreeForm
                    {
                        Form = x.Form,
                        Attestations = vocabulary.AttestationsOf(x.Form),
                        // an unread phrase is left un-greyed, as the browse
                        // leaves one: greying is a claim
                        Attested = (vocabulary.AttestationsOf(x.Form) ?? 1) > 0,
                        Unverified = x.Unverified,
                    })
                    .ToList(),
            })
            .ToList();
        return new LemmaTreePage
        {
            Lemma = links.Lemma,
            Attestations = vocabulary.AttestationsOf(links.Lemma),
            Attested = (vocabulary.AttestationsOf(links.Lemma) ?? 1) > 0,
            Unverified = links.SelfUnverified,
            Groups = groups,
        };
    }
}

/// <summary>A lemma and the forms the tables link to it: the form tree</summary>
public class LemmaTreePage
{
    /// <summary>As the `lemma` column spells it ("aa-aase", "Aachummey")</summary>
    public required string Lemma { get; set; }
    /// <summary>How often the corpus says the lemma by its own spelling; null
    /// while not yet known (see <see cref="CorpusVocabulary.AttestationsOf"/>)</summary>
    public long? Attestations { get; set; }
    /// <summary>Whether the corpus says the lemma by its own spelling — the forms
    /// below answer for the rest of the paradigm</summary>
    public bool Attested { get; set; }
    /// <summary>The lemma's own row is hand-asserted (the vocab supplement's
    /// 'peiagh'): the root itself renders as a guess, as the popup's
    /// unverifiedLink does</summary>
    public bool Unverified { get; set; }
    public required List<LemmaTreeGroup> Groups { get; set; }
}

/// <summary>The forms hanging off a lemma by one kind of link</summary>
public class LemmaTreeGroup
{
    /// <summary>The table's own name for the link ("inflected", "mutation",
    /// "variant", ...): the client puts the reader's words on it</summary>
    public required string LinkType { get; set; }
    public required List<LemmaTreeForm> Forms { get; set; }
}

/// <summary>One form in the tree</summary>
public class LemmaTreeForm
{
    public required string Form { get; set; }
    /// <summary>How often the corpus says the form by this spelling — no lemma
    /// hop, which would answer for the whole paradigm at once; null while not
    /// yet known (a phrase before the corpus has been read for it)</summary>
    public long? Attestations { get; set; }
    /// <summary>Whether any text says the form by this spelling: false only
    /// where <see cref="Attestations"/> is a known 0</summary>
    public bool Attested { get; set; }
    /// <summary>No row attests the link: it was made by rule (a generated
    /// mutation) or hand-asserted (the vocab supplement), and may be wrong</summary>
    public bool Unverified { get; set; }
}
