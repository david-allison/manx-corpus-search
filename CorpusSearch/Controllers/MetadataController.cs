using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using CorpusSearch.Model;
using CorpusSearch.Service;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace CorpusSearch.Controllers;

/// <summary>
/// Unstable API
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MetadataController(WorkService workService, RecentDocumentsService recentDocumentsService)
{
    /// <summary>
    /// Override for property names from <see cref="IDocument"/> when returning JSON
    /// </summary>
    private static readonly Dictionary<string, string> PropertyOverride = new()
    {
        [nameof(IDocument.Ident)] = "Identifier"
    };

    [HttpGet]
    // ReSharper disable once UnusedMember.Global
    public async Task<IDictionary<string, object>> Get([FromQuery] string docId)
    {
        var work = await workService.ByIdent(docId);

        // a copy: GetAllExtensionData is the document's live dictionary, and this
        // method adds ("created") and removes ("uploaded by") entries
        var ret = new Dictionary<string, object>(work.GetAllExtensionData());

        // feeds api/Contributions, but isn't shown on document pages
        foreach (var key in ret.Keys.Where(x => x.Equals("uploaded by", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            ret.Remove(key);
        }

        var skipCreated = work.CreatedCircaStart == work.CreatedCircaEnd;
        if (skipCreated && work.CreatedCircaStart != null)
        {
            ret["created"] = work.CreatedCircaStart.Value.ToString("yyyy-MM-dd");
        }

        // hack: JObject was not serialised correctly
        foreach (var (k,v) in ret)
        {
            if (v is JObject jObject)
            {
                // a JObject serializes to "{...}", never to "null"
                ret[k] = JsonSerializer.Deserialize<JsonObject>(jObject.ToString())!;
            }
        }

        
        foreach (var prop in typeof(IDocument).GetProperties().Where(x => x.CanRead))
        {
            var value = prop.GetValue(work);
            if (value == null || skipCreated && prop.Name is nameof(IDocument.CreatedCircaStart) or nameof(IDocument.CreatedCircaEnd)) continue;
            var outputName = PropertyOverride.GetValueOrDefault(prop.Name, prop.Name);

            outputName = char.ToLowerInvariant(outputName[0]) + outputName[1..];

            if (value is DateTime asDateTime)
            {
                value = asDateTime.ToString("yyyy-MM-dd");
            }
            
            ret[outputName] = value;
        }

        return ret;
    }

    [HttpGet("Latest/")]
    public List<RecentDocumentsService.LatestDocumentDto> LatestDocuments()
    {
        return recentDocumentsService.GetLatestDocuments();
    }
}