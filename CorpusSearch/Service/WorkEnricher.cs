using CorpusSearch.Controllers;
using CorpusSearch.Model;
using System;
using static CorpusSearch.Service.IMuseumNewspaperService;

namespace CorpusSearch.Service
{
    public class WorkEnricher
    {
        internal void Enrich(SearchController.SearchWorkResult result, IDocument document)
        {
            // currently only MNH newspaper sources, extract to a source enricher if we add more
            try
            {
                if (document.GetExtensionData("mnhNewsComponent") == null)
                {
                    return;
                }

                string componentId = (string)document.GetExtensionData("mnhNewsComponent");
                if (document.CreatedCircaEnd != document.CreatedCircaStart)
                {
                    return;
                }
                if (document.CreatedCircaStart == null)
                {
                    return;
                }

                DateTime date = document.CreatedCircaStart.Value;

                string newspaperId = IMuseumNewspaperService.ParseNewspaperId(document.Source);

                var component = NewspaperComponent.FromOrThrow(newspaperId, date, componentId);


                SourceLink link = new SourceLink
                {
                    Text = "Article",
                    Url = component.ToLocalUrl(),
                };

                result.SourceLinks.Add(link);

            } catch
            {

            }
        }
    }
}