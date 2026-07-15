using System.Linq;
using CorpusSearch.Service;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>
/// Browsing a dictionary by its printed index: the letters it has, and the
/// chapters a letter's headwords file under.
/// </summary>
[TestFixture]
public class DictionaryBrowseTest
{
    /// <summary>Cregeen files 'agh-markiagh' among the 'agh...' words and 'atçhim'
    /// before 'att': the hyphen is not a letter, and ç files under c</summary>
    [TestCase("agh-markiagh", "aghmarkiagh")]
    [TestCase("agglish raueagh", "agglishraueagh")]
    [TestCase("atçhim", "atchim")]
    [TestCase("ymmyrçhagh", "ymmyrchagh")]
    [TestCase("-al", "al")]
    [TestCase("’wyne", "wyne")]
    // Kelly prints its headwords in capitals; Cregeen does not
    [TestCase("BILLEY", "billey")]
    public void TheCollationKeyFoldsWhatTheBooksIgnore(string headword, string expected)
    {
        Assert.That(DictionaryBrowse.CollationKey(headword), Is.EqualTo(expected));
    }

    [Test]
    public void ACedillaHeadwordFilesUnderC()
    {
        Assert.That(DictionaryBrowse.LetterOf("çhengey"), Is.EqualTo('c'));
    }

    /// <summary>Cregeen's suffix entries ('-al') file under the letter that
    /// follows the hyphen, not under the hyphen</summary>
    [Test]
    public void ASuffixEntryFilesUnderItsFirstLetter()
    {
        Assert.That(DictionaryBrowse.LetterOf("-al"), Is.EqualTo('a'));
    }

    /// <summary>The letter bar is derived, so a letter the data has is a letter
    /// the bar shows: LetterLookup has no ç, and 39 Cregeen headwords start with
    /// one</summary>
    [Test]
    public void TheLettersComeFromTheHeadwordsThemselves()
    {
        var letters = DictionaryBrowse.LettersOf(
            ["aa-", "baa", "çhengey", "caa", "-al", "ymmyrçhagh"]);

        Assert.That(letters, Is.EqualTo(new[] { 'a', 'b', 'c', 'y' }));
    }

    [Test]
    public void AHeadwordThatFoldsToNothingHasNoLetter()
    {
        Assert.That(DictionaryBrowse.LetterOf("-"), Is.EqualTo('\0'));
        Assert.That(DictionaryBrowse.LettersOf(["-", "aa"]), Is.EqualTo(new[] { 'a' }));
    }

    [Test]
    public void AChapterIsTheWholeWordWhenItIsShorterThanTheDepth()
    {
        Assert.That(DictionaryBrowse.PrefixOf("ad", 3), Is.EqualTo("ad"));
        Assert.That(DictionaryBrowse.PrefixOf("aalin", 3), Is.EqualTo("aal"));
    }

    // ---- the letter, chapter by chapter ----

    /// <summary>The chapter is headed in capitals, as a printed index heads its
    /// column, whatever case the book gives the words themselves</summary>
    [Test]
    public void AChapterIsHeadedInCapitals()
    {
        var chapters = DictionaryBrowse.Chapters(["aalin", "BILLEY"]);

        Assert.That(chapters.Select(x => x.Key), Is.EqualTo(new[] { "AAL", "BIL" }));
    }

    /// <summary>A chapter breaks where the prefix changes, and the words inside
    /// keep the order they came in: 'aalin' was printed before 'aalid'</summary>
    [Test]
    public void AChapterBreaksWhereThePrefixChanges()
    {
        var chapters = DictionaryBrowse.Chapters(["aalin", "aalid", "aane", "abban"]);

        Assert.Multiple(() =>
        {
            Assert.That(chapters.Select(x => x.Key), Is.EqualTo(new[] { "AAL", "AAN", "ABB" }));
            Assert.That(chapters[0].Words.Select(x => x.Word), Is.EqualTo(new[] { "aalin", "aalid" }));
            Assert.That(chapters[1].Words.Select(x => x.Word), Is.EqualTo(new[] { "aane" }));
        });
    }

    /// <summary>A hyphen is not a letter, so 'agh-markiagh' files under AGH with
    /// the rest of them, which is where Cregeen prints it</summary>
    [Test]
    public void AChapterFilesOnWhatTheCollationKeeps()
    {
        var chapters = DictionaryBrowse.Chapters(["aghin", "agh-markiagh"]);

        Assert.That(chapters.Single().Key, Is.EqualTo("AGH"));
    }

    /// <summary>The chapters follow the book, so a name can come round twice:
    /// Cregeen files 'faar-y-chaagh' among the 'caa' words, and gathering the two
    /// FAAs would move it out of the place the book prints it</summary>
    [Test]
    public void AChapterComesRoundAgainWhereTheBookDoublesBack()
    {
        var chapters = DictionaryBrowse.Chapters(["faar-y-chaagh", "fa", "faag", "faaie"]);

        Assert.Multiple(() =>
        {
            Assert.That(chapters.Select(x => x.Key), Is.EqualTo(new[] { "FAA", "FA", "FAA" }));
            Assert.That(chapters[0].Words.Select(x => x.Word), Is.EqualTo(new[] { "faar-y-chaagh" }));
            Assert.That(chapters[2].Words.Select(x => x.Word), Is.EqualTo(new[] { "faag", "faaie" }));
        });
    }

    /// <summary>Kelly prints five headwords 'A': a word is identified by where it
    /// sits, so a repeat is kept rather than folded away</summary>
    [Test]
    public void AWordTheBookPrintsTwiceIsListedTwice()
    {
        var chapters = DictionaryBrowse.Chapters(["A", "A"]);

        Assert.That(chapters.Single().Words.Select(x => x.Word), Is.EqualTo(new[] { "A", "A" }));
    }

    [Test]
    public void ALetterWithNoHeadwordsHasNoChapters()
    {
        Assert.That(DictionaryBrowse.Chapters([]), Is.Empty);
    }
}
