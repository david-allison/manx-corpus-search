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
}
