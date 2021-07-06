using CorpusSearch.Controllers;
using CorpusSearch.Dependencies;
using CorpusSearch.Model;
using System;
using System.Threading.Tasks;
using static CorpusSearch.Controllers.SearchController;

namespace CorpusSearch.Service
{
    public class DocumentSearchService2
    {
        private readonly WorkService workService;
        private readonly Searcher searcher;
        private readonly WorkEnricher enricher;

        public DocumentSearchService2(WorkService workService, Searcher searcher, WorkEnricher enricher)
        {
            this.workService = workService;
            this.searcher = searcher;
            this.enricher = enricher;
        }

        public async Task<SearchWorkResult> SearchWork(CorpusSearchWorkQuery workQuery)
        {
            IDocument document = await workService.ByIdent(workQuery.Ident);
            string title = document.Name;

            SearchWorkResult ret = SearchWorkResult.Empty(title);

            if (!workQuery.IsValid())
            {
                return ret;
            }

            ret.PdfLink = document.ExternalPdfLink;
            ret.Notes = document.Notes;
            ret.Source = document.Source;

            var results = searcher.SearchWork(workQuery.Ident, workQuery.Query, ToSearchOptions(workQuery));

            enricher.Enrich(ret, document);

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
