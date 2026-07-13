using System.Linq;
using CorpusSearch.Model.Dictionary;
using CorpusSearch.Service.Dictionaries;
using NUnit.Framework;

namespace CorpusSearch.Test;

public class KellyManxToEnglishDictionaryServiceTest
{
    /// <summary>'SPAINEY, YN' (Spain) records the article, not a variant form: a lookup of the
    /// article 'yn' should not return Spain</summary>
    [Test]
    public void TheArticleIsRemovedFromSpain()
    {
        var spain = new KellyManxToEnglishEntry { Words = ["SPAINEY", "YN"], Definition = "S. Spain. (Ir. An Spain.)" };

        KellyManxToEnglishDictionaryService.RemoveArticleFromSpain(spain);

        Assert.That(spain.Words, Is.EqualTo(new[] { "SPAINEY" }));
    }

    [Test]
    public void OtherEntriesKeepTheirVariantForms()
    {
        var een = new KellyManxToEnglishEntry { Words = ["EEN", "YN"], Definition = "when added to a word forms a diminution" };

        KellyManxToEnglishDictionaryService.RemoveArticleFromSpain(een);

        Assert.That(een.Words, Is.EqualTo(new[] { "EEN", "YN" }));
    }

    /// <summary>The shipped artifact carries the split-out plural data end to end:
    /// biljin (a plural, never a headword) finds BILLEY, the summary exposes the
    /// structured plural, and the definition no longer embeds the marker</summary>
    [Test]
    public void APluralFormFindsItsEntry()
    {
        var service = KellyManxToEnglishDictionaryService.Init(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<KellyManxToEnglishDictionaryService>.Instance);
        // kellym2e.json is downloaded on deployment (tools/init.sh): without it
        // the dictionary is deliberately empty, and there is nothing to assert
        Assume.That(service.AllWords, Is.Not.Empty, "kellym2e.json not present");

        Assert.That(service.ContainsWord("biljin"), Is.True);
        var summaries = service.GetSummaries("biljin", basic: false).ToList();
        Assert.That(summaries, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(summaries[0].PrimaryWord, Is.EqualTo("BILLEY"));
            Assert.That(summaries[0].Plurals, Is.EqualTo(new[] { "BILJIN" }));
            Assert.That(summaries[0].Summary, Does.Not.Contain("pl."));
        });
    }

    /// <summary>A plural spelt with ç answers for its c-respelling without the
    /// display list gaining synthetic duplicates (ÇHENTYN under ÇHENNEY)</summary>
    [Test]
    public void ACedillaPluralMatchesItsPlainSpelling()
    {
        var service = KellyManxToEnglishDictionaryService.Init(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<KellyManxToEnglishDictionaryService>.Instance);
        // kellym2e.json is downloaded on deployment (tools/init.sh): without it
        // the dictionary is deliberately empty, and there is nothing to assert
        Assume.That(service.AllWords, Is.Not.Empty, "kellym2e.json not present");

        var summaries = service.GetSummaries("chentyn", basic: false).ToList();
        Assert.That(summaries, Is.Not.Empty);
        var fire = summaries.Single(x => x.PrimaryWord == "ÇHENNEY");
        Assert.That(fire.Plurals, Is.EqualTo(new[] { "ÇHENTYN" }));
    }
}
