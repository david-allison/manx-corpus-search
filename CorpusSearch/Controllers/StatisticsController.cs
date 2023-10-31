using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace CorpusSearch.Controllers;

[Route("[controller]")]
public class StatisticsController
{
    private static long _documentCount;
    private static long _manxWordCount;
    private static List<(string, long)> _termFrequency;

    [HttpGet]
    public CorpusStatistics GetStatistics()
    {
        return new CorpusStatistics(_documentCount, _manxWordCount, _termFrequency.Count);
    }

    public record CorpusStatistics(long DocumentCount, long ManxWordCount, long UniqueManxWordCount);

    public static void Init((long totalDocuments, long totalManxTerms) databaseCount,
        List<(string, long)> termFrequency)
    {
        _documentCount = databaseCount.totalDocuments;
        _manxWordCount = databaseCount.totalManxTerms;
        _termFrequency = termFrequency;
        Console.WriteLine($"{databaseCount.totalManxTerms} in {databaseCount.totalDocuments} documents");
    }
}