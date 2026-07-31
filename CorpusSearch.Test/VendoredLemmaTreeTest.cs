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
}
