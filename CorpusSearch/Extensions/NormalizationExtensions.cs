using System.Text.RegularExpressions;
using CorpusSearch.Controllers;
using CorpusSearch.Service;

namespace CorpusSearch.Extensions
{
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
            if (buffer.IndexOf('\u2013') > -1) { buffer = buffer.Replace('\u2013', '-'); }
            if (buffer.IndexOf('\u2014') > -1) { buffer = buffer.Replace('\u2014', '-'); }
            if (buffer.IndexOf('\u2015') > -1) { buffer = buffer.Replace('\u2015', '-'); }
            if (buffer.IndexOf('\u2017') > -1) { buffer = buffer.Replace('\u2017', '_'); }
            if (buffer.IndexOf('\u2018') > -1) { buffer = buffer.Replace('\u2018', '\''); }
            if (buffer.IndexOf('\u2019') > -1) { buffer = buffer.Replace('\u2019', '\''); }
            if (buffer.IndexOf('\u201a') > -1) { buffer = buffer.Replace('\u201a', ','); }
            if (buffer.IndexOf('\u201b') > -1) { buffer = buffer.Replace('\u201b', '\''); }
            if (buffer.IndexOf('\u201c') > -1) { buffer = buffer.Replace('\u201c', '\"'); }
            if (buffer.IndexOf('\u201d') > -1) { buffer = buffer.Replace('\u201d', '\"'); }
            if (buffer.IndexOf('\u201e') > -1) { buffer = buffer.Replace('\u201e', '\"'); }
            if (buffer.IndexOf('\u2026') > -1) { buffer = buffer.Replace("\u2026", "..."); }
            if (buffer.IndexOf('\u2032') > -1) { buffer = buffer.Replace('\u2032', '\''); }
            if (buffer.IndexOf('\u2033') > -1) { buffer = buffer.Replace('\u2033', '\"'); }
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
}