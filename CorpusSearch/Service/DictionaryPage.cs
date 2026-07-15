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

    public required List<DictionaryPageGroup> Groups { get; set; }
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
