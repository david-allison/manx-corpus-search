using System;
using System.Collections.Generic;
using System.Linq;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Search.Spans;

namespace CorpusSearch.Dependencies.Lucene;

/// <summary>
/// Narrow helpers for the raw Lucene idioms <see cref="LuceneIndex"/> repeats.
/// </summary>
public static class LuceneExtensions
{
    // Stored-field access. GetField returns null when the line was indexed without the field
    // (optional fields are skipped, not stored blank: see LuceneIndex.Add), so absence folds
    // into a null return.

    /// <summary>The stored value of an optional field; null if this line doesn't have it</summary>
    public static string? GetString(this Document document, string field) =>
        document.GetField(field)?.GetStringValue();

    /// <summary>The stored value of a field every line stores</summary>
    /// <exception cref="InvalidOperationException">the field is missing: an indexing bug</exception>
    public static string RequireString(this Document document, string field) =>
        document.GetField(field)?.GetStringValue()
        ?? throw new InvalidOperationException($"line is missing the '{field}' field");

    /// <summary>The stored value of an optional int field; null if this line doesn't have it</summary>
    public static int? GetInt32(this Document document, string field) =>
        document.GetField(field)?.GetInt32Value();

    /// <summary>The stored value of an optional double field; null if this line doesn't have it</summary>
    public static double? GetDouble(this Document document, string field) =>
        document.GetField(field)?.GetDoubleValue();

    /// <summary>A date stored via ToString (see LuceneIndex.Add); null if this line doesn't have it</summary>
    public static DateTime? GetDateTime(this Document document, string field)
    {
        var value = document.GetString(field);
        return value == null ? null : DateTime.Parse(value);
    }

    // Unranked enumeration: the corpus uses Lucene as a line store with a query engine, never
    // for relevance ranking, so callers read every match rather than a scored top-K.

    /// <summary>Every docId matching <paramref name="query"/>, unranked</summary>
    public static IEnumerable<int> AllDocIds(this IndexSearcher searcher, Query query) =>
        searcher.Search(query, int.MaxValue).ScoreDocs.Select(scoreDoc => scoreDoc.Doc);

    /// <summary>The stored fields of every document matching <paramref name="query"/>, unranked</summary>
    public static IEnumerable<Document> AllDocs(this IndexSearcher searcher, Query query, ISet<string> fieldsToLoad) =>
        searcher.AllDocIds(query).Select(docId => searcher.Doc(docId, fieldsToLoad));

    /// <summary>
    /// Every span <paramref name="query"/> matches. Lucene reports spans per index leaf with
    /// leaf-relative document numbers; this walks all leaves and composes the index-wide
    /// DocId (leaf.DocBase + leaf-relative id). Start/End are token positions in the field,
    /// [Start, End). Lazy: enumerate while the reader is open.
    /// </summary>
    public static IEnumerable<(int DocId, int Start, int End)> EnumerateSpans(this SpanQuery query, IndexReader reader)
    {
        foreach (var leaf in reader.Leaves)
        {
            var spans = query.GetSpans(leaf, null, new Dictionary<Term, TermContext>());
            while (spans.MoveNext())
            {
                yield return (leaf.DocBase + spans.Doc, spans.Start, spans.End);
            }
        }
    }
}
