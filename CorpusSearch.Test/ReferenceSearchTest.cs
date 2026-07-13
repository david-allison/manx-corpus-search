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
}
