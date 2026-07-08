namespace CorpusSearch.Model;

/// <summary>A 'did you mean' alternative for a query which found nothing (#158)</summary>
/// <param name="Query">the suggested query, e.g. 'lum-lane' when 'lumlane' was searched</param>
/// <param name="Count">the number of matches the suggestion returns</param>
public record SearchSuggestion(string Query, int Count);
