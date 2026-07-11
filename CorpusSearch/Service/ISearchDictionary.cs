using System.Collections.Generic;

namespace CorpusSearch.Service;

public interface ISearchDictionary
{
    string Identifier { get; }
        
    /// <summary>
    /// The languages which it takes as Queries
    /// For example: Manx -> English would mean a 'gv' QueryLanguage.
    /// Some dictionaries may be bilingual
    /// </summary>
    List<string> QueryLanguages { get; }
        
    /// <summary>
    /// Whether a link to Dictionaries/XXX should be produced.
    /// </summary>
    bool LinkToDictionary { get; }
    IEnumerable<DictionarySummary> GetSummaries(string query, bool basic = false);
}

/// <summary>When a query is made, provide a short summary of the result</summary>
public class DictionarySummary
{
    public required string Summary { get; set; }
    public required string PrimaryWord { get; set; }
    /// <summary>The <see cref="ISearchDictionary.Identifier"/> of the dictionary defining the entry (#51)</summary>
    /// <remarks>Stamped by <see cref="DictionaryLookupService"/> rather than the dictionary itself</remarks>
    public string? DictionaryName { get; set; }
}