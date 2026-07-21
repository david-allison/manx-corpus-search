using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CorpusSearch.Service;

/// <summary>
/// The sense layer's inventory (DESIGN-disambiguation.md Phase 4): which
/// distinguishable senses a lemma id has, from manx-lemma-data's senses.tsv.
///
/// senseId = "&lt;lemmaId&gt;#&lt;n&gt;" ("foddey.a#1"). A lemma with no rows implicitly
/// has one whole-entry sense — most do; rows are minted only where an entry's
/// printed senses are discriminable. entryPath names the printed entry the
/// sense belongs to (dictionary slug + headword), because the sense text is
/// the book's, never generated here.
///
/// Senses are a display refinement, not a recall mechanism: nothing in this
/// layer reaches the search index, so a wrong sense can mislabel a gloss but
/// can never hide a line from search.
/// </summary>
public class SenseInventory
{
    public sealed record Sense(string SenseId, string LemmaId, string Dictionary, string EntryPath, string Gloss);

    private readonly Dictionary<string, List<Sense>> byLemmaId;
    private readonly Dictionary<string, Sense> bySenseId;

    public static readonly SenseInventory Empty = new([]);

    private static readonly Lazy<SenseInventory> Lazy = new(LoadVendored);
    public static SenseInventory Instance => Lazy.Value;

    private SenseInventory(IEnumerable<Sense> senses)
    {
        byLemmaId = senses.GroupBy(x => x.LemmaId).ToDictionary(x => x.Key, x => x.ToList());
        bySenseId = byLemmaId.Values.SelectMany(x => x).ToDictionary(x => x.SenseId);
    }

    /// <summary>The lemma's discriminable senses; empty for the implicit
    /// whole-entry sense (the common case)</summary>
    public IReadOnlyList<Sense> SensesOf(string lemmaId) =>
        byLemmaId.GetValueOrDefault(lemmaId) ?? (IReadOnlyList<Sense>)[];

    public Sense? SenseOf(string senseId) => bySenseId.GetValueOrDefault(senseId);

    public int Count => bySenseId.Count;

    /// <summary>Reads senses.tsv: senseId, lemmaId, dict, entryPath, gloss.
    /// A senseId not shaped "&lt;its lemmaId&gt;#n" is dropped: version skew,
    /// like the resolver's non-narrowing rows.</summary>
    public static SenseInventory Load(TextReader reader)
    {
        var senses = new List<Sense>();
        var dropped = 0;
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith("senseId\t"))
            {
                continue;
            }
            var columns = line.Split('\t');
            if (columns.Length < 5 || !columns[0].StartsWith(columns[1] + "#", StringComparison.Ordinal))
            {
                dropped++;
                continue;
            }
            senses.Add(new Sense(columns[0], columns[1], columns[2], columns[3], columns[4]));
        }
        if (senses.Count > 0 || dropped > 0)
        {
            Serilog.Log.Information("Sense inventory: {Count} senses ({Dropped} rows dropped)",
                senses.Count, dropped);
        }
        return new SenseInventory(senses);
    }

    private static SenseInventory LoadVendored()
    {
        // absent until the data side mints an inventory: every lemma keeps its
        // implicit whole-entry sense
        var path = Startup.GetLocalFile("Resources", "senses.tsv");
        if (!File.Exists(path))
        {
            return Empty;
        }
        using var reader = new StreamReader(path);
        return Load(reader);
    }
}
