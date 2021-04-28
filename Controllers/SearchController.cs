using Codex_API.Extensions;
using Codex_API.Model;
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


        private const string SEARCH_GENERAL = @"select 
works.name as DocumentName, 
works.ident, 
works.startdate,
works.enddate,
count() as count
from 
    translations
join works on works.id = translations.work
where 
    ((@manx IS NOT NULL AND normalizedManx like @manx) OR 
    (@english IS NOT NULL AND normalizedEnglish like @english))
    AND
    (enddate is NULL OR enddate >= @minDate) 
    AND
    (startdate is NULL OR startdate <= @maxDate) 
group by work";

        private const string SEARCH_GENERAL_FULLTEXT = @"select 
works.name as DocumentName, 
works.ident, 
works.startdate,
works.enddate,
count() as count,
from 
    translations
join works on works.id = translations.work
where 
    ((@manx IS NOT NULL AND manx like @manx) OR 
    (@english IS NOT NULL AND english like @english))
    AND
    (enddate is NULL OR enddate >= @minDate) 
    AND
    (startdate is NULL OR startdate <= @maxDate) 
group by work";

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

            var param = new DynamicParameters();
            param.Add("manx", getParam(searchQuery.Query, searchQuery.Manx, searchQuery.FullText));
            param.Add("english", getParam(searchQuery.Query, searchQuery.English, searchQuery.FullText));
            param.Add("minDate", searchQuery.MinDate);
            param.Add("maxDate", searchQuery.MaxDate);
            // on a general search - search for " " + phrase + " " in the normalized output - this ensures all full words are obtained without punctuation issues.
            // On a Fulltext search - we're not looking for words, so search the actual output.
            var results = await conn.QueryAsync<QueryDocumentResult>(searchQuery.FullText ? SEARCH_GENERAL_FULLTEXT : SEARCH_GENERAL, param);

            if (searchQuery.Manx)
            {
                EnrichWithSample(searchQuery.Query, results, searchQuery.FullText);
            }
            

            ret.EnrichResults(results);
            ret.EnrichWithTime(sw);
            ret.NumberOfDocuments = ret.Results.Count;
            return ret;
        }

        private void EnrichWithSample(string query, IEnumerable<QueryDocumentResult> toEnrich, bool fullTextSearch)
        {
            try
            {
                EnrichWithSampleInternal(query, toEnrich, fullTextSearch);
            }
            catch
            {
                // TODO: Log
            }
        }

        private static void EnrichWithSampleInternal(string query, IEnumerable<QueryDocumentResult> toEnrich, bool fullTextSearch)
        {
            // This is really lazy - no point in thinking until we move to a word-based model
            string table = fullTextSearch ? "manx" : "normalizedManx";
            string sql = $@"
select
    translations.manx as manx,
    works.ident
from
    translations
join works on works.id = translations.work
where 
    works.ident in @ids 
AND
    translations.{table} like @query
group by works.ident";


            var dict = toEnrich.ToDictionary(x => x.Ident);

            var results = conn.Query<(string, string)>(sql, new { ids = dict.Keys, query = getParam(query, true, fullTextSearch) });

            foreach (var v in results)
            {
                dict[v.Item2].Sample = v.Item1;
            }
        }


        private static string getParam(string query, bool use, bool fullTextSearch)
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
