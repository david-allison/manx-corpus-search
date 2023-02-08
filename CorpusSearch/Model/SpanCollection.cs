using System.Collections.Generic;
using System.Linq;

namespace CorpusSearch.Model;

public class EmptySpanCollection
{
    private int count;
    private readonly Dictionary<int, int> docIds = new();
    public void Increment(int docId)
    {
        count++;
        var value = docIds.GetValueOrDefault(docId, 0);
        docIds[docId] = value + 1;
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
}