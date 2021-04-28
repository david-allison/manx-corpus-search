using Codex_API.Controllers;
using Codex_API.Model;
using Dapper;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Codex_API.Controllers.SearchController;

namespace Codex_API.Service
{
    public class OverviewSearchService
    {
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

        internal async static Task<IEnumerable<QueryDocumentResult>> CorpusSearch(CorpusSearchQuery searchQuery)
        {
            var param = new DynamicParameters();
            param.Add("manx",  SearchController.getParam(searchQuery.Query, searchQuery.Manx, searchQuery.FullText));
            param.Add("english", SearchController.getParam(searchQuery.Query, searchQuery.English, searchQuery.FullText));
            param.Add("minDate", searchQuery.MinDate);
            param.Add("maxDate", searchQuery.MaxDate);
            // on a general search - search for " " + phrase + " " in the normalized output - this ensures all full words are obtained without punctuation issues.
            // On a Fulltext search - we're not looking for words, so search the actual output.
            var results = await Startup.conn.QueryAsync<QueryDocumentResult>(searchQuery.FullText ? SEARCH_GENERAL_FULLTEXT : SEARCH_GENERAL, param);


            if (searchQuery.Manx)
            {
                EnrichWithSample(searchQuery.Query, results, searchQuery.FullText);
            }

            return results;
        }

        private static void EnrichWithSample(string query, IEnumerable<QueryDocumentResult> toEnrich, bool fullTextSearch)
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

            var results = Startup.conn.Query<(string, string)>(sql, new { ids = dict.Keys, query = getParam(query, true, fullTextSearch) });

            foreach (var v in results)
            {
                dict[v.Item2].Sample = v.Item1;
            }
        }
    }
}
