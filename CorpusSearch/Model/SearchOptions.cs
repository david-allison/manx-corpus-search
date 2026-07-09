namespace CorpusSearch.Model;

/// <summary>The matching options shared by every search endpoint (#316)</summary>
public sealed record SearchOptions
{
    public static SearchOptions Default => new();

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

    /// <summary>Derived from the query's Manx/English flags</summary>
    public SearchType SearchType { get; internal set; }
}
