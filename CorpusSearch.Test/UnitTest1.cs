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
        Assert.AreEqual(new string('-', dash.Length), DocumentLine.NormalizeManx(dash));
        Assert.AreEqual(new string('\'', squote.Length), DocumentLine.NormalizeManx(squote));
        Assert.AreEqual(string.Empty, DocumentLine.NormalizeManx(dquote));
        Assert.AreEqual("_", DocumentLine.NormalizeManx(underbar));
        Assert.AreEqual(",", DocumentLine.NormalizeManx(comma));
        Assert.AreEqual("...", DocumentLine.NormalizeManx(ellipsis));
    }
}