using CorpusSearch.Model.Dictionary;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CorpusSearch.Service
{
    public class CregeenDictionaryService
    {
        public readonly static Dictionary<char, string> LetterLookup = new()
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
            catch (Exception e)
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

        /// <summary>Whether the dictionary contains the provided word (no fuzziness)</summary>
        public bool ContainsWordExact(string s)
        {
            return allWords.Contains(s);
        }
    }
}
