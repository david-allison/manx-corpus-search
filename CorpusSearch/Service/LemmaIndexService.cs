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
                lemma => lemmaTable.LinkSetsFor(lemma)
                    .FirstOrDefault(s => !s.SelfUnverified && s.SelfSource.Length > 0)
                    ?.SelfSource)
            .ToList();
        return page;
    }

    /// <summary>The order the groups read in: the lexeme's own headwords, then the
    /// paradigm, then the rule-made and historical links. A link type the data
    /// grows that is not named here files after these, under its own name.</summary>
    private static readonly string[] GroupOrder =
    [
        "self", "inflected", "plural", "compSup", "irregular", "emphatic",
        "contraction", "contracts", "variant", "mutation", "demutated", "particle",
        "derived", "univerbated", "phillips", "prefixed", "undecided", "override", "typo",
    ];

    /// <summary>
    /// One lemma tree per LEXEME answering to the name, in the book's order:
    /// the spelling ee heads the verb and the pronoun, and the reader gets two
    /// trees, never a merge. Each is the form tree, full depth: every form the
    /// tables link to the lexeme, grouped by link type, each marked for
    /// whether the corpus says it and whether the link rests on a rule or
    /// hand-assertion alone. A form nests what hangs off *it*: the rows
    /// deriving through it (via — 'pyaghyn' inflects the variant 'pyagh', not
    /// peiagh itself), and — where the form heads one lexeme of its own —
    /// that lexeme's whole tree ('deiney' under dooinney carries 'e
    /// gheiney'). Each form is expanded once per tree: the link graph carries
    /// book-true cycles (fee inflects to feeagh, feeagh pluralizes to fee;
    /// see LemmaLinkCycleTest), so the second meeting is a leaf rather than a
    /// circle. Empty when the tables name no such lemma.
    /// </summary>
    public List<LemmaTreePage> Trees(string lemma)
    {
        var sets = lemmaTable.LinkSetsFor(lemma);
        if (sets.Count == 0)
        {
            return [];
        }
        var name = sets[0].Lemma;
        var rootKey = LemmaTable.NormalizeForm(name);
        // upward, at the spelling's level — shared by every homograph: the
        // reverse reading of every link some other tree draws downward, so
        // the graph can be climbed from either end — deiney says it inflects
        // dooinney, aa-ghiennaghtyn that it is written with aa-
        var nameParents = new List<LemmaTreeParent>();
        foreach (var display in lemmaTable.DisplayLemmasFor(name)
                     .Where(x => LemmaTable.NormalizeForm(x) != rootKey)
                     .OrderBy(DictionaryBrowse.CollationKey, StringComparer.Ordinal))
        {
            // a demutation guess and an entry override say the spelling can
            // also read as that lexeme, never that this one hangs under it:
            // kione's paragraph seats ching the genitive, and çhing the sick
            // must not climb into kione through a spelling they merely share
            var linkTypes = lemmaTable.LinkSetsFor(display)
                .SelectMany(s => s.Links)
                .Where(x => x.Form == rootKey
                            && x.LinkType is not ("demutated" or "override"))
                .Select(x => x.LinkType)
                .Distinct()
                .OrderBy(GroupRank)
                .ToList();
            if (linkTypes is { Count: > 0 })
            {
                nameParents.Add(new LemmaTreeParent { Lemma = display, LinkTypes = linkTypes });
            }
        }
        var prefix = lemmaTable.AllDisplayLemmas
            .Where(x => (x.EndsWith('-') || x.EndsWith('‑'))
                        && name.Length > x.Length
                        && name.StartsWith(x, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Length)
            .FirstOrDefault();

        return sets
            .Select((set, index) =>
            {
                var page = Page(set, name, rootKey, nameParents, prefix);
                // the book's homograph number, where one spelling heads
                // several lexemes: the reader ties tree e¹ to entry e¹
                page.Homograph = sets.Count > 1 ? index + 1 : null;
                return page;
            })
            .ToList();
    }

    /// <summary>One lexeme's page: its own links, its own parents (the
    /// spelling's, then its entries'), its own expansion walk</summary>
    private LemmaTreePage Page(LemmaLinkSet set, string name, string rootKey,
        List<LemmaTreeParent> nameParents, string? prefix)
    {
        var expanded = new HashSet<string> { rootKey };
        var byParent = ParentLookup(rootKey, set.Links);
        var groups = Grouped(
            byParent[rootKey].Select(x => (x, byParent)),
            expanded, name);
        var parents = new List<LemmaTreeParent>(nameParents);
        // the family heads this lexeme hangs under, upward: the derived and
        // contracts rows' reverse reading. Not in DisplayLemmasFor — those
        // rows name no reading of the form — so the family parents are
        // asked for by name: the display's, and the entry headwords that
        // name this lexeme (fys's paragraph prints the member as 'cha
        // s'oc', while its lexeme displays particle-free as s'oc)
        var ownNames = set.Links
            .Where(x => x.LinkType == "self")
            .Select(x => x.Form)
            .Prepend(name)
            .ToList();
        // whether the tree's own word is a form of this lexeme at all: the
        // adverb Cregeen heads 'dy-aalin' displays particle-free as aalin
        // (the lemma convention), but that spelling is the adjective's word —
        // no row of the adverb's says it. A head reached through such a
        // lexeme's phrase must carry the phrase, or the page seats the
        // adjective beneath dy
        var rootIsOwnForm = lemmaTable.CandidatesFor(rootKey).Contains(set.LemmaId);
        foreach (var family in ownNames
                     .SelectMany(n => lemmaTable.FamilyParentsOf(n)
                         .Select(x => (x.Head, x.LinkType, Member: n)))
                     .Distinct()
                     .Where(x => parents.All(p => p.Lemma != x.Head))
                     .GroupBy(x => x.Head)
                     .OrderBy(g => DictionaryBrowse.CollationKey(g.Key), StringComparer.Ordinal))
        {
            parents.Add(new LemmaTreeParent
            {
                Lemma = family.Key,
                LinkTypes = family.Select(x => x.LinkType).Distinct()
                    .OrderBy(GroupRank).ToList(),
                // the member headword the paragraph actually prints, where
                // that is the edge's only truth: s'oc (a form of its lexeme,
                // inside 'cha s'oc') still roots under fys bare
                Member = rootIsOwnForm ? null : family.First().Member,
                // the phrase a contracts edge spells out, read off the head's
                // own row for this member: what 'contraction' alone cannot say
                Expansion = family.Any(x => x.LinkType == "contracts")
                    ? lemmaTable.LinkSetsFor(family.Key)
                        .SelectMany(s => s.Links)
                        .FirstOrDefault(l => l.LinkType == "contracts"
                                             && ownNames.Contains(l.Form) && l.Via.Length > 0)?.Via
                    : null,
            });
        }
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
        if (name.EndsWith('-') || name.EndsWith('‑'))
        {
            var family = lemmaTable.AllDisplayLemmas.Select(x => (Form: x, Table: true))
                .Concat((dictionaryServices ?? [])
                    .Where(d => d.QueryLanguages.Contains("gv"))
                    .SelectMany(d => d.AllWords)
                    .Concat(vocabulary.TermsStartingWith(name))
                    // fewest capitals first, so a word the corpus says in
                    // lowercase outranks the same word as Kelly shouts it
                    .OrderBy(t => t.Count(char.IsUpper))
                    .ThenBy(t => t, StringComparer.Ordinal)
                    .Select(x => (Form: x, Table: false)))
                .Where(x => x.Form.Length > name.Length
                            // a phrase's opening word may carry the prefix, but
                            // the phrase is no compound: the word alone is family
                            && !x.Form.Contains(' ')
                            && x.Form.StartsWith(name, StringComparison.OrdinalIgnoreCase))
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
                    Source = lemmaTable.LinkSetsFor(x.Form)
                        .FirstOrDefault(s => !s.SelfUnverified && s.SelfSource.Length > 0)
                        ?.SelfSource,
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
            Lemma = name,
            LemmaId = set.LemmaId,
            Pos = set.Pos.Length > 0 ? set.Pos : null,
            Attestations = vocabulary.AttestationsOf(name),
            Attested = (vocabulary.AttestationsOf(name) ?? 1) > 0,
            Unverified = set.SelfUnverified,
            Source = set.SelfUnverified || set.SelfSource.Length == 0
                ? null
                : set.SelfSource,
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
        // vouches for the form's mutation
        var particles = all.Where(x => x.Link.LinkType == "particle").ToList();
        // the phrase rows that will actually be drawn: one filing under its
        // own entry ('dty vac' under Cregeen's entry 'dty vac') is not among
        // them, and only a drawn row can cover a childless guess
        var drawnParticles = particles.Where(x => !EchoesParent(x.Link, parentForm)).ToList();
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
            // a lone childless guess beside the form's drawn phrase row says
            // nothing the phrase does not: dropped. With a family to carry
            // (gheiney holds the Phillips 'gene') it stays — and so does the
            // guess whose only phrase row is the undrawn echo of the entry
            // above: nothing else says the bare form (vac under Cregeen's
            // entry 'dty vac')
            .Where(x => x.Primary.Link.LinkType != "demutated"
                        || x.Primary.ByParent[x.Primary.Link.Form].Any()
                        || !drawnParticles.Any(p => p.Link.Form == x.Primary.Link.Form))
            // a mutation the book prints is not merely possible: the particle
            // phrase ('e gheiney', 'dty vac') attests it, drawn or not, so
            // the surviving row files under Mutations — the hedge is kept
            // for forms only the generator vouches for
            .Select(x => x.Primary.Link.LinkType == "demutated"
                         && particles.Any(p => p.Link.Form == x.Primary.Link.Form)
                ? (Primary: (Link: x.Primary.Link with { LinkType = "mutation" },
                        x.Primary.ByParent), x.Also)
                : x)
            .ToList();
        // an echoing particle row (its phrase is the entry above) said the
        // entry over again, count and all: not drawn — though it counted
        // above, as the mutation's voucher
        rows.AddRange(drawnParticles.Select(x => (Primary: x, Also: new List<string>())));
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
            // ...and, where the form heads ONE lexeme of its own, that
            // lexeme's tree, its rows parented among themselves by their own
            // vias. One lexeme only — a homograph name (ee the verb, ee the
            // pronoun) cannot say which family to import, so it imports
            // neither. Never through a demutation guess — as
            // RootDisplayLemmasFor refuses the same hop: fee's guessed 'ee'
            // must not import the whole family of *to eat* into a tree about
            // weaving
            var ownSets = link.LinkType == "demutated"
                ? []
                : lemmaTable.LinkSetsFor(link.Form);
            var own = ownSets.Count == 1 ? ownSets[0] : null;
            // a family member prints under its head by its full headword
            // ('cha s'oc' in fys's paragraph), while its lexeme displays
            // particle-free ('s'oc'): the name misses, and the hop goes
            // through the form's one reading instead. One reading only —
            // an ambiguous spelling must not import another word's family
            if (ownSets.Count == 0 && link.LinkType == "derived")
            {
                var displays = lemmaTable.DisplayLemmasFor(link.Form);
                var readingSets = displays.Count == 1
                    ? lemmaTable.LinkSetsFor(displays[0])
                    : [];
                own = readingSets.Count == 1 ? readingSets[0] : null;
            }
            if (own != null)
            {
                var ownByParent = ParentLookup(link.Form, own.Links);
                // the lexeme's own entry row ('cha s'oc' under s'oc) is this
                // node said over again, not a branch of it
                children = children.Concat(ownByParent[link.Form]
                    .Where(x => !(x.LinkType == "self" && x.Form == link.Form))
                    .Select(x => (x, ownByParent)));
            }
            var built = Grouped(children, expanded, link.Form);
            groups = built.Count > 0 ? built : null;
        }
        // and the phrase is what the row counts: the bare spelling rides
        // after any particle at once, and its count answers for all of them
        // together, not for this one
        var counted = particlePhrase ?? link.Form;
        // a derived row names another lexeme's headword: the row wears that
        // lexeme's own spelling ('neu-vondeish'), not the form column's
        // normalization ('neu vondeish')
        var display = link.LinkType == "derived"
            ? lemmaTable.LinkSetsFor(link.Form).FirstOrDefault()?.Lemma ?? link.Form
            : link.Form;
        return new LemmaTreeForm
        {
            Form = display,
            Attestations = vocabulary.AttestationsOf(counted),
            // an unread phrase is left un-greyed, as the browse leaves one:
            // greying is a claim
            Attested = (vocabulary.AttestationsOf(counted) ?? 1) > 0,
            Unverified = link.Unverified,
            // provenance belongs to the attestation: an unverified link has
            // only the generator behind it, and names no book
            Source = link.Unverified || link.Source.Length == 0 ? null : link.Source,
            Via = particlePhrase,
            // a contracts row carries the phrase it spells out: 'v'oc (va oc)'
            Expansion = link.LinkType == "contracts" && link.Via.Length > 0
                ? link.Via
                : null,
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
    /// <summary>The lexeme's id ("ee.v", "ee.x"): what tells homograph pages
    /// apart, where the spelling alone cannot</summary>
    public string? LemmaId { get; set; }
    /// <summary>The printed class of the lexeme's entry ("v.", "pro."): the
    /// reader's label for a homograph tree. Null where the book gives none.</summary>
    public string? Pos { get; set; }
    /// <summary>Which of the spelling's lexemes this is (e¹, e²), in the
    /// book's order. Null where the name heads one lexeme alone.</summary>
    public int? Homograph { get; set; }
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
    /// <summary>The member headword a family edge rides through, where the
    /// tree's own word is no form of the lexeme: 'dy aalin' between the
    /// adverb displayed aalin and its head dy. What prints under dy is the
    /// phrase — the bare spelling is the adjective's word — so the client
    /// says the phrase rather than seating the word beneath the head. Null
    /// where the word itself prints there (camstram in cammey's paragraph;
    /// s'oc, a form of its lexeme inside 'cha s'oc', in fys's).</summary>
    public string? Member { get; set; }
    /// <summary>The phrase a contracts edge spells out ("va oc" above v'oc):
    /// what the contraction actually says, which the parent's name alone
    /// cannot. Null on every other kind of parent.</summary>
    public string? Expansion { get; set; }
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

    /// <summary>The phrase a contracts row spells out ("va oc" on v'oc under
    /// oc): what the contraction says, beside the row rather than instead of
    /// it. Null on every other link type.</summary>
    public string? Expansion { get; set; }

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
