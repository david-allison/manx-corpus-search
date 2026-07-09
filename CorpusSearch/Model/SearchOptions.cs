using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CorpusSearch.Model;

/// <summary>
/// The matching options shared by every search endpoint, bound directly from the query string
/// in SearchController: public-settable properties without [BindNever] are API surface (#316).
/// </summary>
public sealed record SearchOptions
{
    public static SearchOptions Default => new();

    /// <summary>
    /// Whether we want 'ç' to match 'c' (and other diacritics)
    /// </summary>
    [BindNever]
    public bool NormalizeDiacritics { get; set; } = true;

    /// <summary>Binds ?accentSensitive=, inverse of <see cref="NormalizeDiacritics"/></summary>
    public bool AccentSensitive
    {
        get => !NormalizeDiacritics;
        set => NormalizeDiacritics = !value;
    }

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

    /// <summary>Derived from the query's Manx/English flags, never bound from the request</summary>
    [BindNever]
    public SearchType SearchType { get; internal set; }
}
