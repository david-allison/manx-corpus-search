using System.Collections.Generic;

namespace CorpusSearch.Model;

/// <summary>
/// Load-time resolution of each CSV line's effective Manx-column language: the
/// line's own value (the `ManxColumnLanguage` CSV column) if present, else the
/// manifest's collection default (else "gv").
/// </summary>
public static class DocumentLinePreparer
{
    public static void Prepare(Document document, List<DocumentLine> lines)
    {
        var defaultLanguage = NormalizeLanguage(document.ManxColumnLanguage) ?? DocumentLine.ManxLanguageCode;

        foreach (var line in lines)
        {
            line.Language = NormalizeLanguage(line.Language) ?? defaultLanguage;
        }
    }

    /// <summary>"gv", "en", ... - or null for a blank value, so `??` can apply the default</summary>
    private static string? NormalizeLanguage(string? language)
    {
        var trimmed = language?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed.ToLowerInvariant();
    }
}
