using System;

namespace CorpusSearch.Model;

public class ScanOptions
{
    public static ScanOptions Default => new();

    /// <summary>
    /// Whether we want 'ç' to match 'c' (and other diacritics)
    /// </summary>
    public bool NormalizeDiacritics { get; set; } = true;
    public DateTime MaxDate { get; internal set; }
    public DateTime MinDate { get; internal set; }
    public SearchType SearchType { get; internal set; }
}