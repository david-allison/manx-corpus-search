using Lucene.Net.Analysis.Util;
using Lucene.Net.Util;
using System.IO;
using System.Linq;

namespace Codex_API.Dependencies.Lucene
{
    public sealed class ManxTokenizer : CharTokenizer
    {

        public ManxTokenizer(LuceneVersion matchVersion, TextReader input) : base(matchVersion, input)
        {
        }

        protected override int Normalize(int c)
        {
            // TODO: See comment on test 'SearchIsNotCaseSensitive'
            char cc = (char)c;
            return char.ToLower(cc);
        }

        protected override bool IsTokenChar(int c)
        {
            char cc = (char)c;
            bool ret = char.IsLetterOrDigit(cc) || cc == '-' || cc == '\'';
            return ret;
        }
    }
}
