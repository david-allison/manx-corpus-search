using System.Collections.Generic;
using System.Linq;
using CorpusSearch.Service.Dictionaries;
using NUnit.Framework;

namespace CorpusSearch.Test;

public class PhilKellyDictionaryServiceTest
{
    private static PhilKellyDictionaryService Service()
    {
        return new PhilKellyDictionaryService(
            new Dictionary<string, IList<string>>(
                new Dictionary<string, IList<string>>
                {
                    ["billey"] = ["bank bill", "bill", "big bush", "tree"],
                    ["zz - dec 2015"] = ["zz - cng"],
                    ["empty"] = [],
                },
                System.StringComparer.OrdinalIgnoreCase));
    }

    [Test]
    public void GlossesJoinIntoOneSummary()
    {
        var summaries = Service().GetSummaries("billey").ToList();

        Assert.That(summaries, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(summaries[0].PrimaryWord, Is.EqualTo("billey"));
            Assert.That(summaries[0].Summary, Is.EqualTo("bank bill; bill; big bush; tree"));
        });
    }

    [Test]
    public void LookupIsCaseInsensitive()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Service().ContainsWord("Billey"), Is.True);
            Assert.That(Service().GetSummaries("BILLEY").Single().PrimaryWord, Is.EqualTo("billey"));
        });
    }

    [Test]
    public void AGlosslessEntryYieldsNothing()
    {
        Assert.That(Service().GetSummaries("empty"), Is.Empty);
    }

    /// <summary>The source spells ç inconsistently: a tapped çhellveeish must
    /// find the plain-c key</summary>
    [Test]
    public void ACedillaQueryFindsThePlainSpelling()
    {
        var service = new PhilKellyDictionaryService(
            new Dictionary<string, IList<string>>(
                new Dictionary<string, IList<string>> { ["chellveeish"] = ["television"] },
                System.StringComparer.OrdinalIgnoreCase));

        Assert.Multiple(() =>
        {
            Assert.That(service.ContainsWord("çhellveeish"), Is.True);
            Assert.That(service.GetSummaries("çhellveeish").Single().Summary,
                Is.EqualTo("television"));
        });
    }
}
