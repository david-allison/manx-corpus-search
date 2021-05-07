using Codex_API.Model;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Codex_API.Service
{
    public class OverviewSearchService2
    {
        public static Task<IEnumerable<QueryDocumentResult>> CorpusSearch(CorpusSearchQuery searchQuery)
        {
            var result = Startup.searcher.Scan(searchQuery.Query);
            return Task.FromResult((IEnumerable<QueryDocumentResult>) result.DocumentResults);
        }
    }
}
