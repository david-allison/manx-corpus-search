using System.Collections.Generic;
using System.Text.RegularExpressions;
using CorpusSearch.Controllers;
using CorpusSearch.Service;

namespace CorpusSearch.Extensions;

public static class NormalizationExtensions
{
    public static string RemovePunctuation(this string target, string replacement, bool allowQuestionMark)
    {
        var regexString = SearchController.PUNCTUATION_REGEX;
        if (allowQuestionMark)
        {
            regexString = regexString.Replace("?", "");
        }

        return Regex.Replace(target, regexString, replacement);
    }

    public static string RemoveNewLines(this string target)
    {
        return target.Replace('\r', ' ').Replace('\n', ' ');
    }

    public static string RemoveDiacritics(this string target)
    {
        return DiacriticService.Replace(target);
    }

    public static string NormalizeMicrosoftWordQuotes(this string buffer)
    {
        //var quotes = new[] { ('\u2013', '-'),('\u2014', '-') };
        var qmap = new Dictionary<char, char>
        {
            { '\u2013', '-' },
            { '\u2014', '-' },
            { '\u2015', '-' },
            { '\u2017', '_' },
            { '\u2018', '\'' },
            { '\u2019', '\'' },
            { '\u201a', ',' },
            { '\u201b', '\'' },
            { '\u201c', '\"' },
            { '\u201d', '\"' },
            { '\u201e', '\"' },
            { '\u2032', '\'' },
            { '\u2033', '\"' }
        };
        foreach (var key in qmap.Keys)
            if (buffer.IndexOf(key) > -1)
                buffer = buffer.Replace(key, qmap[key]);
        if (buffer.IndexOf('\u2026') > -1)
            buffer = buffer.Replace("\u2026", "...");
        return buffer;
    }

    public static string RemoveDoubleQuotes(this string target)
    {
        return target.Replace("\"", "");
    }

    public static string RemoveBrackets(this string target)
    {
        return target.Replace("(", "").Replace(")", "");
    }

    public static string RemoveColon(this string target)
    {
        return target.Replace(":", "");
    }

}