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
        Assert.AreEqual("abc",DocumentLine.NormalizeManx("abc"));
        string dash = "\u2013\u2014\u2015";
        string underbar ="\u2017";
        string comma ="\u201a";
        string squote ="\u2018\u2019\u201b\u2032";
        string dquote ="\u201c\u201d\u201e\u2033";
        Assert.AreEqual(new string('-',dash.Length),DocumentLine.NormalizeManx(dash));
        Assert.AreEqual(new string('\'',squote.Length),DocumentLine.NormalizeManx(squote));
        Assert.AreEqual(System.String.Empty,DocumentLine.NormalizeManx(dquote));
        Assert.AreEqual("_",DocumentLine.NormalizeManx(underbar));
        Assert.AreEqual(",",DocumentLine.NormalizeManx(comma));
        Assert.AreEqual("...",DocumentLine.NormalizeManx("\u2026"));
    }
}