using Lucene.Net.Analysis.Util;
using Lucene.Net.Util;
using System.Globalization;
using System.IO;

namespace CorpusSearch.Dependencies.Lucene;

public sealed class ManxTokenizer(LuceneVersion matchVersion, TextReader input, bool preserveCase = false) : CharTokenizer(matchVersion, input)
{
    protected override int Normalize(int c)
    {
            // the default fields are case-folded; the cased fields back the case-sensitive option
            if (preserveCase)
            {
                return c;
            }
            char cc = (char)c;
            return char.ToLower(cc);
        }

    protected override bool IsTokenChar(int c)
    {
            char cc = (char)c;
            bool ret = char.IsLetterOrDigit(cc) || cc == '-' || cc == '\'' 
                || cc == '?'; // #15 - we need '???' or '?' as a token, but want to strip a question mark [token] + '?' i the token filter

            if (!ret)
            {
                ret = CharUnicodeInfo.GetUnicodeCategory(cc) == UnicodeCategory.NonSpacingMark;
            }

            return ret;
        }
}