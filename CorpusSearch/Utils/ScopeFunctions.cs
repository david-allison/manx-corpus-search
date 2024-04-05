#nullable enable

using System;

namespace CorpusSearch.Utils;

public static class ScopeFunctions
{
    public static TOut? Let<TIn, TOut>(this TIn input, Func<TIn, TOut?> func)
    {
        return func(input);
    } 
    
    /// <summary>
    /// https://kotlinlang.org/docs/scope-functions.html#let
    ///
    /// Also useful shortcut for executing code only when a receiver is not null AND `?.` syntax can't be used
    /// In addition, this means x.Length is executed once in the below block
    ///
    /// string x = i % 2 == 0 ? "Branch: Even" : null;
    /// x?.Length?.Let(len => Console.WriteLine($"{i} is even. String Length: {len}"));
    /// </summary>
    /// <returns></returns>
    public static void Let<TIn>(this TIn input, Action<TIn> func)
    {
        func(input);
    } 
}