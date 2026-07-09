using System.Linq;
using CorpusSearch.Model;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>'Expand context' when searching inside a document (#286)</summary>
[TestFixture]
public class LineRangeTest : QueryBase
{
    private const string DOC = "doc";

    private void AddNumberedDocument(string name, params (int LineNumber, string Manx)[] lines)
    {
        luceneIndex.Add(new TestDocument(name, DOC_DATE), lines.Select(x => new DocumentLine
        {
            Manx = x.Manx,
            English = "",
            CsvLineNumber = x.LineNumber,
        }));
    }

    [Test]
    public void ReturnsTheLinesInTheRange()
    {
        AddNumberedDocument(DOC, (2, "a"), (3, "b"), (4, "c"), (5, "d"), (6, "e"));

        var (lines, total) = luceneIndex.GetLines(DOC, start: 3, end: 5, limit: 100, fromEnd: false, getTranscript: false);

        Assert.That(lines.Select(x => x.CsvLineNumber), Is.EqualTo(new[] { 3, 4, 5 }));
        Assert.That(lines.Select(x => x.Manx), Is.EqualTo(new[] { "b", "c", "d" }));
        Assert.That(total, Is.EqualTo(3));
    }

    [Test]
    public void LimitTakesTheFirstLines()
    {
        AddNumberedDocument(DOC, (2, "a"), (3, "b"), (4, "c"), (5, "d"), (6, "e"));

        var (lines, total) = luceneIndex.GetLines(DOC, start: 2, end: 6, limit: 2, fromEnd: false, getTranscript: false);

        Assert.That(lines.Select(x => x.CsvLineNumber), Is.EqualTo(new[] { 2, 3 }));
        Assert.That(total, Is.EqualTo(5));
    }

    [Test]
    public void FromEndTakesTheLastLines()
    {
        AddNumberedDocument(DOC, (2, "a"), (3, "b"), (4, "c"), (5, "d"), (6, "e"));

        var (lines, total) = luceneIndex.GetLines(DOC, start: 2, end: 6, limit: 2, fromEnd: true, getTranscript: false);

        Assert.That(lines.Select(x => x.CsvLineNumber), Is.EqualTo(new[] { 5, 6 }));
        Assert.That(total, Is.EqualTo(5));
    }

    [Test]
    public void BlankLinesAreSkipped()
    {
        AddNumberedDocument(DOC, (2, "a"), (3, ""), (4, "c"));

        var (lines, total) = luceneIndex.GetLines(DOC, start: 2, end: 4, limit: 100, fromEnd: false, getTranscript: false);

        Assert.That(lines.Select(x => x.CsvLineNumber), Is.EqualTo(new[] { 2, 4 }));
        Assert.That(total, Is.EqualTo(2));
    }

    [Test]
    public void OtherDocumentsAreExcluded()
    {
        AddNumberedDocument(DOC, (2, "a"));
        AddNumberedDocument("other", (2, "x"), (3, "y"));

        var (lines, total) = luceneIndex.GetLines(DOC, start: 2, end: 3, limit: 100, fromEnd: false, getTranscript: false);

        Assert.That(lines.Select(x => x.Manx), Is.EqualTo(new[] { "a" }));
        Assert.That(total, Is.EqualTo(1));
    }

    [Test]
    public void EmptyRangeReturnsNothing()
    {
        AddNumberedDocument(DOC, (2, "a"), (10, "b"));

        var (lines, total) = luceneIndex.GetLines(DOC, start: 3, end: 9, limit: 100, fromEnd: false, getTranscript: false);

        Assert.That(lines, Is.Empty);
        Assert.That(total, Is.EqualTo(0));
    }

    [Test]
    public void LineNumberRangeReturnsTheDocumentBounds()
    {
        AddNumberedDocument(DOC, (2, "a"), (10, "b"), (5, "c"));

        Assert.That(luceneIndex.GetLineNumberRange(DOC), Is.EqualTo((2, 10)));
    }

    [Test]
    public void LineNumberRangeIsNullForAnUnknownDocument()
    {
        AddNumberedDocument(DOC, (2, "a"));

        Assert.That(luceneIndex.GetLineNumberRange("unknown"), Is.Null);
    }
}
