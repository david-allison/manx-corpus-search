using System.IO;
using System.Linq;
using CorpusSearch.Dependencies.Lucene;
using CorpusSearch.Service;
using NUnit.Framework;

namespace CorpusSearch.Test;

public class DictionaryHistoryServiceTest
{
    [Test]
    public void CognateRunsAreExtracted()
    {
        var cognates = DictionaryHistoryService
            .CognatesIn("s. a tree. (Ir. bile; S.G. bil.) also (W. pren.) but (not this)")
            .ToList();

        Assert.That(cognates, Is.EqualTo(new[] { "Ir. bile; S.G. bil.", "W. pren." }));
    }

    /// <summary>'ass' (out) is a headword itself: its history must not merge
    /// in the demutation guess fass - that reading belongs to the popup's
    /// root chain, not to a timeline that would mix two words</summary>
    [Test]
    public void AHeadwordKeepsItsOwnLexemeOnly()
    {
        var table = LemmaTable.Load(new StringReader(
            "form\tlemmaId\tlemma\tlinkType\n" +
            "ass\tass.x\tass\tself\n" +
            "ass\tfass.v\tfass\tmutation\n" +
            "vee\tbee.v\tbee\tmutation\n"));

        Assert.Multiple(() =>
        {
            Assert.That(DictionaryHistoryService.LemmaReadingsFor(table, "ass"),
                Is.EqualTo(new[] { "ass" }));
            // a purely inflected form has no self reading: candidates stay
            Assert.That(DictionaryHistoryService.LemmaReadingsFor(table, "vee"),
                Is.EqualTo(new[] { "bee" }));
        });
    }

    /// <summary>The history view walks lemma -> forms, the reverse of every
    /// other lemma lookup: the cluster carries the mutated spellings</summary>
    [Test]
    public void FormsOfReturnsTheLexemeCluster()
    {
        var table = LemmaTable.Load(new StringReader(
            "form\tlemmaId\tlemma\tlinkType\n" +
            "billey\tbilley.n\tbilley\tself\n" +
            "villey\tbilley.n\tbilley\tmutation\n" +
            "meir\tmair.n\tmair\tplural\n"));

        Assert.Multiple(() =>
        {
            Assert.That(table.FormsOf("billey"), Is.EqualTo(new[] { "billey", "villey" }));
            Assert.That(table.FormsOf("mair"), Is.EqualTo(new[] { "meir" }));
            Assert.That(table.FormsOf("unknown"), Is.Empty);
        });
    }
}
