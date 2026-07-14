using System.Linq;
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

    [TestCase("plain text, no label")]
    [TestCase("starts plainly <i>with italics later</i>")]
    [TestCase("<i>this italic run is far too long to be a grammar label at all</i> text")]
    [TestCase(null)]
    public void OtherShapesCarryNoLabel(string? html)
    {
        Assert.That(CregeenDictionaryService.GrammarLabelOf(html), Is.Null);
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
}
