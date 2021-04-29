using Dapper;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Codex_API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AnalysisController
    {

        [HttpGet("WordCount")]
        public async Task<WordCountResult> WordCount()
        {
            var query = "select manx, sum(count) from wordfreq group by manx";

            var result = await Startup.conn.QueryAsync<(int, int)>(query);

            return new WordCountResult()
            {
                EnglishWords = result.Where(x => x.Item1 == 0).Sum(x => x.Item2),
                ManxWords = result.Where(x => x.Item1 == 1).Sum(x => x.Item2),
            };
        }

        public class WordCountResult
        {
            public int ManxWords { get; set; }
            public int EnglishWords { get; set; }
        }


        /// <summary>
        /// Obtains a list of manx words which are not in the dictionary.
        /// </summary>
        /// <returns></returns>
        [HttpGet("manxWords")]
        public async Task<Dictionary<string, int>> words()
        {
            IEnumerable<string> res = await Startup.conn.QueryAsync<string>("select manx from translations");

            IEnumerable<String> skipPrefixes = new List<string> {
                "ashlish:",
                "yamys:",
                "hebrewnee:",
                "titus:",
                "thessalonianee:",
                "timothy:",
                "ean:",
                "mark:",
                "mian:",
                "zechariah:",
                "zechariah:",
                "psalmyn:",
                "creeney:",
                "philippianee:",
                "dobberan:",
                "jeremiah:",
                "ezekiel:",
                "daniel:",
                "hosea:",
                "joel:",
                "amos:",
                "habakkuk:",
                "luke:",
                "jannoo:",
                "romanee:",
                "corinthianee:",
                "galatianee:",
                "ephesianee:",
                "colossianee:",
                "samuel:",
                "isaiah:",
                "solomon:",
                "job:",
                "nehemiah:",
                "reeaghyn:",
                "briwnyn:",
                "joshua:",
                "deuteronomy:",
                "earrooyn:",
                "leviticus:",
                "exodus:",
                "genesis:",
                "jonah:",
                "peddyr:",
                "ruth:",
                "recortyssyn:",
                "ezra:",
                "esther:",
                "ecclesiastes:",
                "malachi:",
                "haggai:",
                "obadiah:",
                "zephaniah:",
                "philemon:",
                "nahum:",
                "micah:",
            };

            return res.SelectMany(x => x
                .Split(" ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                .ToLookup(x => x.ToLowerInvariant())
                .Where(x => !Startup.ManxDictionary.ContainsKey(Regex.Replace(x.Key, @"[^\w\s]", string.Empty)))
                .Where(x => !Startup.ManxDictionary.ContainsKey(x.Key))
                .Where(x => skipPrefixes.All(prefix => !x.Key.StartsWith(prefix)))
                .OrderByDescending(x => x.Count())
                .ToDictionary(x => x.Key, x => x.Count());
        }
    }
}
