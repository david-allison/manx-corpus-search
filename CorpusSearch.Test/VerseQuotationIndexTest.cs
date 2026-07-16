using System.Collections.Generic;
using CorpusSearch.Service;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>
/// The reverse of a dictionary entry's verse citations: a corpus verse line
/// names the entries quoting it (quotes.nvh direction 2).
/// </summary>
[TestFixture]
public class VerseQuotationIndexTest
{
    private sealed class QuotingFake(string identifier, string slug, params (string Word, string Text)[] entries)
        : ISearchDictionary, IQuotingDictionary
    {
        public string Identifier => identifier;
        public string Slug => slug;
        public List<string> QueryLanguages => ["gv"];
        public bool LinkToDictionary => true;
        public IEnumerable<(string Word, string Text)> QuotableEntries => entries;
        public IEnumerable<DictionarySummary> GetSummaries(string query, bool basic = false) => [];
        public bool ContainsWord(string word) => false;
        public IEnumerable<string> AllWords => [];
        public IReadOnlyList<string> Headwords => [];
    }

    [Test]
    public void AVerseNamesTheEntriesQuotingIt()
    {
        var index = new VerseQuotationIndex([
            new QuotingFake("J Kelly Manx to English", "kelly-m2e",
                ("aalid", "beauty, comeliness. Ta dty aalid, O Israel. Ps. 45, 12."),
                ("baare", "the top; no citation here")),
            new QuotingFake("Cregeen", "cregeen",
                ("eh", "he, it, him. sometimes; 2 Kings xi. 2: as dollee ad eh")),
        ]);

        Assert.That(index.For("psalms.45.12"), Is.EqualTo(new[]
        {
            new VerseQuotation("J Kelly Manx to English", "kelly-m2e", "aalid", "Ps. 45, 12"),
        }));
        Assert.That(index.For("2-kings.11.2"), Is.EqualTo(new[]
        {
            new VerseQuotation("Cregeen", "cregeen", "eh", "2 Kings xi. 2"),
        }));
        Assert.That(index.For("psalms.23.1"), Is.Null);
    }

    [Test]
    public void AnEntryQuotingTheSameVerseTwiceIsOneRow()
    {
        var index = new VerseQuotationIndex([
            new QuotingFake("Cregeen", "cregeen",
                ("foo", "Jud. xii. 6; and again Jud. xii, 6 differently printed")),
        ]);

        Assert.That(index.For("judges.12.6"), Has.Count.EqualTo(1));
    }

    [Test]
    public void ANonQuotingDictionaryIsIgnored()
    {
        var index = new VerseQuotationIndex([new PlainFake()]);

        Assert.That(index.For("psalms.45.12"), Is.Null);
    }

    private sealed class PlainFake : ISearchDictionary
    {
        public string Identifier => "Plain";
        public string Slug => "plain";
        public List<string> QueryLanguages => ["gv"];
        public bool LinkToDictionary => true;
        public IEnumerable<DictionarySummary> GetSummaries(string query, bool basic = false) =>
            [new DictionarySummary { Summary = "beauty. Ps. 45, 12.", PrimaryWord = "aalid" }];
        public bool ContainsWord(string word) => false;
        public IEnumerable<string> AllWords => [];
        public IReadOnlyList<string> Headwords => [];
    }
}
