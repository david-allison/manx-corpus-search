using CorpusSearch.Model;
using NUnit.Framework;

namespace CorpusSearch.Test;

public class Tests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void Test1()
    {
        Assert.Pass();
    }

    [Test]
    public void TestNormalizeQuotes()
    {
        var dash = "\u2013\u2014\u2015";
        var underbar = "\u2017";
        var comma = "\u201a";
        var squote = "\u2018\u2019\u201b\u2032";
        var dquote = "\u201c\u201d\u201e\u2033";
        var ellipsis = "\u2026";
        Assert.That(new string('-', dash.Length), Is.EqualTo(DocumentLine.NormalizeManx(dash)));
        Assert.That(new string('\'', squote.Length), Is.EqualTo(DocumentLine.NormalizeManx(squote)));
        Assert.That(string.Empty, Is.EqualTo(DocumentLine.NormalizeManx(dquote)));
        Assert.That("_", Is.EqualTo(DocumentLine.NormalizeManx(underbar)));
        Assert.That(",", Is.EqualTo(DocumentLine.NormalizeManx(comma)));
        Assert.That("...", Is.EqualTo(DocumentLine.NormalizeManx(ellipsis)));
    }
}