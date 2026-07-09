using CorpusSearch.Controllers;
using CorpusSearch.Dependencies;
using CorpusSearch.Model;
using System.Collections.Generic;
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

        var searchOptions = ToSearchOptions(workQuery);
        searchOptions.ReturnTranscriptData = HasTranscript(document);

        var results = searcher.SearchWork(workQuery.Ident, workQuery.Query, searchOptions);

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

    /// <summary>Whether lines carry subtitle timings/speakers: the document is a YouTube transcription</summary>
    private static bool HasTranscript(IDocument document)
    {
        return document.Source != null &&
               (document.Source.Trim().StartsWith("https://youtube.com") ||
                document.Source.Trim().StartsWith("https://www.youtube.com"));
    }

    private static SearchOptions ToSearchOptions(CorpusSearchWorkQuery workQuery)
    {
        return new SearchOptions
        {
            Type = workQuery.Manx ? SearchType.Manx : SearchType.English,
            IgnoreHyphens = workQuery.IgnoreHyphens,
        };
    }
}