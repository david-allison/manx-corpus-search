using System.Collections.Generic;
using System.Linq;

namespace CorpusSearch.Model;

public class EmptySpanCollection
{
    private int count;
    private readonly Dictionary<int, int> docIds = new();

    // one token can satisfy a SpanOr through several of its co-positioned lemma
    // ids (an ambiguous 'voddey' is both moddey.n and foddey.a), so the raw span
    // count over-counts it; the distinct start positions count tokens
    private readonly Dictionary<int, HashSet<int>> startsByDoc = new();

    public void Increment(int docId, int start)
    {
        count++;
        var value = docIds.GetValueOrDefault(docId, 0);
        docIds[docId] = value + 1;
        if (!startsByDoc.TryGetValue(docId, out var starts))
        {
            startsByDoc[docId] = starts = [];
        }
        starts.Add(start);
    }

    public IEnumerable<(int DocId, int Count)> DistinctDocuments()
    {
        return docIds.Keys.Select(x => (Key: x, Value: docIds[x] ));
    }

    public IEnumerable<int> DistinctDocumentIds()
    {
        return docIds.Keys;
    }

    public int GetCount(int key) => docIds.GetValueOrDefault(key, 0);

    public int GetTotalCount()
    {
        return count;
    }

    /// <summary>The matched tokens in the document: distinct span starts</summary>
    public int GetDistinctCount(int key) =>
        startsByDoc.TryGetValue(key, out var starts) ? starts.Count : 0;

    /// <summary>The matched tokens over every document</summary>
    public int GetTotalDistinctCount()
    {
        return startsByDoc.Values.Sum(x => x.Count);
    }
}
