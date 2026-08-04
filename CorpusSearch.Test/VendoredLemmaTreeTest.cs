using System.Collections.Generic;
using System.Linq;
using CorpusSearch.Dependencies.Lucene;
using CorpusSearch.Service;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>
/// Regression pins over the vendored table (external/manx-lemma-data): the
/// trees a reader actually gets, not fixtures. Each of these went missing
/// once — from the data, or from the drawing — and the fixture tests alone
/// would not have said so.
/// </summary>
[TestFixture]
public class VendoredLemmaTreeTest
{
    private static LemmaTreePage TreeOf(string lemma)
    {
        var table = LemmaTable.Instance;
        if (!table.AllDisplayLemmas.Any())
        {
            Assert.Ignore("cregeen.tsv not vendored (manx-lemma-data submodule not initialised)");
        }
        var tree = new LemmaIndexService(table, new CorpusVocabulary(table)).Tree(lemma);
        Assert.That(tree, Is.Not.Null, $"the table names no lemma '{lemma}'");
        return tree!;
    }

    private static IEnumerable<(string LinkType, string Form, LemmaTreeForm Node)> Flatten(
        IEnumerable<LemmaTreeGroup>? groups)
    {
        foreach (var group in groups ?? [])
        {
            foreach (var form in group.Forms)
            {
                yield return (group.LinkType, form.Form, form);
                foreach (var nested in Flatten(form.Groups))
                {
                    yield return nested;
                }
            }
        }
    }

    /// <summary>Cregeen's entry for vac is the phrase 'dty vac', whose particle
    /// row is an undrawn echo of its own entry: the demutation guess must
    /// survive it, upgraded to a mutation by the phrase's print. vac — said
    /// over a thousand times — once vanished from mac's tree entirely.</summary>
    [Test]
    public void MacCarriesItsMutationVac()
    {
        var rows = Flatten(TreeOf("mac").Groups).ToList();
        Assert.That(rows.Where(x => x.Form == "vac").Select(x => x.LinkType),
            Does.Contain("mutation"),
            "vac must stand in mac's tree as a mutation");
    }

    /// <summary>Cregeen prints the cha s'X contractions — cha fys X, "they do
    /// not know" — inside fys's own paragraph, and the s' is fys itself: not
    /// the copula, not a mutation. The family must root them under fys (they
    /// once sat rootless, each its own headless tree); the stem the strip
    /// leaves behind is oc's paradigm, not the contraction's (bare ocsyn once
    /// answered to three lemmas, and the word page drew three families); and
    /// the printed emphatic is print, not a rule's guess (the book's radical
    /// marker names the oc embedded after the s', and the demutation pass
    /// once read it as an unprovable mutation claim and hedged every row).</summary>
    [Test]
    public void FysCarriesItsContractedNegatives()
    {
        var member = TreeOf("fys").Groups
            .Single(g => g.LinkType == "derived")
            .Forms.SingleOrDefault(f => f.Form == "cha s'oc");
        Assert.That(member, Is.Not.Null, "cha s'oc (cha fys oc) must file under fys as derived");
        var paradigm = Flatten(member!.Groups).ToList();
        Assert.That(paradigm.Select(x => x.Form), Does.Contain("cha s'ocsyn"),
            "the contraction's own emphatic must ride along");
        Assert.That(paradigm.Select(x => x.Form), Does.Not.Contain("ocsyn"),
            "the bare emphatic is oc's paradigm, not the contraction's");
        var emphatic = paradigm.Single(x => x.Form == "cha s'ocsyn").Node;
        Assert.That(emphatic.Unverified, Is.False,
            "Cregeen prints cha s'ocsyn: it must not wear the rule-made hedge");
        Assert.That(emphatic.Source, Is.EqualTo("cregeen"),
            "the printed form names its book");
        // and the same edge read upward: the member's own tree names fys as
        // its head, though the derived row keys the printed 'cha s'oc' and
        // the lexeme displays particle-free
        Assert.That(TreeOf("s'oc").Parents?.Where(p => p.LinkTypes.Contains("derived"))
                .Select(p => p.Lemma),
            Does.Contain("fys"),
            "s'oc must climb to fys, as the reader climbs down from it");
    }

    /// <summary>The book's word family, whole: vondeish => vondeishagh =>
    /// s'vondeishagh. The superlative once had no route from vondeish at all —
    /// dropped from the letter index as a grammar row, and the noun and its
    /// adjective unlinked in the tables.</summary>
    [Test]
    public void VondeishReachesItsSuperlativeThroughItsAdjective()
    {
        var member = TreeOf("vondeish").Groups
            .Single(g => g.LinkType == "derived")
            .Forms.SingleOrDefault(f => f.Form == "vondeishagh");
        Assert.That(member, Is.Not.Null, "vondeishagh must file under vondeish as derived");
        Assert.That(Flatten(member!.Groups).Select(x => x.Form),
            Does.Contain("s'vondeishagh"),
            "the member's own paradigm must ride along");
    }

    /// <summary>The pronoun-preposition emphatics belong to their own words
    /// alone. Each of these spellings was once claimed by contraction lexemes
    /// too — the apostrophe strip handed ocsyn to v'oc and s'oc, ayms to
    /// v'aym and s'aym — so the word pages drew a family per claimant and
    /// every count wore the shared-spelling mark. The withdrawal pass cured
    /// them; an exporter change that re-plants a claim fails here by name.</summary>
    [Test]
    public void AnEmphaticBelongsToItsOwnWordAlone()
    {
        var table = LemmaTable.Instance;
        if (!table.AllDisplayLemmas.Any())
        {
            Assert.Ignore("cregeen.tsv not vendored (manx-lemma-data submodule not initialised)");
        }
        Assert.Multiple(() =>
        {
            foreach (var (emphatic, word) in new[]
                     {
                         ("ocsyn", "oc"), ("ayds", "ayd"), ("ayms", "aym"),
                         ("echeysyn", "echey"), ("euish", "eu"), ("orts", "ort"),
                         // homed by the paradigm file, the book recording them
                         // only inside the contractions: eeish once answered
                         // to nine lexemes. Its home is ish — the emphatic's
                         // own entry — never ee, whose spelling also eats
                         ("eeish", "ish"), ("ainyn", "ain"), ("ecish", "eck"),
                     })
            {
                Assert.That(table.DisplayLemmasFor(emphatic), Is.EqualTo(new[] { word }),
                    $"{emphatic} is {word}'s alone: another claimant means a page " +
                    "of extra family trees and a false shared-spelling mark");
            }
            // a suffixed form's three-letter stem (no'ins -> ins) is a
            // fragment, not a word: never on the table at all
            Assert.That(table.DisplayLemmasFor("ins"), Is.Empty,
                "ins is in + -s, a word to nobody");
        });
    }

    /// <summary>The book says v'oc is va oc, but no entry spells the expansion
    /// out, so the contraction relation could not redirect and once died
    /// silently: v'oc related to nothing but the be paradigm, and oc's page
    /// never mentioned it. The contraction hangs under each word it fuses,
    /// and climbs back to both.</summary>
    [Test]
    public void VocHangsUnderOcAsItsContraction()
    {
        Assert.That(TreeOf("oc").Groups
                .Single(g => g.LinkType == "contracts")
                .Forms.Select(f => f.Form),
            Does.Contain("v'oc"),
            "v'oc (va oc) must hang under oc as a contraction");
        Assert.That(TreeOf("v'oc").Parents?
                .Where(p => p.LinkTypes.Contains("contracts"))
                .Select(p => p.Lemma),
            Is.SupersetOf(new[] { "oc", "va" }),
            "v'oc must climb to the words it fuses");
    }

    /// <summary>Everything in fys's printed family is print, not a rule's
    /// guess. Cregeen's radical marker on the cha s'X contractions (O on cha
    /// s'oc, for the oc embedded after the fys-contracting s') was once read
    /// as an unprovable mutation claim, and the five marked entries' whole
    /// paradigms wore "worked out by rule, and may be wrong" while their four
    /// markerless siblings sat clean beside them. They went wrong together;
    /// hold the family together.</summary>
    [Test]
    public void FysFamilyWearsNoRuleMadeHedge()
    {
        var hedged = TreeOf("fys").Groups
            .Where(g => g.LinkType == "derived")
            .SelectMany(g => g.Forms)
            .SelectMany(member => Flatten(member.Groups)
                .Select(x => x.Node)
                .Prepend(member))
            .Where(node => node.Unverified)
            .Select(node => node.Form)
            .ToList();
        Assert.That(hedged, Is.Empty,
            "Cregeen prints fys's family entire: no member or paradigm row " +
            "may wear the unverified mark");
    }

    /// <summary>The spelling ee heads two lexemes — the verb 'eat' and the
    /// pronoun 'she' — and for years they drew as one tree: eating futures
    /// beside her emphatics, and eeish's page said 'eating'. One tree per
    /// lexeme now, each wearing the book's own class label.</summary>
    [Test]
    public void TheVerbAndThePronounEeStandApart()
    {
        var table = LemmaTable.Instance;
        if (!table.AllDisplayLemmas.Any())
        {
            Assert.Ignore("cregeen.tsv not vendored (manx-lemma-data submodule not initialised)");
        }
        var trees = new LemmaIndexService(table, new CorpusVocabulary(table)).Trees("ee");
        Assert.That(trees.Select(t => t.LemmaId), Is.EquivalentTo(new[] { "ee.v", "ee.x" }),
            "ee names the verb and the pronoun: two trees, never a merge");
        var verb = trees.Single(t => t.LemmaId == "ee.v");
        var pronoun = trees.Single(t => t.LemmaId == "ee.x");
        Assert.Multiple(() =>
        {
            Assert.That(verb.Pos, Is.EqualTo("v."));
            Assert.That(Flatten(verb.Groups).Select(x => x.Form), Does.Contain("eeym"),
                "the verb keeps its futures");
            Assert.That(Flatten(verb.Groups).Select(x => x.Form), Does.Not.Contain("ish"),
                "and no emphatic pronouns");
            Assert.That(Flatten(pronoun.Groups).Select(x => x.Form), Does.Contain("ish"),
                "the pronoun keeps its emphatic");
            Assert.That(Flatten(pronoun.Groups).Select(x => x.Form), Does.Not.Contain("eeym"),
                "and does not eat");
        });
    }

    /// <summary>Cregeen prints two entries headed e — the interjection of
    /// wonder and the possessive his-and-hers — and for years they minted one
    /// merged lexeme (in. and pro. share the id class), whose chimera tree
    /// wore the interjection's class and the possessive's page. Explicit ids
    /// split them, and each tree wears the book's homograph number, tying
    /// tree e¹ to entry e¹.</summary>
    [Test]
    public void TheTwoEsOfCregeenStandApartAndWearTheirNumbers()
    {
        var table = LemmaTable.Instance;
        if (!table.AllDisplayLemmas.Any())
        {
            Assert.Ignore("cregeen.tsv not vendored (manx-lemma-data submodule not initialised)");
        }
        var trees = new LemmaIndexService(table, new CorpusVocabulary(table)).Trees("e");
        Assert.Multiple(() =>
        {
            Assert.That(trees.Select(t => (t.LemmaId, t.Homograph, t.Pos)),
                Is.EqualTo(new[] { ("e-1", (int?)1, "in."), ("e-2", (int?)2, "pro.") }),
                "the interjection first and the possessive second, as the book prints them");
            Assert.That(Flatten(trees[0].Groups).Select(x => x.Form),
                Does.Contain("eh"),
                "eh and eshyn are the interjection's printed spellings, not the possessive's");
            Assert.That(Flatten(trees[1].Groups).Select(x => x.Form),
                Does.Not.Contain("eh"));
        });
    }

    /// <summary>The book prints 'eab or eabb*' over the noun AND the verb —
    /// the starred spelling, per Cregeen's introduction, is the stem the
    /// suffixes join to — but the variant fold keeps one canonical
    /// (document-first, the noun), and the verb whose -agh forms are built
    /// on eabb once never claimed it. A folded spelling reaches every
    /// same-family homograph of its home.</summary>
    [Test]
    public void TheStarredSpellingBelongsToBothEabs()
    {
        var table = LemmaTable.Instance;
        if (!table.AllDisplayLemmas.Any())
        {
            Assert.Ignore("cregeen.tsv not vendored (manx-lemma-data submodule not initialised)");
        }
        var trees = new LemmaIndexService(table, new CorpusVocabulary(table)).Trees("eab");
        var noun = trees.Single(t => t.LemmaId == "eab.n");
        var verb = trees.Single(t => t.LemmaId == "eab.v");
        Assert.Multiple(() =>
        {
            Assert.That(noun.Groups.SingleOrDefault(g => g.LinkType == "variant")?
                    .Forms.Select(f => f.Form) ?? [],
                Does.Contain("eabb"),
                "the noun keeps its printed alternative");
            Assert.That(verb.Groups.SingleOrDefault(g => g.LinkType == "variant")?
                    .Forms.Select(f => f.Form) ?? [],
                Does.Contain("eabb"),
                "eabb is the stem the verb's -agh forms join to: the verb claims it");
            Assert.That(Flatten(verb.Groups).Select(x => x.Form),
                Does.Contain("eabbagh"),
                "and the suffixed forms are built on it");
        });
    }

    /// <summary>The demutation sweep read thaa — Cregeen's verb, welding —
    /// as saa with its s eclipsed, and aeg's tree once listed thaa as a
    /// clean, Cregeen-credited mutation (and thaa's page said it was built
    /// from aeg). The guess is now vetoed at the source (cregeen-nvh
    /// link-exclusions.nvh, the link-plausibility campaign's worked
    /// example): the row must stay out of the table. theihll keeps its
    /// clean row — the book itself prints theihll in seihll's paragraph,
    /// and a print-attested pair must not fall with the guesses.</summary>
    [Test]
    public void TheWeldingThaaIsNoMutationOfAeg()
    {
        var thaa = Flatten(TreeOf("aeg").Groups)
            .Where(x => x.Form == "thaa")
            .ToList();
        var theihll = Flatten(TreeOf("seihll").Groups)
            .Single(x => x.Form == "theihll").Node;
        Assert.Multiple(() =>
        {
            Assert.That(thaa, Is.Empty,
                "thaa is vetoed in link-exclusions.nvh: aeg's tree must not say the welding verb");
            Assert.That(theihll.Unverified, Is.False,
                "seihll's paragraph prints theihll: the book attests the pair");
        });
    }

    /// <summary>Cregeen prints dy-aalin among dy's few sample adverbs, and
    /// the adverb lexeme displays particle-free as aalin — the adjective's
    /// own word. The aalin page once rooted that word beneath dy, as though
    /// the book filed the adjective there; the head carries the phrase
    /// instead, and only the phrase may be said to print under dy.</summary>
    [Test]
    public void TheAdverbAalinDoesNotSeatTheAdjectiveUnderDy()
    {
        var table = LemmaTable.Instance;
        if (!table.AllDisplayLemmas.Any())
        {
            Assert.Ignore("cregeen.tsv not vendored (manx-lemma-data submodule not initialised)");
        }
        var trees = new LemmaIndexService(table, new CorpusVocabulary(table)).Trees("aalin");
        var adverb = trees.Single(t => t.LemmaId == "aalin.x");
        var head = adverb.Parents!.Single(p => p.LinkTypes.Contains("derived"));
        Assert.Multiple(() =>
        {
            Assert.That(head.Lemma, Is.EqualTo("dy"));
            Assert.That(head.Member, Is.EqualTo("dy aalin"),
                "the phrase prints under dy; bare aalin is the adjective's word");
            Assert.That(trees.Single(t => t.LemmaId == "aalin.a").Parents, Is.Null,
                "the adjective's own tree climbs nowhere");
        });
    }

    /// <summary>Every family edge, both ways, table-wide. One lexeme answers
    /// to three names — the printed member headword ('cha s'oc', what the
    /// derived rows key), the display lemma ('s'oc', what pages and trees
    /// key), and the lemma id — and a consumer that asks by the wrong one
    /// breaks a single direction silently: fys once listed cha s'oc while
    /// s'oc's own page climbed nowhere. This sweep holds the plumbing
    /// symmetric — the head's tree names the member, some page the member
    /// form names climbs back — and leaves the truth of any given edge to
    /// the pins above: an edge wrong in both directions passes here.</summary>
    [Test]
    public void EveryFamilyEdgeClimbsBothWays()
    {
        var table = LemmaTable.Instance;
        if (!table.AllDisplayLemmas.Any())
        {
            Assert.Ignore("cregeen.tsv not vendored (manx-lemma-data submodule not initialised)");
        }
        var service = new LemmaIndexService(table, new CorpusVocabulary(table));
        var headTrees = new Dictionary<string, List<LemmaTreePage>>();
        var oneWay = new List<string>();
        var edges = 0;
        foreach (var form in table.AllForms)
        {
            var heads = table.FamilyParentsOf(form);
            if (heads.Count == 0)
            {
                continue;
            }
            // the pages the member form names: its own lexeme(s) — the
            // display spelled like it, or one whose entry headword it is.
            // A display it merely inflects has no business climbing. Every
            // homograph page of a naming display counts: the reader lands
            // on them all.
            var pages = table.DisplayLemmasFor(form)
                .Where(display => LemmaTable.NormalizeForm(display) == form
                                  || table.LinkSetsFor(display)
                                      .SelectMany(s => s.Links)
                                      .Any(l => l.LinkType == "self" && l.Form == form))
                .SelectMany(service.Trees)
                .ToList();
            foreach (var (head, linkType) in heads)
            {
                edges++;
                // downward: the member stands somewhere in one of the head's
                // trees with the family link riding — as its own row, as a
                // merged row the edge rides on ('gaccan (+derived)' under
                // accan's self group), or nested beneath the head's printed
                // headword ('as adsyn' under 'as ad' inside ad's tree)
                if (!headTrees.TryGetValue(head, out var headPages))
                {
                    headTrees[head] = headPages = service.Trees(head);
                }
                var listed = headPages
                    .SelectMany(t => Flatten(t.Groups))
                    .Any(x => LemmaTable.NormalizeForm(x.Form) == form
                              && (x.LinkType == linkType
                                  || x.Node.AlsoLinkedAs?.Contains(linkType) == true));
                if (!listed)
                {
                    oneWay.Add($"'{form}' hangs under {head} as {linkType}, but {head}'s tree does not list it");
                }
                // upward: some page the member names climbs to the head — by
                // any label, since a head already climbed to as a paradigm
                // parent folds the family reading into that line
                if (!pages.Any(t => t.Parents?.Any(p => p.Lemma == head) == true))
                {
                    oneWay.Add($"'{form}' hangs under {head} as {linkType}, but no page of its own climbs to it");
                }
            }
        }
        Assert.That(oneWay, Is.Empty,
            $"{oneWay.Count} one-way family edges; first: {oneWay.FirstOrDefault()}");
        // the sweep must actually sweep: a data-path break would otherwise
        // pass it vacuously
        Assert.That(edges, Is.GreaterThan(1000), "the derived rows have gone missing");
    }
}
