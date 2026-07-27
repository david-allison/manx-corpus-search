using System.IO;
using System.Linq;
using CorpusSearch.Service;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>
/// What a printed word list says about a word — a citation, not a reading.
/// </summary>
[TestFixture]
public class WordListCitationServiceTest
{
    private const string Sources =
        "listId\tname\tcredit\tdate\tdocIdent\turl\tnotes\n" +
        "morrison-plants\tManx Plant Names\tSophia Morrison\t1908\tManx-Plant-Names\thttps://example.invalid\tManx Wild Flowers, 1908\n";

    private const string Rows =
        "form\theadword\tlistId\tgloss\tbinomial\tnote\n" +
        "keirn\tKeirn\tmorrison-plants\tAsh (mountain)\tPyrus aucuparia\t\n" +
        "aghaue\tAghaue\tmorrison-plants\tHemlock\tConium maculatum\t\n" +
        "aghaue\tAghaue\tmorrison-plants\tWater hemlock\tCircuta virosa\t\n" +
        "lus ny geayee\tLus-ny-Geayee\tmorrison-plants\tAnemome (wood)\tAnemone nemorosa\tthe page prints 'Anemome (wood)'; read 'Anemone (wood)'\n" +
        "luss\tYn luss\tmorrison-plants\tVervain\tVerbena officinalis\t\n" +
        "smeyr\tSmeyr\tmorrison-plants\tBlackberry (bramble)\t\t\n";

    private static WordListCitationService Loaded(string rows = Rows, string sources = Sources)
    {
        return new WordListCitationService(new StringReader(rows), new StringReader(sources), NullLogger.Instance);
    }

    [Test]
    public void AWordTheListPrintsIsCited()
    {
        var citation = Loaded().For("Keirn").Single();
        Assert.Multiple(() =>
        {
            Assert.That(citation.Gloss, Is.EqualTo("Ash (mountain)"));
            Assert.That(citation.Binomial, Is.EqualTo("Pyrus aucuparia"));
            Assert.That(citation.Source.Credit, Is.EqualTo("Sophia Morrison"));
        });
    }

    [Test]
    public void AWordNoListPrintsHasNoCitation()
    {
        Assert.That(Loaded().For("jaagh"), Is.Empty);
    }

    /// <summary>The lookup folds its argument the way the table is keyed, so the
    /// spelling a reader types finds the row the page printed</summary>
    [Test]
    public void TheLookupFoldsSpellingTheWayTheTableIsKeyed()
    {
        Assert.That(Loaded().For("Lus-ny-Geayee").Single().Headword, Is.EqualTo("Lus-ny-Geayee"));
    }

    /// <summary>One name, two plants: the page names both, so the word's page
    /// shows both rather than picking one</summary>
    [Test]
    public void OneNameForTwoPlantsKeepsBoth()
    {
        Assert.That(Loaded().For("aghaue").Select(x => x.Gloss),
            Is.EquivalentTo(new[] { "Hemlock", "Water hemlock" }));
    }

    /// <summary>The generator keys an article-led head on the bare word too; the
    /// citation still shows the phrase as the page sets it</summary>
    [Test]
    public void AnArticleLedHeadIsCitedAsPrinted()
    {
        Assert.That(Loaded().For("luss").Single().Headword, Is.EqualTo("Yn luss"));
    }

    /// <summary>A page's English typo stands in the gloss; the note reads it back,
    /// so the citation is faithful without leaving the reader guessing</summary>
    [Test]
    public void APrintedTypoStandsAndTheNoteReadsItBack()
    {
        var citation = Loaded().For("lus ny geayee").Single();
        Assert.Multiple(() =>
        {
            Assert.That(citation.Gloss, Is.EqualTo("Anemome (wood)"));
            Assert.That(citation.Note, Does.Contain("read 'Anemone (wood)'"));
        });
    }

    /// <summary>Not every line names a species: the list prints some heads with no
    /// Latin name at all, and an empty column is nothing rather than ""</summary>
    [Test]
    public void ALineWithNoLatinNameCarriesNone()
    {
        Assert.That(Loaded().For("smeyr").Single().Binomial, Is.Null);
    }

    /// <summary>A row naming a list with no source row is dropped, not shown
    /// half-cited: a citation with no book to point at is worse than silence</summary>
    [Test]
    public void ARowWithNoSourceIsDropped()
    {
        var orphan = "form\theadword\tlistId\tgloss\tbinomial\tnote\n" +
                     "cushag\tCushag\tno-such-list\tRagwort\tSenecio jacobaea\t\n";
        Assert.That(Loaded(rows: orphan).For("cushag"), Is.Empty);
    }

    [Test]
    public void AnEmptyWordIsNotLookedUp()
    {
        Assert.That(Loaded().For(""), Is.Empty);
    }
}
