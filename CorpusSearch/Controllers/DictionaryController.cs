using System.Collections.Generic;
using System.Linq;
using CorpusSearch.Service;
using CorpusSearch.Utils;
using Microsoft.AspNetCore.Mvc;

namespace CorpusSearch.Controllers;

/// <summary>
/// Unstable API
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DictionaryController(DictionaryLookupService lookupService)
{
    /// <summary>
    /// Returns diction
    /// </summary>
    /// <param name="lang"></param>
    /// <param name="word"></param>
    /// <returns></returns>
    [HttpGet]
    public IEnumerable<DictionarySummary> Get([FromQuery] string lang, [FromQuery] string word)
    {
        var result = lookupService.Lookup(lang, word);
        AnonymousAnalytics.Track("Dictionary Lookup", new Dictionary<string, object> { ["success"] = result.Any() });
        return result;
    }
}
