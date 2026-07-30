using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CorpusSearch.Service.Dictionaries;
using NUnit.Framework;

namespace CorpusSearch.Test.LemmaAdjudication;

/// <summary>
/// Mints the Cregeen sense inventory (DESIGN-disambiguation.md Phase 4):
/// one row per discriminable printed sense, senseId = lemmaId#n with n in
/// book order, entryPath = the printed headword the sense belongs to.
///
/// Rows are minted only where a lemma id has two or more printed entries to
/// tell apart - a single-entry id keeps its implicit whole-entry sense, and
/// the id split (aase.n / aase.v) already carries the word-class distinction.
/// Cregeen's double-printed prefix compounds (aa-aase under aa- and at its
/// own place) fold to one sense, as the word page folds them to one link.
///
/// Ids that already have senses.tsv rows are skipped, not regenerated: the
/// existing rows are curated and can be finer than the print (foddey.a#2
/// "long, of time" splits a single printed entry). They go to the skim
/// report for manual reconcile instead.
///
/// Writes senses.generated.tsv (rows to review, then fold into senses.tsv)
/// and senses.generated.skim.tsv (what needs a human) beside the inputs.
///
/// Run: LEMMA_DATA_DIR=&lt;manx-lemma-data&gt;
///   dotnet test CorpusSearch.Test --filter FullyQualifiedName~SenseInventoryGenerator
/// </summary>
[TestFixture]
[Explicit("artifact generator over cregeen.json and the lemma table, not a regression test")]
public class SenseInventoryGenerator
{
    /// <summary>One printed entry, deduplicated: headword, its printed word
    /// classes, and the gloss snippet that discriminates it</summary>
    internal sealed record PrintedSense(string Headword, IReadOnlyList<string> Pos, string Gloss);

    /// <summary>Gloss carries the whole definition; the vendored TSV snips it
    /// to the pilot's snippet length, the review file shows it entire. Flag
    /// is the review's margin note ("single-id-attach", "near-identical"),
    /// empty on rows nothing doubts.</summary>
    internal sealed record MintedSense(string SenseId, string LemmaId, string EntryPath, string Gloss)
    {
        public string Flag { get; set; } = "";
    }

    /// <summary>A sense the generator could not place, and why: the human
    /// queue, not an error list</summary>
    internal sealed record SkimRow(string Headword, string Reason, string Detail);

    [Test]
    public void Generate()
    {
        var dataDir = Environment.GetEnvironmentVariable("LEMMA_DATA_DIR");
        Assert.That(dataDir, Is.Not.Null.And.Not.Empty, "set LEMMA_DATA_DIR to the manx-lemma-data checkout");

        var entries = CregeenDictionaryService.GetEntries();
        Assume.That(entries, Is.Not.Empty, "cregeen.json not present");

        var idsByWord = ReadSelfIds(Path.Combine(dataDir!, "cregeen.tsv"));
        var existing = ReadExistingSenseIds(Path.Combine(dataDir!, "senses.tsv"));

        var senses = PrintedSensesOf(entries);
        var (minted, skim) = Mint(senses, idsByWord, existing);

        // ids the table minted from an apparatus label, taking the printed
        // reading over its correction (foddey.v from "<v>[a]."): nonsense at
        // the lexeme layer. The fix is the cregeen-nvh table generator using
        // the corrected reading (cregeen.json's PartsOfSpeech already does),
        // then a rekey; enumerated here so the senses over them are read
        // with suspicion until then
        foreach (var (id, pos) in ReadApparatusIds(Path.Combine(dataDir!, "cregeen.tsv")))
        {
            skim.Add(new SkimRow(id, "apparatus-pos-id",
                $"table pos is the raw apparatus \"{pos}\"; the correction names the class"));
        }

        var output = new StringBuilder("senseId\tlemmaId\tdict\tentryPath\tgloss\n");
        foreach (var row in minted)
        {
            output.Append($"{row.SenseId}\t{row.LemmaId}\tcregeen\t{row.EntryPath}\t{Snip(row.Gloss)}\n");
        }
        File.WriteAllText(Path.Combine(dataDir!, "senses.generated.tsv"), output.ToString());

        // the review file is for a human's eye alone: an id's readings stay
        // together (judging "two senses or one?" needs them side by side),
        // groups anything doubts float to the top whole, definitions are
        // uncut, and a blank line breathes between ids
        var review = new StringBuilder("flag\tsenseId\tentryPath\tgloss\n");
        var groups = minted.GroupBy(x => x.LemmaId)
            .OrderBy(g => g.All(x => x.Flag.Length == 0))
            .ThenBy(g => g.Key, StringComparer.Ordinal);
        foreach (var group in groups)
        {
            foreach (var row in group)
            {
                review.Append($"{row.Flag}\t{row.SenseId}\t{row.EntryPath}\t{row.Gloss}\n");
            }
            review.Append('\n');
        }
        File.WriteAllText(Path.Combine(dataDir!, "senses.review.tsv"), review.ToString());

        var skimOut = new StringBuilder("headword\treason\tdetail\n");
        foreach (var row in skim.OrderBy(x => x.Reason, StringComparer.Ordinal))
        {
            skimOut.Append($"{row.Headword}\t{row.Reason}\t{row.Detail}\n");
        }
        File.WriteAllText(Path.Combine(dataDir!, "senses.generated.skim.tsv"), skimOut.ToString());

        Console.WriteLine($"printed senses (deduped): {senses.Count}");
        Console.WriteLine($"minted rows: {minted.Count} across {minted.Select(x => x.LemmaId).Distinct().Count()} lemma ids");
        foreach (var reason in skim.GroupBy(x => x.Reason).OrderByDescending(x => x.Count()))
        {
            Console.WriteLine($"skim/{reason.Key}: {reason.Count()}");
        }
    }

    /// <summary>The printed entries in book order, children included, one
    /// sense each, with Cregeen's double-printings folded to their first
    /// appearance. Children walk because the table files phrase readings
    /// under the parent's id ('dy cheilley' → cheilley.x): a reading with no
    /// senseId is one the sidecar can never assign to a token in context.
    /// The grammatical children ('e edjag' his feather) fold away by
    /// <see cref="SenseKey"/> instead, in <see cref="Mint"/>.</summary>
    internal static List<PrintedSense> PrintedSensesOf(IEnumerable<Model.Dictionary.CregeenEntry> entries)
    {
        var senses = new List<PrintedSense>();
        var seen = new HashSet<string>();
        void Walk(IEnumerable<Model.Dictionary.CregeenEntry> level)
        {
            foreach (var entry in level)
            {
                var headword = entry.Words.FirstOrDefault();
                if (headword != null)
                {
                    var pos = entry.PartsOfSpeech ?? [];
                    var gloss = CollapseGloss(entry.Definition);
                    var key = $"{headword.ToLowerInvariant()}\t{string.Join(",", pos)}\t{NormalizeGloss(gloss)}";
                    if (seen.Add(key))
                    {
                        senses.Add(new PrintedSense(headword, pos, gloss));
                    }
                }
                Walk(entry.SafeChildren);
            }
        }
        Walk(entries);
        return senses;
    }

    /// <summary>The entry's plain definition as one line, whole: the folds
    /// compare it and the review shows it</summary>
    internal static string CollapseGloss(string? definition) =>
        string.Join(" ", (definition ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>The sense's discriminating snippet: the whole gloss capped
    /// the way the pilot rows are, for the vendored TSV alone</summary>
    internal static string GlossOf(string? definition) => Snip(CollapseGloss(definition));

    private static string Snip(string gloss) =>
        gloss.Length <= 80 ? gloss : gloss[..79].TrimEnd() + "…";

    /// <summary>What makes two glosses the same sense: the book's two
    /// printings of a compound differ by a hyphen, a trailing semicolon or
    /// the spacing of a compound ("a great grand child" / "a great
    /// grandchild;"), and that is one sense, not two. Letters and digits
    /// alone decide.</summary>
    internal static string NormalizeGloss(string gloss) =>
        new(gloss.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    /// <summary>The words of Cregeen's grammatical formulas: what a child
    /// entry's gloss wraps around the parent's ("his feather", "your, &amp;c.
    /// feather", "to push, &amp;c.", "hath, &amp;c. reared"). Content words
    /// they are not, so they carry no sense of their own.</summary>
    private static readonly HashSet<string> FormulaWords =
    [
        "his", "her", "your", "our", "their", "my", "thy", "the", "a", "an",
        "of", "to", "c", "did", "didst", "doth", "dost", "hath", "hast",
        "have", "had", "would", "wouldst", "should", "shouldst", "shall",
        "shalt", "will", "wilt", "art", "am", "is", "are", "was", "wast",
        "were", "wert", "be", "been", "or", "and", "too", "not",
    ];

    /// <summary>What a gloss says once the grammatical formula is stripped:
    /// "his feather" and "a feather" both say "feather" - the same sense
    /// through a possessive, not two senses. A gloss that is nothing but
    /// formula ("would be") falls back to its plain letters, so the
    /// auxiliaries' own entries stay distinguishable.</summary>
    internal static string SenseKey(string gloss)
    {
        var folded = new string(gloss.ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : ' ')
            .ToArray());
        var content = string.Concat(
            folded.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => !FormulaWords.Contains(w)));
        return content.Length > 0 ? content : NormalizeGloss(gloss);
    }

    /// <summary>The lemma id a printed word class files under: the table's
    /// suffix scheme (n / v / a, x for the closed classes). Null when the
    /// entry has no single printed class to map.</summary>
    internal static string? SuffixOf(IReadOnlyList<string> pos)
    {
        if (pos.Count != 1)
        {
            return null;
        }
        return pos[0] switch
        {
            "Noun" => "n",
            "Verb" => "v",
            "Adjective" => "a",
            _ => "x",
        };
    }

    private static string IdSuffix(string lemmaId)
    {
        var dot = lemmaId.LastIndexOf('.');
        return dot < 0 ? lemmaId : lemmaId[(dot + 1)..];
    }

    /// <summary>
    /// Attach each sense to a lemma id, then mint rows for every id with two
    /// or more senses to tell apart. Ordinals count in book order, per id.
    /// </summary>
    internal static (List<MintedSense> Minted, List<SkimRow> Skim) Mint(
        IEnumerable<PrintedSense> sensesInBookOrder,
        IReadOnlyDictionary<string, IReadOnlyList<string>> idsByWord,
        IReadOnlySet<string> idsWithExistingRows)
    {
        var attached = new List<(string Id, PrintedSense Sense, bool Fallback)>();
        var skim = new List<SkimRow>();

        foreach (var sense in sensesInBookOrder)
        {
            var ids = idsByWord.GetValueOrDefault(sense.Headword.ToLowerInvariant());
            if (ids == null || ids.Count == 0)
            {
                // a printed word the table has no id for is the table's gap,
                // not the inventory's: Phase 2's queue, only counted here
                continue;
            }

            var suffix = SuffixOf(sense.Pos);
            var matches = suffix == null ? [] : ids.Where(x => IdSuffix(x) == suffix).ToList();
            if (matches.Count == 1)
            {
                attached.Add((matches[0], sense, Fallback: false));
            }
            else if (ids.Count == 1)
            {
                // one id is the only place the sense can live, whatever the
                // label says - attached, and reported when it mints, so a
                // mislabel is seen where it matters
                attached.Add((ids[0], sense, Fallback: true));
            }
            else
            {
                skim.Add(new SkimRow(sense.Headword,
                    matches.Count > 1 ? "ambiguous-ids" : sense.Pos.Count == 1 ? "pos-unmatched" : "no-single-pos",
                    $"pos [{string.Join(", ", sense.Pos)}] ids [{string.Join(", ", ids)}]: {sense.Gloss}"));
            }
        }

        var minted = new List<MintedSense>();
        foreach (var group in attached.GroupBy(x => x.Id))
        {
            // discriminable = distinct readings: the double-printings that
            // survive the entry-level fold (a hyphen, a trailing semicolon,
            // one printing truncating the other's gloss) are one sense here
            var discriminable = Discriminable(group.ToList());
            if (discriminable.Count < 2)
            {
                continue; // the implicit whole-entry sense suffices
            }
            if (idsWithExistingRows.Contains(group.Key))
            {
                skim.Add(new SkimRow(group.First().Sense.Headword, "existing-rows",
                    $"{group.Key} already has curated senses; reconcile by hand"));
                continue;
            }
            if (discriminable.Any(x => x.Sense.Gloss.Contains('<') || x.Sense.Gloss.Contains('[')))
            {
                // an apparatus gloss is two readings in one string: whether
                // its printings are one sense is the apparatus question, not
                // a spelling one - a human's, until the upstream handling
                skim.Add(new SkimRow(group.First().Sense.Headword, "apparatus-gloss",
                    $"{group.Key}: {string.Join(" | ", discriminable.Select(x => x.Sense.Gloss))}"));
                continue;
            }
            var ordinal = 0;
            var rows = new List<MintedSense>();
            foreach (var (id, sense, fallback) in discriminable)
            {
                ordinal++;
                rows.Add(new MintedSense($"{id}#{ordinal}", id, $"cregeen:{sense.Headword}", sense.Gloss)
                {
                    Flag = fallback ? "single-id-attach" : "",
                });
                if (fallback)
                {
                    skim.Add(new SkimRow(sense.Headword, "single-id-attach",
                        $"pos [{string.Join(", ", sense.Pos)}] onto {id}: {sense.Gloss}"));
                }
            }
            FlagNearIdentical(rows);
            minted.AddRange(rows);
        }
        return (minted, skim);
    }

    /// <summary>The distinct readings among one id's printed senses: two
    /// glosses whose stemmed content words half-overlap are one reading -
    /// the grammatical children ("his feather" says "feather"), the
    /// truncated and label-prefixed reprintings ("discord, division;"),
    /// and the same verb glossed across its inflections ("lifting, rearing"
    /// / "to lift, rear"). The fuller gloss survives the fold. What is left
    /// is a reading of its own: 'dy cheilley' "together, joined" beside
    /// "one another".</summary>
    private static List<(string Id, PrintedSense Sense, bool Fallback)> Discriminable(
        List<(string Id, PrintedSense Sense, bool Fallback)> group)
    {
        var kept = new List<(string Id, PrintedSense Sense, bool Fallback)>();
        foreach (var candidate in group.Where(x => x.Sense.Gloss.Length > 0)
                     .DistinctBy(x => SenseKey(x.Sense.Gloss)))
        {
            var words = ContentWords(candidate.Sense.Gloss);
            var at = kept.FindIndex(k => SameReading(words, ContentWords(k.Sense.Gloss)));
            if (at < 0)
            {
                kept.Add(candidate);
            }
            else if (candidate.Sense.Gloss.Length > kept[at].Sense.Gloss.Length)
            {
                kept[at] = (kept[at].Id, candidate.Sense, kept[at].Fallback);
            }
        }
        return kept;
    }

    /// <summary>Marks the pairs one letter-slip apart (crudled/curdled shapes
    /// whose content words happen not to meet): the fold will not guess at
    /// them, so the reviewer's eye is asked instead</summary>
    private static void FlagNearIdentical(List<MintedSense> rows)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            for (var j = i + 1; j < rows.Count; j++)
            {
                var a = NormalizeGloss(rows[i].Gloss);
                var b = NormalizeGloss(rows[j].Gloss);
                if (Similarity(a, b) < 0.85)
                {
                    continue;
                }
                foreach (var row in new[] { rows[i], rows[j] }
                             .Where(x => !x.Flag.Contains("near-identical")))
                {
                    row.Flag = row.Flag.Length == 0 ? "near-identical" : row.Flag + "+near-identical";
                }
            }
        }
    }

    /// <summary>1 at identical, scaled down by edit distance</summary>
    private static double Similarity(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0)
        {
            return 0;
        }
        var previous = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++)
        {
            var current = new int[b.Length + 1];
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                current[j] = Math.Min(
                    Math.Min(previous[j] + 1, current[j - 1] + 1),
                    previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            }
            previous = current;
        }
        return 1.0 - (double)previous[b.Length] / Math.Max(a.Length, b.Length);
    }

    /// <summary>Whether half of the smaller gloss's content already says what
    /// the other says. All-formula glosses ("would be") never match here:
    /// they stay apart unless spelled alike.</summary>
    private static bool SameReading(HashSet<string> a, HashSet<string> b) =>
        a.Count > 0 && b.Count > 0
        && a.Intersect(b).Count() * 2 >= Math.Min(a.Count, b.Count);

    /// <summary>The gloss's stemmed content words: formula words drop, and a
    /// crude English stem folds the inflections the book glosses with
    /// ("lifting"/"lift", "reared"/"rear")</summary>
    internal static HashSet<string> ContentWords(string gloss)
    {
        var folded = new string(gloss.ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : ' ')
            .ToArray());
        return folded.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !FormulaWords.Contains(w))
            .Select(Stem)
            .ToHashSet();
    }

    private static string Stem(string word)
    {
        var stem = word;
        if (stem.Length > 5 && stem.EndsWith("ing", StringComparison.Ordinal))
        {
            stem = stem[..^3];
        }
        else if (stem.Length > 4 && (stem.EndsWith("eth", StringComparison.Ordinal)
                                     || stem.EndsWith("est", StringComparison.Ordinal)))
        {
            stem = stem[..^3];
        }
        else if (stem.Length > 4 && (stem.EndsWith("ed", StringComparison.Ordinal)
                                     || stem.EndsWith("es", StringComparison.Ordinal)
                                     || stem.EndsWith("en", StringComparison.Ordinal)))
        {
            stem = stem[..^2];
        }
        else if (stem.Length > 3 && stem.EndsWith("s", StringComparison.Ordinal))
        {
            stem = stem[..^1];
        }
        // 'shaking' stems to 'shak' and bare 'shake' must meet it there
        return stem.Length > 3 && stem.EndsWith("e", StringComparison.Ordinal) ? stem[..^1] : stem;
    }

    /// <summary>headword (lower) → its lemma ids, from the table's self rows,
    /// keyed by FORM: a self row is the table saying "this spelling is a
    /// citation form of this id", and the printed headword is not always the
    /// lemma display (Cregeen headwords the lenited 'cheilley' under keeill;
    /// its id is keilley.a, whose display is the unlenited spelling)</summary>
    private static Dictionary<string, IReadOnlyList<string>> ReadSelfIds(string cregeenTsv) =>
        SelfIdsByForm(File.ReadLines(cregeenTsv));

    internal static Dictionary<string, IReadOnlyList<string>> SelfIdsByForm(IEnumerable<string> lines)
    {
        var byForm = new Dictionary<string, List<string>>();
        foreach (var line in lines)
        {
            if (line.StartsWith('#') || line.StartsWith("form\t"))
            {
                continue;
            }
            var columns = line.Split('\t');
            if (columns.Length < 4 || columns[3] != "self")
            {
                continue;
            }
            var form = columns[0].ToLowerInvariant();
            if (!byForm.TryGetValue(form, out var ids))
            {
                byForm[form] = ids = [];
            }
            if (!ids.Contains(columns[1]))
            {
                ids.Add(columns[1]);
            }
        }
        return byForm.ToDictionary(x => x.Key, x => (IReadOnlyList<string>)x.Value);
    }

    /// <summary>Ids whose table pos still carries the editorial apparatus
    /// ("&lt;v&gt;[a]."): minted from the printed reading, not its correction</summary>
    private static List<(string Id, string Pos)> ReadApparatusIds(string cregeenTsv)
    {
        return File.ReadLines(cregeenTsv)
            .Where(x => !x.StartsWith('#') && !x.StartsWith("form\t"))
            .Select(x => x.Split('\t'))
            .Where(x => x.Length >= 5 && (x[4].Contains('<') || x[4].Contains('[')))
            .Select(x => (Id: x[1], Pos: x[4]))
            .Distinct()
            .OrderBy(x => x.Id, StringComparer.Ordinal)
            .ToList();
    }

    private static HashSet<string> ReadExistingSenseIds(string sensesTsv)
    {
        if (!File.Exists(sensesTsv))
        {
            return [];
        }
        return File.ReadLines(sensesTsv)
            .Where(x => x.Length > 0 && !x.StartsWith('#') && !x.StartsWith("senseId\t"))
            .Select(x => x.Split('\t'))
            .Where(x => x.Length >= 2)
            .Select(x => x[1])
            .ToHashSet();
    }
}
