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

    /// <summary>Kelly opens a definition with the printed class ("s. a weasel")</summary>
    [TestCase("s. a weasel, a squirrel, a pert woman or girl.", "Noun")]
    [TestCase("s. pl. EE. a dog.", "Noun")]
    [TestCase("v. to grow.", "Verb")]
    [TestCase("a. small.", "Adjective")]
    [TestCase("adj. small.", "Adjective")]
    [TestCase("adv. out; out of him.", "Adverb")]
    [TestCase("pron. he.", "Pronoun")]
    [TestCase("pro. he.", "Pronoun")]
    // the classes the regex used to drop, leaving the entry unlabelled
    [TestCase("prep. out, without, from or of, in or on.", "Preposition")]
    [TestCase("pre. out, without.", "Preposition")]
    [TestCase("conj. and.", "Conjunction")]
    [TestCase("interj. alas!", "Interjection")]
    [TestCase("int. alas!", "Interjection")]
    public void ThePrintedClassIsRecovered(string definition, string expected)
    {
        Assert.That(KellyManxToEnglishDictionaryService.PartsOfSpeechOf(definition),
            Is.EqualTo(new[] { expected }));
    }

    /// <summary>A participle is a form of a verb, not a class beside it: labelling
    /// one would filter it out of its own verb's root chain, so it stays unlabelled
    /// and is therefore always kept</summary>
    [Test]
    public void AParticipleIsNotGivenAClassOfItsOwn()
    {
        Assert.That(KellyManxToEnglishDictionaryService.PartsOfSpeechOf("part. growing."), Is.Null);
    }

    [TestCase("a weasel, with no printed class")]
    [TestCase("St. Patrick's day.")]
    [TestCase("")]
    public void AnUndeclaredClassIsNull(string definition)
    {
        Assert.That(KellyManxToEnglishDictionaryService.PartsOfSpeechOf(definition), Is.Null);
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
