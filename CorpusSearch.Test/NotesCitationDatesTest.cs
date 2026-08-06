using System;
using System.Collections.Generic;
using System.Linq;
using CorpusSearch.Model;
using CorpusSearch.Services;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>
/// The fragments-collection contract (Brooillagh, Manx Notes &amp; Queries): each
/// line's Date cell dates it - "21/09/1901" day first, or a bare "1869" book year -
/// with the Notes citation read in data predating the column, lines with neither
/// belonging to the last dated fragment, and the collection's own date range the
/// span of its lines - so an 1858 newspaper quotation never attests its words in
/// the year the file was typed up.
/// </summary>
[TestFixture]
public class NotesCitationDatesTest
{
    // the real formats of Brooillagh 2/document.csv
    [TestCase("[M.H., 05/05/1858]", "1858-05-05")]
    [TestCase("[M.A., 10/07/1802]", "1802-07-10")]
    [TestCase("[M.S. 05/02/1881]", "1881-02-05")] // no comma
    [TestCase("[M.S.16/02/1881]", "1881-02-16")] // no space at all
    [TestCase("[IoMT, May 02/05/1868]", "1868-05-02")] // stray month name
    [TestCase("[(Rev Radcliffe, Liverpool) M.H., 28/01/1885]", "1885-01-28")]
    [TestCase("[9/3/1878]", "1878-03-09")] // single-digit day and month
    [TestCase("M.S. 07/09/1889]", "1889-09-07")] // missing opening bracket
    public void ACitationsFullDateParses(string note, string expected)
    {
        Assert.That(NotesCitationDates.Parse(note), Is.EqualTo(DateTime.Parse(expected)));
    }

    // the real formats of Manx Notes & Quieries (and Brooillagh's month-name
    // citations), each as its transcriber wrote it
    [TestCase("[1] IoME, Sat, Sep 21, 1901; Page: 3", "1901-09-21")]
    [TestCase("[19] IoME, Sat, Dec 14 1901; P: 6", "1901-12-14")] // the comma slipped
    [TestCase("[THE NEW LANDlNG PIER  (Messrs Cowle): M.H., , July 03, 1872]", "1872-07-03")]
    [TestCase("[COMPLETION OF THE BATTERY PIER. (The Isle of Man Harbour Commissioners): M.H., , September 03, 1879]", "1879-09-03")]
    [TestCase("Flyer [MNH F71/MAT 35816] 30th & 31st October 1912.]", "1912-10-31")] // day first
    [TestCase("[IoME, Sat, Apr 12, 1902; P: 6, Sat, Apr 26, 1902; P:", "1902-04-26")] // the last citation wins
    public void AMonthNameCitationParses(string note, string expected)
    {
        Assert.That(NotesCitationDates.Parse(note), Is.EqualTo(DateTime.Parse(expected)));
    }

    /// <summary>The transcriber's slips read as they were meant - the file is not
    /// made to churn for the parser's benefit</summary>
    [TestCase("[M.S., 23/051868]", "1868-05-23")] // second slash missing
    [TestCase("[M.H., 17/101877]", "1877-10-17")]
    [TestCase("[M.H., 03/071872]", "1872-07-03")]
    [TestCase("[M.H., 07/01//1880]", "1880-01-07")] // doubled slash
    public void ASlippedCitationStillParses(string note, string expected)
    {
        Assert.That(NotesCitationDates.Parse(note), Is.EqualTo(DateTime.Parse(expected)));
    }

    /// <summary>A book cites a year, not a day: the citation's last plausible year</summary>
    [TestCase("[Mona Miscellany; Edited by W. Harrison: Manx Society Vol. XVI; 1869]", 1869)]
    [TestCase("[A Tour Through the Isle of Man, D. Robertson. Pub. E. Hodson, London. 1794.", 1794)]
    public void ABookCitationDatesToItsYear(string note, int expected)
    {
        Assert.That(NotesCitationDates.Parse(note), Is.EqualTo(new DateTime(expected, 1, 1)));
    }

    /// <summary>Prose may precede the citation: the last date wins</summary>
    [Test]
    public void TheLastDateInTheNoteWins()
    {
        const string note = "[Boat of this name is said to have run aground at " +
                            "'Gob y Dagon', now known as Go-y-Deigan. [M.H., 01/01/1896]";
        Assert.That(NotesCitationDates.Parse(note), Is.EqualTo(new DateTime(1896, 1, 1)));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("[likely meaning; signing for the side opposing the lord]")]
    [TestCase("What's the way to the vicarage of Kirk Andreas")]
    public void AProseNoteHasNoDate(string? note)
    {
        Assert.That(NotesCitationDates.Parse(note), Is.Null);
    }

    /// <summary>A year outside 1500-2099 is not a date: a volume or page number</summary>
    [Test]
    public void AnImplausibleYearIsNotADate()
    {
        Assert.That(NotesCitationDates.Parse("[Manx Society Vol. 1211]"), Is.Null);
    }

    /// <summary>A year mid-prose is an aside about the line, not its citation:
    /// "the 1904 reprint" must not re-date a fragment published in 1901. Only a
    /// year closing the note (a book citation) dates the line.</summary>
    [Test]
    public void AProseYearIsNotACitation()
    {
        Assert.That(NotesCitationDates.Parse(
            "[12] er shleeu] 'whetted'. In the 1904 reprint this was changed to [er leeu]."),
            Is.Null);
    }

    // unreadable citations: a day/month fragment with no valid full date. Parsing
    // must not guess; the data repo's lint fails on these instead
    [TestCase("[M.H., 31/02/1858]")] // no such calendar day
    [TestCase("[M.H., 99/9/1858]")] // no such day at all
    [TestCase("[M.H., 05/13]")] // no year
    [TestCase("[IoME, Sat, Sep 31, 1901; Page: 3")] // no such calendar day, month-name style
    public void AnUnreadableCitationIsFlaggedForTheLint(string note)
    {
        Assert.That(NotesCitationDates.LooksDatedButUnparsed(note), Is.True, note);
    }

    [TestCase("[M.H., 05/05/1858]")]
    [TestCase("[1] IoME, Sat, Sep 21, 1901; Page: 3")]
    [TestCase("[Mona Miscellany; 1869]")]
    [TestCase("[likely meaning]")]
    [TestCase("")]
    [TestCase(null)]
    public void AWellFormedOrProseNoteIsNotFlagged(string? note)
    {
        Assert.That(NotesCitationDates.LooksDatedButUnparsed(note), Is.False, note ?? "<null>");
    }

    // The explicit Date cell's schema: day-first like the citations, or a bare
    // year where that is all a book fragment has
    [TestCase("21/09/1901", "1901-09-21")]
    [TestCase("5/2/1901", "1901-02-05")] // single digits as Excel writes them
    [TestCase(" 21/09/1901 ", "1901-09-21")]
    [TestCase("1869", "1869-01-01")]
    public void AnExplicitDateCellParses(string cell, string expected)
    {
        Assert.That(NotesCitationDates.ParseExplicitDate(cell), Is.EqualTo(DateTime.Parse(expected)));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("1901-09-21")] // not the schema: Excel rewrites ISO dates on save
    [TestCase("09/21/1901")] // month-first: a mangled cell must not misdate the line
    [TestCase("31/02/1904")] // no such calendar day
    [TestCase("190")] // no such year
    [TestCase("Sep 21, 1901")] // citations belong in Notes
    public void AnUnreadableDateCellIsNull(string? cell)
    {
        Assert.That(NotesCitationDates.ParseExplicitDate(cell), Is.Null);
    }

    private static OpenSourceDocument FragmentsManifest() => new()
    {
        Name = "doc",
        Ident = "doc",
        NotesCitations = true,
    };

    private static DocumentLine Line(string? note, string? dateCell = null) =>
        new() { Manx = "ta", English = "is", Notes = note, DateCell = dateCell };

    [Test]
    public void UncitedLinesBelongToTheLastCitedFragment()
    {
        var lines = new List<DocumentLine>
        {
            Line("[M.H., 05/05/1858]"),
            Line(null), // the rest of the quoted song
            Line("[a prose note on the fragment]"),
            Line("[M.S., 15/11/1856]"), // the next fragment
            Line(""),
        };

        DocumentLinePreparer.Prepare(FragmentsManifest(), lines);

        Assert.That(lines.Select(x => x.Date), Is.EqualTo(new DateTime?[]
        {
            new DateTime(1858, 5, 5),
            new DateTime(1858, 5, 5),
            new DateTime(1858, 5, 5),
            new DateTime(1856, 11, 15),
            new DateTime(1856, 11, 15),
        }));
    }

    [Test]
    public void LinesBeforeTheFirstCitationStayUndated()
    {
        var lines = new List<DocumentLine> { Line(null), Line("[M.H., 05/05/1858]") };

        DocumentLinePreparer.Prepare(FragmentsManifest(), lines);

        Assert.That(lines[0].Date, Is.Null);
        Assert.That(lines[1].Date, Is.EqualTo(new DateTime(1858, 5, 5)));
    }

    /// <summary>The explicit Date cell is authoritative; the citation only dates
    /// rows in data predating the column</summary>
    [Test]
    public void TheDateCellOutranksTheCitation()
    {
        var lines = new List<DocumentLine>
        {
            Line("[1] IoME, Sat, Sep 21, 1901; Page: 3", dateCell: "22/09/1901"),
            Line("[3] IoME, Sat, Sep 28, 1901; Page: 3", dateCell: ""),
            Line(null, dateCell: "09/21/1901"), // mangled: falls through to inheritance
            Line(null, dateCell: "1869"),
        };

        DocumentLinePreparer.Prepare(FragmentsManifest(), lines);

        Assert.That(lines.Select(x => x.Date), Is.EqualTo(new DateTime?[]
        {
            new DateTime(1901, 9, 22),
            new DateTime(1901, 9, 28), // a blank cell falls back to the citation
            new DateTime(1901, 9, 28),
            new DateTime(1869, 1, 1), // a year-only book fragment
        }));
    }

    /// <summary>A collection dated by its cells needs no citations at all</summary>
    [Test]
    public void DateCellsAloneDateTheCollection()
    {
        var document = FragmentsManifest();
        var lines = new List<DocumentLine>
        {
            Line(null, dateCell: "21/09/1901"),
            Line(null, dateCell: "14/05/1904"),
        };

        DocumentLinePreparer.Prepare(document, lines);

        Assert.That(document.CreatedCircaStart, Is.EqualTo(new DateTime(1901, 9, 21)));
        Assert.That(document.CreatedCircaEnd, Is.EqualTo(new DateTime(1904, 5, 14)));
    }

    /// <summary>The collection spans its fragments - and the file need not be
    /// chronological, so the span is min/max, not first/last</summary>
    [Test]
    public void TheCollectionsDateRangeIsTheSpanOfItsLines()
    {
        var document = FragmentsManifest();
        var lines = new List<DocumentLine>
        {
            Line("[M.H., 05/05/1858]"),
            Line("[IoMT, 02/05/1868]"),
            Line("[M.A., 10/07/1802]"), // out of order, like the real file
        };

        DocumentLinePreparer.Prepare(document, lines);

        Assert.That(document.CreatedCircaStart, Is.EqualTo(new DateTime(1802, 7, 10)));
        Assert.That(document.CreatedCircaEnd, Is.EqualTo(new DateTime(1868, 5, 2)));
    }

    /// <summary>A manifest "created" is the transcription's date, not the
    /// fragments': the lines' span replaces it</summary>
    [Test]
    public void TheLinesSpanReplacesAManifestDate()
    {
        var document = FragmentsManifest();
        document.Created = new DateTime(2026, 7, 14);

        DocumentLinePreparer.Prepare(document, [Line("[M.H., 05/05/1858]")]);

        Assert.That(document.CreatedCircaStart, Is.EqualTo(new DateTime(1858, 5, 5)));
        Assert.That(document.CreatedCircaEnd, Is.EqualTo(new DateTime(1858, 5, 5)));
    }

    [Test]
    public void ACollectionWithoutParsableCitationsKeepsItsManifestDate()
    {
        var document = FragmentsManifest();
        document.Created = new DateTime(1900, 1, 1);

        DocumentLinePreparer.Prepare(document, [Line("[a prose note]")]);

        Assert.That(document.CreatedCircaStart, Is.EqualTo(new DateTime(1900, 1, 1)));
    }

    /// <summary>Notes elsewhere in the corpus are annotations, not citations:
    /// nothing happens without the manifest's opt-in</summary>
    [Test]
    public void WithoutTheManifestFlagNotesAreNotDates()
    {
        var document = new OpenSourceDocument { Name = "doc", Ident = "doc" };
        var lines = new List<DocumentLine> { Line("[M.H., 05/05/1858]") };

        DocumentLinePreparer.Prepare(document, lines);

        Assert.That(lines[0].Date, Is.Null);
        Assert.That(document.CreatedCircaStart, Is.Null);
    }
}
