using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace CorpusSearch.Controllers;

[Route("Dictionary/[controller]")]
public class CregeenController(IConfiguration configuration) : Controller
{
    public IActionResult Index()
    {
        ViewData["query"] = "A";
        ViewData["CanonicalUrl"] = SeoUrls.CanonicalBaseUrl(configuration, Request) + "/Dictionary/Cregeen";
        return View();
    }

    [HttpGet("{s}")]
    public IActionResult Get(string s)
    {
        ViewData["query"] = s;
        ViewData["CanonicalUrl"] = SeoUrls.CanonicalBaseUrl(configuration, Request)
                                   + "/Dictionary/Cregeen/" + Uri.EscapeDataString(s);
        return View("~/Views/Cregeen/Index.cshtml");
    }
}