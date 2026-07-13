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

    /// <summary>Pronunciation recording, streamed from the defining source's site</summary>
    public string? AudioUrl { get; set; }

    /// <summary>The defining source's home page: the popup links the citation</summary>
    public string? SourceUrl { get; set; }

    /// <summary>Compact display credit for the corner control ("Spoken
    /// Dictionary"); the full <see cref="DictionaryName"/> carries the citation</summary>
    public string? SourceCredit { get; set; }

    /// <summary>The entry's word classes ("Verb", "Noun", "Adjective", ...) where the
    /// dictionary knows them: lets the root chain keep only the senses the lemma id
    /// means (row -> bee.v offers the verb 'bee', not the food)</summary>
    public List<string>? PartsOfSpeech { get; set; }

    /// <summary>The entry's full headword list where it goes beyond
    /// <see cref="PrimaryWord"/> (Kelly's 'BILL, BILLEY'): lets the client
    /// recognise a homograph headed by another spelling as the selection's own
    /// entry rather than nesting it like a root</summary>
    public List<string>? Words { get; set; }

    /// <summary>Plural forms the dictionary declares for the entry ("BILJIN"
    /// under BILLEY), rendered as structured metadata rather than definition
    /// text</summary>
    public List<string>? Plurals { get; set; }
}