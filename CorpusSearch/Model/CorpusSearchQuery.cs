using System;

namespace CorpusSearch.Model;

public class CorpusSearchQuery(string query)
{
    public string Query { get; } = query;

    public bool Manx { get; internal set; }
    public bool English { get; internal set; }
    public DateTime MinDate { get; internal set; }
    public DateTime MaxDate { get; internal set; }
    public bool CaseSensitive { get; internal set; }

    internal bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(Query) || Query.Length > 30)
        {
            return false;
        }
        if (!Manx && !English)
        {
            return false;
        }
        return true;
    }
}