using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CorpusSearch.Model;

/// <summary>A scripture book: the stable id used in canonical reference keys
/// ("1-thessalonians") and the English display name ("1 Thessalonians").</summary>
public sealed record BibleBook(string Id, string DisplayName);

/// <summary>
/// The 66-book canon, findable by any name the corpus and the dictionaries use:
/// English names, the Manx names of the P Kelly Bible import's verse markers
/// (Mian, Psalmyn, Ashlish, I. Reeaghyn...), and the citation abbreviations of
/// Cregeen's quotes (Gen., Jud., 1 Thess.). Lookup is forgiving the way the
/// sources demand: lowercase, editorial brackets (H[a]b.) and periods dropped,
/// roman ordinals (I./II./III.) read as 1/2/3.
/// </summary>
public static class BibleBooks
{
    /// <summary>The book named by <paramref name="name"/>, or null when the name
    /// isn't in the canon (Hymn 54, Aght Giare's section numbers...)</summary>
    public static BibleBook? Find(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }
        return ByAlias.GetValueOrDefault(Normalize(name));
    }

    /// <summary>The book with canonical id <paramref name="id"/> ("1-thessalonians"), or null</summary>
    public static BibleBook? FindById(string? id) => id == null ? null : ById.GetValueOrDefault(id);

    private static string Normalize(string name)
    {
        // editorial brackets restore elided letters: H[a]b. reads as Hab.
        var cleaned = name.Replace("[", "").Replace("]", "").ToLowerInvariant();
        cleaned = Regex.Replace(cleaned, @"[.,]+", " ");
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
        // ordinal book numbers arrive as romans in both traditions (I. Samuel, II. Peddyr)
        return Regex.Replace(cleaned, @"^i{1,3}(?= )", m => m.Value.Length.ToString());
    }

    // id, display name, extra aliases (the display name and the id's spaced form are
    // implicit aliases). Manx names are the P Kelly Bible import's; abbreviations are
    // the ones Cregeen's citations actually use, plus common variants.
    private static readonly (string Id, string Display, string[] Aliases)[] Canon =
    [
        ("genesis", "Genesis", ["gen"]),
        ("exodus", "Exodus", ["exod", "ex"]),
        ("leviticus", "Leviticus", ["lev"]),
        ("numbers", "Numbers", ["num", "numb", "earrooyn"]),
        ("deuteronomy", "Deuteronomy", ["deut", "deu"]),
        ("joshua", "Joshua", ["josh"]),
        ("judges", "Judges", ["judg", "jud", "briwnyn"]),
        ("ruth", "Ruth", []),
        ("1-samuel", "1 Samuel", ["1 sam"]),
        ("2-samuel", "2 Samuel", ["2 sam"]),
        ("1-kings", "1 Kings", ["1 kgs", "1 kin", "1 king", "1 kings", "1 reeaghyn"]),
        ("2-kings", "2 Kings", ["2 kgs", "2 kin", "2 king", "2 reeaghyn"]),
        ("1-chronicles", "1 Chronicles", ["1 chron", "1 chro", "1 chr", "1 recortyssyn"]),
        ("2-chronicles", "2 Chronicles", ["2 chron", "2 chro", "2 chr", "2 recortyssyn"]),
        ("ezra", "Ezra", []),
        ("nehemiah", "Nehemiah", ["neh"]),
        ("esther", "Esther", ["esth", "est"]),
        ("job", "Job", []),
        // Psl./Pro./Ez. are Cregeen's house abbreviations (Ezra he writes in full)
        ("psalms", "Psalms", ["psalm", "psal", "psl", "ps", "psalmyn"]),
        ("proverbs", "Proverbs", ["prov", "pro", "raaghyn creeney"]),
        ("ecclesiastes", "Ecclesiastes", ["eccles", "eccl", "ecc"]),
        ("song-of-solomon", "Song of Solomon", ["song of songs", "canticles", "cant", "arrane solomon"]),
        ("isaiah", "Isaiah", ["isa"]),
        ("jeremiah", "Jeremiah", ["jer"]),
        ("lamentations", "Lamentations", ["lam", "dobberan"]),
        ("ezekiel", "Ezekiel", ["ezek", "ezk", "ez"]),
        ("daniel", "Daniel", ["dan"]),
        ("hosea", "Hosea", ["hos"]),
        ("joel", "Joel", []),
        ("amos", "Amos", []),
        ("obadiah", "Obadiah", ["obad"]),
        ("jonah", "Jonah", ["jon"]),
        ("micah", "Micah", ["mic"]),
        ("nahum", "Nahum", ["nah"]),
        ("habakkuk", "Habakkuk", ["hab"]),
        ("zephaniah", "Zephaniah", ["zeph", "zep"]),
        ("haggai", "Haggai", ["hag"]),
        ("zechariah", "Zechariah", ["zech", "zec"]),
        ("malachi", "Malachi", ["mal"]),
        ("matthew", "Matthew", ["matt", "mat", "mian"]),
        ("mark", "Mark", []),
        ("luke", "Luke", []),
        ("john", "John", ["ean"]),
        ("acts", "Acts", ["act", "jannoo"]),
        ("romans", "Romans", ["rom", "romanee"]),
        ("1-corinthians", "1 Corinthians", ["1 cor", "1 corinthianee"]),
        ("2-corinthians", "2 Corinthians", ["2 cor", "2 corinthianee"]),
        ("galatians", "Galatians", ["gal", "galatianee"]),
        ("ephesians", "Ephesians", ["eph", "ephesianee"]),
        ("philippians", "Philippians", ["phil", "philippianee"]),
        ("colossians", "Colossians", ["col", "colossianee"]),
        ("1-thessalonians", "1 Thessalonians", ["1 thess", "1 thes", "1 thessalonianee"]),
        ("2-thessalonians", "2 Thessalonians", ["2 thess", "2 thes", "2 thessalonianee"]),
        ("1-timothy", "1 Timothy", ["1 tim"]),
        ("2-timothy", "2 Timothy", ["2 tim"]),
        ("titus", "Titus", ["tit"]),
        ("philemon", "Philemon", ["philem"]),
        ("hebrews", "Hebrews", ["heb", "hebrewnee"]),
        ("james", "James", ["jas", "jam", "screeuyn yamys"]),
        ("1-peter", "1 Peter", ["1 pet", "1 peddyr"]),
        ("2-peter", "2 Peter", ["2 pet", "2 peddyr"]),
        ("1-john", "1 John", ["1 ean"]),
        ("2-john", "2 John", ["2 ean"]),
        ("3-john", "3 John", ["3 ean"]),
        ("jude", "Jude", []),
        ("revelation", "Revelation", ["rev", "ashlish"]),
    ];

    private static readonly Dictionary<string, BibleBook> ById = BuildById();
    private static readonly Dictionary<string, BibleBook> ByAlias = BuildByAlias();

    private static Dictionary<string, BibleBook> BuildById()
    {
        var result = new Dictionary<string, BibleBook>();
        foreach (var (id, display, _) in Canon)
        {
            result.Add(id, new BibleBook(id, display));
        }
        return result;
    }

    private static Dictionary<string, BibleBook> BuildByAlias()
    {
        var result = new Dictionary<string, BibleBook>();
        foreach (var (id, display, aliases) in Canon)
        {
            var book = ById[id];
            var keys = new HashSet<string> { Normalize(display), id.Replace('-', ' ') };
            foreach (var alias in aliases)
            {
                keys.Add(Normalize(alias));
            }
            // Add, not indexer: an alias claimed by two books is a bug worth crashing on
            foreach (var key in keys)
            {
                result.Add(key, book);
            }
        }
        return result;
    }
}
