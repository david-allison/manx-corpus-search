using CorpusSearch.Model;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.TokenAttributes;
using System.Linq;

namespace CorpusSearch.Dependencies.Lucene;

public sealed class ManxTokenFilter : TokenFilter
{
    private readonly ICharTermAttribute termAtt;
    private readonly bool preserveCase;

    public ManxTokenFilter(TokenStream input, bool preserveCase = false) : base(input)
    {
        this.termAtt = AddAttribute<ICharTermAttribute>();
        this.preserveCase = preserveCase;
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
                newContent = DocumentLine.NormalizeManx(term.TrimEnd('?'), allowQuestionMark: true, preserveCase: preserveCase);
            }
            else
            {
                newContent = DocumentLine.NormalizeManx(term, allowQuestionMark: true, preserveCase: preserveCase);
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