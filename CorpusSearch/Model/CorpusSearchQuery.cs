using System;

namespace CorpusSearch.Model;

public record CorpusSearchQuery(string Query)
{
    /// <summary>Keep this in sync with `MAX_QUERY_LENGTH`</summary>
    public const int MAX_LENGTH = 100;

    public bool Manx { get; internal set; }
    public bool English { get; internal set; }
    public DateTime MinDate { get; internal set; }
    public DateTime MaxDate { get; internal set; }
    public SearchOptions Options { get; internal set; } = SearchOptions.Default;

    /// <summary>The options for the index query: a scan searches a single language (Manx preferred)</summary>
    public SearchOptions ToSearchOptions() =>
        Options with { SearchType = Manx ? SearchType.Manx : SearchType.English };

    internal bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(Query) || Query.Length > MAX_LENGTH)
        {
            return false;
        }
        if (!Manx && !English)
        {
            return false;
        }
        return true;
    }
}
