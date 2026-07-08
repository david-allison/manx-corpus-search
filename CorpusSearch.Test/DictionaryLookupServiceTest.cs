using System;
using System.Collections.Generic;
using System.Linq;
using CorpusSearch.Service;
using NUnit.Framework;

namespace CorpusSearch.Test;

public class DictionaryLookupServiceTest
{
    private static DictionaryLookupService Service(params string[] words)
    {
        return new DictionaryLookupService([new FakeDictionary(words)]);
    }

    private static List<string> Lookup(DictionaryLookupService service, string selection, string context = null)
    {
        return service.Lookup("gv", selection, context).Select(x => x.PrimaryWord).ToList();
    }

    [Test]
    public void MatchesASingleWord()
    {
        var service = Service("goll", "mygeayrt");
        Assert.That(Lookup(service, "goll"), Is.EqualTo(new[] { "goll" }));
    }

    [Test]
    public void MatchIsCaseInsensitive()
    {
        var service = Service("goll");
        Assert.That(Lookup(service, "Goll"), Is.EqualTo(new[] { "goll" }));
    }

    [Test]
    public void PunctuationIsTrimmed()
    {
        var service = Service("goll");
        Assert.That(Lookup(service, "(goll,"), Is.EqualTo(new[] { "goll" }));
    }

    [Test]
    public void ApostrophesAreKept()
    {
        var service = Service("mygeayrt y mo'ee");
        Assert.That(Lookup(service, "mygeayrt y mo'ee"), Is.EqualTo(new[] { "mygeayrt y mo'ee" }));
    }

    [Test]
    public void UnknownWordReturnsNothing()
    {
        var service = Service("goll");
        Assert.That(Lookup(service, "braew"), Is.Empty);
    }

    /// <summary>The test case from #135</summary>
    [Test]
    public void CompoundWithoutAnEntryFallsBackToItsParts()
    {
        var service = Service("goll", "mygeayrt");
        Assert.That(Lookup(service, "goll-mygeayrt"), Is.EqualTo(new[] { "goll", "mygeayrt" }));
    }

    [Test]
    public void HyphenatedSelectionMatchesASpacedEntry()
    {
        var service = Service("goll mygeayrt", "goll", "mygeayrt");
        Assert.That(Lookup(service, "goll-mygeayrt"), Is.EqualTo(new[] { "goll mygeayrt" }));
    }

    [Test]
    public void SpacedSelectionMatchesAHyphenatedEntry()
    {
        var service = Service("lieh-cheead");
        Assert.That(Lookup(service, "lieh cheead"), Is.EqualTo(new[] { "lieh-cheead" }));
    }

    [Test]
    public void SelectionExpandsToAPhraseFromTheContext()
    {
        var service = Service("goll mygeayrt", "goll");
        var result = Lookup(service, "goll", context: "v'eh goll mygeayrt y valley");
        // the phrase is the more specific match: it is returned first
        Assert.That(result, Is.EqualTo(new[] { "goll mygeayrt", "goll" }));
    }

    [Test]
    public void PhraseFromContextMatchesAcrossPunctuation()
    {
        var service = Service("goll mygeayrt");
        var result = Lookup(service, "mygeayrt", context: "t'eh goll mygeayrt, dy jarroo");
        Assert.That(result, Is.EqualTo(new[] { "goll mygeayrt" }));
    }

    [Test]
    public void ContextDoesNotMatchPhrasesTheSelectionIsNotPartOf()
    {
        var service = Service("goll mygeayrt");
        Assert.That(Lookup(service, "valley", context: "v'eh goll mygeayrt y valley"), Is.Empty);
    }

    [Test]
    public void MultiWordSelectionIsMatchedDirectly()
    {
        var service = Service("dy hroggal", "dy", "hroggal");
        // the parts are not returned: the phrase entry is sufficient
        Assert.That(Lookup(service, "dy hroggal"), Is.EqualTo(new[] { "dy hroggal" }));
    }

    [Test]
    public void MultiWordSelectionFallsBackToItsWords()
    {
        var service = Service("dy", "hroggal");
        Assert.That(Lookup(service, "dy hroggal"), Is.EqualTo(new[] { "dy", "hroggal" }));
    }

    [Test]
    public void DuplicateMatchesAreRemoved()
    {
        // one entry known under both forms: both hyphen variants of the query resolve to it
        var service = new DictionaryLookupService([new FakeDictionary(new Dictionary<string, string>
        {
            ["goll-mygeayrt"] = "goll mygeayrt",
            ["goll mygeayrt"] = "goll mygeayrt",
        })]);
        Assert.That(Lookup(service, "goll-mygeayrt"), Is.EqualTo(new[] { "goll mygeayrt" }));
    }

    [Test]
    public void EmptySelectionReturnsNothing()
    {
        var service = Service("goll");
        Assert.That(Lookup(service, " ", context: "goll mygeayrt"), Is.Empty);
    }

    [Test]
    public void CitationMarkersInContextAreIgnored()
    {
        var service = Service("goll mygeayrt");
        Assert.That(Lookup(service, "goll", context: "v'eh goll [1] mygeayrt"), Is.EqualTo(new[] { "goll mygeayrt" }));
    }

    private class FakeDictionary : ISearchDictionary
    {
        private readonly Dictionary<string, string> wordToPrimaryWord;

        public FakeDictionary(params string[] words) : this(words.ToDictionary(x => x, x => x)) { }

        public FakeDictionary(Dictionary<string, string> wordToPrimaryWord)
        {
            this.wordToPrimaryWord = new Dictionary<string, string>(wordToPrimaryWord, StringComparer.InvariantCultureIgnoreCase);
        }

        public string Identifier => "Fake";
        public List<string> QueryLanguages => ["gv"];
        public bool LinkToDictionary => false;

        public IEnumerable<DictionarySummary> GetSummaries(string query, bool basic = false)
        {
            if (!wordToPrimaryWord.TryGetValue(query, out var primaryWord))
            {
                yield break;
            }
            yield return new DictionarySummary { PrimaryWord = primaryWord, Summary = $"definition of {primaryWord}" };
        }
    }
}
