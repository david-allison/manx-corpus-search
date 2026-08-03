using System.Linq;
using CorpusSearch.Model.Dictionary;
using CorpusSearch.Service.Dictionaries;
using NUnit.Framework;

namespace CorpusSearch.Test;

public class CregeenDictionaryServiceTest
{
    /// <summary>Cregeen prints the grammar label as the entry's leading italic
    /// run: it becomes structured data (word class and gender for the hover)</summary>
    [TestCase("<i>s. m. </i>a father", "s. m.")]
    [TestCase("<i>s. f.</i> a mother", "s. f.")]
    [TestCase(" <i> v. </i>to run", "v.")]
    public void TheLeadingItalicRunIsTheGrammarLabel(string html, string expected)
    {
        Assert.That(CregeenDictionaryService.GrammarLabelOf(html), Is.EqualTo(expected));
    }

    /// <summary>The transcription corrects the print inside the label - angle
    /// brackets around the print's reading, square around the correction - and
    /// the marks reach the reader as brackets, not as "&amp;lt;" entities</summary>
    [TestCase("<i>&lt;v&gt;[a]. </i>not long, not far F", "<v>[a].")]
    [TestCase("<i>s. &lt;f.&gt; </i>a hand barrow", "s. <f.>")]
    [TestCase("<i>&lt;a. d.&gt;[a. pl.] </i>brave men B", "<a. d.>[a. pl.]")]
    public void TheEditorialMarksInALabelStayAndDecode(string html, string expected)
    {
        Assert.That(CregeenDictionaryService.GrammarLabelOf(html), Is.EqualTo(expected));
    }

    [TestCase("plain text, no label")]
    [TestCase("starts plainly <i>with italics later</i>")]
    [TestCase("<i>this italic run is far too long to be a grammar label at all</i> text")]
    [TestCase(null)]
    public void OtherShapesCarryNoLabel(string? html)
    {
        Assert.That(CregeenDictionaryService.GrammarLabelOf(html), Is.Null);
    }

    /// <summary>The gender check writes its findings into cregeen-nvh as a
    /// "gender:" note; the reader sees the evidence without the tool stamp</summary>
    [TestCase(
        "gender: the corpus points at feminine against the printed s. m. (article: 46 lenited / 4 unlenited) [gender_check 2026-07-19]",
        "the corpus points at feminine against the printed s. m. (article: 46 lenited / 4 unlenited)")]
    [TestCase("check this; gender: the corpus points at masculine [gender_check 2026-07-19]",
        "the corpus points at masculine")]
    public void TheGenderNoteLosesItsToolStamp(string notes, string expected)
    {
        Assert.That(CregeenDictionaryService.GenderNoteOf(notes), Is.EqualTo(expected));
    }

    [TestCase("See also craa; both are used")]
    [TestCase("")]
    [TestCase(null)]
    public void OtherNotesAreNotGenderWarnings(string? notes)
    {
        Assert.That(CregeenDictionaryService.GenderNoteOf(notes), Is.Null);
    }

    /// <summary>A letter page is filed by each entry's own letter, not sliced
    /// between the letters' first printed entries: when the data respelled F's
    /// opening 'fa.' as 'fa', the slice emptied F and poured the rest of the
    /// book into E</summary>
    [Test]
    public void ALetterPageIsFiledByItsEntriesOwnLetters()
    {
        var entries = new[] { "e", "eairk", "fa", "fablagh", "gaal" }
            .Select(word => new CregeenEntry { Words = [word], EntryHtml = "", HeadingHtml = "" })
            .ToList();

        string[] PageOf(char letter) => CregeenDictionaryService.EntriesUnder(letter, entries)
            .Select(x => x.Words[0]).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(PageOf('E'), Is.EqualTo(new[] { "e", "eairk" }));
            Assert.That(PageOf('F'), Is.EqualTo(new[] { "fa", "fablagh" }));
            Assert.That(PageOf('g'), Is.EqualTo(new[] { "gaal" }), "lowercase URLs file alike");
        });
    }

    /// <summary>Every letter of the bar answers with entries. The old slicing
    /// failed silently: a letter whose landmark entry the data no longer
    /// printed was simply empty, and nothing said so</summary>
    [Test]
    public void EveryLetterOfTheBarHasAPage()
    {
        var entries = CregeenDictionaryService.GetEntries();
        // cregeen.json is downloaded on deployment (tools/init.sh): without it
        // the dictionary is deliberately empty, and there is nothing to assert
        Assume.That(entries, Is.Not.Empty, "cregeen.json not present");

        Assert.Multiple(() =>
        {
            foreach (var letter in CregeenDictionaryService.Letters)
            {
                Assert.That(CregeenDictionaryService.EntriesUnder(letter, entries),
                    Is.Not.Empty, $"the {letter} page has no entries");
            }
        });
    }

    /// <summary>The 702 entries without a plain Definition fall back to the
    /// full entry text, which opens with the printed label: it must not show
    /// twice (once as the chip, once inline) - moir keeps its label out of
    /// the summary either way, s'aashagh's fallback text loses the "a. id."</summary>
    [Test]
    public void TheLabelLeavesTheFallbackSummaryText()
    {
        var service = CregeenDictionaryService.Init(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CregeenDictionaryService>.Instance);
        // cregeen.json is downloaded on deployment (tools/init.sh): without it
        // the dictionary is deliberately empty, and there is nothing to assert
        Assume.That(service.AllWords, Is.Not.Empty, "cregeen.json not present");

        var aashagh = service.GetSummaries("s'aashagh", basic: true).First();
        Assert.Multiple(() =>
        {
            Assert.That(aashagh.GrammarLabel, Is.Not.Null);
            Assert.That(aashagh.Summary.TrimStart(), Does.Not.StartWith(aashagh.GrammarLabel!));
        });

        var moir = service.GetSummaries("moir", basic: true).First();
        Assert.Multiple(() =>
        {
            Assert.That(moir.GrammarLabel, Is.EqualTo("s. f."));
            Assert.That(moir.Summary, Does.StartWith("mother"));
        });
    }

    /// <summary>The book heads both entries 'eab or eabb*' — the starred
    /// spelling is the stem the suffixes join to — but the classic export
    /// once merged the bare eabb entry into the adjacent verb group alone,
    /// and a search for eabb never found the noun.</summary>
    [Test]
    public void TheStarredSpellingAnswersForBothEabs()
    {
        var service = CregeenDictionaryService.Init(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CregeenDictionaryService>.Instance);
        Assume.That(service.AllWords, Is.Not.Empty, "cregeen.json not present");

        var labels = service.GetSummaries("eabb", basic: true)
            .Select(x => x.GrammarLabel)
            .ToList();
        Assert.Multiple(() =>
        {
            Assert.That(labels, Does.Contain("s. m."),
                "the noun prints 'eab or eabb*' and must answer for the starred spelling");
            Assert.That(labels, Does.Contain("v."));
        });
    }
}
