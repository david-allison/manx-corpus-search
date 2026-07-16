using Lucene.Net.Analysis.Util;
using Lucene.Net.Util;
using System.Globalization;
using System.IO;

namespace CorpusSearch.Dependencies.Lucene;

public sealed class ManxTokenizer(LuceneVersion matchVersion, TextReader input, bool preserveCase = false) : CharTokenizer(matchVersion, input)
{
    /// <summary>The raw texts mix typewriter (') and typographic (’ and kin) apostrophes:
    /// both are word-internal, and both token as ' so "s’liak" matches "s'liak". The
    /// indexed fields are pre-normalized (NormalizationMapper), but raw-text callers
    /// (the coverage view) and queries reach this tokenizer unnormalized.</summary>
    private static bool IsApostrophe(char c) => c is '\'' or '‘' or '’' or '‛' or '′';

    protected override int Normalize(int c)
    {
            char cc = (char)c;
            if (IsApostrophe(cc))
            {
                return '\'';
            }
            // the default fields are case-folded; the cased fields back the case-sensitive option
            if (preserveCase)
            {
                return c;
            }
            return char.ToLower(cc);
        }

    protected override bool IsTokenChar(int c)
    {
            char cc = (char)c;
            bool ret = char.IsLetterOrDigit(cc) || cc == '-' || IsApostrophe(cc)
                || cc == '?'; // #15 - we need '???' or '?' as a token, but want to strip a question mark [token] + '?' i the token filter

            if (!ret)
            {
                ret = CharUnicodeInfo.GetUnicodeCategory(cc) == UnicodeCategory.NonSpacingMark;
            }

            return ret;
        }
}