using System;

namespace CorpusSearch.Model;

public class CorpusSearchQuery(string query)
{
    /// <summary>Keep this in sync with `MAX_QUERY_LENGTH`</summary>
    public const int MAX_LENGTH = 100;

    public string Query { get; } = query;

    public bool Manx { get; internal set; }
    public bool English { get; internal set; }
    public DateTime MinDate { get; internal set; }
    public DateTime MaxDate { get; internal set; }
    public bool CaseSensitive { get; internal set; }
    /// <inheritdoc cref="ScanOptions.IgnoreHyphens"/>
    public bool IgnoreHyphens { get; internal set; }

    internal bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(Query) || Query.Length > MAX_LENGTH)
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