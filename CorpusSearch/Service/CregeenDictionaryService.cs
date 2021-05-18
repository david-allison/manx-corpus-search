using CorpusSearch.Model.Dictionary;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace CorpusSearch.Service
{
    public class CregeenDictionaryService
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

        public CregeenDictionaryService(ISet<string> allWords)
        {
            this.allWords = allWords;
        }

        public string Identifier => "Cregeen";

        public static CregeenDictionaryService Init()
        {
            HashSet<string> allWords;

            try
            {
                var entries = GetEntries();
                var allEntries = entries.SelectMany(x => x.ChildrenRecursive).SelectMany(x => x.Words);
                allWords = new HashSet<string>(allEntries, StringComparer.InvariantCultureIgnoreCase);
            } 
            catch (Exception)
            {
                // TODO: Add to health check
                Console.WriteLine("Failed to load Cregeen");
                allWords = new HashSet<string>();
            }
            
            return new CregeenDictionaryService(allWords);
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

        public static List<CregeenEntry> FuzzySearch(string query, IEnumerable<CregeenEntry> entries)
        {
            HashSet<CregeenEntry> cregeenEntries = new HashSet<CregeenEntry>(FuzzySearchInternal(query, entries));

            return FuzzySearchInternal(query, entries).Distinct().ToList();
        }

        private static IEnumerable<CregeenEntry> FuzzySearchInternal(string query, IEnumerable<CregeenEntry> entryData)
        {
            var flatEntries = entryData.SelectMany(x => x.ChildrenRecursive);

            // exact match
            foreach (var e in flatEntries.Where(x => x.ContainsWordExact(query)))
            {
                yield return e;
            }

            // Prefix
            foreach (var e in flatEntries.Where(x => x.Words.Where(x => x.StartsWith(query)).Any()))
            {
                yield return e;
            }

            // Contains
            foreach (var e in flatEntries.Where(x => x.Words.Where(x => x.Contains(query)).Any()))
            {
                yield return e;
            }
        }

        /// <summary>Whether the dictionary contains the provided word (no fuzziness)</summary>
        public bool ContainsWordExact(string s)
        {
            return allWords.Contains(s);
        }

        private class CaseInsensitiveCharComparer : IEqualityComparer<char>
        {
            public bool Equals(char x, char y) => char.ToUpperInvariant(x) == char.ToUpperInvariant(y);
            public int GetHashCode([DisallowNull] char c) => char.ToUpperInvariant(c).GetHashCode();
        }
    }
}
