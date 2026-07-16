using System.Collections.Generic;
using System.IO;
using CorpusSearch.Dependencies.Lucene;
using Lucene.Net.Analysis.TokenAttributes;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>
/// Numbers and ?-run illegibility markers are searchable but are not Manx
/// words: the statistics stream (manx_gv) drops them, the search fields keep
/// them — and a lone "?" is a real "?" token, never an empty term (#15).
/// </summary>
[TestFixture]
public class NonWordTokenFilterTest
{
    private static List<string> Terms(string field, string text)
    {
        using var analyzer = new ManxAnalyzer();
        using var stream = analyzer.GetTokenStream(field, new StringReader(text));
        var term = stream.GetAttribute<ICharTermAttribute>();
        var terms = new List<string>();
        stream.Reset();
        while (stream.IncrementToken())
        {
            terms.Add(term.ToString());
        }
        stream.End();
        return terms;
    }

    [Test]
    public void TheStatsFieldDropsNumbersAndMarkers()
    {
        Assert.That(Terms(LuceneIndex.DOCUMENT_MANX_GV, "ayns 1874 va ? cabdil 23 ???"),
            Is.EqualTo(new[] { "ayns", "va", "cabdil" }));
    }

    [Test]
    public void TheSearchFieldKeepsNumbersAndMarkers()
    {
        Assert.That(Terms(LuceneIndex.DOCUMENT_NORMALIZED_MANX, "ayns 1874 va ? cabdil ???"),
            Is.EqualTo(new[] { "ayns", "1874", "va", "?", "cabdil", "???" }));
    }

    [Test]
    public void ALoneQuestionMarkIsARealTokenNotAnEmptyTerm()
    {
        // stripping "?" to "" left 967 unsearchable empty terms in the index
        Assert.That(Terms(LuceneIndex.DOCUMENT_NORMALIZED_MANX, "cre ?"),
            Is.EqualTo(new[] { "cre", "?" }));
        // a word's trailing question mark is still punctuation
        Assert.That(Terms(LuceneIndex.DOCUMENT_NORMALIZED_MANX, "vel oo cheet?"),
            Is.EqualTo(new[] { "vel", "oo", "cheet" }));
    }

    [Test]
    public void MixedAlphanumericsAreWords()
    {
        // "u-235", "1st", "7oo" (a scan artifact) are words for the statistics
        Assert.That(Terms(LuceneIndex.DOCUMENT_MANX_GV, "u-235 1st 7oo"),
            Is.EqualTo(new[] { "u-235", "1st", "7oo" }));
        Assert.That(NonWordTokenFilter.IsNotAWord("1874"), Is.True);
        Assert.That(NonWordTokenFilter.IsNotAWord("???"), Is.True);
        Assert.That(NonWordTokenFilter.IsNotAWord(""), Is.True);
        Assert.That(NonWordTokenFilter.IsNotAWord("1st"), Is.False);
        Assert.That(NonWordTokenFilter.IsNotAWord("cre"), Is.False);
    }
}
