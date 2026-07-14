using System.Linq;
using CorpusSearch.Dependencies;
using CorpusSearch.Model;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>
/// Verse/chapter references (HANDOFF-verse-references.md): line metadata like
/// Speaker, indexed through a digit-preserving analyzer so "Thessalonians"
/// and "2.16" still find the line, while the Manx token stream stays clean.
/// </summary>
[TestFixture]
public class ReferenceSearchTest : QueryBase
{
    private const string DOC = "doc";

    private Searcher NewSearcher() => new(luceneIndex, parser);

    private void AddReferencedLines()
    {
        luceneIndex.Add(new TestDocument(DOC, DOC_DATE), [
            new DocumentLine
            {
                Manx = "Liettal shin vei loayrt",
                English = "Forbidding us to speak",
                Reference = "MS 1 Thessalonians 2.16",
                CsvLineNumber = 2,
            },
            new DocumentLine
            {
                Manx = "Ta'n Chiarn er my hroggal",
                English = "",
                CsvLineNumber = 3,
            },
        ]);
    }

    [Test]
    public void AReferenceWordFindsTheLine()
    {
        AddReferencedLines();

        var result = NewSearcher().SearchWork(DOC, "thessalonians", SearchOptions.Default, false);
        Assert.That(result.Lines.Single().Reference, Is.EqualTo("MS 1 Thessalonians 2.16"));
    }

    /// <summary>The reference analyzer keeps digits: ManxTokenizer would drop them</summary>
    [Test]
    public void AVerseNumberFindsTheLine()
    {
        AddReferencedLines();

        var result = NewSearcher().SearchWork(DOC, "2.16", SearchOptions.Default, false);
        Assert.That(result.Lines.Single().Reference, Is.EqualTo("MS 1 Thessalonians 2.16"));
    }

    [Test]
    public void AManxSearchStillWorksAndCarriesTheReference()
    {
        AddReferencedLines();

        var result = NewSearcher().SearchWork(DOC, "loayrt", SearchOptions.Default, false);
        var line = result.Lines.Single();
        Assert.Multiple(() =>
        {
            Assert.That(line.Reference, Is.EqualTo("MS 1 Thessalonians 2.16"));
            Assert.That(line.ManxHighlights, Is.Not.Null); // text matches still highlight
        });
    }

    /// <summary>Reference tokens never reach the Manx statistics stream</summary>
    [Test]
    public void ReferencesStayOutOfManxStatistics()
    {
        AddReferencedLines();

        var terms = luceneIndex.GetTermFrequencyList().Select(x => x.Item1).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(terms, Does.Contain("loayrt"));
            Assert.That(terms, Does.Not.Contain("thessalonians"));
            Assert.That(terms, Does.Not.Contain("ms"));
        });
    }

    /// <summary>Corpus-wide scan (the main search page) also reaches references</summary>
    [Test]
    public void AScanFindsReferencedDocuments()
    {
        AddReferencedLines();
        luceneIndex.Compact();

        var result = NewSearcher().Scan("thessalonians");
        Assert.Multiple(() =>
        {
            Assert.That(result.NumberOfDocuments, Is.EqualTo(1));
            Assert.That(result.NumberOfMatches, Is.EqualTo(1));
        });
    }

    private void AddPsalmTranslations()
    {
        // a verse-level version (the Bible), a chapter-level one (the Metrical
        // Psalms sing whole psalms), and a neighbouring verse that must stay out
        // of a verse-level alignment
        luceneIndex.Add(new TestDocument("bible", DOC_DATE), [
            new DocumentLine
            {
                Manx = "Ta'n Chiarn my vochilley",
                English = "The Lord is my shepherd",
                Reference = "Psalmyn:23:1",
                CanonicalReference = "psalms.23.1",
                CsvLineNumber = 2,
            },
            new DocumentLine
            {
                Manx = "Cha bee'm feme erbee",
                English = "I shall not want",
                Reference = "Psalmyn:23:2",
                CanonicalReference = "psalms.23.2",
                CsvLineNumber = 3,
            },
        ]);
        luceneIndex.Add(new TestDocument("metrical", DOC_DATE), [
            new DocumentLine
            {
                Manx = "",
                English = "",
                Reference = "PSALM 23",
                CanonicalReference = "psalms.23",
                CsvLineNumber = 2,
            },
            new DocumentLine
            {
                Manx = "Yn Chiarn hene my vochilley mie",
                English = "",
                CsvLineNumber = 3,
            },
        ]);
    }

    /// <summary>A verse aligns with its exact row everywhere, plus the chapter
    /// rows of versions that never number verses — never with a neighbour verse</summary>
    [Test]
    public void TheVerseAlignmentFindsEveryTranslation()
    {
        AddPsalmTranslations();

        var lines = luceneIndex.GetVerseAlignment(["psalms.23.1", "psalms.23"], chapterPrefix: null);
        Assert.That(
            lines.Select(x => (x.DocumentIdent, x.Line.CanonicalReference)),
            Is.EquivalentTo(new[] { ("bible", "psalms.23.1"), ("metrical", "psalms.23") }));
    }

    /// <summary>A chapter aligns with its heading rows and any verse under it</summary>
    [Test]
    public void AChapterAlignsWithHeadingsAndItsVerses()
    {
        AddPsalmTranslations();

        var lines = luceneIndex.GetVerseAlignment(["psalms.23"], chapterPrefix: "psalms.23.");
        Assert.That(
            lines.Select(x => x.Line.CanonicalReference),
            Is.EquivalentTo(new[] { "psalms.23", "psalms.23.1", "psalms.23.2" }));
    }

    /// <summary>A reference-only heading row (both text cells empty) is still a row
    /// of the document: the '*' search renders it as a section heading</summary>
    [Test]
    public void AReferenceOnlyHeadingRowSurvivesTheStarSearch()
    {
        AddPsalmTranslations();

        var result = NewSearcher().SearchWork("metrical", "*", SearchOptions.Default, false);
        Assert.That(result.Lines.Select(x => x.Reference), Does.Contain("PSALM 23"));
    }

    /// <summary>...and 'expand context' must not skip over it either</summary>
    [Test]
    public void AReferenceOnlyHeadingRowSurvivesContextExpansion()
    {
        AddPsalmTranslations();

        var (lines, _) = luceneIndex.GetLines("metrical", 1, 5, 5, fromEnd: false, getTranscript: false);
        Assert.That(lines.Select(x => x.Reference), Does.Contain("PSALM 23"));
    }
}
