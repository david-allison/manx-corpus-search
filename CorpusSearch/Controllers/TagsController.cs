#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CorpusSearch.Model;
using CorpusSearch.Service;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace CorpusSearch.Controllers;

/// <summary>
/// Handles listing and searching for tags
/// </summary>
/// <remarks>Currently only single tags. No Binary operations</remarks>
/// <remarks>Only 1 level, not properly recursive</remarks>
/// <remarks>Serves both the JSON API (absolute /api/Tags routes) and the HTML pages (/Tags)</remarks>
[Route("[controller]")]
public class TagsController(WorkService workService, IConfiguration configuration) : Controller
{
    [HttpGet("/api/Tags/All")]
    public async Task<List<Tag>> GetTags()
    {
        var allDocuments = await workService.GetAll();
        
        var allTags = new List<Tag>
        {
            Tag.Build("Noon as Noal", PathMatcher("Noon as Noal")),
            Tag.Build("Audio", doc => doc.Name.StartsWith("🎥")),
            Tag.Build("Coraa ny Gael", PathMatcher("Coraa ny Gael")),
            Tag.Build("Carn", doc => doc.Name.StartsWith("Carn ")),
            new("Newspapers", 
                IMuseumNewspaperService
                    .NewspaperNames // TODO: Mona's Herald is a known duplicate
                    .Select(x => Tag.Build(x, HasSource(x)))
                    .Where(x => allDocuments.Any(x.Matches)) // SLOW: only return tags which have a document.
                    .ToList(), 
                HasType("Newspaper"))
        };
        return allTags;

        Predicate<IDocument> PathMatcher(string path) => doc => doc.RelativeCsvPath?.Contains(path) ?? false;

        string? GetField(IDocument document, string field)
        {
            var ext = document.GetAllExtensionData();
            return !ext.TryGetValue(field, out var value) ? null : value.ToString();
        }

        Predicate<IDocument> HasType(string typeToFind) => doc => GetField(doc, "type")?.Contains(typeToFind, StringComparison.OrdinalIgnoreCase) ?? false;

        /*
* Suggestions
* * Prominent Authors
  * Ned Beg
  * JJ Kneen
  * D Far
  * Stowell
  * Wilson
  * Phillips
* Methodist Materials
* Carvals
* Sermons
* Poetry
  * Religious Poetry
  * Secular Poetry
* Political
* Biographical
* Folklore
* Scientific
* Agricultural
* Fishing
* Laws
* Vignettes (Newspaper)
* Historical
* Architectural
* N/S/E/W Manx
* Legal
         */
        Predicate<IDocument> HasSource(string toFind) => doc => doc.Source?.Contains(toFind, StringComparison.OrdinalIgnoreCase) ?? false;
    }

    [HttpGet("/api/Tags/List/{tagName}")]
    public async Task<DocumentTree?> GetDocumentsWithTag(string tagName)
    {
        Tag? t = await LookupTag(tagName);
        if (t == null)
        {
            return null;
        }

        // Bug: Documents will want reordering by 'SourceDate' in the Newspapers: 
        // God Save The King (Stowell) Mon, 01 Jan 1827 should be 1899
        // explicit tie-breakers: document insertion order is not guaranteed (#303)
        var documents = (await workService.GetAll()).OrderBy(x => x.CreatedCircaStart)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Ident, StringComparer.Ordinal)
            .ToList();

        var result = DocumentTree.FromDocuments(t, documents);
        if (result == null)
        {
            return new DocumentTree(t.Name, [], []);
        }

        return result;
    }

    private async Task<Tag?> LookupTag(string tagName) => await LookupTag(tagName, await GetTags());

    private async Task<Tag?> LookupTag(string tagName, List<Tag> tags)
    {
        if (!tagName.Contains("::"))
        {
            return tags.Find(x => x.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase));
        }

        var parentTag = await LookupTag(tagName.Split("::").First());
        if (parentTag == null)
        {
            return null;
        }

        var subTag = string.Join("::", tagName.Split("::").Skip(1));
        return await LookupTag(subTag, parentTag.Children); // note: this should not need to be awaitable - no need to worry too much
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        ViewData["CanonicalUrl"] = SeoUrls.CanonicalBaseUrl(configuration, Request) + "/Tags";
        return View("~/Views/Browse/Tags.cshtml", new TagListModel(
            Title: "Category List",
            Tags: await GetTags()
        ));
    }

    [HttpGet("{tag}")]
    public async Task<IActionResult> Get(string tag)
    {
        var result = await GetDocumentsWithTag(tag);
        if (result == null)
        {
            return await Get();
        }
        ViewData["CanonicalUrl"] = SeoUrls.CanonicalBaseUrl(configuration, Request)
                                   + "/Tags/" + Uri.EscapeDataString(tag);
        return View("~/Views/Browse/ViewTag.cshtml", result);
    }
}

public record TagListModel(string Title, List<Tag> Tags);

public record DocumentTree(string Title, List<IDocument> Documents, List<DocumentTree> Children)
{
    public static DocumentTree? FromDocuments(Tag t, List<IDocument> documents)
    {
        var childDocuments = t.Children.Select(x => FromDocuments(x, documents)).Where(x => x != null).Select(x => x!).ToList();
        var toExclude = childDocuments.SelectMany(x => x.Documents)
            .Concat(documents.Where(x => !t.Matches(x))) // and exclude the ones where the tags don't match 
            .ToList();
        if (documents.Count == toExclude.Count && !childDocuments.Any())
        {
            return null;
        }

        var toInclude = documents.Except(toExclude).ToList();
        
        return new DocumentTree(t.Name, toInclude, childDocuments);
    }
}

public record Tag(string Name, List<Tag> Children, [property: JsonIgnore] Predicate<IDocument> Matcher)
{
    public bool Matches(IDocument document) => Matcher(document) || Children.Any(x => x.Matches(document));
    public static Tag Build(string name, Predicate<IDocument> matcher) => new(name, [], matcher);
}