using System.Collections.Generic;
using System.Linq;
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
    public IEnumerable<DictionarySummary> Get([FromQuery] string lang, [FromQuery] string word, [FromQuery] string? context = null)
    {
        return lookupService.Lookup(lang, word, context);
    }

    /// <summary>
    /// The dictionary-coverage debug view: per-token dictionary/lemma status
    /// for each posted line (the client's dictionary debug mode).
    /// </summary>
    [HttpPost("coverage")]
    public CoverageResponse Coverage([FromBody] CoverageRequest request)
    {
        // a debug endpoint, but still bounded: the client chunks its requests
        var lines = request.Lines.Take(500).ToList();
        return new CoverageResponse { Lines = lookupService.Coverage(request.Lang, lines) };
    }

    public class CoverageRequest
    {
        public required string Lang { get; set; }
        public required List<string> Lines { get; set; }
    }

    public class CoverageResponse
    {
        public required List<List<TokenCoverage>> Lines { get; set; }
    }
}
