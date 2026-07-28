using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CorpusSearch.Dependencies.Lucene;
using Microsoft.Extensions.Logging;

namespace CorpusSearch.Service;

/// <summary>
/// The printed word lists (generated from cregeen-nvh wordlists/*.nvh), keyed by
/// the spelling a page set.
///
/// Deliberately not a dictionary and not a lemma layer. A word list says a page
/// printed this spelling against this plant; it gives no word class, no
/// inflection and no root, so there is nothing here to scope a dictionary tab to
/// and nothing to fold into <see cref="LemmaTable"/>. A word's page shows it as a
/// citation — "the list prints this" — and links the corpus document it was
/// transcribed from.
///
/// Lookup is by spelling, not by lemma, and that is the honest reach: claiming
/// the list attests a *lemma* would put a plant sense on every inflected and
/// mutated form of a word the list happens to name, which the page never said.
/// The generator widens the key where the page's own typography narrowed it (a
/// multiword head answers to its collapsed spelling, an article-led head to the
/// bare word), so those rows are still the page's spelling, just findable.
/// </summary>
public class WordListCitationService
{
    private readonly Dictionary<string, IReadOnlyList<WordListCitation>> byForm;

    public WordListCitationService(ILogger<WordListCitationService> logger)
    {
        var rowsPath = Startup.GetLocalFile("Resources", "wordlists.tsv");
        var sourcesPath = Startup.GetLocalFile("Resources", "wordlists.sources.tsv");
        if (!File.Exists(rowsPath) || !File.Exists(sourcesPath))
        {
            // an uninitialised submodule shouldn't take the server down: word
            // pages simply carry no citations
            logger.LogWarning("{Path} not found (is the submodule initialised?): word-list citations disabled", rowsPath);
            byForm = [];
            return;
        }

        using var rowsReader = new StreamReader(rowsPath);
        using var sourcesReader = new StreamReader(sourcesPath);
        byForm = Load(rowsReader, sourcesReader, logger);
        logger.LogInformation("word lists: {Forms} forms found", byForm.Count);
    }

    /// <summary>The two tables as readers, so the parse can be tested without files</summary>
    public WordListCitationService(TextReader rows, TextReader sources, ILogger logger)
    {
        byForm = Load(rows, sources, logger);
    }

    private static Dictionary<string, IReadOnlyList<WordListCitation>> Load(
        TextReader rows, TextReader sources, ILogger logger)
    {
        return ReadRows(rows, ReadSources(sources), logger);
    }

    /// <summary>Every list that prints the word, in the order the lists were read.
    /// Empty when no list names it — the usual answer.</summary>
    public IReadOnlyList<WordListCitation> For(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            return [];
        }
        return byForm.TryGetValue(LemmaTable.NormalizeForm(word), out var found) ? found : [];
    }

    /// <summary>Whether any list prints the word. What makes a naming enough to
    /// count as the only thing that documents a word, where no book does.</summary>
    public bool Names(string word) => For(word).Count > 0;

    /// <summary>How many lists have been read. Counted apart from the books,
    /// which is the whole point of the layer.</summary>
    public int Lists =>
        byForm.Values.SelectMany(x => x).Select(x => x.Source.ListId)
            .Distinct(StringComparer.OrdinalIgnoreCase).Count();

    /// <summary>The printed heads, each said once, as the pages set them.
    ///
    /// The index's business, not the lookup's: the lookup keys on every spelling
    /// a head answers to (collapsed, article-stripped), but a reader walking the
    /// words should meet each head once, spelled the way it was printed.
    /// </summary>
    public IReadOnlyList<string> Headwords =>
        headwords ??= [.. byForm.Values
            .SelectMany(x => x)
            .Select(x => x.Headword)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.InvariantCultureIgnoreCase)];

    private IReadOnlyList<string>? headwords;

    private static Dictionary<string, WordListSource> ReadSources(TextReader reader)
    {
        var sources = new Dictionary<string, WordListSource>(StringComparer.OrdinalIgnoreCase);
        foreach (var columns in ReadTsv(reader, expected: 7))
        {
            sources[columns[0]] = new WordListSource
            {
                ListId = columns[0],
                Name = columns[1],
                Credit = columns[2],
                Date = columns[3],
                DocumentIdent = columns[4],
                Url = columns[5],
                Citation = columns[6],
            };
        }
        return sources;
    }

    private static Dictionary<string, IReadOnlyList<WordListCitation>> ReadRows(
        TextReader reader, Dictionary<string, WordListSource> sources, ILogger logger)
    {
        var accumulated = new Dictionary<string, List<WordListCitation>>();
        foreach (var columns in ReadTsv(reader, expected: 6))
        {
            var listId = columns[2];
            if (!sources.TryGetValue(listId, out var source))
            {
                logger.LogWarning("word lists: row '{Form}' names list '{ListId}', which has no source row", columns[0], listId);
                continue;
            }
            var form = columns[0];
            if (!accumulated.TryGetValue(form, out var citations))
            {
                accumulated[form] = citations = [];
            }
            citations.Add(new WordListCitation
            {
                Source = source,
                Headword = columns[1],
                Gloss = columns[3],
                Binomial = string.IsNullOrEmpty(columns[4]) ? null : columns[4],
                Note = string.IsNullOrEmpty(columns[5]) ? null : columns[5],
            });
        }
        return accumulated.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<WordListCitation>)kv.Value,
            StringComparer.Ordinal);
    }

    /// <summary>The rows of a header-led TSV, short rows padded: the generator
    /// omits nothing, but a hand-edited file should not throw at startup</summary>
    private static IEnumerable<string[]> ReadTsv(TextReader reader, int expected)
    {
        var first = true;
        while (reader.ReadLine() is { } line)
        {
            if (first)
            {
                first = false;
                continue;
            }
            if (line.Length == 0)
            {
                continue;
            }
            var columns = line.Split('\t');
            if (columns.Length < expected)
            {
                columns = [.. columns, .. Enumerable.Repeat("", expected - columns.Length)];
            }
            yield return columns;
        }
    }
}

/// <summary>One printed list, named once</summary>
public class WordListSource
{
    public required string ListId { get; init; }

    /// <summary>The list's title as it is worth showing ("Manx Plant Names")</summary>
    public required string Name { get; init; }

    /// <summary>Who compiled it</summary>
    public required string Credit { get; init; }

    /// <summary>When it was printed, as much of a date as is known ("1908")</summary>
    public required string Date { get; init; }

    /// <summary>The corpus document the list was transcribed from: the citation
    /// links here, so a reader can read the page rather than take our word</summary>
    public required string DocumentIdent { get; init; }

    public required string Url { get; init; }

    /// <summary>The full printed citation, for the reader who wants the book</summary>
    public required string Citation { get; init; }
}

/// <summary>What one list prints against a word</summary>
public class WordListCitation
{
    public required WordListSource Source { get; init; }

    /// <summary>The spelling as the page sets it, which is not always the
    /// spelling that was looked up ("Yn luss" found from "luss")</summary>
    public required string Headword { get; init; }

    /// <summary>The English name, verbatim from the page — including its typos,
    /// which <see cref="Note"/> reads back</summary>
    public required string Gloss { get; init; }

    /// <summary>The Latin name the list sets beside the English one, where it
    /// sets one</summary>
    public string? Binomial { get; init; }

    /// <summary>What the page or the transcription needs said about this line: a
    /// print correction, "always plural"</summary>
    public string? Note { get; init; }
}
