namespace CorpusSearch.Model;

public class CorpusSearchWorkQuery(string query)
{
    public string Query { get; } = query;

    public string Ident { get; set; }
    public bool Manx { get; set; }
    public bool English { get; set; }
    /// <inheritdoc cref="SearchOptions.IgnoreHyphens"/>
    public bool IgnoreHyphens { get; set; }
    /// <inheritdoc cref="SearchOptions.CaseSensitive"/>
    public bool CaseSensitive { get; set; }
    /// <inheritdoc cref="SearchOptions.NormalizeDiacritics"/>
    public bool NormalizeDiacritics { get; set; } = true;

    internal bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(Query) || string.IsNullOrEmpty(Ident) || Query.Length > CorpusSearchQuery.MAX_LENGTH)
        {
            return false;
        }

        return Manx || English;
    }
}