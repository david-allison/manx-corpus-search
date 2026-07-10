using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using CorpusSearch.Service;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace CorpusSearch.Controllers;

/// <summary>
/// robots.txt and sitemap.xml for search engines. Without these routes the SPA
/// fallback answers both URLs with the app shell (200, text/html), so crawlers can
/// only discover pages via rendered links and most of the corpus goes unindexed.
/// </summary>
public class SeoController(WorkService workService, IConfiguration configuration) : Controller
{
    private static readonly XNamespace SitemapNs = "http://www.sitemaps.org/schemas/sitemap/0.9";

    /// <summary>Server-rendered pages, readable without JavaScript</summary>
    private static readonly string[] StaticPages = ["/", "/Browse", "/Tags", "/Dictionary/Cregeen"];

    [HttpGet("/robots.txt")]
    public ContentResult Robots()
    {
        // Allow everything: Googlebot renders the SPA routes (/docs/:id), so the
        // api/search/statistics endpoints the app fetches must stay crawlable.
        return Content($"User-agent: *\nAllow: /\n\nSitemap: {CanonicalBaseUrl()}/sitemap.xml\n",
            "text/plain", Encoding.UTF8);
    }

    [HttpGet("/sitemap.xml")]
    public async Task<ContentResult> Sitemap()
    {
        var baseUrl = CanonicalBaseUrl();
        // Each text is listed as /Browse/{id}, not /docs/{id}: the sitemap should
        // carry the server-rendered version; /docs/* is an empty shell until the
        // client renders it, so it would be crawled as hundreds of identical pages.
        var documentPages = (await workService.GetAll())
            .Select(document => document.Ident)
            .OrderBy(ident => ident, StringComparer.Ordinal)
            .Select(ident => $"/Browse/{Uri.EscapeDataString(ident)}");

        var urlSet = new XElement(SitemapNs + "urlset",
            StaticPages.Concat(documentPages).Select(page =>
                new XElement(SitemapNs + "url",
                    new XElement(SitemapNs + "loc", baseUrl + page))));

        return Content("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" + urlSet,
            "application/xml", Encoding.UTF8);
    }

    /// <summary>
    /// Sitemaps and the robots.txt Sitemap directive require absolute URLs.
    /// Configured in production: the origin sits behind Cloudflare, so the request's
    /// scheme/host are the origin's, not the public https URL. Dev falls back to the request.
    /// </summary>
    private string CanonicalBaseUrl()
    {
        var configured = configuration["Seo:CanonicalBaseUrl"];
        return string.IsNullOrEmpty(configured)
            ? $"{Request.Scheme}://{Request.Host}"
            : configured.TrimEnd('/');
    }
}
