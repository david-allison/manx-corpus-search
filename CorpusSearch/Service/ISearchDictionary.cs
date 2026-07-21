using System.Collections.Generic;
using CorpusSearch.Model;

namespace CorpusSearch.Service;

public interface ISearchDictionary
{
    string Identifier { get; }

    /// <summary>The dictionary's stable URL segment ("cregeen"): what
    /// /dictionary/in/{slug}/{word} scopes a page to.</summary>
    /// <remarks>Declared, never derived from <see cref="Identifier"/>: a display
    /// name may be reworded, and a link that was shared must not break with it</remarks>
    string Slug { get; }


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

    /// <summary>
    /// The dictionary's headwords, as printed and in the order printed: what the
    /// browse index lists, and what next/previous steps through.
    /// </summary>
    /// <remarks>
    /// The order is the file's, which is the book's, and it is not the order a
    /// sort would give: the books alphabetise ignoring hyphens and spaces
    /// ('aghin' before 'agh-markiagh'), so Cregeen contradicts an ordinal sort
    /// 68 times and Kelly 906. Re-sorting would scramble the book. Duplicates
    /// are real too — Kelly prints five headwords 'A' — so a headword is
    /// identified by its position here, never by its spelling.
    /// </remarks>
    IReadOnlyList<string> Headwords { get; }
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

    /// <summary>Set on a root the lemma table only reaches by rule, with no
    /// dictionary page attesting the link (see
    /// <see cref="Dependencies.Lucene.LemmaTable.IsUnverifiedLink"/>): the
    /// client marks it, so a generated guess never reads as documentation</summary>
    public bool UnverifiedLink { get; set; }

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

    /// <summary>The printed sense the clicked occurrence was read as ("long,
    /// of time" on a foddey resolved by its line): the sense sidecar's answer,
    /// stamped by the popup path only (<see cref="SenseResolver"/>). Null
    /// everywhere else — a sense is a claim about one occurrence, never about
    /// the entry.</summary>
    public string? SenseNote { get; set; }

    /// <summary>The printed grammar label of the entry ("s. m.", "s. f.",
    /// "v."): word class and gender as the dictionary abbreviates them, for
    /// display beside the headword with the expansion on hover</summary>
    public string? GrammarLabel { get; set; }

    /// <summary>Corpus evidence against the printed gender, where the
    /// gender check found any (some nouns changed gender since the book):
    /// shown as a warning on the label, and it keeps the sense heading
    /// from wearing the disputed gender</summary>
    public string? GenderNote { get; set; }

    /// <summary>Set when the looked-up word reached its entries through a
    /// Phillips 1610 spelling link: the classical spelling it stands for
    /// ("dooinney" when tapping dwyne). The client explains the hop up front
    /// so the entries never imply a dictionary lists the 1610 spelling.</summary>
    public string? PhillipsSpellingOf { get; set; }

    /// <summary>Scripture citations quoted in <see cref="Summary"/> ("Jud. xii. 6")
    /// with their canonical verse keys ("judges.12.6"): the client turns each
    /// occurrence into a link to the verse in the corpus</summary>
    public List<VerseCitation>? Citations { get; set; }
}