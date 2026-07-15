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
}
