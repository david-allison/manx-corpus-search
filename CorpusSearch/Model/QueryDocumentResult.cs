using System;
using System.Collections.Generic;

namespace CorpusSearch.Model;

public class QueryDocumentResult : Countable
{
    public required string DocumentName { get; set; }
    public required string Ident { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int Count { get; set; }

    /// <summary>Whether the matched lines of a recording say when they are spoken.
    /// Null for print, and unless the scan was asked to look
    /// (<see cref="Dependencies.Lucene.LuceneIndex.Scan"/>'s checkTranscriptTimings).</summary>
    public bool? Timed { get; internal set; }
    /// <summary>
    /// A sample of the first manx result.
    /// </summary>
    public required string Sample { get; set; }

    /// <summary>
    /// Ranges of <see cref="Sample"/> which matched the query.
    /// <code>null</code> when the match cannot be highlighted (e.g. the search was on the English text:
    /// <see cref="Sample"/> is always Manx).
    /// </summary>
    public IReadOnlyList<HighlightRange>? SampleHighlights { get; set; }
}