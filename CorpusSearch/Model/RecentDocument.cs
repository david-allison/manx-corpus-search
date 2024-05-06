using System;

namespace CorpusSearch.Model;

/// <summary>A document which was uploaded recently</summary>
/// <remarks>To allow for the display of 'Recent Changes'</remarks>
public record RecentDocument(IDocument Document, DateTime ModificationTime);