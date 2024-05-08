using CorpusSearch.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CorpusSearch.Controllers;

public static class ResultExtension
{
    public static void EnrichWithTime(this ITimedResult result, Stopwatch stopwatch)
    {
        result.EnrichWithTime(stopwatch.Elapsed);
    }

    public static void EnrichWithTime(this ITimedResult result, TimeSpan elapsed)
    {
        result.TimeTaken = elapsed.TotalMilliseconds + "ms";
    }

    public static void SetResults<T>(this IResultContainer<T> target, IEnumerable<T> result)
    {
        var res = result.ToList();
        int count = typeof(Countable).IsAssignableFrom(typeof(T)) ? res.Cast<Countable>().Sum(x => x.Count) : res.Count;
        target.NumberOfResults = count;
        target.Results = res;
    }

    public static void EmptyResult<T>(this IResultContainer<T> target)
    {
        target.Results = [];
        target.NumberOfResults = 0;
    }
}

public interface IResultContainer<T>
{
    public List<T> Results { get; set; }
    public int NumberOfResults { get; set; }
}

public interface ITimedResult
{
    public string TimeTaken { get; set; }
}