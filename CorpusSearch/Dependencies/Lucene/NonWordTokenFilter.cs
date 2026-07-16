using Lucene.Net.Analysis;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Analysis.Util;
using Lucene.Net.Util;

namespace CorpusSearch.Dependencies.Lucene;

/// <summary>
/// Drops tokens that are not words: pure digits ("23", "1874" — verse, page
/// and year numbers) and the transcriber's ?-run illegibility markers ("?",
/// "???"). Only the statistics stream (manx_gv) runs it: both stay searchable
/// in the search fields, but count for neither the word totals, the term
/// frequency list, nor dictionary coverage (which shares IsNotAWord).
/// </summary>
public sealed class NonWordTokenFilter : FilteringTokenFilter
{
    private readonly ICharTermAttribute termAttribute;

    public NonWordTokenFilter(TokenStream input) : base(LuceneVersion.LUCENE_48, input)
    {
        termAttribute = AddAttribute<ICharTermAttribute>();
    }

    /// <summary>A number or an illegibility marker, not a word: every
    /// character an ASCII digit, or every character a question mark</summary>
    public static bool IsNotAWord(string term)
    {
        if (term.Length == 0)
        {
            return true;
        }
        var (allDigits, allMarks) = (true, true);
        foreach (var c in term)
        {
            allDigits &= c is >= '0' and <= '9';
            allMarks &= c == '?';
        }
        return allDigits || allMarks;
    }

    protected override bool Accept()
    {
        var buffer = termAttribute.Buffer;
        var (allDigits, allMarks) = (termAttribute.Length > 0, termAttribute.Length > 0);
        for (int i = 0; i < termAttribute.Length; i++)
        {
            allDigits &= buffer[i] is >= '0' and <= '9';
            allMarks &= buffer[i] == '?';
        }
        return !allDigits && !allMarks;
    }
}
