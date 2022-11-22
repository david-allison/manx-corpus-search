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
public class DictionaryController
{
    private readonly ISearchDictionary[] dictionaryServices;
    
    public DictionaryController(IEnumerable<ISearchDictionary> dictionaryServices)
    {
        this.dictionaryServices = dictionaryServices.ToArray();
    }
    
    
    /// <summary>
    /// Returns diction
    /// </summary>
    /// <param name="lang"></param>
    /// <param name="word"></param>
    /// <returns></returns>
    [HttpGet]
    // ReSharper disable once UnusedMember.Global
    public IEnumerable<DictionarySummary> Get([FromQuery] string lang, [FromQuery] string word)
    {
        word = word.Replace(".", "").Replace(",", "").Replace("?", "").Replace(";", "")
            .Replace("(", "").Replace(")", "");
        return dictionaryServices.SelectMany(x => x.GetSummaries(word, basic: true));
    }    
}