using CorpusSearch.Dependencies;
using CorpusSearch.Model;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CorpusSearch.Service
{
    public class OverviewSearchService2(WorkService workService, Searcher searcher)
    {
        public async Task<IEnumerable<QueryDocumentResult>> CorpusSearch(CorpusSearchQuery searchQuery)
        {
            var result = searcher.Scan(searchQuery.Query, ToScanOptions(searchQuery));

            var docResults = result.DocumentResults;

            // TODO: This is relatively bad performance compared to performing the query in Lucene.
            var validIdents = await GetValidIdents(searchQuery);

            var validResults = docResults.Where(x => validIdents.Contains(x.Ident)).ToList();

            return validResults;
        }

        /// <summary>Returns all the identifiers which are valid for the date range</summary>
        private async Task<ISet<string>> GetValidIdents(CorpusSearchQuery searchQuery)
        {
            var results = await workService.GetIdentsBetween(searchQuery.MinDate, searchQuery.MaxDate);
            return new HashSet<string>(results);
        }

        private static ScanOptions ToScanOptions(CorpusSearchQuery searchQuery)
        {
            var options = ScanOptions.Default;

            options.MaxDate = searchQuery.MaxDate;
            options.MinDate = searchQuery.MinDate;
            options.SearchType = searchQuery.Manx ? SearchType.Manx : SearchType.English;

            return options;
        }
    }
}
