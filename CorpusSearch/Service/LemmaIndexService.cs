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
    /// One lemma's form tree, full depth: every form the tables link to it,
    /// grouped by link type, each marked for whether the corpus says it and
    /// whether the link rests on a rule or hand-assertion alone. A form nests
    /// what hangs off *it*: the rows deriving through it (via — 'pyaghyn'
    /// inflects the variant 'pyagh', not peiagh itself), and — where the form
    /// heads a lexeme of its own — that lexeme's whole tree ('deiney' under
    /// dooinney carries 'e gheiney'). Each form is expanded once: the link
    /// graph carries book-true cycles (fee inflects to feeagh, feeagh
    /// pluralizes to fee; see LemmaLinkCycleTest), so the second meeting is a
    /// leaf rather than a circle. Null when the tables name no such lemma.
    /// </summary>
    public LemmaTreePage? Tree(string lemma)
    {
        var links = lemmaTable.LinksOf(lemma);
        if (links == null)
        {
            return null;
        }
        var rootKey = LemmaTable.NormalizeForm(links.Lemma);
        var expanded = new HashSet<string> { rootKey };
        var byParent = ParentLookup(rootKey, links.Links);
        return new LemmaTreePage
        {
            Lemma = links.Lemma,
            Attestations = vocabulary.AttestationsOf(links.Lemma),
            Attested = (vocabulary.AttestationsOf(links.Lemma) ?? 1) > 0,
            Unverified = links.SelfUnverified,
            Source = links.SelfUnverified || links.SelfSource.Length == 0
                ? null
                : links.SelfSource,
            Groups = Grouped(byParent[rootKey].Select(x => (x, byParent)), expanded),
        };
    }

    /// <summary>Each link filed under the form it derives through: its via where
    /// that names another of the lemma's own forms, the lemma itself otherwise
    /// (a via naming no form here would dangle, so it hangs off the root)</summary>
    private static ILookup<string, LemmaLink> ParentLookup(
        string rootKey, IReadOnlyList<LemmaLink> links)
    {
        var forms = links.Select(x => x.Form).ToHashSet();
        return links.ToLookup(x =>
            x.Via.Length > 0 && x.Via != x.Form && forms.Contains(x.Via) ? x.Via : rootKey);
    }

    /// <summary>Children as branches: grouped by link type in reading order,
    /// each form once per way it is linked, in collation order within</summary>
    private List<LemmaTreeGroup> Grouped(
        IEnumerable<(LemmaLink Link, ILookup<string, LemmaLink> ByParent)> children,
        HashSet<string> expanded)
    {
        return children
            .GroupBy(x => x.Link.LinkType)
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
                    // the same (link type, form) can arrive from both the via
                    // rows and a nested lexeme's own: one node
                    .GroupBy(x => x.Link.Form)
                    .Select(x => x.First())
                    .OrderBy(x => DictionaryBrowse.CollationKey(x.Link.Form), StringComparer.Ordinal)
                    .ThenBy(x => x.Link.Form, StringComparer.Ordinal)
                    .Select(x => Node(x.Link, x.ByParent, expanded))
                    .ToList(),
            })
            .ToList();
    }

    /// <summary>One form of the tree, its own children nested — unless it has
    /// been drawn already: a form met again (a shared intermediate, or a
    /// book-true cycle) is a leaf the second time, not a circle</summary>
    private LemmaTreeForm Node(
        LemmaLink link, ILookup<string, LemmaLink> byParent, HashSet<string> expanded)
    {
        List<LemmaTreeGroup>? groups = null;
        if (expanded.Add(link.Form))
        {
            // the rows of the enclosing lemma that derive through this form...
            var children = byParent[link.Form].Select(x => (x, byParent));
            // ...and, where the form heads a lexeme of its own, that lexeme's
            // tree, its rows parented among themselves by their own vias. Never
            // through a demutation guess — as RootDisplayLemmasFor refuses the
            // same hop: fee's guessed 'ee' must not import the whole family of
            // *to eat* into a tree about weaving
            var own = link.LinkType == "demutated" ? null : lemmaTable.LinksOf(link.Form);
            if (own != null)
            {
                var ownByParent = ParentLookup(link.Form, own.Links);
                children = children.Concat(ownByParent[link.Form].Select(x => (x, ownByParent)));
            }
            var built = Grouped(children, expanded);
            groups = built.Count > 0 ? built : null;
        }
        return new LemmaTreeForm
        {
            Form = link.Form,
            Attestations = vocabulary.AttestationsOf(link.Form),
            // an unread phrase is left un-greyed, as the browse leaves one:
            // greying is a claim
            Attested = (vocabulary.AttestationsOf(link.Form) ?? 1) > 0,
            Unverified = link.Unverified,
            // provenance belongs to the attestation: an unverified link has
            // only the generator behind it, and names no book
            Source = link.Unverified || link.Source.Length == 0 ? null : link.Source,
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
    /// <summary>The file whose print attests the lemma itself ("cregeen",
    /// "names", ...): what lets a lemma no text uses say a book records it.
    /// Null when nothing does.</summary>
    public string? Source { get; set; }
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
    /// <summary>The file whose print attests the link ("cregeen", "names",
    /// ...): what lets a form no text uses say a book records it. Null for an
    /// unverified link — only the generator is behind one — and for the
    /// treebank's closed-class paradigm rows, which no book may claim.</summary>
    public string? Source { get; set; }
    /// <summary>What hangs off this form in turn: rows deriving through it, and
    /// — where it heads a lexeme of its own — that lexeme's tree. Null at a
    /// leaf, and at a form the tree has already drawn (a book-true cycle's
    /// second meeting).</summary>
    public List<LemmaTreeGroup>? Groups { get; set; }
}
