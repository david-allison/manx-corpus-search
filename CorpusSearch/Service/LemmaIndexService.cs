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
public class LemmaIndexService(LemmaTable lemmaTable, CorpusVocabulary vocabulary,
    IEnumerable<ISearchDictionary>? dictionaryServices = null)
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
                vocabulary.IsAttested,
                // a lemma no text uses still stands in a book: name it, as
                // the tree names it, or the grey reads as a phantom
                lemma => lemmaTable.LinksOf(lemma) is
                         { SelfUnverified: false } links
                         && links.SelfSource.Length > 0
                    ? links.SelfSource
                    : null)
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
        "univerbated", "phillips", "prefixed", "undecided", "override", "typo",
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
        var groups = Grouped(
            byParent[rootKey].Select(x => (x, byParent)),
            expanded, links.Lemma);
        // upward: the reverse reading of every link some other tree draws
        // downward, so the graph can be climbed from either end — deiney says
        // it inflects dooinney, aa-ghiennaghtyn that it is written with aa-
        var parents = new List<LemmaTreeParent>();
        foreach (var display in lemmaTable.DisplayLemmasFor(links.Lemma)
                     .Where(x => LemmaTable.NormalizeForm(x) != rootKey)
                     .OrderBy(DictionaryBrowse.CollationKey, StringComparer.Ordinal))
        {
            var linkTypes = lemmaTable.LinksOf(display)?.Links
                .Where(x => x.Form == rootKey)
                .Select(x => x.LinkType)
                .Distinct()
                .OrderBy(GroupRank)
                .ToList();
            if (linkTypes is { Count: > 0 })
            {
                parents.Add(new LemmaTreeParent { Lemma = display, LinkTypes = linkTypes });
            }
        }
        var prefix = lemmaTable.AllDisplayLemmas
            .Where(x => (x.EndsWith('-') || x.EndsWith('‑'))
                        && links.Lemma.Length > x.Length
                        && links.Lemma.StartsWith(x, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Length)
            .FirstOrDefault();
        if (prefix != null)
        {
            parents.Add(new LemmaTreeParent { Lemma = prefix, LinkTypes = ["prefixed"] });
        }
        // a prefix is spelled into its family, and spelling — never the
        // lemma table's say-so — is the whole relationship: the family is
        // whatever is written with it. The books' headwords and the corpus's
        // own words join the table's lemmas ('aa-chroo' is in no book and no
        // table, and Wilson's Sermons says it all the same); the table goes
        // first, its spellings being the display ones and its rows carrying
        // the source. Only hyphen-spelled compounds are claimed, and
        // suffixes go without: nothing is spelled '*-ys'.
        if (links.Lemma.EndsWith('-') || links.Lemma.EndsWith('‑'))
        {
            var family = lemmaTable.AllDisplayLemmas.Select(x => (Form: x, Table: true))
                .Concat((dictionaryServices ?? [])
                    .Where(d => d.QueryLanguages.Contains("gv"))
                    .SelectMany(d => d.AllWords)
                    .Concat(vocabulary.TermsStartingWith(links.Lemma))
                    // fewest capitals first, so a word the corpus says in
                    // lowercase outranks the same word as Kelly shouts it
                    .OrderBy(t => t.Count(char.IsUpper))
                    .ThenBy(t => t, StringComparer.Ordinal)
                    .Select(x => (Form: x, Table: false)))
                .Where(x => x.Form.Length > links.Lemma.Length
                            // a phrase's opening word may carry the prefix, but
                            // the phrase is no compound: the word alone is family
                            && !x.Form.Contains(' ')
                            && x.Form.StartsWith(links.Lemma, StringComparison.OrdinalIgnoreCase))
                .GroupBy(x => x.Form.ToLowerInvariant())
                .Select(g => (
                    // the table's spelling stands where it has one; a word only
                    // ever printed in Kelly's capitals is lowered
                    Form: g.Select(x => x.Form).FirstOrDefault(f => f.Any(char.IsLower)) ?? g.Key,
                    Table: g.Any(x => x.Table)))
                .OrderBy(x => DictionaryBrowse.CollationKey(x.Form), StringComparer.Ordinal)
                .ThenBy(x => x.Form, StringComparer.Ordinal)
                .Select(x => new LemmaTreeForm
                {
                    Form = x.Form,
                    Attestations = vocabulary.AttestationsOf(x.Form),
                    Attested = (vocabulary.AttestationsOf(x.Form) ?? 1) > 0,
                    // a member the table never linked is gathered by spelling
                    // alone: derived, and said to be
                    Unverified = !x.Table,
                    // each table member is its own printed entry: a greyed
                    // one still says whose book records it
                    Source = lemmaTable.LinksOf(x.Form) is { SelfUnverified: false } own
                             && own.SelfSource.Length > 0
                        ? own.SelfSource
                        : null,
                    SharedWithOtherLemmas = lemmaTable.DisplayLemmasFor(x.Form).Count > 1,
                })
                .ToList();
            if (family.Count > 0)
            {
                groups.Add(new LemmaTreeGroup { LinkType = "prefixed", Forms = family });
            }
        }
        return new LemmaTreePage
        {
            Lemma = links.Lemma,
            Attestations = vocabulary.AttestationsOf(links.Lemma),
            Attested = (vocabulary.AttestationsOf(links.Lemma) ?? 1) > 0,
            Unverified = links.SelfUnverified,
            Source = links.SelfUnverified || links.SelfSource.Length == 0
                ? null
                : links.SelfSource,
            Parents = parents.Count > 0 ? parents : null,
            Groups = groups,
        };
    }

    /// <summary>Where a link type files in <see cref="GroupOrder"/>; unknown
    /// types after every known one</summary>
    private static int GroupRank(string linkType)
    {
        var known = Array.IndexOf(GroupOrder, linkType);
        return known < 0 ? GroupOrder.Length : known;
    }

    /// <summary>Whether a child link would only say its parent over again: a
    /// particle row files under its phrase, and where the phrase is itself
    /// the entry above ('e haaght' under Cregeen's entry 'e haaght'), the row
    /// repeats that entry, count and all</summary>
    private static bool EchoesParent(LemmaLink link, string parentForm) =>
        link.LinkType == "particle" && link.Via == parentForm;

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
    /// one row per form, in collation order within.
    ///
    /// One row however many ways the form is linked: 'deiney' is inflected AND
    /// plural of dooinney — two links in the tables, one word to the reader,
    /// and drawing it twice read as two. The best-ranked link draws the row
    /// and the others ride on it (<see cref="LemmaTreeForm.AlsoLinkedAs"/>).
    ///
    /// A particle row stands apart from that merge: it is the phrase's row
    /// ('e gheiney ×85'), not the form's, and it hosts nothing — the form's
    /// own family (its Phillips spellings) derives from the bare form, never
    /// from its use after a particle. The bare form's row is the demutation
    /// guess where that is all the tables hold, kept when there is a family
    /// to carry and dropped when it would only echo the phrase beside it.</summary>
    private List<LemmaTreeGroup> Grouped(
        IEnumerable<(LemmaLink Link, ILookup<string, LemmaLink> ByParent)> children,
        HashSet<string> expanded, string parentForm)
    {
        var all = children
            // the same (link type, form) can arrive from both the via rows
            // and a nested lexeme's own: one node
            .GroupBy(x => (x.Link.LinkType, x.Link.Form))
            .Select(x => x.First())
            .ToList();
        // every particle link, echoes included: an undrawn phrase row still
        // vouches for the form's mutation and still covers a childless guess
        var particles = all.Where(x => x.Link.LinkType == "particle").ToList();
        var rows = all
            .Where(x => x.Link.LinkType != "particle")
            .GroupBy(x => x.Link.Form)
            .Select(forms =>
            {
                var links = forms
                    .OrderBy(x => GroupRank(x.Link.LinkType))
                    .ThenBy(x => x.Link.LinkType, StringComparer.Ordinal)
                    .ToList();
                if (links.Count > 1)
                {
                    // a guess is not another fact about the form, only a
                    // worse claim to the same one
                    links.RemoveAll(x => x.Link.LinkType == "demutated");
                }
                return (Primary: links[0],
                    Also: links.Skip(1).Select(x => x.Link.LinkType).ToList());
            })
            // a lone childless guess beside the form's particle row says
            // nothing the phrase does not: dropped. With a family to carry
            // (gheiney holds the Phillips 'gene') it stays — the phrase
            // cannot carry it.
            .Where(x => x.Primary.Link.LinkType != "demutated"
                        || x.Primary.ByParent[x.Primary.Link.Form].Any()
                        || !particles.Any(p => p.Link.Form == x.Primary.Link.Form))
            // a mutation the book prints is not merely possible: the particle
            // phrase ('e gheiney') attests it, so the surviving row files
            // under Mutations — the hedge is kept for forms only the
            // generator vouches for
            .Select(x => x.Primary.Link.LinkType == "demutated"
                         && particles.Any(p => p.Link.Form == x.Primary.Link.Form)
                ? (Primary: (Link: x.Primary.Link with { LinkType = "mutation" },
                        x.Primary.ByParent), x.Also)
                : x)
            .ToList();
        rows.AddRange(particles
            // a particle row filing under its own phrase would say the entry
            // above over again, count and all: not drawn — though it still
            // counted above, as the mutation's voucher
            .Where(x => !EchoesParent(x.Link, parentForm))
            .Select(x => (Primary: x, Also: new List<string>())));
        return rows
            .GroupBy(x => x.Primary.Link.LinkType)
            .OrderBy(g => GroupRank(g.Key))
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new LemmaTreeGroup
            {
                LinkType = g.Key,
                Forms = g
                    .OrderBy(x => DictionaryBrowse.CollationKey(x.Primary.Link.Form), StringComparer.Ordinal)
                    .ThenBy(x => x.Primary.Link.Form, StringComparer.Ordinal)
                    .Select(x => Node(x.Primary.Link, x.Primary.ByParent, expanded, x.Also))
                    .ToList(),
            })
            .ToList();
    }

    /// <summary>One form of the tree, its own children nested — unless it has
    /// been drawn already: a form met again (a shared intermediate, or a
    /// book-true cycle) is a leaf the second time, not a circle</summary>
    private LemmaTreeForm Node(
        LemmaLink link, ILookup<string, LemmaLink> byParent, HashSet<string> expanded,
        IReadOnlyList<string>? alsoLinkedAs = null)
    {
        // a particle row's via is the phrase itself ('e gheiney'): the one
        // link type whose whole point — which particle — the form alone
        // cannot say. Elsewhere the via is structure, already drawn as the
        // nesting.
        var particlePhrase = link.LinkType == "particle" && link.Via.Length > 0
            ? link.Via
            : null;
        List<LemmaTreeGroup>? groups = null;
        // a particle row is the phrase, and a phrase hosts nothing: the form's
        // own family derives from the bare form, never from its use after a
        // particle — so this row is always a leaf, and must not spend the
        // form's one expansion either
        if (particlePhrase == null && expanded.Add(link.Form))
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
            var built = Grouped(children, expanded, link.Form);
            groups = built.Count > 0 ? built : null;
        }
        // and the phrase is what the row counts: the bare spelling rides
        // after any particle at once, and its count answers for all of them
        // together, not for this one
        var counted = particlePhrase ?? link.Form;
        return new LemmaTreeForm
        {
            Form = link.Form,
            Attestations = vocabulary.AttestationsOf(counted),
            // an unread phrase is left un-greyed, as the browse leaves one:
            // greying is a claim
            Attested = (vocabulary.AttestationsOf(counted) ?? 1) > 0,
            Unverified = link.Unverified,
            // provenance belongs to the attestation: an unverified link has
            // only the generator behind it, and names no book
            Source = link.Unverified || link.Source.Length == 0 ? null : link.Source,
            Via = particlePhrase,
            AlsoLinkedAs = alsoLinkedAs is { Count: > 0 } ? alsoLinkedAs.ToList() : null,
            SharedWithOtherLemmas = lemmaTable.DisplayLemmasFor(link.Form).Count > 1,
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
    /// <summary>The lemmas this one hangs off, upward — the reverse reading of
    /// links other trees draw downward ('deiney' inflects dooinney), plus the
    /// prefix it is spelled with ('aa-ghiennaghtyn' is written with aa-). Null
    /// at a root nothing claims.</summary>
    public List<LemmaTreeParent>? Parents { get; set; }
    public required List<LemmaTreeGroup> Groups { get; set; }
}

/// <summary>A lemma another lemma hangs off, and how</summary>
public class LemmaTreeParent
{
    public required string Lemma { get; set; }
    /// <summary>The link types read upward ("inflected", "plural"; "prefixed"
    /// for a spelling parent), in the tree's reading order</summary>
    public required List<string> LinkTypes { get; set; }
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

    /// <summary>The phrase a particle row derives through ("e gheiney"): the
    /// particle itself, which the form and its group name cannot say. Null on
    /// every other link type, whose via is structure the nesting already
    /// draws.</summary>
    public string? Via { get; set; }

    /// <summary>The other ways the same form is linked at this level
    /// ("plural" on the row 'Inflected forms' files deiney under): one row
    /// however many links, the best-ranked drawing it and the rest named
    /// here. Null where the row's group says it all.</summary>
    public List<string>? AlsoLinkedAs { get; set; }

    /// <summary>Whether another lexeme also uses this spelling (voddey answers
    /// to moddey and foddey): the count is of the spelling, so some of it may
    /// be the other word's — the tree marks the claim rather than making it</summary>
    public bool SharedWithOtherLemmas { get; set; }
    /// <summary>What hangs off this form in turn: rows deriving through it, and
    /// — where it heads a lexeme of its own — that lexeme's tree. Null at a
    /// leaf, and at a form the tree has already drawn (a book-true cycle's
    /// second meeting).</summary>
    public List<LemmaTreeGroup>? Groups { get; set; }
}
