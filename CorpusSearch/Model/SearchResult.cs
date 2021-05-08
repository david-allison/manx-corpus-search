using System.Collections.Generic;

namespace Codex_API.Model
{
    public class SearchResult
    {
        public List<DocumentLine> Lines { get; set; }
        public int TotalMatches { get; internal set; }
    }
}
