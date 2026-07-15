using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CorpusSearch.Dependencies;
using CorpusSearch.Dependencies.Lucene;
using CorpusSearch.Model;

namespace CorpusSearch.Service;

/// <summary>
/// The history view behind the dictionary page (experimental): when a lexeme
/// is first attested in the corpus, in what spelling, how its use spreads
/// over time, and whether it belongs to traditional or revived Manx. All
/// claims are corpus-bounded on purpose - "first attested 1748" improves as
/// earlier sources (Phillips) are ingested, rather than asserting an origin
/// the data cannot back.
/// </summary>
public class DictionaryHistoryService(
    Searcher searcher, LemmaTable lemmaTable, DictionaryLookupService lookupService)
{
    /// <summary>The working boundary between traditional and revived Manx
    /// (Yn Çheshaght Ghailckagh, 1899). A document-level period declared by
    /// the data should replace this when the manifests carry one.</summary>
    public const int RevivalBoundaryYear = 1900;

    /// <summary>Scans are per form: bound the cluster so one request cannot
    /// fan out over an unbounded mutation list</summary>
    private const int MaxForms = 24;

    /// <summary>The popup dictionaries by era: the sources themselves date a
    /// word when the corpus cannot (a Phil Kelly-only word is a revival-era
    /// coinage by definition)</summary>
    private static readonly Dictionary<string, string> DictionaryEras = new()
    {
        ["Cregeen"] = "traditional (1835)",
        ["J Kelly Manx to English"] = "traditional (1866)",
        ["LearnManx Spoken Dictionary"] = "revived",
        ["Phil Kelly Manx to English"] = "revived",
    };

    /// <summary>Kelly's parenthesised cognates: "(Ir. bile; S.G. bil.)"</summary>
    private static readonly Regex CognateRun = new(
        @"\(((?:Ir|S\.G|Sc\.G|Gal|W|Lat|lat|Gr|Heb|Arm|Cor)\.\s[^()]{1,90})\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public DictionaryHistory History(string lang, string word)
    {
        // the lexeme the word belongs to; the surface word is its own cluster
        // seed when the table doesn't know it
        var lemmas = LemmaReadingsFor(lemmaTable, word);
        var forms = lemmas
            .SelectMany(lemmaTable.FormsOf)
            .Distinct(StringComparer.InvariantCultureIgnoreCase)
            .Order()
            .ToList();
        if (forms.Count == 0)
        {
            forms = [LemmaTable.NormalizeForm(word)];
        }
        var truncated = Math.Max(0, forms.Count - MaxForms);
        forms = forms.Take(MaxForms).ToList();

        var attested = new List<HistoryForm>();
        // the timeline counts documents, not occurrences: the Bible is by far
        // the largest work and would otherwise dominate every graph
        var attestingDocs = new HashSet<(string Ident, int Year)>();
        foreach (var form in forms)
        {
            try
            {
                var scanned = ScanForm(form);
                if (scanned != null)
                {
                    attested.Add(scanned.Value.Form);
                    attestingDocs.UnionWith(scanned.Value.DatedDocs);
                }
            }
            catch (Exception)
            {
                // a cluster member the query grammar cannot parse is skipped,
                // never fatal to the whole history
            }
        }

        var summaries = lookupService.Lookup(lang, word);
        var own = summaries.Where(x => x.RootDepth == 0 && x.NearMatchOf == null).ToList();

        return new DictionaryHistory
        {
            Word = word,
            Lemmas = lemmas.ToList(),
            RevivalBoundaryYear = RevivalBoundaryYear,
            Forms = attested,
            TruncatedForms = truncated,
            Earliest = attested
                .Where(x => x.EarliestYear != null)
                // an unambiguous spelling makes the headline claim; a shared
                // form ('vee') may be another lexeme entirely
                .OrderBy(x => x.SharedWithOtherLemmas)
                .ThenBy(x => x.EarliestYear)
                .FirstOrDefault(),
            Decades = attestingDocs
                .GroupBy(x => x.Year / 10 * 10)
                .Select(g => new DecadeCount
                {
                    Decade = g.Key,
                    Count = g.Select(x => x.Ident).Distinct().Count(),
                })
                .OrderBy(x => x.Decade)
                .ToList(),
            TraditionalCount = attested.Sum(x => x.TraditionalCount),
            RevivedCount = attested.Sum(x => x.RevivedCount),
            UndatedCount = attested.Sum(x => x.UndatedCount),
            Dictionaries = own
                .Select(x => x.DictionaryName)
                .OfType<string>()
                .Distinct()
                .Select(name => new HistoryDictionary
                {
                    Name = name,
                    Era = DictionaryEras.GetValueOrDefault(name),
                })
                .ToList(),
            Cognates = own
                .SelectMany(x => CognatesIn(x.Summary ?? ""))
                .Distinct()
                .Take(8)
                .ToList(),
        };
    }

    /// <summary>The cognates a definition cites: "(Ir. bile; S.G. bil.)" ->
    /// "Ir. bile; S.G. bil."</summary>
    internal static IEnumerable<string> CognatesIn(string summary)
    {
        return CognateRun.Matches(summary).Select(m => m.Groups[1].Value);
    }

    /// <summary>The lemma readings whose history the word should show. A word
    /// that is a headword itself keeps only its own lexeme: 'ass' (out) must
    /// not mix in the demutation guess fass (the popup's root chain offers
    /// that reading; the history must not merge two words' timelines).</summary>
    internal static IReadOnlyList<string> LemmaReadingsFor(LemmaTable table, string word)
    {
        var displays = table.DisplayLemmasFor(word);
        if (displays.Count == 0)
        {
            displays = table.CliticDisplayLemmasFor(word);
        }
        var self = displays
            .Where(d => LemmaTable.NormalizeForm(d) == LemmaTable.NormalizeForm(word))
            .ToList();
        return self.Count > 0 ? self : displays;
    }

    private (HistoryForm Form, List<(string Ident, int Year)> DatedDocs)? ScanForm(string form)
    {
        var scan = searcher.Scan(form);
        if (scan.NumberOfMatches == 0)
        {
            return null;
        }
        var dated = scan.DocumentResults
            .Where(x => x.StartDate != null)
            .OrderBy(x => x.StartDate)
            .ToList();
        var earliest = dated.FirstOrDefault();
        var otherLemmas = lemmaTable.DisplayLemmasFor(form);
        var historyForm = new HistoryForm
        {
            Form = form,
            Total = scan.NumberOfMatches,
            Documents = scan.NumberOfDocuments,
            SharedWithOtherLemmas = otherLemmas.Count > 1,
            EarliestYear = earliest?.StartDate?.Year,
            EarliestIdent = earliest?.Ident,
            EarliestTitle = earliest?.DocumentName,
            Sample = earliest?.Sample,
            SampleHighlights = earliest?.SampleHighlights,
            TraditionalCount = dated.Where(x => x.StartDate!.Value.Year < RevivalBoundaryYear).Sum(x => x.Count),
            RevivedCount = dated.Where(x => x.StartDate!.Value.Year >= RevivalBoundaryYear).Sum(x => x.Count),
            UndatedCount = scan.DocumentResults.Where(x => x.StartDate == null).Sum(x => x.Count),
        };
        return (historyForm, dated.Select(x => (x.Ident, x.StartDate!.Value.Year)).ToList());
    }
}

public class DictionaryHistory
{
    public required string Word { get; set; }
    /// <summary>The display lemmas the word resolves to; empty when the lemma
    /// table doesn't know the word (the word itself is scanned instead)</summary>
    public required List<string> Lemmas { get; set; }
    public int RevivalBoundaryYear { get; set; }
    /// <summary>The lexeme's corpus-attested spellings, mutations included</summary>
    public required List<HistoryForm> Forms { get; set; }
    /// <summary>How many cluster forms were not scanned (bounded fan-out)</summary>
    public int TruncatedForms { get; set; }
    /// <summary>The headline attestation: the earliest dated occurrence of an
    /// unambiguous spelling (falling back to a shared one)</summary>
    public HistoryForm? Earliest { get; set; }
    public required List<DecadeCount> Decades { get; set; }
    public int TraditionalCount { get; set; }
    public int RevivedCount { get; set; }
    public int UndatedCount { get; set; }
    public required List<HistoryDictionary> Dictionaries { get; set; }
    /// <summary>Cognates the dictionaries cite ("Ir. bile; S.G. bil.")</summary>
    public required List<string> Cognates { get; set; }
}

public class HistoryForm
{
    public required string Form { get; set; }
    public int Total { get; set; }
    public int Documents { get; set; }
    /// <summary>True when the spelling also belongs to another lexeme
    /// (mutation ambiguity): its counts include the other reading</summary>
    public bool SharedWithOtherLemmas { get; set; }
    public int? EarliestYear { get; set; }
    public string? EarliestIdent { get; set; }
    public string? EarliestTitle { get; set; }
    public string? Sample { get; set; }
    /// <summary>Where the form sits in <see cref="Sample"/>: lets the page quote a
    /// few words around the word rather than the head of a long verse</summary>
    public IReadOnlyList<HighlightRange>? SampleHighlights { get; set; }
    public int TraditionalCount { get; set; }
    public int RevivedCount { get; set; }
    public int UndatedCount { get; set; }
}

public class DecadeCount
{
    /// <summary>The decade's first year (1740 covers 1740-1749)</summary>
    public int Decade { get; set; }
    /// <summary>Documents attesting the lexeme in the decade - documents, not
    /// occurrences, so the largest works cannot dominate the graph</summary>
    public int Count { get; set; }
}

public class HistoryDictionary
{
    public required string Name { get; set; }
    /// <summary>"traditional (1835)" / "revived"; null when unclassified</summary>
    public string? Era { get; set; }
}
