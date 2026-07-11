#nullable disable // not yet migrated, see the .csproj
using System.Collections.Generic;

namespace CorpusSearch.Model;

public class SearchResult
{
    public List<DocumentLine> Lines { get; set; }
    public int? TotalMatches { get; internal set; }
}