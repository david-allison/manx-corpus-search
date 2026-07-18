using System;
using System.Collections.Generic;
using System.Linq;
using CorpusSearch.Dependencies.Lucene;
using CorpusSearch.Model;

namespace CorpusSearch.Service;

/// <summary>
/// The dictionary's front-page numbers: how much of what the corpus says the
/// books can answer for, how much of it can be heard, and how much of the
/// lemma table the texts attest.
///
/// Counted over the corpus rather than over the books, because that is the
/// claim the front page makes: not "Phil Kelly is large" but "the word you
/// meet in a text will probably have an entry". Weighted both ways - a
/// distinct-words share tells a reader of word lists, a running-words share
/// tells a reader of texts - since 'as' said 40,000 times and a hapax are one
/// word each by the first count and could not differ more by the second.
/// </summary>
public class DictionaryStatsService(LemmaTable lemmaTable, CorpusVocabulary vocabulary,
    IEnumerable<ISearchDictionary> dictionaryServices, WorkService workService)
{
    private readonly ISearchDictionary[] gvDictionaries = dictionaryServices
        .Where(d => d.QueryLanguages.Contains("gv")).ToArray();

    /// <summary>Every distinct word the recordings say, set by the startup pass
    /// over the 🎥 documents. Null until then: the page says the recordings are
    /// unread rather than counting them at zero.</summary>
    private volatile AudioVocabulary? audio;

    private sealed record AudioVocabulary(int Recordings, HashSet<string> Words);

    /// <summary>The one walk of the term list, kept: the corpus does not change
    /// under a running server, and only the audio pass landing can change the
    /// answer - so the cache holds until it does</summary>
    private (AudioVocabulary? Audio, DictionaryStats Stats)? cached;

    /// <summary>The words the recordings say, read once behind the server as
    /// the phrase scan is: a couple of dozen documents, but nobody's first
    /// page-load should wait on them</summary>
    /// <param name="lines">the Manx of every recording's line, as the
    /// statistics count it</param>
    public void InitAudio(int recordings, IEnumerable<string> lines)
    {
        var words = new HashSet<string>();
        foreach (var line in lines)
        {
            foreach (var word in DocumentLine.NormalizeManx(line)
                         .Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                words.Add(word);
            }
        }
        audio = new AudioVocabulary(recordings, words);
    }

    public DictionaryStats Stats()
    {
        var read = audio;
        if (cached is { } kept && ReferenceEquals(kept.Audio, read))
        {
            return kept.Stats;
        }
        var built = Build(read);
        cached = (read, built);
        return built;
    }

    private DictionaryStats Build(AudioVocabulary? read)
    {
        long distinct = 0, running = 0;
        long definedWords = 0, definedRunning = 0;
        long audioWords = 0, audioRunning = 0;
        foreach (var (term, frequency) in vocabulary.TermFrequencies)
        {
            distinct++;
            running += frequency;
            if (Defined(term))
            {
                definedWords++;
                definedRunning += frequency;
            }
            if (read != null && read.Words.Contains(term))
            {
                audioWords++;
                audioRunning += frequency;
            }
        }
        return new DictionaryStats
        {
            Texts = workService.GetAll().Result.Count,
            RunningWords = running,
            DistinctWords = distinct,
            Books = gvDictionaries.Length,
            Entries = gvDictionaries.Sum(d => (long)d.Headwords.Count),
            DefinedWords = definedWords,
            DefinedRunningWords = definedRunning,
            Lemmas = lemmaTable.AllDisplayLemmas.Count(),
            AttestedLemmas = vocabulary.AttestedLemmaCount,
            Recordings = read?.Recordings,
            AudioWords = read == null ? null : audioWords,
            AudioRunningWords = read == null ? null : audioRunning,
        };
    }

    /// <summary>Whether some book answers for the word - by its own spelling,
    /// or by a lemma the table reads it as: 'deiney' is answered by the entry
    /// 'dooinney', which is how the word page itself would answer it</summary>
    private bool Defined(string term) =>
        gvDictionaries.Any(d => d.ContainsWord(term))
        || lemmaTable.DisplayLemmasFor(term)
            .Any(lemma => gvDictionaries.Any(d => d.ContainsWord(lemma)));
}

/// <summary>Counts, never percentages: the page turning a pair of counts into
/// "82.9%" can say so beside the pair, and no share survives here rounded</summary>
public record DictionaryStats
{
    public required long Texts { get; init; }
    public required long RunningWords { get; init; }
    public required long DistinctWords { get; init; }
    public required int Books { get; init; }
    public required long Entries { get; init; }
    /// <summary>distinct corpus words some book answers for</summary>
    public required long DefinedWords { get; init; }
    public required long DefinedRunningWords { get; init; }
    public required long Lemmas { get; init; }
    public required long AttestedLemmas { get; init; }
    /// <summary>null until the startup pass has read the recordings</summary>
    public required int? Recordings { get; init; }
    public required long? AudioWords { get; init; }
    public required long? AudioRunningWords { get; init; }
}
