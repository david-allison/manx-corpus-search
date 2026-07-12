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
    private readonly Dictionary<string, string[]> displayLemmasByForm;
    private readonly HashSet<string> lemmaIds;

    // read once, shared by the index-time analyzer and the query path
    private static readonly Lazy<LemmaTable> Lazy = new(LoadVendored);
    public static LemmaTable Instance => Lazy.Value;

    private LemmaTable(Dictionary<string, string[]> candidatesByForm,
        Dictionary<string, string[]> displayLemmasByForm, HashSet<string> lemmaIds)
    {
        this.candidatesByForm = candidatesByForm;
        this.displayLemmasByForm = displayLemmasByForm;
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

    /// <summary>The display lemmas of <paramref name="form"/> ("daase" -> "aase"): the
    /// radical, particle-free headwords a reader would look up in a dictionary</summary>
    public IReadOnlyList<string> DisplayLemmasFor(string form)
    {
        return displayLemmasByForm.TryGetValue(NormalizeForm(form), out var lemmas) ? lemmas : [];
    }

    private static readonly char[] TrimChars = [' ', '.', ',', ';', ':', '\''];

    /// <summary>
    /// Mirrors the `form` column's normalization exactly (the cregeen.tsv contract):
    /// lowercase; `‑`→`-`; `’`→`'`; `ç`→`c`; hyphen runs → a single space; whitespace
    /// collapsed; ` .,;:'` trimmed. Multiword forms also appear space-collapsed in the
    /// table, so a hyphenated token ("aa-aase") resolves via its spaced form.
    /// </summary>
    public static string NormalizeForm(string input)
    {
        // runs for every token of the corpus at index time: most arrive already
        // lowercase and plain from the analyzer, and skip the allocating path
        return NeedsNormalization(input) ? NormalizeSlow(input) : input;
    }

    private static bool NeedsNormalization(string input)
    {
        if (input.Length == 0)
        {
            return false;
        }
        if (IsTrimChar(input[0]) || IsTrimChar(input[^1]))
        {
            return true;
        }
        foreach (var c in input)
        {
            if (char.IsUpper(c) || c is 'ç' or '’' or '‑' or '-' || char.IsWhiteSpace(c))
            {
                return true;
            }
        }
        return false;

        static bool IsTrimChar(char c) => System.Array.IndexOf(TrimChars, c) >= 0;
    }

    private static string NormalizeSlow(string input)
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
        var displayListsByForm = new Dictionary<string, List<string>>();
        var lemmaIds = new HashSet<string>();

        reader.ReadLine(); // header
        while (reader.ReadLine() is { } line)
        {
            var columns = line.Split('\t');
            if (columns.Length < 3 || columns[0].Length == 0)
            {
                continue;
            }
            var (form, lemmaId, displayLemma) = (columns[0], columns[1], columns[2]);
            lemmaIds.Add(lemmaId);
            if (!listsByForm.TryGetValue(form, out var candidates))
            {
                listsByForm[form] = candidates = [];
                displayListsByForm[form] = [];
            }
            // homographs repeat the (form, lemmaId) pair across rows: one candidate each
            if (!candidates.Contains(lemmaId))
            {
                candidates.Add(lemmaId);
            }
            var displays = displayListsByForm[form];
            if (!displays.Contains(displayLemma))
            {
                displays.Add(displayLemma);
            }
        }

        var candidatesByForm = new Dictionary<string, string[]>(listsByForm.Count);
        var displayLemmasByForm = new Dictionary<string, string[]>(displayListsByForm.Count);
        foreach (var (form, candidates) in listsByForm)
        {
            candidatesByForm[form] = [.. candidates];
            displayLemmasByForm[form] = [.. displayListsByForm[form]];
        }
        return new LemmaTable(candidatesByForm, displayLemmasByForm, lemmaIds);
    }

    private static LemmaTable LoadVendored()
    {
        var path = Startup.GetLocalFile("Resources", "cregeen.tsv");
        if (!File.Exists(path))
        {
            // a broken checkout shouldn't take the whole server down: lemma
            // search just finds nothing
            Serilog.Log.Warning("{Path} not found: lemma search disabled", path);
            return new LemmaTable([], [], []);
        }
        using var reader = new StreamReader(path);
        return Load(reader);
    }
}
