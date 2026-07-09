namespace CorpusSearch.Model;

public class CorpusSearchWorkQuery(string query)
{
    public string Query { get; } = query;

    public string Ident { get; set; }
    public bool Manx { get; set; }
    public bool English { get; set; }
    public SearchOptions Options { get; set; } = SearchOptions.Default;

    /// <summary>The options for the index query: a search targets a single language (Manx preferred)</summary>
    public SearchOptions ToSearchOptions() =>
        Options with { SearchType = Manx ? SearchType.Manx : SearchType.English };

    internal bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(Query) || string.IsNullOrEmpty(Ident) || Query.Length > CorpusSearchQuery.MAX_LENGTH)
        {
            return false;
        }

        return Manx || English;
    }
}
