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

    /// <summary>Cregeen prints 'an-' as a headword: a prefix, only ever the front
    /// of a longer word, so no text says one. But the corpus says 'an' 252 times,
    /// and the hyphen is the only thing telling the two apart — NormalizeForm
    /// folds it away, so a history that normalizes first goes and finds all 252
    /// and reports the prefix as attested since 1610.</summary>
    [Test]
    public void AnAffixIsScannedForNothing()
    {
        Add("Psalms", 1610, "ta an dooinney");

        var history = Service().History("gv", "an-");

        Assert.Multiple(() =>
        {
            Assert.That(history.Forms, Is.Empty);
            Assert.That(history.Earliest, Is.Null);
            Assert.That(history.TraditionalCount, Is.Zero);
            Assert.That(history.Decades, Is.Empty);
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
