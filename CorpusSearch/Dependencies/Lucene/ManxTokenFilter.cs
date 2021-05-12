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
            while (m_input.IncrementToken())
            {
                var term = new string(this.termAtt.Buffer).Substring(0, termAtt.Length);

                string newContent;
                // trailing ? as a question mark
                if (term.EndsWith("?") && !term.EndsWith("??"))
                {
                    newContent = DocumentLine.NormalizeManx(term.TrimEnd('?'), allowQuestionMark: true);
                }
                else
                {
                    newContent = DocumentLine.NormalizeManx(term, allowQuestionMark: true);
                }

                HandleContentChange(newContent, termAtt);

                return true;
            }
            return false;
        }

        private static void HandleContentChange(string newContent, ICharTermAttribute termAtt)
        {
            termAtt.ResizeBuffer(newContent.Length);
            termAtt.SetLength(newContent.Length);
            foreach (var (c, i) in newContent.Select((x, i) => (x, i)))
            {
                termAtt.Buffer[i] = c;
            }
        }
    }
}
