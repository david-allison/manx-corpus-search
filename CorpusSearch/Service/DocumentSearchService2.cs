using CorpusSearch.Controllers;
using CorpusSearch.Dependencies;
using CorpusSearch.Model;
using System.Collections.Generic;
using System.Threading.Tasks;
using static CorpusSearch.Controllers.SearchController;

namespace CorpusSearch.Service
{
    public class DocumentSearchService2
    {
        private readonly WorkService workService;
        private readonly Searcher searcher;
        private readonly NewspaperSourceEnricher newspaperSourceEnricher;

        public DocumentSearchService2(WorkService workService, Searcher searcher, NewspaperSourceEnricher newspaperSourceEnricher)
        {
            this.workService = workService;
            this.searcher = searcher;
            this.newspaperSourceEnricher = newspaperSourceEnricher;
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
            ret.GoogleBooksId = document.GoogleBooksId;
            ret.Notes = document.Notes;
            ret.Original = document.Original;
            ret.Source = document.Source;

            var results = searcher.SearchWork(workQuery.Ident, workQuery.Query, ToSearchOptions(workQuery));

            newspaperSourceEnricher.Enrich(ret, document);

            ret.EnrichResults(results.Lines);
            // Handles more than one result per document line
            ret.TotalMatches = results.TotalMatches;

            return ret;
        }
        
        /// <summary>
        /// Returns all lines for a provided document
        /// </summary>
        /// <param name="ident">The ID of the document</param>
        internal List<DocumentLine> GetAllLines(string ident)
        {
            return searcher.GetAllLines(ident);
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
