using System.Collections.Generic;
using System.Linq;
using CorpusSearch.Service;
using Microsoft.AspNetCore.Mvc;

namespace CorpusSearch.Controllers;

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
    public string Get([FromQuery] string lang, [FromQuery] string word)
    {
        word = word.Replace(".", "").Replace(",", "").Replace("?", "").Replace(";", "");
        return string.Join("", dictionaryServices.SelectMany(x => x.GetSummaries(word)).Select(x => x.Summary));
    }    
}