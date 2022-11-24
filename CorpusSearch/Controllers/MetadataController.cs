using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CorpusSearch.Model;
using CorpusSearch.Service;
using Microsoft.AspNetCore.Mvc;

namespace CorpusSearch.Controllers;

/// <summary>
/// Unstable API
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MetadataController
{
    private readonly WorkService workService;

    /// <summary>
    /// Override for property names from <see cref="IDocument"/> when returning JSON
    /// </summary>
    private static readonly Dictionary<string, string> PropertyOverride = new()
    {
        [nameof(IDocument.Ident)] = "Identifier"
    };
    
    public MetadataController(WorkService workService)
    {
        this.workService = workService;
    }

    [HttpGet]
    // ReSharper disable once UnusedMember.Global
    public async Task<IDictionary<string, object>> Get([FromQuery] string docId)
    {
        var work = await workService.ByIdent(docId);

        var ret = work.GetAllExtensionData();

        var skipCreated = work.CreatedCircaStart == work.CreatedCircaEnd;
        if (skipCreated && work.CreatedCircaStart != null)
        {
            ret["created"] = work.CreatedCircaStart?.ToString("yyyy-MM-dd");
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
}