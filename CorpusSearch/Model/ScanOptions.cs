using System;

namespace CorpusSearch.Model;

public class ScanOptions
{
    public static ScanOptions Default => new();

    /// <summary>
    /// Whether we want 'ç' to match 'c' (and other diacritics)
    /// </summary>
    public bool NormalizeDiacritics { get; set; } = true;

    /// <summary>
    /// Whether hyphens, spaces and joined words are interchangeable:
    /// 'lhiam-lhiat' matches 'lhiam lhiat' and 'lhiamlhiat' (and vice-versa)
    /// </summary>
    public bool IgnoreHyphens { get; set; }

    /// <summary>
    /// Whether case must match: 'Moir' does not match 'moir' (#19).
    /// Independent of <see cref="NormalizeDiacritics"/>: 'Chengey' still matches 'Çhengey'.
    /// </summary>
    public bool CaseSensitive { get; set; }
    public DateTime MaxDate { get; internal set; }
    public DateTime MinDate { get; internal set; }
    public SearchType SearchType { get; internal set; }
}