using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CorpusSearch.Dependencies.Lucene;

namespace CorpusSearch.Service;

/// <summary>
/// Per-occurrence sense resolutions (DESIGN-disambiguation.md Phase 4), from
/// manx-lemma-data's sense.sidecar.tsv — the lemma sidecar's schema with
/// senseIds in place of lemmaIds, keyed the same way (hash of the line's
/// token stream + token index), so the same corpus edits orphan the same rows.
///
/// The layers are ordered: a sense verdict is meaningful only for a token
/// whose LEMMA is already settled, so <see cref="SenseFor"/> takes the
/// resolved lemma id and answers null unless the row's senses belong to it.
/// Rows whose senseIds the inventory does not know are dropped at load —
/// the same version-skew rule as <see cref="LemmaResolver"/>. Nothing here
/// touches the search index: display refinement only.
/// </summary>
public class SenseResolver
{
    private readonly Dictionary<(string Key, int Index), (string Form, string[] SenseIds)> byToken;

    public static readonly SenseResolver Empty = new([]);

    private static readonly Lazy<SenseResolver> Lazy = new(LoadVendored);
    public static SenseResolver Instance => Lazy.Value;

    private SenseResolver(Dictionary<(string, int), (string, string[])> byToken)
    {
        this.byToken = byToken;
    }

    public bool HasRows => byToken.Count > 0;

    /// <summary>The senses of <paramref name="resolvedLemmaId"/> the occurrence
    /// was read as; null when unresolved, when the row is for another form
    /// (corruption guard), or when the senses are another lemma's (the lemma
    /// layer disagrees, and it is the one that has been validated)</summary>
    public IReadOnlyList<SenseInventory.Sense>? SenseFor(SenseInventory inventory,
        string lineKey, int tokenIndex, string form, string resolvedLemmaId)
    {
        if (!byToken.TryGetValue((lineKey, tokenIndex), out var row) || row.Form != form)
        {
            return null;
        }
        var senses = row.SenseIds
            .Select(inventory.SenseOf)
            .OfType<SenseInventory.Sense>()
            .Where(x => x.LemmaId == resolvedLemmaId)
            .ToList();
        return senses.Count == row.SenseIds.Length ? senses : null;
    }

    /// <summary>Reads sense.sidecar.tsv
    /// (docId, key, englishHash, tokenIndex, form, senseIds, tier, humanVerified):
    /// rows naming senses the inventory lacks are version skew and are dropped.
    /// Every surviving row is display-tier by nature; the tier column is kept
    /// for schema parity with the lemma sidecar.</summary>
    public static SenseResolver Load(TextReader reader, SenseInventory inventory)
    {
        var byToken = new Dictionary<(string, int), (string, string[])>();
        var dropped = 0;
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith("docId\t"))
            {
                continue;
            }
            var columns = line.Split('\t');
            if (columns.Length < 6 || !int.TryParse(columns[3], out var tokenIndex))
            {
                dropped++;
                continue;
            }
            var senseIds = columns[5].Split(',');
            if (senseIds.Length == 0 || !senseIds.All(x => inventory.SenseOf(x) != null))
            {
                dropped++;
                continue;
            }
            byToken[(columns[1], tokenIndex)] = (columns[4], senseIds);
        }
        if (byToken.Count > 0 || dropped > 0)
        {
            Serilog.Log.Information("Sense resolutions: {Count} rows ({Dropped} dropped as unknown to the inventory)",
                byToken.Count, dropped);
        }
        return new SenseResolver(byToken);
    }

    private static SenseResolver LoadVendored()
    {
        var path = Startup.GetLocalFile("Resources", "sense.sidecar.tsv");
        if (!File.Exists(path))
        {
            return Empty;
        }
        using var reader = new StreamReader(path);
        return Load(reader, SenseInventory.Instance);
    }
}
