using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CorpusSearch.Model;
using CorpusSearch.Service;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace CorpusSearch.Controllers;

/// <summary>
/// Displays HTML documents, for archive.org and non-JS crawlers.
/// </summary>
[Route("[controller]")]
public class BrowseController(
    DocumentSearchService documentSearchService,
    WorkService workService,
    IConfiguration configuration)
    : Controller
{
    public async Task<IActionResult> Index()
    {
        ViewData["Documents"] = await workService.GetAll();
        ViewData["CanonicalUrl"] = SeoUrls.CanonicalBaseUrl(configuration, Request) + "/Browse";
        return View("~/Views/Browse/Index.cshtml");
    }

    [HttpGet("{documentId}")]
    public async Task<IActionResult> Get(string documentId)
    {
        if (!workService.HasIdent(documentId))
        {
            return Redirect("/Browse");
        }
        // the interactive app page is the indexable version of each text; this
        // server-rendered duplicate exists for crawlers that don't execute JS
        ViewData["CanonicalUrl"] = SeoUrls.CanonicalBaseUrl(configuration, Request)
                                   + "/docs/" + Uri.EscapeDataString(documentId);
        // trim the end in-case the CSV had excess blank lines
        var lines = documentSearchService.GetAllLines(documentId).TrimEnd(x => String.IsNullOrEmpty(x.English + x.Manx + x.Notes));
        var document = await workService.ByIdent(documentId);
        ViewData["Title"] = document.Name;
        ViewData["GitHubLink"] = document.GetGitHubLink();
        ViewData["DownloadText"] = document.GetDownloadTextLink();
        ViewData["DownloadMetadata"] = document.GetDownloadMetadataLink();
        ViewData["OriginalLanguage"] = document.Original;
        ViewData["docId"] = documentId;
        ViewData["lines"] = lines;
        return View("~/Views/Browse/Browse.cshtml");
    }
}

public static class Extensions {
    public static IList<T> TrimEnd<T>(this IList<T> target, Func<T, bool> toRemoveIf)
    {
        // TODO: This shouldn't mutate the input
        for (var i = target.Count - 1; i >= 0; i--)
        {
            try
            {
                if (toRemoveIf(target[i]))
                {
                    target.RemoveAt(i);
                }
            }
            catch
            {
                throw new Exception("a");
            }
            

        }
        return target;
    }
}