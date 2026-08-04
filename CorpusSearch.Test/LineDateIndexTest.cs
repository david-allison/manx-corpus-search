using System;
using System.Linq;
using CorpusSearch.Dependencies;
using CorpusSearch.Model;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>
/// Index-side half of the fragments-collection contract (<see cref="NotesCitationDates"/>):
/// a line carrying its own date is indexed under it, and a scan reports the span of the
/// *matched* lines - so a Brooillagh fragment attests its words in its citation's year,
/// wherever it sits in the file. Documents whose lines share their date are unchanged.
/// </summary>
[TestFixture]
public class LineDateIndexTest : QueryBase
{
    private const string DOC = "doc";

    private void AddLines(params DocumentLine[] lines)
    {
        luceneIndex.Add(new TestDocument(DOC, DOC_DATE), lines);
    }

    private static DocumentLine Line(int lineNumber, string manx, DateTime? date = null)
        => new() { Manx = manx, English = "", CsvLineNumber = lineNumber, Date = date };

    private ScanResult Scan(string query) => new Searcher(luceneIndex, parser).Scan(query);

    [Test]
    public void AScanReportsTheSpanOfTheMatchedLines()
    {
        AddLines(
            Line(2, "yn boggane mooar", new DateTime(1890, 3, 1)),
            Line(3, "shenn boggane", new DateTime(1856, 11, 15)),
            Line(4, "gyn boggane erbee", new DateTime(1872, 2, 21)));

        var result = Scan("boggane").DocumentResults.Single();

        Assert.Multiple(() =>
        {
            Assert.That(result.StartDate, Is.EqualTo(new DateTime(1856, 11, 15)));
            Assert.That(result.EndDate, Is.EqualTo(new DateTime(1890, 3, 1)));
        });
    }

    /// <summary>The fragments file is not chronological: the sample (the home page's
    /// KWIC, the attestation walk's earliest form) is the earliest-dated matched
    /// line, not the first in the file</summary>
    [Test]
    public void TheSampleIsTheEarliestDatedMatch()
    {
        AddLines(
            Line(2, "yn boggane mooar", new DateTime(1890, 3, 1)),
            Line(3, "shenn voggane", new DateTime(1856, 11, 15)));

        var result = Scan("boggane or voggane").DocumentResults.Single();

        Assert.That(result.Sample, Is.EqualTo("shenn voggane"));
    }

    /// <summary>Only the matched lines answer: a scan touching one fragment
    /// reports that fragment's date, not the collection's whole span</summary>
    [Test]
    public void AnUnmatchedLinesDateDoesNotWiden()
    {
        AddLines(
            Line(2, "yn boggane mooar", new DateTime(1890, 3, 1)),
            Line(3, "red elley", new DateTime(1856, 11, 15)));

        var result = Scan("boggane").DocumentResults.Single();

        Assert.Multiple(() =>
        {
            Assert.That(result.StartDate, Is.EqualTo(new DateTime(1890, 3, 1)));
            Assert.That(result.EndDate, Is.EqualTo(new DateTime(1890, 3, 1)));
        });
    }

    /// <summary>Everything else in the corpus: lines without their own date index
    /// under their document's, and the first match in the file stays the sample</summary>
    [Test]
    public void UndatedLinesKeepTheDocumentDateAndFirstLineSample()
    {
        AddLines(
            Line(2, "yn boggane mooar"),
            Line(3, "shenn voggane"));

        var result = Scan("boggane or voggane").DocumentResults.Single();

        Assert.Multiple(() =>
        {
            Assert.That(result.StartDate, Is.EqualTo(DOC_DATE));
            Assert.That(result.EndDate, Is.EqualTo(DOC_DATE));
            Assert.That(result.Sample, Is.EqualTo("yn boggane mooar"));
        });
    }

    /// <summary>A document with no date at all still scans as undated</summary>
    [Test]
    public void AnUndatedDocumentScansAsUndated()
    {
        luceneIndex.Add(new TestDocument(DOC, null), new[] { Line(2, "yn boggane mooar") }.ToList());

        var result = Scan("boggane").DocumentResults.Single();

        Assert.Multiple(() =>
        {
            Assert.That(result.StartDate, Is.Null);
            Assert.That(result.EndDate, Is.Null);
        });
    }
}
