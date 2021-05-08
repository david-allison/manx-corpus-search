﻿using Codex_API.Extensions;
using Codex_API.Model;
using Codex_API.Service;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Codex_API.Controllers
{
    // TODO: Handle a search for a word at the end of a sentence
    [ApiController]
    [Route("[controller]")]
    public partial class SearchController : ControllerBase
    {
        public static string PUNCTUATION_REGEX = "[,.;!?\\-\\s]";

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
        public async Task<SearchWorkResult> SearchWork(string workIdent, string query = null, bool manx = true, bool english = true, bool fullTextSearch = false)
        {
            var sw = Stopwatch.StartNew();
            SearchWorkResult ret = await DocumentSearchService.SearchWork(workIdent, query, manx, english, fullTextSearch);
            ret.EnrichWithTime(sw);
            return ret;
        }

        [HttpGet("Search/{query}")]
        public async Task<QueryDocumentSearchResult> SearchCorpus(string query, bool manx = true, bool english = true, bool fullTextSearch = false, int minDate = 1600, int maxDate = 2100)
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
                FullText = fullTextSearch,
                MinDate = DateTimeUtil.FromYear(Math.Max(1, minDate)),
                MaxDate = DateTimeUtil.FromYearMax(maxDate)
            };
            if (!searchQuery.IsValid())
            {
                ret.EnrichWithTime(sw);
                return ret;
            }

            var results = await OverviewSearchService.CorpusSearch(searchQuery);

            results = results.OrderBy(x => x.StartDate);

            ret.EnrichResults(results);
            ret.EnrichWithTime(sw);
            ret.NumberOfDocuments = ret.Results.Count;
            return ret;
        }

       
        public static string getParam(string query, bool use, bool fullTextSearch)
        {
            if (!use)
            {
                return null;
            }

            if (fullTextSearch)
            {
                return "%" + query + "%";
            }
            else
            {
                return "% " + query + " %";
            }
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
