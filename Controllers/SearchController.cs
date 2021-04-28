using Codex_API.Extensions;
using Codex_API.Model;
using Codex_API.Service;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using static Codex_API.Startup;

namespace Codex_API.Controllers
{
    // TODO: Handle a search for a word at the end of a sentence
    [ApiController]
    [Route("[controller]")]
    public partial class SearchController : ControllerBase
    {
        public static string PUNCTUATION_REGEX = "[,.;!?\\-\\s]";


        private const string SEARCH_SPECIFIC_WORK = @"
select 
    translations.english as english,
    translations.manx as manx,
    translations.page as page,
    translations.notes as notes
from 
    translations 
join works on works.id = translations.work
where 
    works.ident = @workIdent 
        AND 
    ((@manx IS NOT NULL AND normalizedManx like @manx) OR (@english IS NOT NULL AND normalizedEnglish like @english))";

        private const string SEARCH_SPECIFIC_WORK_FULLTEXT = @"
select 
    translations.english as english,
    translations.manx as manx,
    translations.page as page,
    translations.notes as notes
from 
    translations 
join works on works.id = translations.work
where 
    works.ident = @workIdent 
        AND 
    ((@manx IS NOT NULL AND manx like @manx) OR (@english IS NOT NULL AND english like @english))";


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
            var workNameParam = new DynamicParameters();
            workNameParam.Add("ident", workIdent);
            var title = await conn.QuerySingleAsync<string>("SELECT name FROM works where ident = @ident", workNameParam);

            var ret = SearchWorkResult.Empty(title);
            if (string.IsNullOrWhiteSpace(query) || string.IsNullOrEmpty(workIdent) || query.Length > 30)
            {
                ret.EnrichWithTime(sw);
                return SearchWorkResult.Empty(title);
            }
            if (!manx && !english)
            {
                ret.EnrichWithTime(sw);
                return SearchWorkResult.Empty(title);
            }

            var param = new DynamicParameters();
            param.Add("manx", getParam(query, manx, fullTextSearch));
            param.Add("english", getParam(query, english, fullTextSearch));
            param.Add("workIdent", workIdent);

            var results = await conn.QueryAsync<DocumentLine>(fullTextSearch ? SEARCH_SPECIFIC_WORK_FULLTEXT : SEARCH_SPECIFIC_WORK, param);

            if (!fullTextSearch)
            {
                // Search English, and you get Manx
                var manxTranslations = EnglishDictionary.GetValueOrDefault(query.ToLowerInvariant(), new List<string>());
                var englishTranslations = ManxDictionary.GetValueOrDefault(query.ToLowerInvariant(), new List<string>());

                ret.ManxTranslations.AddRange(manxTranslations.Select(x => " " + x + " "));
                ret.EnglishTranslations.AddRange(englishTranslations.Select(x => " " + x + " "));
            }

            ret.EnrichResults(results);
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
                MaxDate = DateTimeUtil.FromYear(maxDate)
            };
            if (!searchQuery.IsValid())
            {
                ret.EnrichWithTime(sw);
                return ret;
            }

            var results = await OverviewSearchService.CorpusSearch(searchQuery); 
            

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

        public class QueryDocumentResult : Countable
        {
            public string DocumentName { get; set; }
            public string Ident { get; set; }
            public DateTime? StartDate { get; set; }
            public DateTime? EndDate { get; set; }
            public int Count { get; set; }
            /// <summary>
            /// A sample of the first manx result.
            /// </summary>
            public string Sample { get; set; }
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
