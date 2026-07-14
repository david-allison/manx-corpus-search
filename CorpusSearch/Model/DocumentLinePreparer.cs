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
        var referenceFormats = BuildReferenceRegexes(document.InlineReferences);

        foreach (var line in lines)
        {
            line.Language = NormalizeLanguage(line.Language) ?? defaultLanguage;

            if (line.Manx == null)
            {
                continue;
            }

            if (speakerCode != null)
            {
                var match = speakerCode.Match(line.Manx);
                if (match.Success)
                {
                    // a filled Speaker column wins; the marker is dropped from the text either way
                    if (string.IsNullOrWhiteSpace(line.Speaker))
                    {
                        line.Speaker = match.Groups["code"].Value.ToUpperInvariant();
                    }
                    line.Manx = line.Manx[match.Length..];
                }
            }

            foreach (var format in referenceFormats)
            {
                var match = format.Match(line.Manx);
                if (!match.Success)
                {
                    continue;
                }
                // an explicit Reference column wins; the marker leaves the text either way
                if (string.IsNullOrWhiteSpace(line.Reference))
                {
                    line.Reference = match.Groups["ref"].Value.Trim();
                }
                line.Manx = line.Manx[match.Length..].TrimStart();
                // the translation column repeats the marker ("19 ¶ Then Joseph..."):
                // strip it there too, but the Manx side owns the Reference
                if (line.English != null)
                {
                    var english = format.Match(line.English);
                    if (english.Success)
                    {
                        line.English = line.English[english.Length..].TrimStart();
                    }
                }
                break;
            }
        }
    }

    /// <summary>"gv", "en", ... - or null for a blank value, so `??` can apply the default</summary>
    private static string? NormalizeLanguage(string? language)
    {
        var trimmed = language?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed.ToLowerInvariant();
    }

    /// <summary>
    /// A leading `CODE.` / `CODE:` / `[CODE]` (or bare `CODE`) marker, for the codes
    /// the manifest declares; null when it declares none. The code must be a whole
    /// word: code "Q" must not match "Quirk".
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

        var joined = string.Join("|", patterns);
        // the bracketed form covers dramatic speakers ([SATAN] in Pargeiys Caillit)
        return new Regex(@"^\s*(?:\[(?<code>" + joined + @")\]\s*|(?<code>" + joined + @")(?:[.:]\s*|\s+|$))",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
    }

    /// <summary>
    /// The manifest's named inline reference formats, as anchored regexes with a
    /// `ref` group. Unknown names are ignored (an old consumer must not crash on a
    /// newer manifest). Formats apply in declaration order; the first match wins.
    /// </summary>
    internal static IReadOnlyList<Regex> BuildReferenceRegexes(IReadOnlyCollection<string>? formats)
    {
        const RegexOptions options =
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant;
        var result = new List<Regex>();
        foreach (var format in formats ?? [])
        {
            switch (format?.Trim())
            {
                case "bracketed-citation":
                    // [MS 1 Thessalonians 2.16] Text...
                    result.Add(new Regex(@"^\s*\[(?<ref>[^\]]+)\]\s*", options));
                    break;
                case "bracketed-number":
                    // [3] Text...
                    result.Add(new Regex(@"^\s*\[(?<ref>\d+)\]\s*", options));
                    break;
                case "leading-number":
                    // 1 Lioar Sheeloghe... (Mian 1748, Acocrypha): a bare verse
                    // number opens the cell, optionally with a KJV-style pilcrow
                    // ("19 ¶ ") or a period ("8. ", the Phillips psalter); digits
                    // never tokenized as Manx, so this is display/metadata
                    // cleanliness
                    result.Add(new Regex(@"^\s*(?<ref>\d+)\.?(?:\s*¶)?\s+(?=\S)", options));
                    break;
                case "incipit-psalm-heading":
                    // Beatus vir qui non abiit. psal. 1. (the Phillips psalter):
                    // the whole cell is a Latin incipit heading - as reference it
                    // stays searchable while the Latin leaves the Manx stream
                    result.Add(new Regex(@"^\s*(?<ref>\S.{0,80}?\bpsal\.\s*\d+)\.?\s*$", options));
                    break;
                case "colon-verse":
                    // Genesis:1:1. Ayns y toshiaght... (the P Kelly Bible import);
                    // book names are Manx and may contain spaces (Arrane Solomon)
                    result.Add(new Regex(@"^\s*(?<ref>[^:\s][^:]{0,40}:\d+:\d+)\.?\s*", options));
                    break;
                case "heading-line":
                    // the whole cell is a chapter/psalm heading: CAB. II. / Psalm 23 / CHAPTER V
                    result.Add(new Regex(@"^\s*(?<ref>(?:cab\.?|cabdil|chapter|psalm)\s+[ivxlcdm\d]+\.?)\s*$", options));
                    break;
            }
        }
        return result;
    }
}
