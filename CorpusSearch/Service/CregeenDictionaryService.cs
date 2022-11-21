using CorpusSearch.Model.Dictionary;
using HtmlAgilityPack;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Web;

namespace CorpusSearch.Service
{
    public class CregeenDictionaryService : ISearchDictionary
    {
        public readonly static Dictionary<char, string> LetterLookup = new Dictionary<char, string>(new CaseInsensitiveCharComparer())
        {
            [' '] = "  ",
            ['A'] = "aa-",
            ['B'] = "baa",
            ['C'] = "caa",
            ['D'] = "da",
            ['E'] = "e",
            ['F'] = "fa.",
            ['G'] = "ga",
            ['H'] = "ha",
            ['I'] = "ick",
            ['J'] = "jaagh",
            ['K'] = "kaart",
            ['L'] = "laa",
            ['M'] = "maaig",
            ['N'] = "na",
            ['O'] = "O",
            ['P'] = "paa",
            ['Q'] = "quaagh",
            ['R'] = "raa",
            ['S'] = "saagh",
            ['T'] = "taagh",
            ['U'] = "udlan",
            ['V'] = "vaidjin",
            ['W'] = "wagaan",
            ['Y'] = "y",
        };

        private readonly ISet<string> allWords;
        private readonly IList<CregeenEntry> allEntries;

        public CregeenDictionaryService(ISet<string> allWords, IList<CregeenEntry> entries)
        {
            this.allWords = allWords;
            this.allEntries = entries;
        }

        public string Identifier => "Cregeen";

        public static CregeenDictionaryService Init()
        {
            HashSet<string> allWords;

            IList<CregeenEntry> entries = new List<CregeenEntry>();
            try
            {
                entries = GetEntries();
                var allEntries = entries.SelectMany(x => x.ChildrenRecursive).SelectMany(x => x.Words);
                allWords = new HashSet<string>(allEntries, StringComparer.InvariantCultureIgnoreCase);
            } 
            catch (Exception)
            {
                // TODO: Add to health check
                Console.WriteLine("Failed to load Cregeen");
                allWords = new HashSet<string>();
            }
            
            return new CregeenDictionaryService(allWords, entries);
        }

        /// <summary>Loads the tree of entries from Cregeen</summary>
        public static IList<CregeenEntry> GetEntries()
        {
            // TODO: This shouldn't be static

            var path = Startup.GetLocalFile("Resources", "cregeen.json");
            
            var text = File.ReadAllText(path);

            return JsonConvert.DeserializeObject<List<CregeenEntry>>(text);
        }

        public static bool IsValidSearch(string query)
        {
            return !string.IsNullOrWhiteSpace(query) // invalid if whitespace
                && (query.Length > 1 // valid if longer than 1 char
                        || LetterLookup.ContainsKey(query[0]) // valid if 1 char and in the lookup
                    );
        }

        public static IList<CregeenEntry> FuzzySearch(string query, IEnumerable<CregeenEntry> entries)
        {
            return FuzzySearchInternal(query, entries).Distinct().ToList();
        }

        private static IEnumerable<CregeenEntry> FuzzySearchInternal(string query, IEnumerable<CregeenEntry> entryData)
        {
            var flatEntries = entryData.SelectMany(x => x.ChildrenRecursive).ToList();

            // exact match
            foreach (var e in flatEntries.Where(x => x.ContainsWordExact(query)))
            {
                yield return e;
            }

            // Prefix
            foreach (var e in flatEntries.Where(entry => entry.Words.Any(word => word.StartsWith(query))))
            {
                yield return e;
            }

            // Contains
            foreach (var e in flatEntries.Where(entry => entry.Words.Any(word => word.Contains(query))))
            {
                yield return e;
            }
        }

        /// <summary>Whether the dictionary contains the provided word (no fuzziness)</summary>
        public bool ContainsWordExact(string s)
        {
            return allWords.Contains(s);
        }

        public IEnumerable<DictionarySummary> GetSummaries(string query, bool basic)
        {
            if (!ContainsWordExact(query)) { yield break; }

            string GetSummary(CregeenEntry entry)
            {
                if (basic && !string.IsNullOrWhiteSpace(entry.Definition))
                {
                    return entry.Definition;
                }
                
                // decode the HTML
                HtmlDocument doc = new();
                doc.LoadHtml(entry.EntryHtml);

                return HttpUtility.HtmlDecode(doc.DocumentNode.InnerText);
            }

            // PERF: extract to member
            var entries = allEntries.SelectMany(x => x.ChildrenRecursive).ToList();

            foreach (var validEntry in entries.Where(e => e.Words.Contains(query, StringComparer.InvariantCultureIgnoreCase)))
            {
                yield return new DictionarySummary
                {
                    Summary = GetSummary(validEntry)
                };
            }
        }

        private class CaseInsensitiveCharComparer : IEqualityComparer<char>
        {
            public bool Equals(char x, char y) => char.ToUpperInvariant(x) == char.ToUpperInvariant(y);
            public int GetHashCode(char c) => char.ToUpperInvariant(c).GetHashCode();
        }
    }
}
