using System;
using System.Collections.Generic;

namespace Codex_API.Dependencies.Lucene
{
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