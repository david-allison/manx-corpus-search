﻿using CorpusSearch.Model;
using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CorpusSearch.Service
{
    public class OverviewSearchService2
    {
        public static async Task<IEnumerable<QueryDocumentResult>> CorpusSearch(CorpusSearchQuery searchQuery)
        {
            var result = Startup.searcher.Scan(searchQuery.Query, ToScanOptions(searchQuery));

            var docResults = result.DocumentResults;

            // TODO: This is relatively bad performance compared to performing the query in Lucene.
            var validIdents = await GetValidIdents(searchQuery);

            var validResults = docResults.Where(x => validIdents.Contains(x.Ident)).ToList();

            return validResults;
        }

        /// <summary>Returns all the identifiers which are valid for the date range</summary>
        private static async Task<ISet<string>> GetValidIdents(CorpusSearchQuery searchQuery)
        {
            var results = await Startup.conn.QueryAsync<string>(@"select ident from works where 
    (enddate is NULL OR enddate >= @minDate) 
    AND
    (startdate is NULL OR startdate <= @maxDate) 
                ", new { minDate = searchQuery.MinDate, maxDate = searchQuery.MaxDate });
            return new HashSet<string>(results);
        }

        private static ScanOptions ToScanOptions(CorpusSearchQuery searchQuery)
        {
            var options = ScanOptions.Default;

            options.MaxDate = searchQuery.MaxDate;
            options.MinDate = searchQuery.MinDate;
            options.SearchType = searchQuery.Manx ? SearchType.Manx : SearchType.English;

            return options;
        }
    }
}
