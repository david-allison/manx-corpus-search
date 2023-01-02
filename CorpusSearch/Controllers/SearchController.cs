using CorpusSearch.Extensions;
using CorpusSearch.Model;
using CorpusSearch.Service;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CorpusSearch.Utils;

namespace CorpusSearch.Controllers
{
    // TODO: Handle a search for a word at the end of a sentence
    [ApiController]
    [Route("[controller]")]
    public partial class SearchController : ControllerBase
    {
        public static string PUNCTUATION_REGEX = "[,.;!?\\s]";
        private readonly DocumentSearchService2 documentSearchService;
        private readonly OverviewSearchService2 overviewSearchService;
        private readonly ISearchDictionary[] dictionaryServices;
        private readonly WorkService workService;

        public SearchController(DocumentSearchService2 documentSearchService, OverviewSearchService2 overviewSearchService, IEnumerable<ISearchDictionary> dictionaryServices, WorkService workService)
        {
            this.documentSearchService = documentSearchService;
            this.overviewSearchService = overviewSearchService;
            this.dictionaryServices = dictionaryServices.ToArray();
            this.workService = workService;
        }

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
                var results = Startup.ManxToEnglishDictionary.GetValueOrDefault(query, new List<string>());
                var ret = new Translations
                {
                    { "en", results }
                };
                return ret;
            }

            public static Translations FromEnglish(string query)
            {
                var results = Startup.EnglishToManxDictionary.GetValueOrDefault(query, new List<string>());
                var ret = new Translations
                {
                    { "gv", results }
                };
                return ret;
            }
        }

        public class SearchWorkResult : IResultContainer<DocumentLine>, ITimedResult
        {
            public List<DocumentLine> Results { get; set; } = new();
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
            public Dictionary<string, List<string>> DefinedInDictionaries { get; set; } = new();

            /// <summary>https uri to the file on GitHub</summary>
            /// <remarks>This views the file, as "edit" on GitHub does not handle line numbers</remarks>
            public string GitHubLink { get; set; }
            public object Notes { get; internal set; }
            public string Source { get; set; }
            public List<SourceLink> SourceLinks { get; internal set; } = new();
            

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
            AnonymousAnalytics.Track("Search Work");
            var workQuery = new CorpusSearchWorkQuery(query)
            {
                Ident = workIdent,
                Manx = manx,
                English = english,
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

            ret.GitHubLink = (await this.workService.ByIdent(workIdent))?.GetGitHubLink();
            ret.DefinedInDictionaries = DictionaryLookup(query);

            ret.EnrichWithTime(sw);
            return ret;
        }

        [HttpGet("Search/{query}")]
        public async Task<QueryDocumentSearchResult> SearchCorpus(string query, bool manx = true, bool english = true, int minDate = 1600, int maxDate = 2100)
        {
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
                MaxDate = DateTimeUtil.FromYearMax(maxDate)
            };
            if (!searchQuery.IsValid())
            {
                ret.EnrichWithTime(sw);
                return ret;
            }

            var results = await overviewSearchService.CorpusSearch(searchQuery);

            results = results.OrderBy(x => x.StartDate);
            
            if (manx)
            {
                ret.Translations = Translations.FromManx(query);
            } 
            else if (english)
            {
                ret.Translations = Translations.FromEnglish(query);
            }

            ret.DefinedInDictionaries = DictionaryLookup(query);
            ret.SetResults(results);
            ret.EnrichWithTime(sw);
            ret.NumberOfDocuments = ret.Results.Count;
            return ret;
        }

        private Dictionary<string, List<string>> DictionaryLookup(string query)
        {
            var lookup = dictionaryServices.ToDictionary(x => x.Identifier, x => x.GetSummaries(query));
            return lookup
                    .Where(x => x.Value.Any()) // where there are results
                    .ToDictionary(x => x.Key, x => x.Value.Select(x => x.Summary).ToList()); // extract the summary
        }

        public class QueryDocumentSearchResult : IResultContainer<QueryDocumentResult>, ITimedResult
        {
            public string Query { get; set; }
            public int NumberOfDocuments { get; set; }
            public List<QueryDocumentResult> Results { get; set; }
            public int NumberOfResults { get; set; }
            public string TimeTaken { get; set; }
            public Dictionary<string, List<string>> DefinedInDictionaries { get; internal set; } = new();
            public Translations Translations { get; set; } = new();
        }
    }
}
