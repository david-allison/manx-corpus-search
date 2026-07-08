using System.Collections.Generic;
using System.Text.RegularExpressions;
using CorpusSearch.Controllers;

namespace CorpusSearch.Model;

/// <summary>
/// Mapping-aware implementation of <see cref="DocumentLine.NormalizeManx"/> and
/// <see cref="DocumentLine.NormalizeEnglish"/>: produces the same text, plus a map from each
/// normalized character back to the input character which produced it, so that match offsets
/// in the (indexed) normalized text can be converted to offsets in the original text.
/// </summary>
/// <remarks>
/// Each step replicates one of the string extensions previously chained in
/// <c>NormalizationExtensions</c>, in the same order. All of those operations are per-character
/// (RemovePunctuation is a single-character class), so each is expressible as
/// <see cref="MappedText.MapChars"/>.
/// </remarks>
public static class NormalizationMapper
{
    // PUNCTUATION_REGEX, with and without the '?' which RemovePunctuation strips from the
    // pattern when allowQuestionMark is set
    private static readonly Regex Punctuation = new(SearchController.PUNCTUATION_REGEX, RegexOptions.Compiled);
    private static readonly Regex PunctuationExceptQuestionMark =
        new(SearchController.PUNCTUATION_REGEX.Replace("?", ""), RegexOptions.Compiled);

    /// <summary>NormalizeMicrosoftWordQuotes as a per-character map</summary>
    private static readonly Dictionary<char, string> MicrosoftWordQuotes = new()
    {
        ['–'] = "-",
        ['—'] = "-",
        ['―'] = "-",
        ['‗'] = "_",
        ['‘'] = "'",
        ['’'] = "'",
        ['‚'] = ",",
        ['‛'] = "'",
        ['“'] = "\"",
        ['”'] = "\"",
        ['„'] = "\"",
        ['′'] = "'",
        ['″'] = "\"",
        ['…'] = "...",
    };

    public static MappedText NormalizeManxMapped(string manx, bool allowQuestionMark = true) =>
        Normalize(manx, allowQuestionMark, removeColon: true);

    public static MappedText NormalizeEnglishMapped(string english, bool allowQuestionMark = false) =>
        Normalize(english, allowQuestionMark, removeColon: false);

    /// <summary>Equivalent of <see cref="DocumentLine.NormalizedManx"/>: the indexed field text</summary>
    public static MappedText PaddedManx(string manx) => NormalizeManxMapped(manx).Pad(" ", " ");

    /// <summary>Equivalent of <see cref="DocumentLine.NormalizedEnglish"/>: the indexed field text</summary>
    public static MappedText PaddedEnglish(string english) => NormalizeEnglishMapped(english).Pad(" ", " ");

    private static MappedText Normalize(string text, bool allowQuestionMark, bool removeColon)
    {
        var mapped = MappedText.Identity(text)
            // RemovePunctuation
            .MapChars(c => IsPunctuation(c, allowQuestionMark) ? " " : c.ToString())
            // RemoveNewLines
            .MapChars(c => c is '\r' or '\n' ? " " : c.ToString())
            // NormalizeMicrosoftWordQuotes
            .MapChars(c => MicrosoftWordQuotes.GetValueOrDefault(c) ?? c.ToString())
            // RemoveBrackets, RemoveColon (Manx only), RemoveDoubleQuotes
            .MapChars(c => c == '(' || c == ')' || c == '"' || (removeColon && c == ':') ? "" : c.ToString());
        return ToLower(mapped).Trim();
    }

    private static bool IsPunctuation(char c, bool allowQuestionMark)
    {
        var regex = allowQuestionMark ? PunctuationExceptQuestionMark : Punctuation;
        System.ReadOnlySpan<char> span = [c];
        return regex.IsMatch(span);
    }

    private static MappedText ToLower(MappedText mapped)
    {
        // .NET uses length-preserving case mappings; fall back per-char if that ever changes
        var lowered = mapped.Text.ToLower();
        return lowered.Length == mapped.Text.Length
            ? mapped.ReplaceTextSameLength(lowered)
            : mapped.MapChars(c => c.ToString().ToLower());
    }
}
