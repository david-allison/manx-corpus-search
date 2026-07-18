using System.Collections.Generic;
using System.IO;
using System.Linq;
using CorpusSearch.Dependencies.Lucene;
using CorpusSearch.Service;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>
/// The dictionary's front-page numbers: coverage of the corpus by the books,
/// the recordings and the lemma table, counted over what the texts say.
/// </summary>
[TestFixture]
public class DictionaryStatsServiceTest
{
    private static LemmaTable Table() => LemmaTable.Load(new StringReader(
        "form\tlemmaId\tlemma\tlinkType\tpos\tvia\tnote\n" +
        "dooinney\tdooinney.n\tdooinney\tself\ts. m.\tdooinney\t\n" +
        "deiney\tdooinney.n\tdooinney\tinflected\ts. m.\tdooinney\t\n" +
        "aase\taase.n\taase\tself\ts. m.\taase\t"));

    private static DictionaryStatsService Service(LemmaTable table, CorpusVocabulary vocabulary) =>
        new(table, vocabulary, [new StubDictionary(["dooinney", "as"])], new WorkService());

    private static CorpusVocabulary Vocabulary(LemmaTable table)
    {
        var vocabulary = new CorpusVocabulary(table);
        // 'deiney' has no entry of its own but the table reads it as dooinney;
        // 'ayns' is answered by nobody
        vocabulary.Init([("dooinney", 10), ("deiney", 5), ("as", 100), ("ayns", 40)]);
        return vocabulary;
    }

    [Test]
    public void CountsTheCorpusAndWhatTheBooksAnswerFor()
    {
        var table = Table();
        var stats = Service(table, Vocabulary(table)).Stats();
        Assert.Multiple(() =>
        {
            Assert.That(stats.DistinctWords, Is.EqualTo(4));
            Assert.That(stats.RunningWords, Is.EqualTo(155));
            // dooinney by its entry, as by its entry, deiney by its lemma
            Assert.That(stats.DefinedWords, Is.EqualTo(3));
            Assert.That(stats.DefinedRunningWords, Is.EqualTo(115));
            Assert.That(stats.Books, Is.EqualTo(1));
            Assert.That(stats.Entries, Is.EqualTo(2));
            // the table holds dooinney and aase; only dooinney is said
            Assert.That(stats.Lemmas, Is.EqualTo(2));
            Assert.That(stats.AttestedLemmas, Is.EqualTo(1));
        });
    }

    [Test]
    public void TheAudioTrioIsNullUntilTheRecordingsAreRead()
    {
        var table = Table();
        var stats = Service(table, Vocabulary(table)).Stats();
        Assert.Multiple(() =>
        {
            Assert.That(stats.Recordings, Is.Null);
            Assert.That(stats.AudioWords, Is.Null);
            Assert.That(stats.AudioRunningWords, Is.Null);
        });
    }

    [Test]
    public void TheRecordingsWeighTheWordsTheySay()
    {
        var table = Table();
        var service = Service(table, Vocabulary(table));
        service.InitAudio(2, ["as dooinney as", "dooinney"]);

        var stats = service.Stats();
        Assert.Multiple(() =>
        {
            Assert.That(stats.Recordings, Is.EqualTo(2));
            // two distinct words heard; weighted by the whole corpus's use of
            // them, not by how often the recordings say them
            Assert.That(stats.AudioWords, Is.EqualTo(2));
            Assert.That(stats.AudioRunningWords, Is.EqualTo(110));
        });
    }

    /// <summary>A book that answers for its word list and nothing else</summary>
    private sealed class StubDictionary(string[] words) : ISearchDictionary
    {
        public string Identifier => "Stub";
        public string Slug => "stub";
        public List<string> QueryLanguages => ["gv"];
        public bool LinkToDictionary => false;
        public IEnumerable<DictionarySummary> GetSummaries(string query, bool basic = false) => [];
        public bool ContainsWord(string word) => words.Contains(word);
        public IEnumerable<string> AllWords => words;
        public IReadOnlyList<string> Headwords => words;
    }
}
