using CorpusSearch.Dependencies;
using CorpusSearch.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CorpusSearch.Service;

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

    /// <summary>
    /// 'Did you mean' suggestions for a query which found nothing (#158): alternate
    /// hyphenations which do have matches, e.g. 'lum-lane (2 matches)' for 'lumlane'.
    /// </summary>
    public async Task<List<SearchSuggestion>> GetSuggestions(CorpusSearchQuery searchQuery)
    {
        var alternates = searcher.GetHyphenAlternates(searchQuery.Query, ToScanOptions(searchQuery));

        var suggestions = new List<SearchSuggestion>();
        foreach (var alternate in alternates)
        {
            int count;
            try
            {
                // the real pipeline, so the counts respect the date range
                var results = await CorpusSearch(new CorpusSearchQuery(alternate)
                {
                    Manx = searchQuery.Manx,
                    English = searchQuery.English,
                    MinDate = searchQuery.MinDate,
                    MaxDate = searchQuery.MaxDate,
                });
                count = results.Sum(x => x.Count);
            }
            catch (ArgumentException)
            {
                // an index term need not round-trip through the query parser
                continue;
            }
            if (count > 0)
            {
                suggestions.Add(new SearchSuggestion(alternate, count));
            }
        }

        return suggestions.OrderByDescending(x => x.Count).Take(3).ToList();
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
        options.IgnoreHyphens = searchQuery.IgnoreHyphens;

        return options;
    }
}