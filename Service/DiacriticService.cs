using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex_API.Service
{
    public class DiacriticService
    {
        private static Dictionary<char, string> diacriticMap = new Dictionary<char, string>
        {
            ['á'] = "a",
            ['é'] = "e",
            ['í'] = "i",
            ['ó'] = "o",
            ['ú'] = "u",
            ['ẃ'] = "w",
            ['ï'] = "i",
            ['â'] = "a",
            ['ý'] = "y",
            ['ø'] = "o",
            ['ö'] = "o",
            ['ẁ'] = "w",
            ['ç'] = "c",
            ['ỳ'] = "y",
            ['ê'] = "e",
            ['ô'] = "o",
            ['ŷ'] = "y",
            ['ǎ'] = "a",
            ['ì'] = "i",
            ['ě'] = "e",
            ['ë'] = "e",
            ['ŵ'] = "w",
            ['û'] = "u",
            ['ò'] = "o",
            ['æ'] = "ae",
            ['ǔ'] = "u",
            ['œ'] = "oe",
            ['ù'] = "u",
            ['è'] = "e",
            ['ǒ'] = "o",
            ['ŕ'] = "r",
            ['ǐ'] = "i",
            ['à'] = "a",
            ['î'] = "i",
            ['ĵ'] = "j",
            ['ﬆ'] = "st",
        };

        public static string Replace(string input)
        {
            StringBuilder output = new StringBuilder();

            foreach (var c in input)
            {
                output.Append(diacriticMap.GetValueOrDefault(c, c.ToString()));
            }

            return output.ToString();
        }
    }
}
