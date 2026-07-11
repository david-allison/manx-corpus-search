#nullable disable // not yet migrated, see the .csproj
using CorpusSearch.Infrastructure;
using CorpusSearch.Service;
using CorpusSearch.Services;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>
/// URLs the SPA has no page for must 404 in production instead of serving the app
/// shell with a 200 (a "soft 404": crawlers penalise sites where junk URLs succeed).
/// </summary>
[TestFixture]
public class SpaRouteGuardTest
{
    private WorkService workService;

    [SetUp]
    public void SetUp()
    {
        workService = new WorkService();
        workService.AddWork(new OpenSourceDocument { Ident = "PargeiysCaillit", Name = "Pargeiys Caillit" });
    }

    [TestCase("/")]
    [TestCase("")]
    [TestCase("/tools/youtube")]
    // React Router matches routes case-insensitively and with a trailing slash
    [TestCase("/Tools/YouTube/")]
    [TestCase("/docs/PargeiysCaillit")]
    [TestCase("/docs/PargeiysCaillit/")]
    public void SpaPagesFallThroughToTheShell(string path)
    {
        Assert.That(SpaRouteGuard.IsSpaPage(path, workService), Is.True);
    }

    [TestCase("/docs/NoSuchDocument")]
    [TestCase("/docs/pargeiyscaillit")] // idents are case-sensitive, like the api/Metadata lookup
    [TestCase("/docs/PargeiysCaillit/extra")]
    [TestCase("/docs")]
    [TestCase("/docs/")]
    [TestCase("/this-page-definitely-does-not-exist")]
    [TestCase("/api/NoSuchEndpoint")]
    [TestCase("/tools")]
    [TestCase("/tools/youtube/extra")]
    public void UnknownPagesAre404(string path)
    {
        Assert.That(SpaRouteGuard.IsSpaPage(path, workService), Is.False);
    }
}
