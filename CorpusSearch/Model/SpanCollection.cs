using System.Collections.Generic;
using System.Linq;

namespace CorpusSearch.Dependencies.Lucene
{
    public class EmptySpanCollection
    {
        private int count;
        private readonly ISet<int> docIds = new HashSet<int>();
        public void Increment(int docId)
        {
            count++;
            docIds.Add(docId);
        }

        public IEnumerable<int> DistinctDocuments()
        {
            return docIds;
        }

        public int GetTotalCount()
        {
            return count;
        }
    }
    
      /// <summary>
    /// A collection of token spans per document
    /// </summary>
    public class SpanCollection
    {
        private readonly Dictionary<int, Span> lookup = new();
        private readonly Dictionary<int, int> counts = new();

        public int GetCount(int docId) => counts.GetValueOrDefault(docId, 0);

        public void Add(int docId, Span span)
        {
            AddLookup(docId, span);
            Increment(docId);
        }

        private void Increment(int docId)
        {
            if (counts.ContainsKey(docId))
            {
                counts[docId] = counts[docId] + 1;
            }
            else
            {
                counts[docId] = 1;
            }
        }

        private void AddLookup(int docId, Span span)
        {
            if (lookup.ContainsKey(docId))
            {
                return;
            }
            lookup.Add(docId, span);
        }

        internal List<int> DistinctDocuments()
        {
            return counts.Keys.ToList();
        }

        internal int GetTotalCount()
        {
            return counts.Values.Sum();
        }
    }

    public class Span
    {
        public Span(int start, int end)
        {
            Start = start;
            End = end;
        }

        public int Start { get; set; }
        public int End { get; set; }
    }
}