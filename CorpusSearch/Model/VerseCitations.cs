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
    private const string SpanPattern =
        @"\b(?:[1-3]\s+|I{1,3}\.?\s+)?\p{Lu}[\p{L}\[\]]{1,20}\.?\s+(?:[ivxlc]+|[IVXLC]+|\d{1,3})[.,:]?\s*\d{1,3}(?:\s?[-–]\s?\d{1,3})?";

    private const RegexOptions SpanOptions = RegexOptions.Compiled | RegexOptions.CultureInvariant;

    private static readonly Regex CitationSpan = new(SpanPattern, SpanOptions);

    // the same span with its closing period, for whole-citation removal
    // ("Rom. v. 10. as bee eh" must not leave a stray period behind)
    private static readonly Regex BareCitation = new(SpanPattern + @"\.?", SpanOptions);

    // (Rom. ii. 4) / (Isa. xlv. 24, 25.) — a parenthesized aside short enough to
    // be a citation; the citation grammar decides whether it actually is one
    private static readonly Regex ParenthesizedAside = new(
        @"\(\s*(?<inner>[^()]{4,60}?)\s*\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// <paramref name="text"/> with its mid-text scripture citations removed -
    /// parenthesized asides ("(Rom. ii. 4)" in the Homilies) and bare citations
    /// ("Rom. v. 10." in Coyrle Sodjey) alike - for the Manx-language statistics
    /// stream: the abbreviations stop counting as Manx words while the displayed
    /// and searched text keep them. Null when nothing was stripped. Ordinary
    /// parentheticals ("(myr shen)") and prose are untouched: only spans the
    /// citation grammar resolves against the canon leave.
    /// </summary>
    public static string? Strip(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        // parenthesized asides first (their inner text is the citation), then any
        // bare citation spans left in the prose
        var stripped = text.Contains('(')
            ? ParenthesizedAside.Replace(text,
                m => ReferenceResolver.TryParseCitation(m.Groups["inner"].Value) != null ? " " : m.Value)
            : text;
        stripped = BareCitation.Replace(stripped,
            m => ReferenceResolver.TryParseCitation(m.Value) != null ? " " : m.Value);
        return stripped == text ? null : stripped;
    }

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
