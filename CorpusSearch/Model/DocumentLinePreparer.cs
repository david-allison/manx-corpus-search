using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CorpusSearch.Model;

/// <summary>
/// Load-time cleanup of a document's CSV lines under the manifest's contract:
/// resolves each line's effective language (line value ⇒ collection default ⇒ "gv")
/// and moves inline speaker codes ("NM. Ta fys aym..." in interview transcriptions)
/// out of the Manx text into <see cref="DocumentLine.Speaker"/>, so they reach the
/// UI as speakers instead of polluting the token stream.
/// </summary>
public static class DocumentLinePreparer
{
    public static void Prepare(Document document, List<DocumentLine> lines)
    {
        var defaultLanguage = NormalizeLanguage(document.ManxColumnLanguage) ?? DocumentLine.ManxLanguageCode;
        var speakerCode = BuildSpeakerCodeRegex(document.InlineSpeakerCodes);

        foreach (var line in lines)
        {
            line.Language = NormalizeLanguage(line.Language) ?? defaultLanguage;

            if (speakerCode == null || line.Manx == null)
            {
                continue;
            }
            var match = speakerCode.Match(line.Manx);
            if (!match.Success)
            {
                continue;
            }
            // a filled Speaker column wins; the marker is dropped from the text either way
            if (string.IsNullOrWhiteSpace(line.Speaker))
            {
                line.Speaker = match.Groups["code"].Value.ToUpperInvariant();
            }
            line.Manx = line.Manx[match.Length..];
        }
    }

    /// <summary>"gv", "en", ... - or null for a blank value, so `??` can apply the default</summary>
    private static string? NormalizeLanguage(string? language)
    {
        var trimmed = language?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed.ToLowerInvariant();
    }

    /// <summary>
    /// A leading `CODE.` / `CODE:` (or bare `CODE`) marker, for the codes the manifest
    /// declares; null when it declares none. The code must be a whole word: code "Q"
    /// must not match "Quirk".
    /// </summary>
    internal static Regex? BuildSpeakerCodeRegex(IReadOnlyCollection<string>? codes)
    {
        var patterns = (codes ?? [])
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            // longest first, so a code which prefixes another cannot shadow it
            .OrderByDescending(code => code.Length)
            .Select(Regex.Escape)
            .ToList();
        if (patterns.Count == 0)
        {
            return null;
        }

        return new Regex(@"^\s*(?<code>" + string.Join("|", patterns) + @")(?:[.:]\s*|\s+|$)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
    }
}
