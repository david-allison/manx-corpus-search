using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using CorpusSearch.Controllers;
using CorpusSearch.Model;
using NUnit.Framework;

namespace CorpusSearch.Test;

public class MappedTextTest
{
    private static (int Start, int End)? ManxRange(string raw, int start, int end) =>
        NormalizationMapper.NormalizeManxMapped(raw).MapRangeToSource(start, end);

    [Test]
    public void CurlyApostropheMapsOneToOne()
    {
        var mapped = NormalizationMapper.NormalizeManxMapped("Va’n");
        Assert.That(mapped.Text, Is.EqualTo("va'n"));
        Assert.That(mapped.MapRangeToSource(0, 4), Is.EqualTo((0, 4)));
    }

    [Test]
    public void EllipsisExpandsToThreeCharacters()
    {
        var mapped = NormalizationMapper.NormalizeManxMapped("abc… def");
        Assert.That(mapped.Text, Is.EqualTo("abc... def"));
        // "def" after the 1->3 expansion
        Assert.That(mapped.MapRangeToSource(7, 10), Is.EqualTo((5, 8)));
        // all three dots map to the single ellipsis character
        Assert.That(mapped.MapRangeToSource(3, 6), Is.EqualTo((3, 4)));
    }

    [Test]
    public void RemovedBracketsShiftTheRange()
    {
        var mapped = NormalizationMapper.NormalizeManxMapped("(cre)");
        Assert.That(mapped.Text, Is.EqualTo("cre"));
        Assert.That(mapped.MapRangeToSource(0, 3), Is.EqualTo((1, 4)));
    }

    [Test]
    public void ColonIsOnlyRemovedForManx()
    {
        Assert.That(NormalizationMapper.NormalizeManxMapped("gra:").Text, Is.EqualTo("gra"));
        Assert.That(NormalizationMapper.NormalizeEnglishMapped("gra:").Text, Is.EqualTo("gra:"));
    }

    [Test]
    public void RemovedDoubleQuotesShiftTheRange()
    {
        var mapped = NormalizationMapper.NormalizeManxMapped("\"moghrey\" mie");
        Assert.That(mapped.Text, Is.EqualTo("moghrey mie"));
        Assert.That(mapped.MapRangeToSource(0, 7), Is.EqualTo((1, 8)));
    }

    [Test]
    public void TrimAndPaddingAreAccountedFor()
    {
        var mapped = NormalizationMapper.PaddedManx("  jee.  ");
        Assert.That(mapped.Text, Is.EqualTo(" jee "));
        Assert.That(mapped.MapRangeToSource(1, 4), Is.EqualTo((2, 5)));
    }

    [Test]
    public void TrailingPunctuationIsTrimmed()
    {
        // #237 - "jee." is indexed as "jee"
        var mapped = NormalizationMapper.NormalizeManxMapped("jee.");
        Assert.That(mapped.Text, Is.EqualTo("jee"));
        Assert.That(mapped.MapRangeToSource(0, 3), Is.EqualTo((0, 3)));
    }

    [Test]
    public void NonBreakingSpaceIsPunctuation()
    {
        var mapped = NormalizationMapper.NormalizeManxMapped("cre\u00A0ta");
        Assert.That(mapped.Text, Is.EqualTo("cre ta"));
        Assert.That(mapped.MapRangeToSource(4, 6), Is.EqualTo((4, 6)));
    }

    [Test]
    public void EmDashMapsOneToOne()
    {
        var mapped = NormalizationMapper.NormalizeManxMapped("cre—ta");
        Assert.That(mapped.Text, Is.EqualTo("cre-ta"));
        Assert.That(mapped.MapRangeToSource(0, 6), Is.EqualTo((0, 6)));
    }

    [Test]
    public void PaddingOnlyRangesMapToNothing()
    {
        var mapped = NormalizationMapper.PaddedManx("x");
        Assert.That(mapped.Text, Is.EqualTo(" x "));
        Assert.That(mapped.MapRangeToSource(0, 1), Is.Null);
        Assert.That(mapped.MapRangeToSource(2, 3), Is.Null);
        Assert.That(mapped.MapRangeToSource(0, 3), Is.EqualTo((0, 1)));
    }

    [Test]
    public void EmptyAndInvalidRangesMapToNothing()
    {
        var mapped = NormalizationMapper.NormalizeManxMapped("cre");
        Assert.That(mapped.MapRangeToSource(1, 1), Is.Null);
        Assert.That(mapped.MapRangeToSource(2, 1), Is.Null);
        Assert.That(NormalizationMapper.PaddedManx("").MapRangeToSource(0, 2), Is.Null);
    }

    [Test]
    public void PaddedTextMatchesTheIndexedFieldContent()
    {
        foreach (var raw in OracleStrings())
        {
            var line = new DocumentLine { Manx = raw, English = raw };
            Assert.That(NormalizationMapper.PaddedManx(raw).Text, Is.EqualTo(line.NormalizedManx), raw);
            Assert.That(NormalizationMapper.PaddedEnglish(raw).Text, Is.EqualTo(line.NormalizedEnglish), raw);
        }
    }

    [Test]
    public void MatchesLegacyNormalizationForKnownStrings()
    {
        foreach (var s in OracleStrings())
        {
            AssertMatchesLegacy(s);
        }
    }

    [Test]
    public void MatchesLegacyNormalizationForEveryChar()
    {
        var failures = new List<string>();
        for (int i = char.MinValue; i <= char.MaxValue; i++)
        {
            string s = ((char)i).ToString();
            if (DocumentLine.NormalizeManx(s) != LegacyNormalizeManx(s)
                || DocumentLine.NormalizeManx(s, allowQuestionMark: false) != LegacyNormalizeManx(s, allowQuestionMark: false)
                || DocumentLine.NormalizeEnglish(s) != LegacyNormalizeEnglish(s)
                || DocumentLine.NormalizeEnglish(s, allowQuestionMark: true) != LegacyNormalizeEnglish(s, allowQuestionMark: true))
            {
                failures.Add($"U+{i:X4}");
            }
        }
        Assert.That(failures, Is.Empty);
    }

    [Test]
    public void MatchesLegacyNormalizationForRandomStrings()
    {
        // deterministic: this is a differential test, not a fuzzer
        var random = new Random(42);
        const string alphabet =
            "abcdefghij ABC çÇáÉæßİ-'?!,.;:()\"\r\n\t\u00A0" +
            "–—―‗‘’‚‛“”„…′″😀";
        for (int i = 0; i < 10_000; i++)
        {
            var s = new StringBuilder();
            int length = random.Next(0, 40);
            for (int j = 0; j < length; j++)
            {
                s.Append(alphabet[random.Next(alphabet.Length)]);
            }
            AssertMatchesLegacy(s.ToString());
        }
    }

    [Test]
    public void MappingIsInBoundsAndMonotonic()
    {
        var random = new Random(43);
        const string alphabet = "ab (…“’:\"?!. \r\n\u00A0";
        for (int i = 0; i < 2_000; i++)
        {
            var s = new StringBuilder();
            int length = random.Next(0, 40);
            for (int j = 0; j < length; j++)
            {
                s.Append(alphabet[random.Next(alphabet.Length)]);
            }
            string raw = s.ToString();
            var mapped = NormalizationMapper.PaddedManx(raw);

            int lastStart = -1;
            for (int j = 0; j < mapped.Text.Length; j++)
            {
                var range = mapped.MapRangeToSource(j, j + 1);
                if (range == null)
                {
                    continue;
                }
                var (start, end) = range.Value;
                if (start < 0 || end <= start || end > raw.Length)
                {
                    Assert.Fail($"out of bounds: char {j} of '{mapped.Text}' -> ({start},{end}) in '{raw}'");
                }
                if (start < lastStart)
                {
                    Assert.Fail($"not monotonic: char {j} of '{mapped.Text}' -> {start} after {lastStart} in '{raw}'");
                }
                lastStart = start;
            }
        }
        Assert.Pass();
    }

    private static void AssertMatchesLegacy(string s)
    {
        Assert.That(DocumentLine.NormalizeManx(s), Is.EqualTo(LegacyNormalizeManx(s)), $"Manx: '{s}'");
        Assert.That(DocumentLine.NormalizeManx(s, allowQuestionMark: false),
            Is.EqualTo(LegacyNormalizeManx(s, allowQuestionMark: false)), $"Manx no ?: '{s}'");
        Assert.That(DocumentLine.NormalizeEnglish(s), Is.EqualTo(LegacyNormalizeEnglish(s)), $"English: '{s}'");
        Assert.That(DocumentLine.NormalizeEnglish(s, allowQuestionMark: true),
            Is.EqualTo(LegacyNormalizeEnglish(s, allowQuestionMark: true)), $"English ?: '{s}'");
    }

    private static IEnumerable<string> OracleStrings() =>
    [
        // from TestNormalizeQuotes
        "–—―", "‗", "‚", "‘’‛′", "“”„″", "…",
        // from TestNormalizeTrailingPunctuation (#237)
        "jee.", "jee,", "God.",
        // this file's mapping cases
        "Va’n", "abc… def", "(cre)", "gra:", "\"moghrey\" mie", "  jee.  ",
        "cre\u00A0ta", "cre—ta", "x", "",
        // general shapes
        "Ta çhengey aym", "cre-erbee t’ou?", "C'red t'ou gra???", "l'oie",
    ];

    // ---------------------------------------------------------------------------------------
    // The pre-refactor implementation of DocumentLine.NormalizeManx/NormalizeEnglish
    // (previously chained string extensions in NormalizationExtensions), kept verbatim as a
    // differential oracle: NormalizationMapper must produce byte-identical text, otherwise
    // the content of the search index would change.
    // ---------------------------------------------------------------------------------------

    private static string LegacyNormalizeManx(string manx, bool allowQuestionMark = true)
    {
        return LegacyRemoveColon(
                LegacyNormalizeMicrosoftWordQuotes(
                    LegacyRemoveNewLines(
                        LegacyRemovePunctuation(manx, allowQuestionMark)))
                .Replace("(", "").Replace(")", ""))
            .Replace("\"", "")
            .ToLower()
            .Trim();
    }

    private static string LegacyNormalizeEnglish(string english, bool allowQuestionMark = false)
    {
        return LegacyNormalizeMicrosoftWordQuotes(
                LegacyRemoveNewLines(
                    LegacyRemovePunctuation(english, allowQuestionMark)))
            .Replace("(", "").Replace(")", "")
            .Replace("\"", "")
            .ToLower()
            .Trim();
    }

    private static string LegacyRemovePunctuation(string target, bool allowQuestionMark)
    {
        var regexString = SearchController.PUNCTUATION_REGEX;
        if (allowQuestionMark)
        {
            regexString = regexString.Replace("?", "");
        }
        return Regex.Replace(target, regexString, " ");
    }

    private static string LegacyRemoveNewLines(string target)
    {
        return target.Replace('\r', ' ').Replace('\n', ' ');
    }

    private static string LegacyRemoveColon(string target)
    {
        return target.Replace(":", "");
    }

    private static string LegacyNormalizeMicrosoftWordQuotes(string buffer)
    {
        var qmap = new Dictionary<char, char>
        {
            { '–', '-' },
            { '—', '-' },
            { '―', '-' },
            { '‗', '_' },
            { '‘', '\'' },
            { '’', '\'' },
            { '‚', ',' },
            { '‛', '\'' },
            { '“', '\"' },
            { '”', '\"' },
            { '„', '\"' },
            { '′', '\'' },
            { '″', '\"' }
        };
        foreach (var key in qmap.Keys)
        {
            if (buffer.IndexOf(key) > -1)
            {
                buffer = buffer.Replace(key, qmap[key]);
            }
        }

        if (buffer.IndexOf('…') > -1)
        {
            buffer = buffer.Replace("…", "...");
        }

        return buffer;
    }
}
