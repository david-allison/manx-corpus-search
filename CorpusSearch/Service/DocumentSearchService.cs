using CorpusSearch.Controllers;
using CorpusSearch.Dependencies;
using CorpusSearch.Model;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static CorpusSearch.Controllers.SearchController;

namespace CorpusSearch.Service;

public class DocumentSearchService(
    WorkService workService,
    Searcher searcher,
    NewspaperSourceEnricher newspaperSourceEnricher)
{
    public async Task<SearchWorkResult> SearchWork(CorpusSearchWorkQuery workQuery)
    {
        IDocument document = await workService.ByIdent(workQuery.Ident);
        string title = document.Name;

        SearchWorkResult ret = SearchWorkResult.Empty(title);

        if (!workQuery.IsValid())
        {
            return ret;
        }

        ret.PdfLink = document.ExternalPdfLink;
        ret.GoogleBooksId = document.GoogleBooksId;
        ret.Notes = document.Notes;
        ret.Original = document.Original;
        ret.Source = document.Source;

        var results = searcher.SearchWork(workQuery.Ident, workQuery.Query, workQuery.ToSearchOptions(),
            returnTranscriptData: HasTranscript(document));

        newspaperSourceEnricher.Enrich(ret, document);

        ret.SetResults(results.Lines);
        // Handles more than one result per document line
        ret.TotalMatches = results.TotalMatches;

        // Bounds for 'expand context' (#286): a '*' search (TotalMatches == null) already
        // returns every line, so has nothing to expand
        if (results.TotalMatches != null && results.Lines.Count > 0)
        {
            var lineNumberRange = searcher.GetLineNumberRange(workQuery.Ident);
            ret.FirstLineNumber = lineNumberRange?.First;
            ret.LastLineNumber = lineNumberRange?.Last;
        }

        return ret;
    }

    /// <summary>
    /// Returns all lines for a provided document
    /// </summary>
    /// <param name="ident">The ID of the document</param>
    internal List<DocumentLine> GetAllLines(string ident)
    {
        return searcher.GetAllLines(ident);
    }

    /// <summary>
    /// The lines of a document with a CsvLineNumber in [start, end]: the first
    /// <paramref name="limit"/> of them, or the last if <paramref name="fromEnd"/>.
    /// Expands the context around a search result (#286).
    /// </summary>
    public async Task<(List<DocumentLine> Lines, int TotalInRange)> GetLines(string ident, int start, int end,
        int limit, bool fromEnd)
    {
        IDocument document = await workService.ByIdent(ident);
        return searcher.GetLines(ident, start, end, limit, fromEnd, HasTranscript(document));
    }

    /// <summary>
    /// The verse named by canonical key <paramref name="key"/> across every document
    /// that has it: one entry per document, preferring its exact verse row over a
    /// chapter heading, ordered by translation date. A chapter-level key
    /// ("psalms.23") aligns on the chapter: headings in versions that have them,
    /// the chapter's first verse elsewhere. Null when the key doesn't parse.
    /// </summary>
    public VerseAlignmentResult? GetVerseAlignment(string key)
    {
        var reference = CanonicalReference.TryParseKey(key);
        if (reference == null)
        {
            return null;
        }

        // a verse also matches versions with only chapter rows (the Metrical Psalms);
        // a chapter also matches any verse under it
        string[] keys = reference.Verse == null
            ? [reference.Key]
            : [reference.Key, reference.ChapterKey];
        var chapterPrefix = reference.Verse == null ? reference.ChapterKey + "." : null;

        var documents = searcher.GetVerseAlignment(keys, chapterPrefix)
            .GroupBy(x => x.DocumentIdent)
            .Select(byDocument =>
            {
                var best = byDocument
                    .OrderBy(x => x.Line.CanonicalReference == reference.Key ? 0 : 1)
                    .ThenBy(x => x.Line.CsvLineNumber)
                    .First();
                return new VerseAlignmentDocument
                {
                    Ident = best.DocumentIdent,
                    Name = best.DocumentName,
                    Year = best.Created?.Year,
                    CsvLineNumber = best.Line.CsvLineNumber,
                    Reference = best.Line.Reference,
                    CanonicalReference = best.Line.CanonicalReference,
                    Manx = best.Line.Manx,
                    English = best.Line.English,
                };
            })
            .OrderBy(x => x.Year ?? int.MaxValue)
            .ThenBy(x => x.Name)
            .ToList();

        return new VerseAlignmentResult
        {
            Key = reference.Key,
            Display = reference.Display,
            Documents = documents,
        };
    }

    /// <summary>Whether lines carry subtitle timings/speakers: the document is a YouTube transcription</summary>
    private static bool HasTranscript(IDocument document)
    {
        return document.Source != null &&
               (document.Source.Trim().StartsWith("https://youtube.com") ||
                document.Source.Trim().StartsWith("https://www.youtube.com"));
    }
}

/// <summary>A verse across every translation that has it (see
/// <see cref="DocumentSearchService.GetVerseAlignment"/>)</summary>
public class VerseAlignmentResult
{
    public required string Key { get; init; }
    /// <summary>"Psalms 23:1"</summary>
    public required string Display { get; init; }
    public required List<VerseAlignmentDocument> Documents { get; init; }
}

/// <summary>One document's rendering of an aligned verse</summary>
public class VerseAlignmentDocument
{
    public required string Ident { get; init; }
    public required string Name { get; init; }
    public int? Year { get; init; }
    public int CsvLineNumber { get; init; }
    public string? Reference { get; init; }
    public string? CanonicalReference { get; init; }
    public string? Manx { get; init; }
    public string? English { get; init; }
}