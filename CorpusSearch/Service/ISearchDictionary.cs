﻿using System.Collections.Generic;

namespace CorpusSearch.Service
{
    public interface ISearchDictionary
    {
        string Identifier { get; }
        IEnumerable<DictionarySummary> GetSummaries(string query, bool basic = false);
    }

    /// <summary>When a query is made, provide a short summary of the result</summary>
    public class DictionarySummary
    {
        public string Summary { get; set; }
    }
}
