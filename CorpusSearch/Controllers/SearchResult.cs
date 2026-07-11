using CorpusSearch.Model;
using System.Collections.Generic;
using System.Linq;

namespace CorpusSearch.Controllers;

public static class ResultExtension
{
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