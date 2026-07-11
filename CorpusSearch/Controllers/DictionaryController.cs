using System.Collections.Generic;
using CorpusSearch.Service;
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
        return lookupService.Lookup(lang, word, context);
    }
}
