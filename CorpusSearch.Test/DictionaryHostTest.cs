using CorpusSearch.Infrastructure;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>
/// The dictionary host's front door is "/", so its bare /dictionary
/// permanently moves there; every sub-page stays under the prefix, and the
/// corpus host is left alone entirely.
/// </summary>
[TestFixture]
public class DictionaryHostTest
{
    private static HttpRequest Request(string host, string path, string query = "")
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(host);
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query);
        return context.Request;
    }

    [TestCase("/dictionary")]
    [TestCase("/dictionary/")]
    [TestCase("/Dictionary")]
    public void TheBareLandingMovesToTheRoot(string path)
    {
        Assert.That(
            DictionaryHost.RootRedirectTarget(Request("dictionary.gaelg.im", path)),
            Is.EqualTo("/"));
    }

    [Test]
    public void TheQueryStringRidesAlong()
    {
        Assert.That(
            DictionaryHost.RootRedirectTarget(
                Request("dictionary.gaelg.im", "/dictionary", "?q=geddyn")),
            Is.EqualTo("/?q=geddyn"));
    }

    // the sub-pages stay under the prefix on both hosts, and the corpus host
    // keeps its landing where it is
    [TestCase("dictionary.gaelg.im", "/dictionary/geddyn")]
    [TestCase("dictionary.gaelg.im", "/dictionary/browse/cregeen")]
    [TestCase("dictionary.gaelg.im", "/")]
    [TestCase("dictionary.gaelg.im", "/dictionaryextra")]
    [TestCase("gaelg.im", "/dictionary")]
    [TestCase("localhost", "/dictionary")]
    public void NothingElseMoves(string host, string path)
    {
        Assert.That(DictionaryHost.RootRedirectTarget(Request(host, path)), Is.Null);
    }
}
