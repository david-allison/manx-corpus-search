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
                     })
            {
                Assert.That(table.DisplayLemmasFor(emphatic), Is.EqualTo(new[] { word }),
                    $"{emphatic} is {word}'s alone: another claimant means a page " +
                    "of extra family trees and a false shared-spelling mark");
            }
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
        var headTrees = new Dictionary<string, LemmaTreePage?>();
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
            // A display it merely inflects has no business climbing.
            var pages = table.DisplayLemmasFor(form)
                .Where(display => LemmaTable.NormalizeForm(display) == form
                                  || table.LinksOf(display)?.Links
                                      .Any(l => l.LinkType == "self" && l.Form == form) == true)
                .Select(service.Tree)
                .Where(t => t != null)
                .ToList();
            foreach (var (head, linkType) in heads)
            {
                edges++;
                // downward: the member stands somewhere in the head's tree
                // with the family link riding — as its own row, as a merged
                // row the edge rides on ('gaccan (+derived)' under accan's
                // self group), or nested beneath the head's printed headword
                // ('as adsyn' under 'as ad' inside ad's tree)
                if (!headTrees.TryGetValue(head, out var headTree))
                {
                    headTrees[head] = headTree = service.Tree(head);
                }
                var listed = headTree != null && Flatten(headTree.Groups)
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
                if (!pages.Any(t => t!.Parents?.Any(p => p.Lemma == head) == true))
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
