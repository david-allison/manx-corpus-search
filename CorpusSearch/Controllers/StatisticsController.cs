using System;
using System.Collections.Generic;
using System.Linq;
using CorpusSearch.Service;
using Microsoft.AspNetCore.Mvc;

namespace CorpusSearch.Controllers;

[Route("[controller]")]
public class StatisticsController : Controller
{
    private static long _documentCount;
    private static long _manxWordCount;
    private static List<TermFrequency> _termFrequency;

    [HttpGet]
    public CorpusStatistics GetStatistics()
    {
        return new CorpusStatistics(_documentCount, _manxWordCount, _termFrequency.Count);
    }

    public record CorpusStatistics(long DocumentCount, long ManxWordCount, long UniqueManxWordCount);

    [HttpGet("TermFrequency/{language}")]
    public FileResult ManxTermFrequency([FromRoute] string language)
    {
        if (language != "gv")
        {
            throw new NotImplementedException($"Unhandled language: {language}");
        }
        return this.ExportCsv(_termFrequency, "TermFrequency.csv");
    }

    public static void Init((long totalDocuments, long totalManxTerms) databaseCount,
        List<(string, long)> termFrequency)
    {
        _documentCount = databaseCount.totalDocuments;
        _manxWordCount = databaseCount.totalManxTerms;
        _termFrequency = termFrequency.Select(x => new TermFrequency()
        {
            Term = x.Item1,
            Frequency = x.Item2
        }).OrderByDescending(x => x.Frequency).ToList();
        Console.WriteLine($"{databaseCount.totalManxTerms} in {databaseCount.totalDocuments} documents");
    }
    
    record TermFrequency
    {
        public string Term { get; init; }
        public long Frequency { get; init; }
    }
}