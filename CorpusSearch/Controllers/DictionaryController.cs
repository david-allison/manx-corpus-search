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
    /// <param name="context">optional: the text surrounding the word, used to match phrases/idioms</param>
    /// <returns></returns>
    [HttpGet]
    public IEnumerable<DictionarySummary> Get([FromQuery] string lang, [FromQuery] string word, [FromQuery] string context = null)
    {
        var result = lookupService.Lookup(lang, word, context);
        AnonymousAnalytics.Track("Dictionary Lookup", new Dictionary<string, object>
        {
            ["success"] = result.Any(),
            ["hasContext"] = context != null,
        });
        return result;
    }
}
