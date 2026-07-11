using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CorpusSearch.Service;

public class DiacriticService
{
    private static readonly Dictionary<string, string> diacriticMap = new()
    {
        ["á"] = "a",
        ["é"] = "e",
        ["í"] = "i",
        ["ó"] = "o",
        ["ú"] = "u",
        ["ẃ"] = "w",
        ["ï"] = "i",
        ["â"] = "a",
        ["ý"] = "y",
        ["ø"] = "o",
        ["ö"] = "o",
        ["ẁ"] = "w",
        ["ç"] = "c", // check this one: c or ch?
        ["ỳ"] = "y",
        ["ê"] = "e",
        ["ô"] = "o",
        ["ŷ"] = "y",
        ["ǎ"] = "a",
        ["ì"] = "i",
        ["ě"] = "e",
        ["ë"] = "e",
        ["ŵ"] = "w",
        ["û"] = "u",
        ["ò"] = "o",
        ["æ"] = "ae",
        ["ǔ"] = "u",
        ["œ"] = "oe",
        ["ù"] = "u",
        ["è"] = "e",
        ["ǒ"] = "o",
        ["ŕ"] = "r",
        ["ǐ"] = "i",
        ["à"] = "a",
        ["î"] = "i",
        ["ĵ"] = "j",
        ["ﬆ"] = "st",
        ["ğ"] = "g",  // two characters
    };

    /// <summary>
    /// string to diacritic
    /// </summary>
    private static readonly ILookup<string, string> reverseMap;

    static DiacriticService() 
    {
        reverseMap = diacriticMap.ToLookup(x => x.Value, x => x.Key);
    }

    /// <summary>
    /// TODO: This doesn't do st or ae
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static IList<string> Replace(char input)
    {
        var ret = diacriticMap.GetValueOrDefault(input.ToString());
        if (ret != null)
        {
            return new[] { ret };
        }

        var reversed = reverseMap[input.ToString()].Select(x => x.ToString()).ToList();
        if (reversed.Count != 0 || !char.IsUpper(input))
        {
            return reversed;
        }

        // the map only lists lowercase: uppercase letters (which only reach here from
        // case-sensitive queries, #19) fold like their lowercase forms - 'Ç' <-> 'C'
        return Replace(char.ToLowerInvariant(input)).Select(x => x.ToUpperInvariant()).ToList();
    }

    public static string Replace(string input)
    {
        StringBuilder output = new StringBuilder();

        foreach (var c in input)
        {
            output.Append(diacriticMap.GetValueOrDefault(c.ToString(), c.ToString()));
        }

        return output.ToString();
    }
}