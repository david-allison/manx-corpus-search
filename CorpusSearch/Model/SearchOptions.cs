namespace CorpusSearch.Model;

public class SearchOptions
{
    public SearchType Type { get; set; }
    public bool ReturnTranscriptData { get; set; }
    /// <inheritdoc cref="ScanOptions.IgnoreHyphens"/>
    public bool IgnoreHyphens { get; set; }
    /// <inheritdoc cref="ScanOptions.CaseSensitive"/>
    public bool CaseSensitive { get; set; }
}