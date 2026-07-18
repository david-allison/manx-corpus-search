using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CorpusSearch.Dependencies.Lucene;

/// <summary>
/// The lemma table (the external/manx-lemma-data submodule pins its revision;
/// published as Resources/cregeen.tsv):
/// normalized surface form -> candidate lemma ids ("aase.v", "bagh-1").
/// A form maps to several lemmas (homographs; mutation candidates are additive), so
/// the candidate set is ambiguous by design: consumers index and query every candidate.
/// </summary>
public class LemmaTable
{
    private readonly Dictionary<string, string[]> candidatesByForm;
    private readonly Dictionary<string, string[]> displayLemmasByForm;
    private readonly Dictionary<string, string[]> rootDisplayLemmasByForm;
    private readonly HashSet<string> lemmaIds;
    private readonly Dictionary<string, string> displayLemmaById;
    private readonly Dictionary<string, string> nameTypeById;
    private readonly Dictionary<string, string[]> phillipsViaByForm;
    private readonly HashSet<(string Form, string DisplayLemma)> unverifiedLinks;
    private readonly Dictionary<string, LemmaLinkSet> linkSetsByDisplay;
    // built on first use: the history view walks lemma -> forms, the reverse
    // of every other lookup
    private readonly Lazy<Dictionary<string, string[]>> formsByDisplay;

    // read once, shared by the index-time analyzer and the query path
    private static readonly Lazy<LemmaTable> Lazy = new(LoadVendored);
    public static LemmaTable Instance => Lazy.Value;

    private LemmaTable(Dictionary<string, string[]> candidatesByForm,
        Dictionary<string, string[]> displayLemmasByForm,
        Dictionary<string, string[]> rootDisplayLemmasByForm, HashSet<string> lemmaIds,
        Dictionary<string, string> displayLemmaById, Dictionary<string, string> nameTypeById,
        Dictionary<string, string[]> phillipsViaByForm,
        HashSet<(string, string)>? unverifiedLinks = null,
        Dictionary<string, LemmaLinkSet>? linkSetsByDisplay = null)
    {
        this.candidatesByForm = candidatesByForm;
        this.displayLemmasByForm = displayLemmasByForm;
        this.rootDisplayLemmasByForm = rootDisplayLemmasByForm;
        this.lemmaIds = lemmaIds;
        this.displayLemmaById = displayLemmaById;
        this.nameTypeById = nameTypeById;
        this.phillipsViaByForm = phillipsViaByForm;
        this.unverifiedLinks = unverifiedLinks ?? [];
        this.linkSetsByDisplay = linkSetsByDisplay ?? [];
        AllDisplayLemmas = this.linkSetsByDisplay.Values
            .Select(x => x.Lemma)
            .Order(StringComparer.Ordinal)
            .ToArray();
        formsByDisplay = new Lazy<Dictionary<string, string[]>>(() =>
            this.displayLemmasByForm
                .SelectMany(kv => kv.Value.Select(display => (Display: NormalizeForm(display), Form: kv.Key)))
                .GroupBy(x => x.Display, x => x.Form)
                .ToDictionary(g => g.Key, g => g.Distinct().Order().ToArray()));
    }

    public int FormCount => candidatesByForm.Count;

    /// <summary>Every form the tables map, normalized (the `form` column). The
    /// phrase scan reads the corpus for the multiword ones, so the lemma tree can
    /// grey a spaced form ('er n'aase') by evidence rather than a guess.</summary>
    public IEnumerable<string> AllForms => candidatesByForm.Keys;

    /// <summary>
    /// Every display lemma the tables link a form to — the `lemma` column's
    /// distinct spellings (~14,100 with the supplements, transcription rows
    /// aside) — sorted: the file order is the generator's, not an alphabet's
    /// ('yn nah' is row 2).
    /// </summary>
    public IReadOnlyList<string> AllDisplayLemmas { get; }

    /// <summary>
    /// The forms the tables link to <paramref name="displayLemma"/> (normalized
    /// here) and how — the lemma tree's data. Rows linking the lemma's own
    /// spelling to itself draw no branch: their standing surfaces as
    /// <see cref="LemmaLinkSet.SelfUnverified"/> instead. Null when the tables
    /// name no such lemma.
    /// </summary>
    public LemmaLinkSet? LinksOf(string displayLemma) =>
        linkSetsByDisplay.TryGetValue(NormalizeForm(displayLemma), out var links) ? links : null;

    /// <summary>The lemma ids of <paramref name="form"/> (normalized here); empty when unknown</summary>
    public IReadOnlyList<string> CandidatesFor(string form)
    {
        return candidatesByForm.TryGetValue(NormalizeForm(form), out var ids) ? ids : [];
    }

    /// <summary>Every phrase a particle link derives through ('e gheiney',
    /// 'dy aa-vioghey'): what the corpus phrase read must include, so the
    /// lemma tree can count a particle row by its phrase — the bare form
    /// rides after any particle at once, and its count answers for all of
    /// them together</summary>
    public IEnumerable<string> ParticlePhrases =>
        linkSetsByDisplay.Values
            .SelectMany(x => x.Links)
            .Where(x => x.LinkType == "particle" && x.Via.Contains(' '))
            .Select(x => x.Via);

    /// <summary>Whether <paramref name="value"/> is a lemma id itself ("aase.v"): a query
    /// for one skips form resolution</summary>
    public bool IsLemmaId(string value) => lemmaIds.Contains(value);

    /// <summary>The proper-noun class of <paramref name="lemmaId"/> ("personal", "place",
    /// "language", "ethnonym", "other"; "" for a bare np.), from the names supplement's
    /// pos column; null when the id is not a name</summary>
    public string? NameTypeOf(string lemmaId) =>
        nameTypeById.TryGetValue(lemmaId, out var nameType) ? nameType : null;

    /// <summary>The display lemma of <paramref name="lemmaId"/>; null when unknown</summary>
    public string? DisplayLemmaOf(string lemmaId) =>
        displayLemmaById.TryGetValue(lemmaId, out var lemma) ? lemma : null;

    /// <summary>The names supplement's display lemmas: near-match suggestion pool
    /// material — their popup content is the proper-noun metadata</summary>
    public IEnumerable<string> NameDisplayLemmas =>
        nameTypeById.Keys.Select(id => displayLemmaById.GetValueOrDefault(id)).OfType<string>();

    /// <summary>The display lemmas of <paramref name="form"/> ("daase" -> "aase"): the
    /// radical, particle-free headwords a reader would look up in a dictionary</summary>
    public IReadOnlyList<string> DisplayLemmasFor(string form)
    {
        return displayLemmasByForm.TryGetValue(NormalizeForm(form), out var lemmas) ? lemmas : [];
    }

    /// <summary><see cref="DisplayLemmasFor(string)"/> restricted to the readings of
    /// <paramref name="lemmaIds"/>: how a resolution layer narrows the popup's root chain</summary>
    public IReadOnlyList<string> DisplayLemmasFor(string form, IReadOnlyCollection<string> lemmaIds)
    {
        return DisplayLemmasFor(form)
            .Where(display => lemmaIds.Any(id => displayLemmaById.GetValueOrDefault(id) == display))
            .ToList();
    }

    /// <summary>The classical spellings a Phillips 1610 form stands for
    /// ("dwyne" -> "dooinney"), from the phillips supplement's via column:
    /// the UI explains the hop instead of implying a dictionary lists the
    /// 1610 spelling. Empty for every other form.</summary>
    public IReadOnlyList<string> PhillipsSpellingsOf(string form)
    {
        return phillipsViaByForm.TryGetValue(NormalizeForm(form), out var vias) ? vias : [];
    }

    /// <summary>Every form the table links to <paramref name="displayLemma"/>
    /// ("billey" -> billey, villey, biljyn...): the lexeme's attested-spelling
    /// cluster, for the history view. A form may belong to several lemmas
    /// (mutation ambiguity); pair with <see cref="DisplayLemmasFor(string)"/>
    /// to mark the shared ones.</summary>
    public IReadOnlyList<string> FormsOf(string displayLemma)
    {
        return formsByDisplay.Value.TryGetValue(NormalizeForm(displayLemma), out var forms) ? forms : [];
    }

    /// <summary>The display lemmas <paramref name="form"/> belongs to as part of
    /// another lexeme's paradigm ("deiney" -> "dooinney"): rows whose link is
    /// neither the form's own entry nor a demutation guess. Lets a root chain be
    /// walked ('gheiney' -> 'deiney' -> 'dooinney') without wandering into
    /// mutation ambiguity ('aase' -/-> 'faase').</summary>
    public IReadOnlyList<string> RootDisplayLemmasFor(string form)
    {
        return rootDisplayLemmasByForm.TryGetValue(NormalizeForm(form), out var lemmas) ? lemmas : [];
    }

    /// <summary>
    /// Whether the table reaches <paramref name="displayLemma"/> from
    /// <paramref name="form"/> only by rule, with no dictionary page attesting
    /// the link: a generated mutation ('Vonaco' under Monaco), an unvalidated
    /// demutation, a univerbation or a particle strip (see
    /// <see cref="IsUnverifiedRow"/>). A pair the print attests anywhere is
    /// verified, however many rules also produce it.
    /// </summary>
    /// <remarks>
    /// The generators reproduce the printed dictionaries plus rules over them,
    /// and the rules are where a wrong lemma comes from — so a reader deserves
    /// to see which of the two a root hangs on.
    /// </remarks>
    public bool IsUnverifiedLink(string form, string displayLemma) =>
        unverifiedLinks.Contains((NormalizeForm(form), displayLemma));

    /// <summary>
    /// The generator's own note that a row rests on a rule alone: a mutation spelt
    /// by <c>mutateForward</c> that no page lists ("generated-lenition"/"-eclipsis"),
    /// a radical it could not validate against the entry inventory
    /// ("demutation-unvalidated"), or a synthesized participle ("synthetic").
    /// </summary>
    /// <remarks>
    /// The link type is deliberately not consulted: `particle`, `univerbated` and
    /// cregeen's apostrophe-strip `mutation` all restate a headword the print
    /// carries ("e gheiney" -> gheiney), which is transcription, not derivation —
    /// flagging those would mark the ordinary chain and teach readers to ignore
    /// the mark.
    /// </remarks>
    private static readonly HashSet<string> UnverifiedNotes =
        ["generated-lenition", "generated-eclipsis", "demutation-unvalidated", "synthetic",
         // the vocab supplement: hand-asserted rows awaiting review
         "unverified"];

    // the note column holds space-separated tags ("modern-variant unverified")
    private static bool IsUnverifiedRow(string note) =>
        note.Split(' ').Any(UnverifiedNotes.Contains);

    private static bool HasNoteTag(string note, string tag) =>
        note.Split(' ').Contains(tag);

    /// <summary>
    /// The candidate ids of a form read as a productive clitic contraction:
    /// `t'X`/`v'X` are present-/past-of-'bee' + X, `X'n` is X + the article 'yn'.
    /// The fallback for forms the table doesn't cover directly — callers give a
    /// direct table row precedence.
    /// </summary>
    public IReadOnlyList<string> CliticCandidatesFor(string form)
    {
        return CliticLookup(form, CandidatesFor);
    }

    /// <summary>The display lemmas of a clitic contraction's parts
    /// (see <see cref="CliticCandidatesFor"/>)</summary>
    public IReadOnlyList<string> CliticDisplayLemmasFor(string form)
    {
        return CliticLookup(form, DisplayLemmasFor);
    }

    private static IReadOnlyList<string> CliticLookup(string form, Func<string, IReadOnlyList<string>> lookup)
    {
        var combined = new List<string>();
        foreach (var part in CliticParts(NormalizeForm(form)) ?? [])
        {
            foreach (var value in lookup(part))
            {
                if (!combined.Contains(value))
                {
                    combined.Add(value);
                }
            }
        }
        return combined;
    }

    private static string[]? CliticParts(string form)
    {
        if (form.Length > 2 && form.StartsWith("t'")) return ["ta", form[2..]];
        if (form.Length > 2 && form.StartsWith("v'")) return ["va", form[2..]];
        if (form.Length > 2 && form.EndsWith("'n")) return [form[..^2], "yn"];
        return null;
    }

    private static readonly char[] TrimChars = [' ', '.', ',', ';', ':', '\''];

    /// <summary>
    /// Mirrors the `form` column's normalization exactly (the cregeen.tsv contract):
    /// lowercase; `‑`→`-`; `’`→`'`; combining marks stripped after NFD decomposition
    /// (`ç`→`c`, `benreïn`→`benrein`, `mârish`→`marish` — diacritics in the sources
    /// are display conventions, never lexically contrastive); hyphen runs → a single
    /// space; whitespace collapsed; ` .,;:'` trimmed. Multiword forms also appear
    /// space-collapsed in the table, so a hyphenated token ("aa-aase") resolves via
    /// its spaced form.
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
            // any non-ASCII goes the slow path: it may carry combining marks
            if (char.IsUpper(c) || c is '-' || c > (char)127 || char.IsWhiteSpace(c))
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
            .Replace('’', '\''); // curly apostrophe
        lowered = StripCombiningMarks(lowered); // ç→c, benreïn→benrein, mârish→marish

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

    /// <summary>Diacritics are display conventions in the sources: combining marks
    /// are stripped after NFD decomposition, mirroring the generator's fold</summary>
    private static string StripCombiningMarks(string input)
    {
        var hasNonAscii = false;
        foreach (var c in input)
        {
            if (c > (char)127)
            {
                hasNonAscii = true;
                break;
            }
        }
        if (!hasNonAscii)
        {
            return input;
        }
        var builder = new StringBuilder(input.Length);
        foreach (var c in input.Normalize(NormalizationForm.FormD))
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }
        return builder.ToString();
    }

    /// <summary>Reads the TSV (header row; `form` and `lemmaId` are the first columns)</summary>
    public static LemmaTable Load(TextReader reader)
    {
        return Load([reader]);
    }

    /// <summary>One table over several TSVs (cregeen.tsv plus the names.tsv supplement):
    /// candidate sets merge per form, and a repeated (form, lemmaId) pair — a supplement
    /// bridge entry re-heading a Cregeen name — stays one candidate</summary>
    public static LemmaTable Load(IEnumerable<TextReader> readers)
    {
        // unlabeled: links carry no provenance, which only the lemma tree names
        return Load(readers.Select(reader => (reader, "")));
    }

    /// <summary><see cref="Load(IEnumerable{TextReader})"/> with each file's
    /// source named ("cregeen", "names", "phillips", "vocab"): what lets the
    /// lemma tree say which book records a form no corpus text uses</summary>
    public static LemmaTable Load(IEnumerable<(TextReader Reader, string Source)> sources)
    {
        var listsByForm = new Dictionary<string, List<string>>();
        var displayListsByForm = new Dictionary<string, List<string>>();
        var rootListsByForm = new Dictionary<string, List<string>>();
        var lemmaIds = new HashSet<string>();
        var displayLemmaById = new Dictionary<string, string>();
        var nameTypeById = new Dictionary<string, string>();
        var phillipsViaLists = new Dictionary<string, List<string>>();
        // (form, displayLemma) links seen by rule, and those the print attests:
        // a pair in both is verified, so the attested set subtracts at the end
        var unverifiedLinks = new HashSet<(string, string)>();
        var verifiedLinks = new HashSet<(string, string)>();
        var linkSets = new Dictionary<string, MutableLinkSet>();

        foreach (var (reader, source) in sources)
        {
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
                displayLemmaById.TryAdd(lemmaId, displayLemma);
                // the names supplement's pos column: "np. personal", "np. place", ...
                if (columns.Length > 4 && columns[4].StartsWith("np."))
                {
                    nameTypeById.TryAdd(lemmaId, columns[4]["np.".Length..].Trim());
                }
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
                // paradigm links (see RootDisplayLemmasFor): not the form's own entry,
                // not a demutation guess
                var linkType = columns.Length > 3 ? columns[3] : "self";
                var note = columns.Length > 6 ? columns[6] : "";
                var unverifiedRow = IsUnverifiedRow(note);
                // a closed-class paradigm row (ta -> bee) is the treebank's
                // hand-declared suppletion, not the book's print: certain
                // enough to draw unmarked, but no book may take the credit
                var rowSource = HasNoteTag(note, "closed-class") ? "" : source;
                // the lemma tree's rows: every way a form hangs off this display
                // lemma, deduped per (linkType, form) — a link any row attests
                // stays verified however many rules also produce it. The
                // analyzer's transcription rows — a spaced headword written
                // solid ('evee' for Cregeen's 'e vee': `univerbated` links, and
                // `collapsed`-noted respellings; for a hyphenated lemma their
                // lemma column is solid too ('meeaarlooid') — draw no branch
                // and name no lemma: machinery for matching run-together text,
                // not forms of the word.
                if (linkType != "univerbated" && !HasNoteTag(note, "collapsed"))
                {
                    var displayKey = NormalizeForm(displayLemma);
                    if (!linkSets.TryGetValue(displayKey, out var linkSet))
                    {
                        linkSets[displayKey] = linkSet = new MutableLinkSet { Lemma = displayLemma };
                    }
                    if (form == displayKey)
                    {
                        // the lemma's own row draws no branch, but a hand-asserted
                        // one (the vocab supplement) makes the root itself a guess
                        if (linkType == "self")
                        {
                            // the first attesting file names the root's book;
                            // a guessed row must not (a verified one may later)
                            if (!linkSet.SelfVerifiedSeen && !unverifiedRow)
                            {
                                linkSet.SelfSource = rowSource;
                            }
                            linkSet.SelfSeen = true;
                            linkSet.SelfVerifiedSeen |= !unverifiedRow;
                        }
                    }
                    else
                    {
                        // the via column names what the form derives through:
                        // 'pyaghyn' inflects pyagh, the variant, not peiagh
                        // itself. Kept only when it is a genuine intermediate —
                        // deriving through yourself or the lemma says nothing.
                        var viaKey = columns.Length > 5 ? NormalizeForm(columns[5]) : "";
                        if (viaKey == form || viaKey == displayKey)
                        {
                            viaKey = "";
                        }
                        var linkKey = (linkType, form);
                        if (linkSet.Links.TryGetValue(linkKey, out var soFar))
                        {
                            linkSet.Links[linkKey] = (
                                soFar.Unverified && unverifiedRow,
                                soFar.Via.Length > 0 ? soFar.Via : viaKey,
                                // the source is the attestation's: a verified
                                // row's file names the link's book, however
                                // many rules from elsewhere also produce it
                                soFar.Unverified && !unverifiedRow ? rowSource : soFar.Source);
                        }
                        else
                        {
                            linkSet.Links[linkKey] = (unverifiedRow, viaKey, rowSource);
                        }
                    }
                }
                // the Phillips supplement's via column: the classical spelling
                // the 1610 form stands for
                if (linkType == "phillips" && columns.Length > 5 && columns[5].Length > 0)
                {
                    if (!phillipsViaLists.TryGetValue(form, out var vias))
                    {
                        phillipsViaLists[form] = vias = [];
                    }
                    if (!vias.Contains(columns[5]))
                    {
                        vias.Add(columns[5]);
                    }
                }
                if (linkType is not ("self" or "demutated"))
                {
                    if (!rootListsByForm.TryGetValue(form, out var roots))
                    {
                        rootListsByForm[form] = roots = [];
                    }
                    if (!roots.Contains(displayLemma))
                    {
                        roots.Add(displayLemma);
                    }
                    (unverifiedRow ? unverifiedLinks : verifiedLinks)
                        .Add((form, displayLemma));
                }
            }
        }

        var candidatesByForm = new Dictionary<string, string[]>(listsByForm.Count);
        var displayLemmasByForm = new Dictionary<string, string[]>(displayListsByForm.Count);
        var rootDisplayLemmasByForm = new Dictionary<string, string[]>(rootListsByForm.Count);
        foreach (var (form, candidates) in listsByForm)
        {
            candidatesByForm[form] = [.. candidates];
            displayLemmasByForm[form] = [.. displayListsByForm[form]];
        }
        foreach (var (form, roots) in rootListsByForm)
        {
            rootDisplayLemmasByForm[form] = [.. roots];
        }
        unverifiedLinks.ExceptWith(verifiedLinks);
        var linkSetsByDisplay = linkSets.ToDictionary(
            kv => kv.Key,
            kv => new LemmaLinkSet
            {
                Lemma = kv.Value.Lemma,
                SelfUnverified = kv.Value.SelfSeen && !kv.Value.SelfVerifiedSeen,
                SelfSource = kv.Value.SelfSource,
                Links = kv.Value.Links
                    .Select(link => new LemmaLink(
                        link.Key.LinkType, link.Key.Form, link.Value.Unverified,
                        link.Value.Via, link.Value.Source))
                    .ToArray(),
            });
        return new LemmaTable(candidatesByForm, displayLemmasByForm, rootDisplayLemmasByForm, lemmaIds,
            displayLemmaById, nameTypeById,
            phillipsViaLists.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray()),
            unverifiedLinks, linkSetsByDisplay);
    }

    /// <summary>Accumulates one display lemma's rows while <see cref="Load(IEnumerable{TextReader})"/> reads</summary>
    private sealed class MutableLinkSet
    {
        public required string Lemma { get; init; }
        public bool SelfSeen;
        public bool SelfVerifiedSeen;
        public string SelfSource = "";
        public Dictionary<(string LinkType, string Form), (bool Unverified, string Via, string Source)> Links { get; } = [];
    }

    private static LemmaTable LoadVendored()
    {
        var path = Startup.GetLocalFile("Resources", "cregeen.tsv");
        if (!File.Exists(path))
        {
            // an uninitialised submodule shouldn't take the whole server down:
            // lemma search just finds nothing
            Serilog.Log.Warning("{Path} not found (is the submodule initialised?): lemma search disabled", path);
            return new LemmaTable([], [], [], [], [], [], []);
        }
        // the supplements ride beside the table when vendored: proper nouns,
        // the Phillips 1610 spelling links, and the hand-asserted vocabulary.
        // Each is named, so a link can say which book or supplement attests it.
        var sources = new List<(TextReader Reader, string Source)>
        {
            (new StreamReader(path), "cregeen"),
        };
        foreach (var supplement in new[] { "names.tsv", "phillips.tsv", "vocab.tsv" })
        {
            var supplementPath = Startup.GetLocalFile("Resources", supplement);
            if (File.Exists(supplementPath))
            {
                sources.Add((new StreamReader(supplementPath),
                    supplement[..^".tsv".Length]));
            }
        }
        try
        {
            return Load(sources);
        }
        finally
        {
            sources.ForEach(x => x.Reader.Dispose());
        }
    }
}

/// <summary>Everything the tables hang off one display lemma: the lemma tree's data</summary>
public sealed class LemmaLinkSet
{
    /// <summary>The lemma as the `lemma` column spells it ("aa-aase", "Aachummey"):
    /// the form-keyed lookups fold this, the tree displays it</summary>
    public required string Lemma { get; init; }

    /// <summary>The lemma's own row rests on a hand-assertion or a rule alone
    /// (the vocab supplement's 'peiagh'): the root itself is a guess, not print</summary>
    public required bool SelfUnverified { get; init; }

    /// <summary>The file whose print attests the lemma's own row ("cregeen",
    /// "names", ...); "" when nothing does, when the source is unnamed, or for
    /// a closed-class paradigm row, which no book may take credit for</summary>
    public string SelfSource { get; init; } = "";

    /// <summary>Each form linked to the lemma, once per way it is linked: 'deiney'
    /// is both `inflected` and `plural` of dooinney, and that is two links</summary>
    public required IReadOnlyList<LemmaLink> Links { get; init; }
}

/// <summary>One form's link to a display lemma. <paramref name="Unverified"/>
/// mirrors <see cref="LemmaTable.IsUnverifiedLink"/>: no row of this link type
/// attests the pair — it rests on a rule ('generated-lenition') or a
/// hand-assertion (the vocab supplement) alone. <paramref name="Via"/> is the
/// intermediate the form derives through, normalized ('pyaghyn' inflects the
/// variant 'pyagh', not peiagh itself); "" where it derives from the lemma
/// directly. <paramref name="Source"/> names the attesting row's file
/// ("cregeen", "names", ...): the verified row's, however many rules from
/// elsewhere also produce the link; "" when unnamed or when the row is the
/// treebank's closed-class paradigm, which no book may take credit for.</summary>
public sealed record LemmaLink(
    string LinkType, string Form, bool Unverified, string Via = "", string Source = "");
