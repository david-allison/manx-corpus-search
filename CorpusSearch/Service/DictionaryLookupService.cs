using System.Collections.Generic;
using System.Linq;

namespace CorpusSearch.Service;

/// <summary>
/// Resolves a user's selection to summaries from the dictionaries which handle the query language
/// </summary>
public class DictionaryLookupService(IEnumerable<ISearchDictionary> dictionaryServices)
{
    private readonly ISearchDictionary[] dictionaryServices = dictionaryServices.ToArray();

    public List<DictionarySummary> Lookup(string lang, string selection)
    {
        selection = selection.Replace(".", "").Replace(",", "").Replace("?", "").Replace(";", "")
            .Replace("(", "").Replace(")", "");
        return dictionaryServices
            .Where(x => x.QueryLanguages.Contains(lang))
            .SelectMany(x => x.GetSummaries(selection, basic: true))
            .ToList();
    }
}
