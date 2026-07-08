using System.Collections.Generic;
using System.Linq;
using Lucene.Net.Search.Spans;

namespace CorpusSearch.Dependencies.Lucene;

/// <summary>
/// Decomposes a rewritten <see cref="SpanQuery"/> into the sub-queries whose spans should be
/// highlighted.
///
/// A query such as <c>x and y</c> is a <see cref="SpanNearQuery"/> with unlimited slop: its
/// spans cover the whole stretch of text between the two terms, but only the terms themselves
/// should be highlighted.
/// </summary>
public static class SpanQueryHighlightExtractor
{
    /// <param name="rewritten">a query which has been rewritten against the reader, so contains
    /// no <see cref="SpanMultiTermQueryWrapper{Q}"/> (those rewrite to a <see cref="SpanOrQuery"/>)</param>
    public static List<SpanQuery> GetHighlightLeaves(SpanQuery rewritten)
    {
        var result = new List<SpanQuery>();
        Collect(rewritten, result);
        return result;
    }

    private static void Collect(SpanQuery query, List<SpanQuery> result)
    {
        if (IsSafe(query))
        {
            result.Add(query);
            return;
        }

        switch (query)
        {
            case SpanNearQuery near: // slop > 0 ('and'): highlight the clauses, not the stretch between them
                foreach (var clause in near.GetClauses())
                {
                    Collect(clause, result);
                }
                break;
            case SpanOrQuery or:
                foreach (var clause in or.GetClauses())
                {
                    Collect(clause, result);
                }
                break;
            case SpanNotQuery not: // its matches are spans of Include
                Collect(not.Include, result);
                break;
            default: // unknown: over-highlight rather than fail
                result.Add(query);
                break;
        }
    }

    /// <summary>Whether the query's spans are no wider than the matched terms themselves</summary>
    private static bool IsSafe(SpanQuery query)
    {
        return query switch
        {
            SpanTermQuery => true,
            // slop == 0: a phrase - the whole contiguous span is the match
            SpanNearQuery near => near.Slop == 0 && near.GetClauses().All(IsSafe),
            // enumerates each clause's spans individually; kept as a single leaf so that a
            // wildcard rewritten to thousands of terms only requires one walk over the index
            SpanOrQuery or => or.GetClauses().All(IsSafe),
            SpanNotQuery not => IsSafe(not.Include),
            _ => false,
        };
    }
}
