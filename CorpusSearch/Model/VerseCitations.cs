using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CorpusSearch.Model;

/// <summary>A scripture citation as printed in running text ("Jud. xii. 6"),
/// with the canonical verse key it resolves to ("judges.12.6").</summary>
public sealed record VerseCitation(string Text, string Key);

/// <summary>
/// Finds the scripture citations the printed dictionaries quote inside their
/// definition text, so the client can turn each one into a link to the verse in
/// the corpus. Only spans <see cref="ReferenceResolver.TryParseCitation"/>
/// resolves are returned: Cregeen's OED/EDD references and his Apocrypha
/// citations (Ecclesiasticus) stay plain text.
/// </summary>
public static class VerseCitations
{
    // Jud. xii. 6 / Job xxxix, 19 / 2 Sam. xxiv. 4 / Ps. 45, 12 (Kelly): the book
    // capitalized as printed (brackets restore elisions: Ecclesiast[es]), the
    // chapter roman or arabic, the verse arabic, an optional range
    private static readonly Regex CitationSpan = new(
        @"\b(?:[1-3]\s+|I{1,3}\.?\s+)?\p{Lu}[\p{L}\[\]]{1,20}\.?\s+(?:[ivxlc]+|[IVXLC]+|\d{1,3})[.,:]?\s*\d{1,3}(?:\s?[-–]\s?\d{1,3})?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>The citations in <paramref name="text"/>, one entry per distinct
    /// printed form; null when there are none (the common case, kept off the wire)</summary>
    public static List<VerseCitation>? FindAll(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        List<VerseCitation>? result = null;
        foreach (Match match in CitationSpan.Matches(text))
        {
            var reference = ReferenceResolver.TryParseCitation(match.Value);
            if (reference == null)
            {
                continue;
            }
            result ??= [];
            if (result.All(x => x.Text != match.Value))
            {
                result.Add(new VerseCitation(match.Value, reference.Key));
            }
        }
        return result;
    }
}
