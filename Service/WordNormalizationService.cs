using Codex_API.Services;
using Dapper;
using MoreLinq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using static Codex_API.Startup;

namespace Codex_API.Service
{
    /// <summary>
    /// Responsible for handling the removal of diacritics from manx text.
    /// 
    /// This allows a search to optionally include the diacritic removal (a normal operation), and to return the forms of the word which were available.
    /// 
    /// Let's say we search for 'farkey', we also want this to include 'fárkey'
    /// 
    /// If we search for 'fárkey' (via copy/paste), then we also want to search for 'farkey'
    /// 
    /// We perform this via normalising all the inputs that we add to the corpus, so we know what's available, and searching for these in the corpus.
    /// 
    /// so 'fárkey' => normalise => 'farkey' => GetLemmatisation => ['farkey', 'fárkey', 'farkéy', ...]
    /// </summary>
    public class WordNormalizationService
    {
        public static async Task<IEnumerable<string>> GetLemmatisation(string word)
        {
            var result = await conn.QueryAsync<string>("select source from dictionary where normalized = @word", new { word = ToNormalizedWord(word) });

            return result.Concat(new[] { word, ToNormalizedWord(word) }).Distinct();
        }
        public static string ToNormalizedWord(string input)
        {
            return input.Trim().ToLower().RemoveDiacritics();
        }
        public static void CreateTable(Microsoft.Data.Sqlite.SqliteConnection conn)
        {
            conn.ExecSql("create table dictionary (" +
                "normalized varchar, " +
                "source varchar, " +
                "PRIMARY KEY (normalized, source)" +
                ")");
        }

        internal static void AddDocument(List<DocumentLine> validData)
        {
            // .Key is the normalized word, .Value is the words which create it (called 'sources')
            var groups = validData.SelectMany(x => x.NormalizedManx.Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).
                ToLookup(ToNormalizedWord)
                .ToDictionary(x => x.Key, x => x.Concat(new[] { x.Key }).Distinct().ToList());

            // Filter to keys where normalisation occurred.
            // Since we concat the key back into the list, if there's only one value, nothing occurred.
            var keys = groups.Where(x => x.Value.Count() > 1).ToList();

            // Add to the database: a collection of non-normalized (source) => normalized (normalized)
            // Do nothing if the mapping already exists in the database
            var allKeys = keys.SelectMany(x => x.Value);
            HashSet<string> toAdd = GetExistingSourcesForKeys(allKeys);
            foreach (var k in keys.SelectMany(x => x.Value.Select(y => (x.Key, source: y))))
            {
                if (toAdd.Contains(k.source))
                {
                    continue;
                }
                conn.Execute("insert into dictionary (normalized, source) values (@normalized, @source)", new { normalized = k.Key, source = k.source });
            }
        }

        private static HashSet<string> GetExistingSourcesForKeys(IEnumerable<string> allKeys)
        {
            return new HashSet<string>(allKeys.Batch(999).SelectMany(words => conn.Query<string>("select source from dictionary where source in @words", new { words = words })));
        }
    }
}
