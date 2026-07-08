using System.Collections.Generic;
using System.Linq;
using CorpusSearch.Dependencies;
using CorpusSearch.Model;
using CorpusSearch.Test.TestUtils;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>
/// Tests that search results carry character ranges of the *raw* text which matched the
/// (normalized, diacritic-folded) query - see #40.
/// </summary>
public class HighlightingTest : QueryBase
{
    private const string DOC = "doc";

    private SearchResult SearchWork(string query, SearchType type = SearchType.Manx, bool ignoreHyphens = false)
    {
        return new Searcher(luceneIndex, parser)
            .SearchWork(DOC, query, new SearchOptions { Type = type, IgnoreHyphens = ignoreHyphens });
    }

    /// <summary>The substrings of the raw text selected by the returned highlight ranges</summary>
    private static List<string> Highlighted(string raw, IReadOnlyList<HighlightRange> ranges)
    {
        Assert.That(ranges, Is.Not.Null, $"expected highlights on '{raw}'");
        return ranges.Select(x => raw[x.Start..x.End]).ToList();
    }

    private void AssertSingleLineHighlights(string query, params string[] expected)
    {
        var line = SearchWork(query).Lines.Single();
        Assert.That(Highlighted(line.Manx, line.ManxHighlights), Is.EqualTo(expected));
        Assert.That(line.EnglishHighlights, Is.Null);
    }

    [Test]
    public void DiacriticFoldedQueryHighlightsTheRawText()
    {
        this.AddManxDoc(DOC, "Ta çhengey aym");
        AssertSingleLineHighlights("chengey", "çhengey");
    }

    [Test]
    public void CaseAndDiacriticDifferences()
    {
        this.AddManxDoc(DOC, "Çhengey ny mayrey");
        AssertSingleLineHighlights("chengey", "Çhengey");
    }

    [Test]
    public void CurlyApostropheInTextStraightInQuery()
    {
        this.AddManxDoc(DOC, "Va’n dooinney");
        AssertSingleLineHighlights("va'n", "Va’n");
    }

    [Test]
    public void WildcardHighlightsEachMatchedForm()
    {
        this.AddManxDoc(DOC, "cabbil as cabbyl");
        AssertSingleLineHighlights("cab*", "cabbil", "cabbyl");
    }

    [Test]
    public void PhraseHighlightsTheWholePhraseOnce()
    {
        this.AddManxDoc(DOC, "moghrey mie dhyt");
        AssertSingleLineHighlights("moghrey mie", "moghrey mie");
    }

    [Test]
    public void AndHighlightsTheTermsNotTheStretchBetweenThem()
    {
        this.AddManxDoc(DOC, "hi baz world");
        var line = SearchWork("hi and world").Lines.Single();
        Assert.That(line.ManxHighlights, Is.EqualTo(new[]
        {
            new HighlightRange(0, 2),   // hi
            new HighlightRange(7, 12),  // world
        }));
    }

    [Test]
    public void OrHighlightsBothAlternatives()
    {
        this.AddManxDoc(DOC, "shoh as shen");
        AssertSingleLineHighlights("shoh or shen", "shoh", "shen");
    }

    [Test]
    public void NotOnlyHighlightsTheIncludedTerm()
    {
        this.AddManxDoc(DOC, "hi foo", "hi world");
        var result = SearchWork("hi not world");
        var line = result.Lines.Single();
        Assert.That(line.Manx, Is.EqualTo("hi foo"));
        Assert.That(Highlighted(line.Manx, line.ManxHighlights), Is.EqualTo(new[] { "hi" }));
    }

    [Test]
    public void TrailingPunctuationIsNotHighlighted()
    {
        // #237 - "jee." is indexed as "jee"
        this.AddManxDoc(DOC, "jee.");
        var line = SearchWork("jee").Lines.Single();
        Assert.That(line.ManxHighlights, Is.EqualTo(new[] { new HighlightRange(0, 3) }));
    }

    [Test]
    public void MatchesAtLineStartAndLineEnd()
    {
        this.AddManxDoc(DOC, "cre ta shen cre");
        var line = SearchWork("cre").Lines.Single();
        Assert.That(line.ManxHighlights, Is.EqualTo(new[]
        {
            new HighlightRange(0, 3),
            new HighlightRange(12, 15),
        }));
    }

    [Test]
    public void QueryMatchingALongerHyphenatedTokenHighlightsAllOfIt()
    {
        // 'cre' matches 'cre-erbee' (ManxQuery allows a trailing hyphenated suffix)
        this.AddManxDoc(DOC, "cre-erbee t’ou");
        AssertSingleLineHighlights("cre", "cre-erbee");
    }

    [Test]
    public void EllipsisExpansionDoesNotShiftTheRange()
    {
        // '…' is normalized to "..." - a 1->3 expansion before the match
        this.AddManxDoc(DOC, "cha nel… agh cre");
        var line = SearchWork("cre").Lines.Single();
        Assert.That(line.ManxHighlights, Is.EqualTo(new[] { new HighlightRange(13, 16) }));
        Assert.That(Highlighted(line.Manx, line.ManxHighlights), Is.EqualTo(new[] { "cre" }));
    }

    [Test]
    public void MultipleMatchingLinesEachGetTheirOwnHighlights()
    {
        this.AddManxDoc(DOC, "cre shoh", "as cre shen", "gyn veg");
        var result = SearchWork("cre");
        Assert.That(result.Lines, Has.Count.EqualTo(2));
        foreach (var line in result.Lines)
        {
            Assert.That(Highlighted(line.Manx, line.ManxHighlights), Is.EqualTo(new[] { "cre" }));
        }
    }

    [Test]
    public void EnglishSearchHighlightsTheEnglishText()
    {
        AddDocument(DOC, new Line { Manx = "yn çhengey", English = "the tongue" });
        var line = SearchWork("tongue", SearchType.English).Lines.Single();
        Assert.That(Highlighted(line.English, line.EnglishHighlights), Is.EqualTo(new[] { "tongue" }));
        Assert.That(line.ManxHighlights, Is.Null);
    }

    [Test]
    public void MultiSegmentIndexHighlightsTheRightDocuments()
    {
        // each Add() flushes, producing multiple index segments (docBase != 0)
        this.AddManxDoc("other1", "çhengey elley");
        this.AddManxDoc("other2", "gyn çhengey");
        this.AddManxDoc(DOC, "yn çhengey ain");

        var line = SearchWork("chengey").Lines.Single();
        Assert.That(line.Manx, Is.EqualTo("yn çhengey ain"));
        Assert.That(Highlighted(line.Manx, line.ManxHighlights), Is.EqualTo(new[] { "çhengey" }));
    }

    [Test]
    public void WildcardMatchingAllWordsHighlightsEverything()
    {
        this.AddManxDoc(DOC, "shoh shen");
        var line = SearchWork("sh*").Lines.Single();
        Assert.That(Highlighted(line.Manx, line.ManxHighlights), Is.EqualTo(new[] { "shoh", "shen" }));
    }

    [Test]
    public void CountsAreUnchangedByHighlighting()
    {
        this.AddManxDoc(DOC, "cre ta shen cre", "cre");
        var result = SearchWork("cre");
        Assert.That(result.TotalMatches, Is.EqualTo(3));
        Assert.That(result.Lines.Select(x => x.MatchesInLine), Is.EquivalentTo(new long?[] { 2, 1 }));
    }

    [Test]
    public void WildcardOnlyQueriesBrowseInsteadOfHighlightingEveryToken(
        [Values("*", " * ", ".*", ",*", "*.", "**")] string query)
    {
        // '.*' normalizes to '*' and previously bypassed the browse short-circuit,
        // matching (and highlighting) every token of every line
        this.AddManxDoc(DOC, "Ta çhengey aym", "gyn veg");
        var result = SearchWork(query);
        Assert.That(result.Lines, Has.Count.EqualTo(2));
        Assert.That(result.TotalMatches, Is.Null, "browse results have no match count");
        Assert.That(result.Lines.Select(x => x.ManxHighlights), Is.All.Null);
    }

    [Test]
    public void IgnoreHyphensHighlightsTheSpacedForm()
    {
        // #18 - a hyphenated query matches (and must highlight) the spaced form
        this.AddManxDoc(DOC, "s’beg lhiam lhiat");
        var line = SearchWork("lhiam-lhiat", ignoreHyphens: true).Lines.Single();
        Assert.That(Highlighted(line.Manx, line.ManxHighlights), Is.EqualTo(new[] { "lhiam lhiat" }));
    }

    [Test]
    public void IgnoreHyphensHighlightsThePunctuatedSpacedForm()
    {
        // punctuation is a token separator, so 'lhiam, lhiat' matches as a phrase:
        // the highlight covers the whole stretch, comma included
        this.AddManxDoc(DOC, "S’beg lhiam, lhiat, lesh");
        var line = SearchWork("lhiam-lhiat", ignoreHyphens: true).Lines.Single();
        Assert.That(Highlighted(line.Manx, line.ManxHighlights), Is.EqualTo(new[] { "lhiam, lhiat" }));
    }

    [Test]
    public void IgnoreHyphensHighlightsTheHyphenatedForm()
    {
        // #18 - the reverse direction: a spaced query highlights the single hyphenated token
        this.AddManxDoc(DOC, "she lhiam-lhiat eh");
        var line = SearchWork("lhiam lhiat", ignoreHyphens: true).Lines.Single();
        Assert.That(Highlighted(line.Manx, line.ManxHighlights), Is.EqualTo(new[] { "lhiam-lhiat" }));
    }

    [Test]
    public void IgnoreHyphensJoinedQueryHighlightsTheHyphenatedForm()
    {
        this.AddManxDoc(DOC, "she lhiam-lhiat eh");
        var line = SearchWork("lhiamlhiat", ignoreHyphens: true).Lines.Single();
        Assert.That(Highlighted(line.Manx, line.ManxHighlights), Is.EqualTo(new[] { "lhiam-lhiat" }));
    }

    private ScanResult Scan(string query, ScanOptions options = null)
    {
        return new Searcher(luceneIndex, parser).Scan(query, options ?? ScanOptions.Default);
    }

    [Test]
    public void ScanIgnoringHyphensHighlightsTheSample()
    {
        // the corpus-search sample line gets the same hyphen-insensitive highlights
        this.AddManxDoc(DOC, "she lhiam-lhiat eh");
        var result = Scan("lhiam lhiat", new ScanOptions { IgnoreHyphens = true })
            .DocumentResults.Single();
        Assert.That(Highlighted(result.Sample, result.SampleHighlights), Is.EqualTo(new[] { "lhiam-lhiat" }));
    }

    [Test]
    public void ScanHighlightsTheSampleLine()
    {
        this.AddManxDoc(DOC, "Ta çhengey aym");
        var result = Scan("chengey").DocumentResults.Single();
        Assert.That(Highlighted(result.Sample, result.SampleHighlights), Is.EqualTo(new[] { "çhengey" }));
    }

    [Test]
    public void ScanHighlightsReferToTheFirstMatchingLine()
    {
        this.AddManxDoc(DOC, "gyn veg ayn", "cre shoh, as cre shen");
        var result = Scan("cre").DocumentResults.Single();
        Assert.That(result.Sample, Is.EqualTo("cre shoh, as cre shen"));
        Assert.That(Highlighted(result.Sample, result.SampleHighlights), Is.EqualTo(new[] { "cre", "cre" }));
        Assert.That(result.Count, Is.EqualTo(2));
    }

    [Test]
    public void ScanHighlightsEachDocumentsOwnSample()
    {
        this.AddManxDoc("doc1", "çhengey elley");
        this.AddManxDoc("doc2", "yn çhengey ain");
        var results = Scan("chengey").DocumentResults;
        Assert.That(results, Has.Count.EqualTo(2));
        foreach (var result in results)
        {
            Assert.That(Highlighted(result.Sample, result.SampleHighlights), Is.EqualTo(new[] { "çhengey" }));
        }
    }

    [Test]
    public void EnglishScanDoesNotHighlightTheManxSample()
    {
        AddDocument(DOC, new Line { Manx = "yn çhengey", English = "the tongue" });
        var result = Scan("tongue", new ScanOptions { SearchType = SearchType.English })
            .DocumentResults.Single();
        Assert.That(result.SampleHighlights, Is.Null);
    }
}
