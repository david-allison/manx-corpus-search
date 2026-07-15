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
    // assigned by SetUp before each test
    private WorkService workService = null!;

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
    [TestCase("/contributions")]
    [TestCase("/Contributions/")]
    [TestCase("/docs/PargeiysCaillit")]
    [TestCase("/docs/PargeiysCaillit/")]
    [TestCase("/dictionary")]
    [TestCase("/dictionary/billey")]
    [TestCase("/Dictionary/Billey/")]
    // the per-dictionary page: /dictionary/in/<slug>/<word>
    [TestCase("/dictionary/in/cregeen/billey")]
    [TestCase("/Dictionary/In/Cregeen/Billey/")]
    // 'in' is a Kelly headword: as one segment it is still the word page
    [TestCase("/dictionary/in")]
    // like 'in', 'browse' alone is a word to look up, not the index
    [TestCase("/dictionary/browse")]
    // the browse index: a dictionary, and optionally where to open it
    [TestCase("/dictionary/browse/cregeen")]
    [TestCase("/dictionary/browse/cregeen/aal")]
    [TestCase("/Dictionary/Browse/Cregeen/")]
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
    [TestCase("/dictionary/billey/extra")]
    [TestCase("/dictionary/in/cregeen")] // scoped route without a word
    [TestCase("/dictionary/in/cregeen/billey/extra")]
    [TestCase("/dictionary//billey")]
    [TestCase("/dictionary/browse/cregeen/aal/extra")]
    // a sub-route the SPA does not render yet: allowing one early would serve
    // the NotFound page with a 200
    [TestCase("/dictionary/lemma/dooinney")]
    public void UnknownPagesAre404(string path)
    {
        Assert.That(SpaRouteGuard.IsSpaPage(path, workService), Is.False);
    }
}
