using System.Linq;
using CorpusSearch.Service;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>
/// Browsing a dictionary by its printed index: the letters it has, and how deep
/// a prefix bar has to go before a group stops being a wall of words.
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
    public void AGroupIsTheWholeWordWhenItIsShorterThanTheDepth()
    {
        Assert.That(DictionaryBrowse.PrefixOf("ad", 3), Is.EqualTo("ad"));
        Assert.That(DictionaryBrowse.PrefixOf("aalin", 3), Is.EqualTo("aal"));
    }

    /// <summary>Two letters is enough while the groups stay small: Cregeen's 'A'
    /// is 150 headwords, and three letters would make 79 groups of two</summary>
    [Test]
    public void AShallowBarIsKeptWhileItsGroupsAreSmall()
    {
        var headwords = Enumerable.Range(0, 20)
            .Select(i => $"a{(char)('a' + i % 10)}{i}")
            .ToList();

        Assert.That(DictionaryBrowse.DepthFor(headwords), Is.EqualTo(2));
    }

    /// <summary>...but a letter with one crowded prefix goes deeper: at two
    /// letters every 'caa...' word is one group</summary>
    [Test]
    public void ACrowdedPrefixPushesTheBarDeeper()
    {
        var headwords = Enumerable.Range(0, 100).Select(i => $"caa{i}").ToList();

        Assert.That(DictionaryBrowse.DepthFor(headwords), Is.GreaterThan(2));
    }

    /// <summary>Four letters is as far as it goes: past that a prefix is most of
    /// the word it is filing, and a letter no depth can split (these share their
    /// first four) is left as it is rather than chased further</summary>
    [Test]
    public void TheBarNeverGoesPastFourLetters()
    {
        var headwords = Enumerable.Range(0, 200).Select(i => $"aaaa{i}").ToList();

        Assert.That(DictionaryBrowse.DepthFor(headwords), Is.EqualTo(4));
    }
}
