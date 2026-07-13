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

    /// <summary>Whether the word appears in an entry's word list (exact,
    /// case-insensitive): the fast containment test behind GetSummaries</summary>
    bool ContainsWord(string word);

    /// <summary>Every word an entry answers for: the near-match suggestion pool</summary>
    IEnumerable<string> AllWords { get; }
}

/// <summary>When a query is made, provide a short summary of the result</summary>
public class DictionarySummary
{
    public required string Summary { get; set; }
    public required string PrimaryWord { get; set; }
    /// <summary>The <see cref="ISearchDictionary.Identifier"/> of the dictionary defining the entry (#51)</summary>
    /// <remarks>Stamped by <see cref="DictionaryLookupService"/> rather than the dictionary itself</remarks>
    public string? DictionaryName { get; set; }
    /// <summary>How many root-lemma hops from the selection the entry was
    /// reached through: 0 for the selection's own entries, 1 for its root
    /// ('gheiney' -> 'deiney'), 2 for the root's root ('deiney' -> 'dooinney').
    /// The client nests each level under the previous one</summary>
    public int RootDepth { get; set; }

    /// <summary>Set on "did you mean" fallback entries only: the near spelling
    /// the entry was reached through, when nothing matched the selection itself</summary>
    public string? NearMatchOf { get; set; }
}