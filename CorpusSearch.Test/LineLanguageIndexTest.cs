using System.Linq;
using CorpusSearch.Dependencies;
using CorpusSearch.Model;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>
/// Index-side half of the line-language contract: non-Manx lines stay searchable but
/// no longer count towards Manx statistics, and their language (like any line's speaker)
/// is returned.
/// </summary>
[TestFixture]
public class LineLanguageIndexTest : QueryBase
{
    private const string DOC = "doc";

    private void AddLines(params DocumentLine[] lines)
    {
        luceneIndex.Add(new TestDocument(DOC, DOC_DATE), lines);
    }

    private static DocumentLine Line(int lineNumber, string manx, string? language = null, string? speaker = null)
        => new() { Manx = manx, English = "", CsvLineNumber = lineNumber, Language = language, Speaker = speaker };

    [Test]
    public void NonManxLinesDoNotCountAsManxTerms()
    {
        AddLines(
            Line(2, "ta mee braew"),
            Line(3, "the cat sat on the mat", language: "en"));

        Assert.That(luceneIndex.CountManxTerms(), Is.EqualTo(3));
    }

    [Test]
    public void NonManxTermsAreExcludedFromTheFrequencyList()
    {
        AddLines(
            Line(2, "ta mee braew"),
            Line(3, "the cat sat on the mat", language: "en"));

        var terms = luceneIndex.GetTermFrequencyList().Select(x => x.Item1).ToList();
        Assert.That(terms, Is.EquivalentTo(new[] { "ta", "mee", "braew" }));
    }

    [Test]
    public void ExplicitGvCountsAsManx()
    {
        AddLines(Line(2, "ta mee braew", language: "gv"));

        Assert.That(luceneIndex.CountManxTerms(), Is.EqualTo(3));
    }

    [Test]
    public void NonManxLinesRemainSearchable()
    {
        AddLines(
            Line(2, "ta mee braew"),
            Line(3, "the cat sat on the mat", language: "en"));

        var result = new Searcher(luceneIndex, parser).SearchWork(DOC, "cat",
            new SearchOptions { SearchType = SearchType.Manx }, returnTranscriptData: false);

        var line = result.Lines.Single();
        Assert.That(line.Manx, Is.EqualTo("the cat sat on the mat"));
        Assert.That(line.Language, Is.EqualTo("en"));
    }

    [Test]
    public void ManxLinesHaveNoLanguageMarker()
    {
        AddLines(Line(2, "ta mee braew"));

        var line = luceneIndex.GetAllLines(DOC, getTranscript: false).Single();
        Assert.That(line.Language, Is.Null);
    }

    [Test]
    public void SpeakerIsReturnedWithoutTranscriptData()
    {
        AddLines(Line(2, "Ta fys aym", speaker: "NM"));

        var browsed = luceneIndex.GetAllLines(DOC, getTranscript: false).Single();
        Assert.That(browsed.Speaker, Is.EqualTo("NM"));

        var searched = new Searcher(luceneIndex, parser).SearchWork(DOC, "fys",
            new SearchOptions { SearchType = SearchType.Manx }, returnTranscriptData: false);
        Assert.That(searched.Lines.Single().Speaker, Is.EqualTo("NM"));
    }
}
