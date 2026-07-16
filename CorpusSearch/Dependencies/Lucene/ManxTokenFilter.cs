using CorpusSearch.Model;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.TokenAttributes;
using System.Linq;

namespace CorpusSearch.Dependencies.Lucene;

public sealed class ManxTokenFilter : TokenFilter
{
    private readonly ICharTermAttribute termAtt;
    private readonly IPositionIncrementAttribute posIncrAtt;
    private readonly bool preserveCase;

    public ManxTokenFilter(TokenStream input, bool preserveCase = false) : base(input)
    {
        this.termAtt = AddAttribute<ICharTermAttribute>();
        this.posIncrAtt = AddAttribute<IPositionIncrementAttribute>();
        this.preserveCase = preserveCase;
    }

    public override bool IncrementToken()
    {
        int skippedPositions = 0;
        while (m_input.IncrementToken())
        {
            var term = new string(this.termAtt.Buffer).Substring(0, termAtt.Length);

            string newContent;
            // a trailing ? is the line's punctuation, not part of the word — but a
            // bare ?-run ("?", "???") is the transcriber's illegibility marker and
            // stays a token (#15). Stripping a lone "?" to "" used to leave 967
            // empty terms in the index.
            if (term.EndsWith("?") && !term.EndsWith("??") && term.TrimEnd('?').Length > 0)
            {
                newContent = DocumentLine.NormalizeManx(term.TrimEnd('?'), allowQuestionMark: true, preserveCase: preserveCase);
            }
            else
            {
                newContent = DocumentLine.NormalizeManx(term, allowQuestionMark: true, preserveCase: preserveCase);
            }

            if (newContent.Length == 0)
            {
                // nothing searches for an empty term: never emit one, but keep
                // its position so phrase queries don't tighten across the gap
                skippedPositions += posIncrAtt.PositionIncrement;
                continue;
            }

            HandleContentChange(newContent, termAtt);
            posIncrAtt.PositionIncrement += skippedPositions;

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