using CorpusSearch.Extensions;
using CorpusSearch.Model;
using CorpusSearch.Service;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace CorpusSearch.Controllers
{
    // TODO: Handle a search for a word at the end of a sentence
    [ApiController]
    [Route("[controller]")]
    public partial class SearchController : ControllerBase
    {
        public static string PUNCTUATION_REGEX = "[,.;!?\\-\\s]";
        private readonly DocumentSearchService2 documentSearchService;
        private readonly OverviewSearchService2 overviewSearchService;

        public SearchController(DocumentSearchService2 documentSearchService, OverviewSearchService2 overviewSearchService)
        {
            this.documentSearchService = documentSearchService;
            this.overviewSearchService = overviewSearchService;
        }

        [HttpGet]
        public string Get()
        {
            return "a";
        }

        public class SearchWorkResult : IResultContainer<DocumentLine>, ITimedResult
        {
            public List<DocumentLine> Results { get; set; } = new List<DocumentLine>();
            public int NumberOfResults { get; set; }
            public string TimeTaken { get; set; }
            public string Title { get; set; }

            public List<string> ManxTranslations { get; set; } = new List<string>();
            public List<string> EnglishTranslations { get; set; } = new List<string>();
            /// <summary>The total number of matches (multiple matches per line)</summary>
            public int TotalMatches { get; internal set; } = -1;
            /// <summary>
            /// Optional link to external PDF
            /// </summary>
            public string PdfLink { get; internal set; }

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
        public async Task<SearchWorkResult> SearchWork(string workIdent, string query = null, bool manx = true, bool english = true)
        {
            var sw = Stopwatch.StartNew();
            var workQuery = new CorpusSearchWorkQuery(query)
            {
                Ident = workIdent,
                Manx = manx,
                English = english,
            };
            SearchWorkResult ret = await documentSearchService.SearchWork(workQuery);
            ret.EnrichWithTime(sw);
            return ret;
        }

        [HttpGet("Search/{query}")]
        public async Task<QueryDocumentSearchResult> SearchCorpus(string query, bool manx = true, bool english = true, int minDate = 1600, int maxDate = 2100)
        {
            var sw = Stopwatch.StartNew();
            QueryDocumentSearchResult ret = new QueryDocumentSearchResult()
            {
                Query = query,
            };

            var searchQuery = new CorpusSearchQuery(query)
            {
                Manx = manx,
                English = english,
                MinDate = DateTimeUtil.FromYear(Math.Max(1, minDate)),
                MaxDate = DateTimeUtil.FromYearMax(maxDate)
            };
            if (!searchQuery.IsValid())
            {
                ret.EnrichWithTime(sw);
                return ret;
            }

            var results = await overviewSearchService.CorpusSearch(searchQuery);

            results = results.OrderBy(x => x.StartDate);

            ret.EnrichResults(results);
            ret.EnrichWithTime(sw);
            ret.NumberOfDocuments = ret.Results.Count;
            return ret;
        }

        public class QueryDocumentSearchResult : IResultContainer<QueryDocumentResult>, ITimedResult
        {
            public string Query { get; set; }
            public int NumberOfDocuments { get; set; }
            public List<QueryDocumentResult> Results { get; set; }
            public int NumberOfResults { get; set; }
            public string TimeTaken { get; set; }
        }
    }
}
