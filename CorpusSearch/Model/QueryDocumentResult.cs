using System;

namespace CorpusSearch.Model
{
    public class QueryDocumentResult : Countable
    {
        public string DocumentName { get; set; }
        public string Ident { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int Count { get; set; }
        /// <summary>
        /// A sample of the first manx result.
        /// </summary>
        public string Sample { get; set; }
    }
}
