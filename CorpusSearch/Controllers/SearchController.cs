using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using CorpusSearch.Extensions;
using CorpusSearch.Model;
using CorpusSearch.Service;
using CorpusSearch.Utils;
using Microsoft.AspNetCore.Mvc;

namespace CorpusSearch.Controllers;

[ApiController]
[Route("[controller]")]
public partial class SearchController(
    DocumentSearchService documentSearchService,
    OverviewSearchService2 overviewSearchService,
    IEnumerable<ISearchDictionary> dictionaryServices,
    WorkService workService)
    : ControllerBase
{
    public static string PUNCTUATION_REGEX = "[,.;!?\\s]";
    private readonly ISearchDictionary[] dictionaryServices = dictionaryServices.ToArray();

    [HttpGet]
    public string Get()
    {
        return "a";
    }

    /// <summary>
    /// Map from source language to words in said language (thunnag): ["en"] => "duck", "duckling"
    /// </summary>
    public class Translations : Dictionary<string, IList<string>>
    {
        public static Translations FromManx(string query)
        {
            var results = Startup.ManxToEnglishDictionary.GetValueOrDefault(query.Trim(), new List<string>());
            var ret = new Translations
            {
                { "Phil Kelly (en)", results }
            };
            return ret;
        }

        public static Translations FromEnglish(string query)
        {
            var results = Startup.EnglishToManxDictionary.GetValueOrDefault(query.Trim(), new List<string>());
            var ret = new Translations
            {
                { "Phil Kelly (gv)", results }
            };
            return ret;
        }
    }

    public class SearchWorkResult : IResultContainer<DocumentLine>, ITimedResult
    {
        public List<DocumentLine> Results { get; set; } = [];
        public int NumberOfResults { get; set; }
        public string TimeTaken { get; set; }
        public string Title { get; set; }
            
        public string Original { get; set; }

        public Translations Translations { get; set; } = new();
        /// <summary>
        /// The total number of matches (multiple matches per line)
        /// </summary>
        /// <remarks>May be null if searching for "*"</remarks>
        public int? TotalMatches { get; internal set; } = -1;
        /// <summary>
        /// Optional link to external PDF
        /// </summary>
        public string PdfLink { get; internal set; }
            
        /// <summary>
        /// Optional ID of the content on Google Books
        /// </summary>
        public string GoogleBooksId { get; set; }

        /// <summary>A list of the dictionaries that the word is defined in</summary>
        public Dictionary<string, DictionaryData> DefinedInDictionaries { get; set; } = new();

        /// <summary>https uri to the file on GitHub</summary>
        /// <remarks>This views the file, as "edit" on GitHub does not handle line numbers</remarks>
        public string GitHubLink { get; set; }
        public object Notes { get; internal set; }
        public string Source { get; set; }
        public List<SourceLink> SourceLinks { get; internal set; } = [];

        /// <summary>
        /// The document's first CsvLineNumber: lets the client offer 'expand context' above the
        /// first result. Null when searching for '*' or when there are no results.
        /// </summary>
        public int? FirstLineNumber { get; set; }

        /// <summary>The document's last CsvLineNumber (see <see cref="FirstLineNumber"/>)</summary>
        public int? LastLineNumber { get; set; }

        internal static SearchWorkResult Empty(string title)
        {
            var ret = new SearchWorkResult
            {
                Title = title,
            };

            ret.EmptyResult();
            return ret;
        }
    }

    [HttpGet("SearchWork/{workIdent}/{query}")]
    public async Task<ActionResult<SearchWorkResult>> SearchWork(string workIdent, string query = null, bool manx = true, bool english = true, bool ignoreHyphens = false)
    {
        if (QueryTooLong(query))
        {
            return QueryTooLongResult();
        }
        var sw = Stopwatch.StartNew();
        AnonymousAnalytics.Track("Search Work");
        var workQuery = new CorpusSearchWorkQuery(query)
        {
            Ident = workIdent,
            Manx = manx,
            English = english,
            IgnoreHyphens = ignoreHyphens,
        };
        SearchWorkResult ret = await documentSearchService.SearchWork(workQuery);

        if (manx)
        {
            ret.Translations = Translations.FromManx(query);
        }
        else if (english)
        {
            ret.Translations = Translations.FromEnglish(query);
        }

        ret.GitHubLink = (await workService.ByIdent(workIdent))?.GetGitHubLink();
        ret.DefinedInDictionaries = DictionaryLookup(query, new QueryLanguages(Manx: manx, English: english));

        ret.EnrichWithTime(sw);
        return ret;
    }

    /// <summary>The most lines an 'expand context' request may return</summary>
    public const int MAX_CONTEXT_LINES = 100;

    public class WorkLinesResult
    {
        /// <summary>The requested lines, in document order</summary>
        public List<DocumentLine> Lines { get; set; } = [];

        /// <summary>
        /// The number of lines in [start, end] before the limit was applied: if this is no more
        /// than the limit, the range is exhausted
        /// </summary>
        public int TotalInRange { get; set; }
    }

    /// <summary>
    /// Lines of a document by CsvLineNumber range: 'expand context' around a search result (#286).
    /// Returns the first <paramref name="limit"/> lines of the range, or the last if
    /// <paramref name="fromEnd"/>.
    /// </summary>
    [HttpGet("Lines/{workIdent}")]
    public async Task<ActionResult<WorkLinesResult>> GetLines(string workIdent, [FromQuery] int start,
        [FromQuery] int end, [FromQuery] int limit = 5, [FromQuery] bool fromEnd = false)
    {
        if (start < 1 || end < start)
        {
            return BadRequest("invalid line range");
        }
        if (limit is < 1 or > MAX_CONTEXT_LINES)
        {
            return BadRequest($"limit must be between 1 and {MAX_CONTEXT_LINES}");
        }
        AnonymousAnalytics.Track("Expand Context");
        var (lines, totalInRange) = await documentSearchService.GetLines(workIdent, start, end, limit, fromEnd);
        return new WorkLinesResult { Lines = lines, TotalInRange = totalInRange };
    }

    [HttpGet("Match/{workIdent}")]
    public async Task<MatchReference> GetMatch(string workIdent, [FromQuery] string query, [FromQuery(Name = "match")] int matchNumber, bool ignoreHyphens = false)
    {
        // PREF: Much slower than necessary
        if (matchNumber < 1 || query == null || workIdent == null) return null;
        var workResult = (await SearchWork(workIdent, query: query, manx: true, english: false, ignoreHyphens: ignoreHyphens)).Value;
        if (workResult == null) return null;
        var selectedIndex = QueryMatchIndex(workResult.Results, matchNumber);
        if (selectedIndex == null) return null;
        var line = workResult.Results[selectedIndex.Value.LineIndex];
        return new MatchReference(workIdent, matchNumber, line.Manx,
            selectedIndex.Value.IndexInLine,
            LineNumber: selectedIndex.Value.LineIndex + 1,
            Highlights: line.ManxHighlights);
    }

    private (int LineIndex, int IndexInLine)? QueryMatchIndex(List<DocumentLine> workResultResults, int line)
    {
        // PERF: Skip
        int currentMatch = 1;
        int lineIndex = 0;
        foreach (var documentLine in workResultResults)
        {
            for (int i = 0; i < documentLine.MatchesInLine; i++)
            {
                if (currentMatch == line)
                {
                    return (lineIndex, i);
                }
                currentMatch++;
            }
            lineIndex++;
        }

        return null;
    }

    [HttpGet("Search/{query}")]
    public async Task<ActionResult<QueryDocumentSearchResult>> SearchCorpus(string query, bool manx = true, bool english = true, int minDate = 1600, int maxDate = 2100, bool ignoreHyphens = false)
    {
        if (QueryTooLong(query))
        {
            return QueryTooLongResult();
        }
        var sw = Stopwatch.StartNew();
        AnonymousAnalytics.Track("Search Corpus");
        QueryDocumentSearchResult ret = new QueryDocumentSearchResult()
        {
            Query = query,
        };

        var searchQuery = new CorpusSearchQuery(query)
        {
            Manx = manx,
            English = english,
            MinDate = DateTimeUtil.FromYear(Math.Max(1, minDate)),
            MaxDate = DateTimeUtil.FromYearMax(maxDate),
            IgnoreHyphens = ignoreHyphens,
        };
        if (!searchQuery.IsValid())
        {
            ret.EnrichWithTime(sw);
            return ret;
        }

        var results = await overviewSearchService.CorpusSearch(searchQuery);

        // explicit tie-breakers: document insertion order is not guaranteed (#303)
        results = results.OrderBy(x => x.StartDate)
            .ThenBy(x => x.DocumentName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Ident, StringComparer.Ordinal);
            
        if (manx)
        {
            ret.Translations = Translations.FromManx(query);
        } 
        else if (english)
        {
            ret.Translations = Translations.FromEnglish(query);
        }

        ret.DefinedInDictionaries = DictionaryLookup(query, new QueryLanguages(Manx: manx, English: english));
        ret.SetResults(results);
        if (ret.Results.Count == 0 && !ignoreHyphens)
        {
            // #158: 'lumlane' found nothing, but 'lum-lane' exists
            ret.Suggestions = await overviewSearchService.GetSuggestions(searchQuery);
        }
        ret.EnrichWithTime(sw);
        ret.NumberOfDocuments = ret.Results.Count;
        return ret;
    }

    private static bool QueryTooLong(string query) => query is { Length: > CorpusSearchQuery.MAX_LENGTH };

    private BadRequestObjectResult QueryTooLongResult() =>
        BadRequest($"Query too long: the maximum is {CorpusSearchQuery.MAX_LENGTH} characters");

    internal Dictionary<string, DictionaryData> DictionaryLookup(string query, QueryLanguages languages)
    {
        // #159: the dictionaries perform exact-match lookups, so surrounding whitespace returns no results
        query = query.Trim();
        var requestedLanguages = languages.AsList();
        var lookup = dictionaryServices.Where(x => x.QueryLanguages.Any(supportedLanguages => requestedLanguages.Contains(supportedLanguages))).ToDictionary(x => x.Identifier,
            x => new { summaries = x.GetSummaries(query), AllowLookup = x.LinkToDictionary });
        return lookup
            .Where(x => x.Value.summaries.Any()) // where there are results
            .ToDictionary(x => x.Key, 
                x => new DictionaryData(x.Value.summaries.Select(x => x.Summary).ToList(), x.Value.AllowLookup)); // extract the summary
    }

    public record DictionaryData(List<string> Entries, bool AllowLookup);

    public class QueryDocumentSearchResult : IResultContainer<QueryDocumentResult>, ITimedResult
    {
        public string Query { get; set; }
        public int NumberOfDocuments { get; set; }
        public List<QueryDocumentResult> Results { get; set; }
        public int NumberOfResults { get; set; }
        public string TimeTaken { get; set; }
        public Dictionary<string, DictionaryData> DefinedInDictionaries { get; internal set; } = new();
        public Translations Translations { get; set; } = new();
        /// <summary>'Did you mean' alternatives, populated when the search found nothing (#158)</summary>
        public List<SearchSuggestion> Suggestions { get; set; } = [];
    }
}

public record QueryLanguages(bool Manx, bool English)
{
    public List<string> AsList()
    {
        var requestedLanguages = new List<string>();
        if (English) requestedLanguages.Add("en");
        if (Manx) requestedLanguages.Add("gv");
        return requestedLanguages;
    }
}

/// <param name="Highlights">
/// Ranges of <paramref name="Manx"/> which matched the query (all matches in the line;
/// <paramref name="MatchIndexInLine"/> selects the current one)
/// </param>
[SuppressMessage("ReSharper", "NotAccessedPositionalProperty.Global")]
public record MatchReference(string WorkIdent, int MatchNumber, string Manx, long MatchIndexInLine, long LineNumber,
    IReadOnlyList<HighlightRange> Highlights);