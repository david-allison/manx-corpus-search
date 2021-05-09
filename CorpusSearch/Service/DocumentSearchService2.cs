using Codex_API.Controllers;
using Codex_API.Model;
using System;
using System.Threading.Tasks;
using static Codex_API.Controllers.SearchController;

namespace Codex_API.Service
{
    public class DocumentSearchService2
    {
        public static async Task<SearchWorkResult> SearchWork(CorpusSearchWorkQuery workQuery)
        {
            IDocument document = await WorkService.ByIdent(workQuery.Ident);
            string title = document.Name;

            SearchWorkResult ret = SearchWorkResult.Empty(title);

            if (!workQuery.IsValid())
            {
                return ret;
            }

            var results = Startup.searcher.SearchWork(workQuery.Ident, workQuery.Query, ToSearchOptions(workQuery));

            ret.EnrichResults(results.Lines);
            // Handles more than one result per document line
            ret.TotalMatches = results.TotalMatches;

            return ret;
        }

        private static SearchOptions ToSearchOptions(CorpusSearchWorkQuery workQuery)
        {
            return new SearchOptions
            {
                Type = workQuery.Manx ? SearchType.Manx : SearchType.English,
            };
        }
    }
}
