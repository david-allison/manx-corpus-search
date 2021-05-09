using CorpusSearch.Model;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.TokenAttributes;
using System.Linq;

namespace CorpusSearch.Dependencies.Lucene
{
    public sealed class ManxTokenFilter : TokenFilter
    {
        private readonly ICharTermAttribute termAtt;

        public ManxTokenFilter(TokenStream input) : base(input)
        {
            this.termAtt = AddAttribute<ICharTermAttribute>();
        }

        public override bool IncrementToken()
        {
            while (this.m_input.IncrementToken())
            {
                var term = new string(this.termAtt.Buffer).Substring(0, termAtt.Length);

                var newContent = DocumentLine.NormalizeManx(term);

                termAtt.ResizeBuffer(newContent.Length);
                termAtt.SetLength(newContent.Length);
                foreach (var (c, i) in newContent.Select((x, i) => (x, i)))
                {
                    termAtt.Buffer[i] = c;
                }


                return true;
            }
            return false;
        }
    }
}
