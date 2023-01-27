#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CorpusSearch.Model;
using CorpusSearch.Service;
using Microsoft.AspNetCore.Mvc;

namespace CorpusSearch.Controllers;

/// <summary>
/// Handles listing and searching for tags
/// </summary>
/// <remarks>Currently only single tags. No Binary operations</remarks>
/// <remarks>Only 1 level, not properly recursive</remarks>
[Route("[controller]")]
public class TagsController : Controller
{
    private readonly WorkService workService;

    public TagsController(WorkService workService)
    {
        this.workService = workService;
    }

    [HttpGet("All")]
    public async Task<List<Tag>> GetTags()
    {
        Predicate<IDocument> PathMatcher(string path) => doc => doc.RelativeCsvPath.Contains(path);

        string? GetField(IDocument document, string field)
        {
            var ext = document.GetAllExtensionData();
            return !ext.ContainsKey(field) ? null : ext[field].ToString();
        }

        Predicate<IDocument> HasType(string typeToFind) => doc => GetField(doc, "type")?.Contains(typeToFind, StringComparison.OrdinalIgnoreCase) ?? false;
        Predicate<IDocument> HasSource(string toFind) => doc => doc.Source?.Contains(toFind, StringComparison.OrdinalIgnoreCase) ?? false;

        var allDocuments = await workService.GetAll();
        
        var allTags = new List<Tag>
        {
            Tag.Build("Noon as Noal", PathMatcher("Noon as Noal")),
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
    }

    [HttpGet("List/{tagName}")]
    public async Task<DocumentTree?> GetDocumentsWithTag(string tagName)
    {
        Tag? t = await LookupTag(tagName);
        if (t == null)
        {
            return null;
        }

        // Bug: Documents will want reordering by 'SourceDate' in the Newspapers: 
        // God Save The King (Stowell) Mon, 01 Jan 1827 should be 1899
        var documents = (await workService.GetAll()).OrderBy(x => x.CreatedCircaStart).ToList();

        var result = DocumentTree.FromDocuments(t, documents);
        if (result == null)
        {
            return new DocumentTree(t.Name, new List<IDocument>(), new List<DocumentTree>());
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
    public static Tag Build(string name, Predicate<IDocument> matcher) => new(name, new List<Tag>(), matcher);
}