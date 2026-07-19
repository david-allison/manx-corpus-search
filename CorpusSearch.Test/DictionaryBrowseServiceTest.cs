using System.Collections.Generic;
using System.Linq;
using CorpusSearch.Dependencies.Lucene;
using CorpusSearch.Service;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>
/// One page of a dictionary's index: the letters it has, and one letter's
/// headwords in the chapters they file under.
/// </summary>
[TestFixture]
public class DictionaryBrowseServiceTest
{
    /// <summary>Headwords in the order a book prints them, which is not sorted:
    /// the service must never re-order them</summary>
    private class FakeDictionary(string slug, params string[] headwords) : ISearchDictionary
    {
        public string Identifier => $"The {slug} Dictionary";
        public string Slug => slug;
        public List<string> QueryLanguages => ["gv"];
        public bool LinkToDictionary => false;
        public IReadOnlyList<string> Headwords => headwords;
        public IEnumerable<string> AllWords => headwords;
        public bool ContainsWord(string word) => headwords.Contains(word);

        public IEnumerable<DictionarySummary> GetSummaries(string query, bool basic = false) =>
            headwords.Contains(query)
                ? [new DictionarySummary { Summary = $"what {query} means", PrimaryWord = query }]
                : [];
    }

    /// <summary>No corpus behind it, so <see cref="CorpusVocabulary.IsAttested"/>
    /// answers true throughout: these tests are about the index, not about which
    /// of its words a text happens to use</summary>
    private static DictionaryBrowseService Service(params ISearchDictionary[] dictionaries) =>
        new(dictionaries, new CorpusVocabulary(LemmaTable.Instance));

    [Test]
    public void AnUnknownDictionaryHasNoIndex()
    {
        Assert.That(Service(new FakeDictionary("cregeen", "aa")).Page("nope", null), Is.Null);
    }

    /// <summary>The spoken dictionary is a neighbours scope with no book behind
    /// it: stepping walks the heard words in collation order, and a headword
    /// nobody recorded is not a step. Before the recordings are read there is
    /// nothing to step through, and the walk is not offered.</summary>
    [Test]
    public void TheSpokenScopeStepsThroughTheHeardWords()
    {
        var dict = new FakeDictionary("cregeen", "aase", "baase", "caashey", "dooinney");
        var vocabulary = new CorpusVocabulary(LemmaTable.Instance);
        var stats = new DictionaryStatsService(
            LemmaTable.Instance, vocabulary, [dict], new WorkService());
        var service = new DictionaryBrowseService([dict], vocabulary, stats);

        var unread = service.Neighbours("spoken", "baase");
        stats.InitAudio(1, ["baase dooinney", "aase"]);
        var heard = service.Neighbours("spoken", "baase");
        Assert.Multiple(() =>
        {
            Assert.That(unread.Previous, Is.Null);
            Assert.That(unread.Next, Is.Null);
            Assert.That(heard.Previous, Is.EqualTo("aase"));
            // 'caashey' is printed but never recorded: the walk steps past it
            Assert.That(heard.Next, Is.EqualTo("dooinney"));
        });
    }

    /// <summary>The bar is in capitals, as a printed index has it, though no book
    /// here prints its headwords that way</summary>
    [Test]
    public void TheLetterBarComesFromTheHeadwords()
    {
        var page = Service(new FakeDictionary("d", "aa", "baa", "çhengey", "yn"))
            .Page("d", null)!;

        // ç files under c, as the books have it
        Assert.That(page.Letters, Is.EqualTo(new[] { "A", "B", "C", "Y" }));
    }

    [Test]
    public void ADictionaryOpensAtItsFirstLetter()
    {
        var page = Service(new FakeDictionary("d", "baa", "aa")).Page("d", null)!;

        Assert.Multiple(() =>
        {
            Assert.That(page.Letter, Is.EqualTo("A"));
            Assert.That(page.Chapters.Single().Words.Select(x => x.Word), Is.EqualTo(new[] { "aa" }));
        });
    }

    /// <summary>A letter is shown whole: every chapter of it, not one prefix at a
    /// time</summary>
    [Test]
    public void ALetterShowsAllOfItsChapters()
    {
        var page = Service(new FakeDictionary("d", "aalin", "aalid", "abban", "baa"))
            .Page("d", "a")!;

        Assert.Multiple(() =>
        {
            Assert.That(page.Letter, Is.EqualTo("A"));
            Assert.That(page.Chapters.Select(x => x.Key), Is.EqualTo(new[] { "AAL", "ABB" }));
            Assert.That(page.Chapters[0].Words.Select(x => x.Word), Is.EqualTo(new[] { "aalin", "aalid" }));
        });
    }

    /// <summary>The headwords keep the book's order: 'aalin' was printed before
    /// 'aalid' here, and a sort would put them the other way round</summary>
    [Test]
    public void TheHeadwordsKeepTheBooksOrder()
    {
        var page = Service(new FakeDictionary("d", "aalin", "aalid")).Page("d", "aa")!;

        Assert.That(page.Chapters.Single().Words.Select(x => x.Word), Is.EqualTo(new[] { "aalin", "aalid" }));
    }

    /// <summary>A hyphen is not a letter: 'agh-markiagh' belongs with the 'agh'
    /// words, as Cregeen prints it</summary>
    [Test]
    public void AHyphenatedHeadwordFilesWhereTheBookPutsIt()
    {
        var page = Service(new FakeDictionary("d", "aghin", "agh-markiagh")).Page("d", "ag")!;

        Assert.That(page.Chapters.Single().Words.Select(x => x.Word),
            Is.EqualTo(new[] { "aghin", "agh-markiagh" }));
    }

    /// <summary>A prefix was once a page of its own, so links to one are out
    /// there: it opens the letter it names rather than nothing</summary>
    [Test]
    public void AnOldPrefixLinkOpensItsLetter()
    {
        var page = Service(new FakeDictionary("d", "aalin", "azzy")).Page("d", "aal")!;

        Assert.Multiple(() =>
        {
            Assert.That(page.Letter, Is.EqualTo("A"));
            Assert.That(page.Chapters.Select(x => x.Key), Is.EqualTo(new[] { "AAL", "AZZ" }));
        });
    }

    /// <summary>A letter nothing starts with still opens somewhere rather than
    /// empty</summary>
    [Test]
    public void ALetterTheDictionaryHasNoneOfOpensAtTheFirst()
    {
        var page = Service(new FakeDictionary("d", "aalin", "azzy")).Page("d", "q")!;

        Assert.Multiple(() =>
        {
            Assert.That(page.Letter, Is.EqualTo("A"));
            Assert.That(page.Chapters, Is.Not.Empty);
        });
    }

    // ---- stepping through, headword by headword ----

    [Test]
    public void AHeadwordStepsToTheOnesBesideIt()
    {
        var n = Service(new FakeDictionary("d", "aa", "ab", "ac")).Neighbours("d", "ab");

        Assert.Multiple(() =>
        {
            Assert.That(n.Previous, Is.EqualTo("aa"));
            Assert.That(n.Next, Is.EqualTo("ac"));
        });
    }

    [Test]
    public void TheEndsOfTheBookStepOnlyInwards()
    {
        var service = Service(new FakeDictionary("d", "aa", "ab"));

        Assert.That(service.Neighbours("d", "aa").Previous, Is.Null);
        Assert.That(service.Neighbours("d", "ab").Next, Is.Null);
    }

    /// <summary>The steps follow the book, not the alphabet: Cregeen prints
    /// 'aghin' before 'agh-markiagh', and a sort would not</summary>
    [Test]
    public void SteppingFollowsTheBooksOrder()
    {
        var n = Service(new FakeDictionary("d", "aghin", "agh-markiagh", "aker"))
            .Neighbours("d", "agh-markiagh");

        Assert.Multiple(() =>
        {
            Assert.That(n.Previous, Is.EqualTo("aghin"));
            Assert.That(n.Next, Is.EqualTo("aker"));
        });
    }

    /// <summary>Cregeen prints 'baare' twice over, and the URL is the spelling:
    /// the two are one page, so a step lands on the next word rather than back
    /// where it started</summary>
    [Test]
    public void AHeadwordThePrintedTwiceStepsPastItsTwin()
    {
        var service = Service(
            new FakeDictionary("d", "baar-aadjin", "baare", "baare", "baarelagh"));

        var n = service.Neighbours("d", "baare");

        Assert.Multiple(() =>
        {
            Assert.That(n.Next, Is.EqualTo("baarelagh"), "not 'baare' again");
            Assert.That(n.Previous, Is.EqualTo("baar-aadjin"));
        });
    }

    /// <summary>An inflection is no headword, but it still has a place: it steps
    /// from where it would be filed</summary>
    [Test]
    public void AWordThatIsNoHeadwordStepsFromWhereItWouldBeFiled()
    {
        var n = Service(new FakeDictionary("d", "aa", "am", "az")).Neighbours("d", "ap");

        Assert.Multiple(() =>
        {
            Assert.That(n.Previous, Is.EqualTo("am"));
            Assert.That(n.Next, Is.EqualTo("az"));
        });
    }

    /// <summary>No book's order can be kept across books, so the union takes the
    /// reader's; a word in two dictionaries is one step, not two</summary>
    [Test]
    public void AcrossEveryDictionaryTheStepsAreTheUnion()
    {
        var service = Service(
            new FakeDictionary("cregeen", "aa", "billey", "coo"),
            new FakeDictionary("kelly", "BILLEY", "baa"));

        var n = service.Neighbours(null, "baa");

        Assert.Multiple(() =>
        {
            Assert.That(n.Previous, Is.EqualTo("aa"));
            Assert.That(n.Next, Is.EqualTo("billey"), "the two BILLEYs are one word");
        });
    }

    [Test]
    public void AnUnknownDictionaryStepsNowhere()
    {
        var n = Service(new FakeDictionary("d", "aa", "ab")).Neighbours("nope", "aa");

        Assert.That(n.Previous, Is.Null);
        Assert.That(n.Next, Is.Null);
    }

    /// <summary>A span sends the windows either side along whole — previous
    /// pages nearest-first, then next pages nearest-first, each with its own
    /// arrows — so the walk's client steps through them without asking again</summary>
    [Test]
    public void ASpanCarriesTheWindowsEitherSide()
    {
        var service = Service(new FakeDictionary("d", "aa", "baa", "caa", "daa", "eairk"));

        var n = service.Neighbours("d", "caa", span: 2);

        Assert.Multiple(() =>
        {
            Assert.That(n.Nearby.Select(x => x.Word),
                Is.EqualTo(new[] { "baa", "aa", "daa", "eairk" }));
            var daa = n.Nearby.Single(x => x.Word == "daa");
            Assert.That(daa.Previous, Is.EqualTo("caa"));
            Assert.That(daa.Next, Is.EqualTo("eairk"));
            Assert.That(n.Nearby.Single(x => x.Word == "aa").Previous, Is.Null);
            // nothing rides along unasked: the row's own answer stays light
            Assert.That(service.Neighbours("d", "caa").Nearby, Is.Empty);
        });
    }

    [Test]
    public void ASpanStopsAtTheBookEnds()
    {
        var n = Service(new FakeDictionary("d", "aa", "baa"))
            .Neighbours("d", "aa", span: 5);

        Assert.That(n.Nearby.Select(x => x.Word), Is.EqualTo(new[] { "baa" }));
    }

    /// <summary>Same-spelled headwords are one page, so they are one step of
    /// the span too: it must walk the way the arrows walk</summary>
    [Test]
    public void ASpanCollapsesSameSpelledHeadwordsAsTheArrowsDo()
    {
        var service = Service(
            new FakeDictionary("d", "baar-aadjin", "baare", "baare", "baarelagh"));

        var n = service.Neighbours("d", "baar-aadjin", span: 3);

        Assert.That(n.Nearby.Select(x => x.Word),
            Is.EqualTo(new[] { "baare", "baarelagh" }), "the twin 'baare' is one step");
    }

    /// <summary>The dictionary JSON is downloaded on deployment: without it the
    /// dictionary is empty rather than broken, and so is its index</summary>
    [Test]
    public void AnEmptyDictionaryHasAnEmptyIndex()
    {
        var page = Service(new FakeDictionary("d")).Page("d", null)!;

        Assert.Multiple(() =>
        {
            Assert.That(page.Letters, Is.Empty);
            Assert.That(page.Chapters, Is.Empty);
            Assert.That(page.Letter, Is.Null);
        });
    }

    /// <summary>The sampler wants breadth, not proportion: something common,
    /// the middling, the rare, and one word no text says — dealt on purpose,
    /// so a reader meets the book's range rather than the letter A</summary>
    [Test]
    public void TheSamplerDealsAcrossTheRangeOfUse()
    {
        var vocabulary = new CorpusVocabulary(LemmaTable.Instance);
        vocabulary.Init([
            ("cadjin", 500L), ("cadjinagh", 200L),
            ("mean", 40L), ("meanagh", 20L), ("goan", 3L),
        ]);
        var service = new DictionaryBrowseService(
            [new FakeDictionary("d", "cadjin", "cadjinagh", "mean", "meanagh", "goan", "ynrican")],
            vocabulary);

        var samples = service.Samples("d", 6, new System.Random(1))!;

        Assert.Multiple(() =>
        {
            Assert.That(samples.Select(x => x.Word), Is.Unique);
            // exactly one dictionary-only word rides along
            Assert.That(samples.Count(x => !x.Attested), Is.EqualTo(1));
            Assert.That(samples.Single(x => !x.Attested).Word, Is.EqualTo("ynrican"));
            // the rest span the range, counted for the shop window
            Assert.That(samples.Count(x => x.Attestations >= 100), Is.EqualTo(2));
            Assert.That(samples.Single(x => x.Attestations == 3).Word, Is.EqualTo("goan"));
            Assert.That(samples[0].Summary, Does.Contain("means"));
        });
    }

    /// <summary>A sample must open cleanly as a word page: no affixes, and no
    /// trailing-dot keys (Phil Kelly's 'a.r.e.', which the lookup misses)</summary>
    [Test]
    public void TheSamplerSkipsHeadwordsTheWordPageCannotOpen()
    {
        var vocabulary = new CorpusVocabulary(LemmaTable.Instance);
        vocabulary.Init([("glen", 5L)]);
        var service = new DictionaryBrowseService(
            [new FakeDictionary("d", "-al", "a.r.e.", "glen")], vocabulary);

        var samples = service.Samples("d", 6, new System.Random(1))!;

        Assert.That(samples.Select(x => x.Word), Is.EqualTo(new[] { "glen" }));
    }

    [Test]
    public void AnUnknownDictionaryDealsNothing()
    {
        Assert.That(Service(new FakeDictionary("d", "aa")).Samples("nope", 6), Is.Null);
    }
}
