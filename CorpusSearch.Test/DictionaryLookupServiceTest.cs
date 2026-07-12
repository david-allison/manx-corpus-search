using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CorpusSearch.Dependencies.Lucene;
using CorpusSearch.Service;
using NUnit.Framework;

namespace CorpusSearch.Test;

public class DictionaryLookupServiceTest
{
    private static readonly LemmaTable NoLemmas = LemmaTable.Load(new StringReader("form\tlemmaId\tlemma\n"));

    private static DictionaryLookupService Service(params string[] words)
    {
        return new DictionaryLookupService([new FakeDictionary(words)], NoLemmas);
    }

    private static List<string> Lookup(DictionaryLookupService service, string selection, string? context = null)
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

    /// <summary>The test case from #337</summary>
    [Test]
    public void PossessiveWithoutAnEntryFallsBackToTheWord()
    {
        var service = Service("mooad");
        Assert.That(Lookup(service, "mooad's"), Is.EqualTo(new[] { "mooad" }));
        Assert.That(Lookup(service, "MOOAD'S"), Is.EqualTo(new[] { "mooad" }));
        Assert.That(Lookup(service, "mooad’s"), Is.EqualTo(new[] { "mooad" }));
    }

    [Test]
    public void ContractionWithoutAnEntryFallsBackToItsParts()
    {
        // the lone 't' (of 'ta') is not returned: a single letter is a contraction stub, not a word
        var service = Service("eh", "goll");
        Assert.That(Lookup(service, "t'eh goll"), Is.EqualTo(new[] { "eh", "goll" }));
    }

    [Test]
    public void WordWithItsOwnEntryDoesNotFallBackToItsParts()
    {
        // the emphatic -'s is a real suffix ('my chree's'): the exact entry is sufficient
        var service = Service("chree's", "chree");
        Assert.That(Lookup(service, "chree's"), Is.EqualTo(new[] { "chree's" }));
    }

    [Test]
    public void ApostropheStylesAreInterchangeable()
    {
        // Kelly writes typographic apostrophes ('B’ODDEY'), Cregeen typewriter ones ('mo'ee')
        var service = Service("b’oddey", "mo'ee");
        Assert.That(Lookup(service, "b'oddey"), Is.EqualTo(new[] { "b’oddey" }));
        Assert.That(Lookup(service, "mo’ee"), Is.EqualTo(new[] { "mo'ee" }));
    }

    [Test]
    public void CompoundWithPossessiveFallsBackToItsWords()
    {
        var service = Service("goll", "mygeayrt");
        Assert.That(Lookup(service, "goll-mygeayrt's"), Is.EqualTo(new[] { "goll", "mygeayrt" }));
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

    /// <summary>
    /// Kelly heads some entries with variant forms ('EEN, YN'), so looking up 'yn' also matches
    /// them. Entries headed by the query itself must come first.
    /// </summary>
    [Test]
    public void EntriesHeadedByTheQueryAreReturnedFirst()
    {
        var service = new DictionaryLookupService([new FakeDictionary(["EEN", "YN"], ["YN"])], NoLemmas);
        Assert.That(Lookup(service, "yn"), Is.EqualTo(new[] { "YN", "EEN" }));
    }

    [Test]
    public void PhrasesFromTheContextStillOutrankTheHeadedEntry()
    {
        // specificity wins over the headword: the phrase match is more useful than the exact entry
        var service = new DictionaryLookupService([new FakeDictionary(["goll mygeayrt"], ["goll", "gholl"])], NoLemmas);
        var result = Lookup(service, "goll", context: "v'eh goll mygeayrt y valley");
        Assert.That(result, Is.EqualTo(new[] { "goll mygeayrt", "goll" }));
    }

    [Test]
    public void DuplicateMatchesAreRemoved()
    {
        // one entry known under both forms: both hyphen variants of the query resolve to it
        var service = new DictionaryLookupService(dictionaryServices: [new FakeDictionary(new Dictionary<string, string>
        {
            ["goll-mygeayrt"] = "goll mygeayrt",
            ["goll mygeayrt"] = "goll mygeayrt",
        })], lemmaTable: NoLemmas);
        Assert.That(Lookup(service, "goll-mygeayrt"), Is.EqualTo(new[] { "goll mygeayrt" }));
    }

    [Test]
    public void EmptySelectionReturnsNothing()
    {
        var service = Service("goll");
        Assert.That(Lookup(service, " ", context: "goll mygeayrt"), Is.Empty);
    }

    private static LemmaTable Lemmas(params (string Form, string Lemma)[] rows)
    {
        var tsv = "form\tlemmaId\tlemma\n" + string.Join("\n", rows.Select(r => $"{r.Form}\t{r.Lemma}.x\t{r.Lemma}"));
        return LemmaTable.Load(new StringReader(tsv));
    }

    [Test]
    public void AnInflectedSelectionOffersItsRoot()
    {
        // 'daase' has no entry of its own: the reader gets the root's entry
        var service = new DictionaryLookupService([new FakeDictionary(["aase"])],
            Lemmas(("daase", "aase")));

        Assert.That(Lookup(service, "daase"), Is.EqualTo(new[] { "aase" }));
    }

    [Test]
    public void TheExactEntryStaysAheadOfTheRoot()
    {
        var service = new DictionaryLookupService([new FakeDictionary(["daase", "aase"])],
            Lemmas(("daase", "aase")));

        Assert.That(Lookup(service, "daase"), Is.EqualTo(new[] { "daase", "aase" }));
    }

    /// <summary>The popup labels each entry with the dictionary it came from (#51)</summary>
    [Test]
    public void SummariesNameTheirDictionary()
    {
        var service = Service("goll");
        var result = service.Lookup("gv", "goll").Single();
        Assert.That(result.DictionaryName, Is.EqualTo("Fake"));
    }

    [Test]
    public void CitationMarkersInContextAreIgnored()
    {
        var service = Service("goll mygeayrt");
        Assert.That(Lookup(service, "goll", context: "v'eh goll [1] mygeayrt"), Is.EqualTo(new[] { "goll mygeayrt" }));
    }

    private class FakeDictionary : ISearchDictionary
    {
        private readonly List<(List<string> Words, string PrimaryWord)> entries;

        public FakeDictionary(params string[] words) : this(words.Select(x => new[] { x }).ToArray()) { }

        /// <summary>Each entry is its word list, headed by the primary word: ["EEN", "YN"] is the entry 'EEN, YN'</summary>
        public FakeDictionary(params string[][] entryWords)
        {
            entries = entryWords.Select(words => (words.ToList(), words[0])).ToList();
        }

        public FakeDictionary(Dictionary<string, string> wordToPrimaryWord)
        {
            entries = wordToPrimaryWord.Select(x => (new List<string> { x.Key }, x.Value)).ToList();
        }

        public string Identifier => "Fake";
        public List<string> QueryLanguages => ["gv"];
        public bool LinkToDictionary => false;

        public IEnumerable<DictionarySummary> GetSummaries(string query, bool basic = false)
        {
            foreach (var (_, primaryWord) in entries.Where(e => e.Words.Contains(query, StringComparer.InvariantCultureIgnoreCase)))
            {
                yield return new DictionarySummary { PrimaryWord = primaryWord, Summary = $"definition of {primaryWord}" };
            }
        }
    }
}
