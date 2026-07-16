using System;
using System.IO;
using System.Linq;
using CorpusSearch.Dependencies;
using CorpusSearch.Dependencies.Lucene;
using CorpusSearch.Model;
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

/// <summary>The history against a real index: what it scans, and what it must
/// refuse to scan.</summary>
[TestFixture]
public class DictionaryHistoryScanTest : QueryBase
{
    private DictionaryHistoryService Service()
    {
        return new DictionaryHistoryService(
            new Searcher(luceneIndex, parser), LemmaTable.Instance,
            new DictionaryLookupService([], LemmaTable.Instance, LemmaResolver.Empty));
    }

    private void Add(string ident, int year, params string[] manxLines)
    {
        var document = new TestDocument(ident, new DateTime(year, 1, 1));
        luceneIndex.Add(document, manxLines.Select((manx, i) =>
            new DocumentLine { Manx = manx, English = "", CsvLineNumber = i + 2 }));
    }

    /// <summary>An affix is scanned for the words carrying it: 'an-' by 'an-ghoo',
    /// which is the only way a prefix is ever said. The bare 'an' on the same line
    /// is the word meaning *their* and no part of the prefix's history — but
    /// NormalizeForm folds 'an-' to 'an', so a history that normalizes first goes
    /// and reports all 252 of them as the prefix, attested since 1610.</summary>
    [Test]
    public void AnAffixIsScannedForTheWordsCarryingIt()
    {
        Add("Doc", 1748, "ta an dooinney", "yn an-ghoo as an-chreestee");

        var history = Service().History("gv", "an-");

        Assert.Multiple(() =>
        {
            // two carriers on one line, and not the bare 'an' on the other
            Assert.That(history.TraditionalCount, Is.EqualTo(2));
            Assert.That(history.Earliest!.EarliestYear, Is.EqualTo(1748));
            // filed under the headword: 'an-*' is a query, not a spelling
            Assert.That(history.Forms.Single().Form, Is.EqualTo("an-"));
        });
    }

    /// <summary>An affix no text carries has no history, rather than the bare
    /// word's</summary>
    [Test]
    public void AnAffixNoWordCarriesIsScannedForNothing()
    {
        Add("Doc", 1748, "ta an dooinney");

        var history = Service().History("gv", "an-");

        Assert.Multiple(() =>
        {
            Assert.That(history.Forms, Is.Empty);
            Assert.That(history.Earliest, Is.Null);
            Assert.That(history.TraditionalCount, Is.Zero);
        });
    }

    /// <summary>...and the word the prefix is spelled like is still a word</summary>
    [Test]
    public void TheWordAnAffixIsSpelledLikeIsStillScanned()
    {
        Add("Psalms", 1610, "ta an dooinney");

        var history = Service().History("gv", "an");

        Assert.That(history.TraditionalCount, Is.EqualTo(1));
    }
}
