using System;

namespace CorpusSearch.Model
{
    public class CorpusSearchWorkQuery
    {
        public string Query { get; }

        public CorpusSearchWorkQuery(string query)
        {
            Query = query;
        }

        public string Ident { get; set; }
        public bool Manx { get; set; }
        public bool English { get; set; }

        internal bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(Query) || string.IsNullOrEmpty(Ident) || Query.Length > 30)
            {
                return false;
            }

            if (!Manx && !English)
            {
                return false;
            }

            return true;
        }
    }
}