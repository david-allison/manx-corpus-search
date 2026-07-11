#nullable disable // not yet migrated, see the .csproj
using System.Collections.Generic;
using System.Threading.Tasks;
using CorpusSearch.Controllers;
using CorpusSearch.Service;
using CorpusSearch.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace CorpusSearch.Test;

[TestFixture]
public class SeoControllerTest
{
    private const string BaseUrl = "https://corpus.example";

    private static SeoController BuildController(WorkService workService = null, string canonicalBaseUrl = BaseUrl)
    {
        var settings = new Dictionary<string, string>();
        if (canonicalBaseUrl != null)
        {
            settings["Seo:CanonicalBaseUrl"] = canonicalBaseUrl;
        }
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new SeoController(workService ?? new WorkService(), configuration);
    }

    private static WorkService WorkServiceWith(params string[] idents)
    {
        var workService = new WorkService();
        foreach (var ident in idents)
        {
            workService.AddWork(new OpenSourceDocument { Ident = ident, Name = ident });
        }
        return workService;
    }

    [Test]
    public void RobotsAllowsEverythingAndPointsToTheSitemap()
    {
        var result = BuildController().Robots();

        Assert.That(result.ContentType, Does.StartWith("text/plain"));
        // The SPA routes need their API calls crawlable for Googlebot to render them
        Assert.That(result.Content, Does.Contain("User-agent: *"));
        Assert.That(result.Content, Does.Contain("Allow: /"));
        Assert.That(result.Content, Does.Not.Contain("Disallow"));
        Assert.That(result.Content, Does.Contain($"Sitemap: {BaseUrl}/sitemap.xml"));
    }

    [Test]
    public async Task SitemapListsStaticPagesAndEveryDocument()
    {
        var controller = BuildController(WorkServiceWith("PargeiysCaillit", "Carn 130"));

        var result = await controller.Sitemap();

        Assert.That(result.ContentType, Does.StartWith("application/xml"));
        Assert.That(result.Content, Does.Contain("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">"));
        Assert.That(result.Content, Does.Contain($"<loc>{BaseUrl}/</loc>"));
        Assert.That(result.Content, Does.Contain($"<loc>{BaseUrl}/Browse</loc>"));
        Assert.That(result.Content, Does.Contain($"<loc>{BaseUrl}/Tags</loc>"));
        Assert.That(result.Content, Does.Contain($"<loc>{BaseUrl}/Dictionary/Cregeen</loc>"));
        Assert.That(result.Content, Does.Contain($"<loc>{BaseUrl}/docs/PargeiysCaillit</loc>"));
        // idents are not always URL-safe: 'Carn 130' must be escaped
        Assert.That(result.Content, Does.Contain($"<loc>{BaseUrl}/docs/Carn%20130</loc>"));
        // /Browse/{id} pages canonicalise to /docs/{id}; only the canonical belongs here
        Assert.That(result.Content, Does.Not.Contain($"{BaseUrl}/Browse/"));
    }

    [Test]
    public void WithoutConfigurationTheBaseUrlFallsBackToTheRequest()
    {
        var controller = BuildController(canonicalBaseUrl: null);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        controller.Request.Scheme = "http";
        controller.Request.Host = new HostString("localhost", 5000);

        var result = controller.Robots();

        Assert.That(result.Content, Does.Contain("Sitemap: http://localhost:5000/sitemap.xml"));
    }
}
