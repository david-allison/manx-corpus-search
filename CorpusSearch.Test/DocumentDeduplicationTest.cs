using System.Linq;
using CorpusSearch.Model;
using CorpusSearch.Services;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>
/// A duplicated document folder (e.g. an accidentally nested copy of OpenData) must not
/// be indexed twice: every line of the document would appear twice in search results.
/// </summary>
public class DocumentDeduplicationTest
{
    private static Document Doc(string ident, string location = "somewhere") =>
        new OpenSourceDocument { Ident = ident, LocationOnDisk = location };

    [Test]
    public void DuplicateIdentsKeepTheFirstCopy()
    {
        var first = Doc("a", "OpenData/a");
        var copy = Doc("a", "OpenData/OpenData/a");

        var result = Startup.WithoutDuplicates([first, copy, Doc("b")], log: null);

        Assert.That(result.Select(x => x.Ident), Is.EqualTo(new[] { "a", "b" }));
        Assert.That(result[0], Is.SameAs(first));
    }

    [Test]
    public void DistinctIdentsAreAllKept()
    {
        var result = Startup.WithoutDuplicates([Doc("a"), Doc("b"), Doc("c")], log: null);

        Assert.That(result, Has.Count.EqualTo(3));
    }

    [Test]
    public void DocumentsWithoutAnIdentAreKept()
    {
        var result = Startup.WithoutDuplicates([Doc(null), Doc(null)], log: null);

        Assert.That(result, Has.Count.EqualTo(2));
    }
}
