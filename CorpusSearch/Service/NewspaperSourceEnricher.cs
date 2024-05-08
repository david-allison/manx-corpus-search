using CorpusSearch.Controllers;
using CorpusSearch.Model;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using static CorpusSearch.Service.IMuseumNewspaperService;

namespace CorpusSearch.Service;

public class NewspaperSourceEnricher
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


            foreach (var componentId in GetNewspaperComponents(document))
            {
                if (document.CreatedCircaEnd != document.CreatedCircaStart)
                {
                    continue;
                }
                if (document.CreatedCircaStart == null)
                {
                    continue;
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
            }

        } catch
        {

        }
    }

    /// <summary>Sometimes the Newspaper doesn't handle a component correctly, so we use an array </summary>
    private IEnumerable<string> GetNewspaperComponents(IDocument document)
    {
        try
        {
            JArray componentIds = (JArray)document.GetExtensionData("mnhNewsComponent");
            return componentIds.ToObject<List<string>>();
        } catch
        {
            string componentId = (string)document.GetExtensionData("mnhNewsComponent");
            return new[] { componentId };
        }
    }
}