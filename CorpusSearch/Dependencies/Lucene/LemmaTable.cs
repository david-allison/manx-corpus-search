using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CorpusSearch.Dependencies.Lucene;

/// <summary>
/// The vendored lemma table (Resources/cregeen.tsv, provenance in cregeen.tsv.source):
/// normalized surface form -> candidate lemma ids ("aase.v", "bagh-1").
/// A form maps to several lemmas (homographs; mutation candidates are additive), so
/// the candidate set is ambiguous by design: consumers index and query every candidate.
/// </summary>
public class LemmaTable
{
    private readonly Dictionary<string, string[]> candidatesByForm;
    private readonly HashSet<string> lemmaIds;

    // read once, shared by the index-time analyzer and the query path
    private static readonly Lazy<LemmaTable> Lazy = new(LoadVendored);
    public static LemmaTable Instance => Lazy.Value;

    private LemmaTable(Dictionary<string, string[]> candidatesByForm, HashSet<string> lemmaIds)
    {
        this.candidatesByForm = candidatesByForm;
        this.lemmaIds = lemmaIds;
    }

    public int FormCount => candidatesByForm.Count;

    /// <summary>The lemma ids of <paramref name="form"/> (normalized here); empty when unknown</summary>
    public IReadOnlyList<string> CandidatesFor(string form)
    {
        return candidatesByForm.TryGetValue(NormalizeForm(form), out var ids) ? ids : [];
    }

    /// <summary>Whether <paramref name="value"/> is a lemma id itself ("aase.v"): a query
    /// for one skips form resolution</summary>
    public bool IsLemmaId(string value) => lemmaIds.Contains(value);

    private static readonly char[] TrimChars = [' ', '.', ',', ';', ':', '\''];

    /// <summary>
    /// Mirrors the `form` column's normalization exactly (the cregeen.tsv contract):
    /// lowercase; `‑`→`-`; `’`→`'`; `ç`→`c`; hyphen runs → a single space; whitespace
    /// collapsed; ` .,;:'` trimmed. Multiword forms also appear space-collapsed in the
    /// table, so a hyphenated token ("aa-aase") resolves via its spaced form.
    /// </summary>
    public static string NormalizeForm(string input)
    {
        var lowered = input.ToLowerInvariant()
            .Replace('‑', '-') // non-breaking hyphen
            .Replace('’', '\'') // curly apostrophe
            .Replace('ç', 'c');

        var builder = new StringBuilder(lowered.Length);
        var pendingSeparator = false;
        foreach (var c in lowered)
        {
            if (c == '-' || char.IsWhiteSpace(c))
            {
                pendingSeparator = builder.Length > 0;
                continue;
            }
            if (pendingSeparator)
            {
                builder.Append(' ');
                pendingSeparator = false;
            }
            builder.Append(c);
        }

        return builder.ToString().Trim(TrimChars);
    }

    /// <summary>Reads the TSV (header row; `form` and `lemmaId` are the first columns)</summary>
    public static LemmaTable Load(TextReader reader)
    {
        var listsByForm = new Dictionary<string, List<string>>();
        var lemmaIds = new HashSet<string>();

        reader.ReadLine(); // header
        while (reader.ReadLine() is { } line)
        {
            var columns = line.Split('\t');
            if (columns.Length < 2 || columns[0].Length == 0)
            {
                continue;
            }
            var (form, lemmaId) = (columns[0], columns[1]);
            lemmaIds.Add(lemmaId);
            if (!listsByForm.TryGetValue(form, out var candidates))
            {
                listsByForm[form] = candidates = [];
            }
            // homographs repeat the (form, lemmaId) pair across rows: one candidate each
            if (!candidates.Contains(lemmaId))
            {
                candidates.Add(lemmaId);
            }
        }

        var candidatesByForm = new Dictionary<string, string[]>(listsByForm.Count);
        foreach (var (form, candidates) in listsByForm)
        {
            candidatesByForm[form] = [.. candidates];
        }
        return new LemmaTable(candidatesByForm, lemmaIds);
    }

    private static LemmaTable LoadVendored()
    {
        var path = Startup.GetLocalFile("Resources", "cregeen.tsv");
        if (!File.Exists(path))
        {
            // a broken checkout shouldn't take the whole server down: lemma
            // search just finds nothing
            Serilog.Log.Warning("{Path} not found: lemma search disabled", path);
            return new LemmaTable([], []);
        }
        using var reader = new StreamReader(path);
        return Load(reader);
    }
}
