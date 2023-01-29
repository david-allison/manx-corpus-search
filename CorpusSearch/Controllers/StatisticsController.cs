using System;
using Microsoft.AspNetCore.Mvc;

namespace CorpusSearch.Controllers;

[Route("[controller]")]
public class StatisticsController
{
    private static long _documentCount;
    private static long _manxWordCount;
    
    [HttpGet]
    public CorpusStatistics GetStatistics()
    {
        return new CorpusStatistics(_documentCount, _manxWordCount);
    }

    public record CorpusStatistics(long DocumentCount, long ManxWordCount);

    public static void Init((long totalDocuments, long totalManxTerms) databaseCount)
    {
        _documentCount = databaseCount.totalDocuments;
        _manxWordCount = databaseCount.totalManxTerms;
        Console.WriteLine($"{databaseCount.totalManxTerms} in {databaseCount.totalDocuments} documents");
    }
}