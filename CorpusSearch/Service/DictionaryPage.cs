using System.Collections.Generic;

namespace CorpusSearch.Service;

/// <summary>
/// The full dictionary page for a word (the teanglann-style view, experimental):
/// the popup's lookup re-shaped into per-dictionary groups, with the word's own
/// recording pulled out as the page control and near-match suggestions marked
/// as a tier rather than mixed in as entries.
/// </summary>
public class DictionaryPage
{
    public required string Word { get; set; }

    /// <summary>True when nothing matched the word itself and every group is a
    /// "did you mean" near spelling</summary>
    public bool IsSuggestionTier { get; set; }

    /// <summary>The word's own pronunciation recording, when a source has one</summary>
    public DictionaryPageAudio? Audio { get; set; }

    /// <summary>Whether the corpus says the word. False on a page that only a
    /// dictionary knows: there is nothing to search the corpus for, and offering
    /// it promises evidence that is not there.
    ///
    /// Null while the answer is not known yet: a phrase is answered from a read of
    /// the whole corpus (<see cref="CorpusVocabulary.ScanPhrases"/>) which runs
    /// behind the server, and for the seconds before it lands the page says so
    /// rather than claiming either way.</summary>
    public bool? Attested { get; set; }

    /// <summary>The <see cref="ISearchDictionary.Slug"/> of every dictionary with
    /// something to say about the word. The scope picker greys the rest, so that a
    /// reader can see which books answer before clicking each in turn.
    ///
    /// Whatever the page has to show, rather than
    /// <see cref="ISearchDictionary.ContainsWord"/>, which asks only whether the
    /// spelling is a headword: a page reached from an inflected form shows the
    /// root's entry, and greying the book that prints it would contradict the
    /// entry sitting under the greyed name. A book offering only a near spelling
    /// is here too — the page is not empty, and its own note says nothing was
    /// found.
    ///
    /// Stamped unscoped, whatever <c>dict</c> the page was asked for: the picker
    /// lists every dictionary, so it must know about the ones being hidden.</summary>
    public required List<string> Answering { get; set; }

    public required List<DictionaryPageGroup> Groups { get; set; }

    /// <summary>The printed word lists that name the word, if any.
    ///
    /// Not a dictionary group: a list gives a spelling and what it names, never a
    /// definition or a word class, so it is shown as a citation rather than as an
    /// entry a reader might mistake for a book's own reading.</summary>
    public List<WordListCitation> WordLists { get; set; } = [];

    /// <summary>The word's named readings, one block per lemma id the word
    /// resolves to whose sense inventory names any: what the popup picks among
    /// in context, shown here so the reader sees the fork itself (ooh: an egg /
    /// an udder). Empty for the many words with one implicit sense.</summary>
    public List<DictionaryPageSenses> Senses { get; set; } = [];
}

/// <summary>One lemma id's named readings</summary>
public class DictionaryPageSenses
{
    public required string LemmaId { get; set; }
    /// <summary>The lemma as the reader knows it ("foddey")</summary>
    public required string Lemma { get; set; }
    /// <summary>The dictionary slug whose senses these are ("cregeen"):
    /// the inventory is per book, and the strip credits its source the way
    /// every entry on the page does</summary>
    public required string Dictionary { get; set; }
    public required List<DictionaryPageSense> Readings { get; set; }
}

/// <summary>One reading: the book's own gloss for it</summary>
public class DictionaryPageSense
{
    public required string SenseId { get; set; }
    public required string Gloss { get; set; }
    /// <summary>The printed headword the gloss belongs to, for the reader who
    /// wants the entry ("dy cheilley" under cheilley)</summary>
    public required string Headword { get; set; }
}

public class DictionaryPageGroup
{
    public required string Dictionary { get; set; }

    /// <summary>The dictionary's <see cref="ISearchDictionary.Slug"/>: the client
    /// scopes to it rather than to the display name, which is prose and churns</summary>
    public string? Slug { get; set; }

    /// <summary>The defining source's home page: the group heading links the citation</summary>
    public string? SourceUrl { get; set; }

    public required List<DictionarySummary> Entries { get; set; }
}

/// <summary>
/// What one look-up of a word turns up, whoever asked: the books' entries, and
/// the printed lists that name it.
///
/// Built once by <see cref="DictionaryLookupService.Answer"/> and shared by the
/// tap popup and the word page. Add what a look-up learns here rather than to
/// either caller: the two used to assemble their own answers, and the word
/// lists reached the page while a tap on the same word said nothing was found.
/// </summary>
public class DictionaryAnswer
{
    public required List<DictionarySummary> Entries { get; init; }

    /// <summary>Kept apart from <see cref="Entries"/>: a naming is not a book's
    /// entry, and a reader must be able to see which is which</summary>
    public required List<WordListCitation> WordLists { get; init; }
}

/// <summary>A dictionary the page can be scoped to</summary>
public class DictionaryInfo
{
    public required string Slug { get; set; }
    public required string Name { get; set; }
}

public class DictionaryPageAudio
{
    public required string Url { get; set; }
    public string? Credit { get; set; }
    public string? SourceUrl { get; set; }
}
