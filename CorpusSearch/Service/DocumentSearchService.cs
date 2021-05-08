﻿using Codex_API.Controllers;
using Codex_API.Model;
using Dapper;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using static Codex_API.Controllers.SearchController;

namespace Codex_API.Service
{
    public class DocumentSearchService
    {
        private const string SEARCH_SPECIFIC_WORK = @"
select 
    translations.english as english,
    translations.manx as manx,
    translations.page as page,
    translations.notes as notes
from 
    translations 
join works on works.id = translations.work
where 
    works.ident = @workIdent 
        AND 
    ((@manx IS NOT NULL AND normalizedManx like @manx) OR (@english IS NOT NULL AND normalizedEnglish like @english))";

        private const string SEARCH_SPECIFIC_WORK_FULLTEXT = @"
select 
    translations.english as english,
    translations.manx as manx,
    translations.page as page,
    translations.notes as notes
from 
    translations 
join works on works.id = translations.work
where 
    works.ident = @workIdent 
        AND 
    ((@manx IS NOT NULL AND manx like @manx) OR (@english IS NOT NULL AND english like @english))";



        public static async Task<SearchWorkResult> SearchWork(string workIdent, string query, bool manx, bool english, bool fullTextSearch)
        {
            var workNameParam = new DynamicParameters();
            workNameParam.Add("ident", workIdent);
            var title = await Startup.conn.QuerySingleAsync<string>("SELECT name FROM works where ident = @ident", workNameParam);

            SearchWorkResult ret = SearchWorkResult.Empty(title);
            if (string.IsNullOrWhiteSpace(query) || string.IsNullOrEmpty(workIdent) || query.Length > 30)
            {
                return SearchWorkResult.Empty(title);
            }
            if (!manx && !english)
            {
                return SearchWorkResult.Empty(title);
            }

            var param = new DynamicParameters();
            param.Add("manx", getParam(query, manx, fullTextSearch));
            param.Add("english", getParam(query, english, fullTextSearch));
            param.Add("workIdent", workIdent);

            var results = await Startup.conn.QueryAsync<DocumentLine>(fullTextSearch ? SEARCH_SPECIFIC_WORK_FULLTEXT : SEARCH_SPECIFIC_WORK, param);

            if (!fullTextSearch)
            {
                // Search English, and you get Manx
                var manxTranslations = Startup.EnglishDictionary.GetValueOrDefault(query.ToLowerInvariant(), new List<string>());
                var englishTranslations = Startup.ManxDictionary.GetValueOrDefault(query.ToLowerInvariant(), new List<string>());

                ret.ManxTranslations.AddRange(manxTranslations.Select(x => " " + x + " "));
                ret.EnglishTranslations.AddRange(englishTranslations.Select(x => " " + x + " "));
            }

            ret.EnrichResults(results);
            return ret;
        }
    }
}
