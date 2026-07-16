using System.Collections.Generic;
using System.IO;
using CorpusSearch.Dependencies.Lucene;
using Lucene.Net.Analysis.TokenAttributes;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>
/// Numbers are searchable but are not Manx words: the statistics stream
/// (manx_gv) drops pure-digit tokens, the search fields keep them.
/// </summary>
[TestFixture]
public class DigitTokenFilterTest
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
    public void TheStatsFieldDropsPureDigitTokens()
    {
        Assert.That(Terms(LuceneIndex.DOCUMENT_MANX_GV, "ayns 1874 va 23 cabdil"),
            Is.EqualTo(new[] { "ayns", "va", "cabdil" }));
    }

    [Test]
    public void TheSearchFieldKeepsNumbers()
    {
        Assert.That(Terms(LuceneIndex.DOCUMENT_NORMALIZED_MANX, "ayns 1874 va 23 cabdil"),
            Does.Contain("1874").And.Contain("23"));
    }

    [Test]
    public void MixedAlphanumericsAreNotNumbers()
    {
        // "u-235", "1st", "7oo" (a scan artifact) are words for the statistics
        Assert.That(Terms(LuceneIndex.DOCUMENT_MANX_GV, "u-235 1st 7oo"),
            Is.EqualTo(new[] { "u-235", "1st", "7oo" }));
        Assert.That(DigitTokenFilter.IsPureDigits("1874"), Is.True);
        Assert.That(DigitTokenFilter.IsPureDigits("1st"), Is.False);
        Assert.That(DigitTokenFilter.IsPureDigits(""), Is.False);
    }
}
