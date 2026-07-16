using Lucene.Net.Analysis;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Analysis.Util;
using Lucene.Net.Util;

namespace CorpusSearch.Dependencies.Lucene;

/// <summary>
/// Drops pure-digit tokens ("23", "1874" — verse, page and year numbers).
/// Only the statistics stream (manx_gv) runs it: numbers stay searchable in
/// the search fields, but are not Manx words for the counts, the term
/// frequency list, or dictionary coverage (which shares IsPureDigits).
/// </summary>
public sealed class DigitTokenFilter : FilteringTokenFilter
{
    private readonly ICharTermAttribute termAttribute;

    public DigitTokenFilter(TokenStream input) : base(LuceneVersion.LUCENE_48, input)
    {
        termAttribute = AddAttribute<ICharTermAttribute>();
    }

    /// <summary>A number, not a word: every character an ASCII digit</summary>
    public static bool IsPureDigits(string term)
    {
        if (term.Length == 0)
        {
            return false;
        }
        foreach (var c in term)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }
        }
        return true;
    }

    protected override bool Accept()
    {
        var buffer = termAttribute.Buffer;
        for (int i = 0; i < termAttribute.Length; i++)
        {
            if (buffer[i] is < '0' or > '9')
            {
                return true;
            }
        }
        return termAttribute.Length == 0;
    }
}
