namespace CorpusSearch.Model;

/// <summary>
/// A range of a returned (raw, non-normalized) text line which matched the query:
/// character offsets, <see cref="End"/> exclusive
/// </summary>
/// <example>
/// Searching for <c>chengey</c> matches the line <c>"Ta çhengey aym"</c> (the index is
/// diacritic-folded, so the literal query does not occur in the raw text). The result is
/// <c>new HighlightRange(3, 10)</c>: <c>line[3..10]</c> is <c>"çhengey"</c> - the text to highlight.
/// </example>
public record HighlightRange(int Start, int End);
