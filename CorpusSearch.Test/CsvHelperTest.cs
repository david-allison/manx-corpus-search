using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CorpusSearch.Controllers;
using CorpusSearch.Dependencies.CsvHelper;
using CorpusSearch.Model;
using CsvHelper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace CorpusSearch.Test;

[TestFixture]
public class CsvHelperTest
{
    private static List<DocumentLine> Load(string content)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, content);
            return CsvHelperUtils.LoadCsv(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestCase("")]
    [TestCase("English,Manx\n")]
    public void EmptyFilesHaveNoLines(string content)
    {
        Assert.That(Load(content), Is.Empty);
    }

    [Test]
    public void OnlyEnglishAndManxAreRequired()
    {
        var line = Load("English,Manx\nhello,moghrey mie\n").Single();

        Assert.That(line.English, Is.EqualTo("hello"));
        Assert.That(line.Manx, Is.EqualTo("moghrey mie"));
        Assert.That(line.Page, Is.Null);
        Assert.That(line.Notes, Is.Null);
        Assert.That(line.SubStart, Is.Null);
        Assert.That(line.SubEnd, Is.Null);
        Assert.That(line.ManxOriginal, Is.Null);
        Assert.That(line.EnglishOriginal, Is.Null);
        Assert.That(line.CsvLineNumber, Is.EqualTo(2));
    }

    [Test]
    [SetCulture("fr-FR")]
    public void ReadsNamedColumnsAndInvariantNumbers()
    {
        var line = Load(
            "Manx,English,Page,Notes,SubStart,SubEnd,Speaker,Reference,ManxColumnLanguage,Date,Source,Manx Original,English Original,MatchesInLine\n" +
            "çhengey,language,3,a note,12.5,14.75,NM,MS 1 Thessalonians 2.16,gv,21/09/1901,Isle of Man Examiner,chengey,tongue,99\n")
            .Single();

        Assert.That(line.Manx, Is.EqualTo("çhengey"));
        Assert.That(line.English, Is.EqualTo("language"));
        Assert.That(line.Page, Is.EqualTo(3));
        Assert.That(line.Notes, Is.EqualTo("a note"));
        Assert.That(line.SubStart, Is.EqualTo(12.5));
        Assert.That(line.SubEnd, Is.EqualTo(14.75));
        Assert.That(line.Speaker, Is.EqualTo("NM"));
        Assert.That(line.Reference, Is.EqualTo("MS 1 Thessalonians 2.16"));
        Assert.That(line.Language, Is.EqualTo("gv"));
        Assert.That(line.DateCell, Is.EqualTo("21/09/1901"));
        Assert.That(line.Source, Is.EqualTo("Isle of Man Examiner"));
        Assert.That(line.ManxOriginal, Is.EqualTo("chengey"));
        Assert.That(line.EnglishOriginal, Is.EqualTo("tongue"));
        Assert.That(line.MatchesInLine, Is.Null);
    }

    [Test]
    public void BlankOptionalCellsKeepTheirExistingMeaning()
    {
        var line = Load("English,Manx,Page,Notes,SubStart,SubEnd,Manx Original,English Original\n" +
                        "language,çhengey,,,,,  ,\n").Single();

        Assert.That(line.Page, Is.Null);
        Assert.That(line.Notes, Is.EqualTo(""));
        Assert.That(line.SubStart, Is.Null);
        Assert.That(line.SubEnd, Is.Null);
        Assert.That(line.ManxOriginal, Is.Null);
        Assert.That(line.EnglishOriginal, Is.Null);
    }

    [Test]
    public void MissingTrailingOriginalFieldsAreOptional()
    {
        var line = Load("English,Manx,Manx Original,English Original\n" +
                        "language,çhengey\n").Single();

        Assert.That(line.ManxOriginal, Is.Null);
        Assert.That(line.EnglishOriginal, Is.Null);
    }

    [TestCase("\n")]
    [TestCase("\r\n")]
    [TestCase("\r")]
    public void LineNumbersStillCountQuotedAndBlankPhysicalLines(string newline)
    {
        var lines = Load($"English,Manx{newline}{newline}" +
                         $"\"two{newline}lines\",\"daa{newline}linney\"{newline}" +
                         $"third,tree{newline}");

        Assert.That(lines.Select(x => x.CsvLineNumber), Is.EqualTo(new[] { 5, 6 }));
        Assert.That(lines[0].English, Is.EqualTo($"two{newline}lines"));
        Assert.That(lines[0].Manx, Is.EqualTo($"daa{newline}linney"));
    }

    [TestCase("English\nhello\n")]
    [TestCase("Manx\nmoghrey mie\n")]
    public void MissingRequiredColumnsAreRejected(string content)
    {
        Assert.Throws<KeyNotFoundException>(() => Load(content));
    }

    [Test]
    public void MalformedQuotingIsStillRejected()
    {
        Assert.Throws<CsvFormatException>(() => Load("English,Manx\nhello,\"unterminated"));
    }

    [Test]
    public void TermFrequencyDownloadStillWritesHeadersUnicodeAndQuotedFields()
    {
        StatisticsController.Init((1, 3), [("çhengey", 2), ("fockle,\"quoted\"", 1)],
            NullLogger<Startup>.Instance);
        var controller = new StatisticsController
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        var result = (FileStreamResult)controller.ManxTermFrequency("gv");
        using var stream = result.FileStream;
        Assert.That(stream.CanRead, Is.True);
        Assert.That(stream.Position, Is.Zero);
        Assert.That(result.ContentType, Is.EqualTo("text/csv"));
        Assert.That(controller.Response.Headers.ContentDisposition.ToString(),
            Is.EqualTo("attachment; filename=TermFrequency.csv"));
        var bytes = ((MemoryStream)stream).ToArray();
        Assert.That(bytes.Take(3), Is.EqualTo(Encoding.UTF8.GetPreamble()));
        Assert.That(Encoding.UTF8.GetString(bytes[3..]),
            Is.EqualTo("Term,Frequency\r\nçhengey,2\r\n\"fockle,\"\"quoted\"\"\",1\r\n"));
    }
}
