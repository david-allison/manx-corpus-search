using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CorpusSearch.Dependencies.Lucene;
using CorpusSearch.Model;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Util;

namespace CorpusSearch.Service;

/// <summary>
/// Resolves a user's selection to summaries from the dictionaries which handle the query language.
///
/// The selection alone is often not enough (#135): the surrounding text lets us expand a word to a
/// known phrase/idiom, and a compound such as 'goll-mygeayrt' can be broken into its parts.
/// </summary>
public class DictionaryLookupService(IEnumerable<ISearchDictionary> dictionaryServices, LemmaTable lemmaTable,
    LemmaResolver lemmaResolver)
{
    /// <summary>The longest dictionary phrase (in words) we attempt to match around a selection</summary>
    private const int MaxPhraseWords = 4;

    /// <summary>Trimmed from the edges of each word; apostrophes/hyphens are word-internal in Manx</summary>
    private static readonly char[] Punctuation = ['.', ',', '?', ';', ':', '!', '(', ')', '[', ']', '{', '}', '"', '“', '”', '…'];

    /// <summary>Each marks a compound/contraction whose parts may have their own entries</summary>
    private static readonly char[] WordSeparators = ['-', '\'', '’'];

    private readonly ISearchDictionary[] dictionaryServices = dictionaryServices.ToArray();

    /// <param name="lang">the query language, for example 'gv'</param>
    /// <param name="selection">the word/phrase the user selected</param>
    /// <param name="context">the text surrounding the selection (typically the line it appears in)</param>
    public List<DictionarySummary> Lookup(string lang, string selection, string? context = null)
    {
        var dictionaries = dictionaryServices.Where(x => x.QueryLanguages.Contains(lang)).ToList();

        List<DictionarySummary> GetSummaries(IEnumerable<string> queries) =>
            queries.SelectMany(query =>
                dictionaries.SelectMany(d => d.GetSummaries(query, basic: true)
                        .Select(summary =>
                        {
                            // let the client attribute each entry to its dictionary (#51)
                            summary.DictionaryName = d.Identifier;
                            return summary;
                        }))
                    // an entry can list the query as a mere variant ('EEN, YN'): those headed by it come first
                    .OrderBy(x => string.Equals(x.PrimaryWord, query, StringComparison.InvariantCultureIgnoreCase) ? 0 : 1))
                .ToList();

        var candidates = GetCandidates(selection, context);
        var results = GetSummaries(candidates);
        if (lang == "gv")
        {
            // the root chain of an inflected/mutated selection follows the surface
            // candidates, each hop tagged with its depth so the client can nest it
            // ('gheiney' -> 'deiney' -> 'dooinney'): the reader always gets the
            // headwords a dictionary actually lists, without them posing as
            // entries for the selection
            var seen = new HashSet<string>(candidates, StringComparer.InvariantCultureIgnoreCase);
            var frontier = ResolvedDisplayLemmas(selection, context).Where(x => !seen.Contains(x)).ToList();
            // which sense of a root the chain means: the word classes of the
            // candidate ids that produced each display (row -> bee.v is the
            // verb 'bee', never the food)
            var expectedPos = ExpectedPosByDisplay(selection, context);
            for (var depth = 1; frontier.Count > 0 && depth <= 3; depth++)
            {
                seen.UnionWith(frontier);
                foreach (var display in frontier)
                {
                    var summaries = GetSummaries([display]);
                    if (depth == 1 && expectedPos.TryGetValue(display, out var expected))
                    {
                        // entries without a declared class are kept; if the filter
                        // would empty the list, the guess loses and everything stays
                        var filtered = summaries
                            .Where(x => x.PartsOfSpeech == null || x.PartsOfSpeech.Count == 0
                                || x.PartsOfSpeech.Any(expected.Contains))
                            .ToList();
                        if (filtered.Count > 0)
                        {
                            summaries = filtered;
                        }
                    }
                    foreach (var summary in summaries)
                    {
                        summary.RootDepth = depth;
                        results.Add(summary);
                    }
                }
                // deeper hops follow paradigm links only: a mutation guess is a
                // candidate reading of the selection, not a root of the root
                frontier = frontier
                    .SelectMany(root => lemmaTable.RootDisplayLemmasFor(root))
                    .Where(x => !seen.Contains(x))
                    .Distinct(StringComparer.InvariantCultureIgnoreCase)
                    .ToList();
            }
        }

        if (results.Count == 0 && lang == "gv")
        {
            // the names supplement documents proper nouns no dictionary lists
            // (Solomon, Yudah): the popup still identifies the name
            results = NameSummaries(selection);
        }

        if (results.Count == 0)
        {
            // 'goll-mygeayrt' has no entry, but 'goll' and 'mygeayrt' do
            results = GetSummaries(GetParts(selection));
        }

        if (results.Count == 0 && lang == "gv")
        {
            // last resort: entries for near spellings ("did you mean"), tagged
            // so the client presents them as suggestions, never as matches
            foreach (var suggestion in NearMatches(selection))
            {
                var entries = GetSummaries([suggestion]);
                if (entries.Count == 0)
                {
                    entries = NameSummaries(suggestion);
                }
                foreach (var entry in entries)
                {
                    entry.NearMatchOf = suggestion;
                }
                results.AddRange(entries);
            }
        }

        return Deduplicate(results);
    }

    /// <summary>The teanglann-style full page for a word (experimental): the
    /// lookup re-shaped into per-dictionary groups, the word's own recording
    /// pulled out as the page control, near-match suggestions marked as a tier</summary>
    public DictionaryPage Page(string lang, string word)
    {
        var summaries = WithoutDemutationGuesses(word, Lookup(lang, word));
        var audio = summaries.FirstOrDefault(x =>
            x.AudioUrl != null && x.NearMatchOf == null
            && string.Equals(x.PrimaryWord, word, StringComparison.InvariantCultureIgnoreCase));
        return new DictionaryPage
        {
            Word = word,
            IsSuggestionTier = summaries.Count > 0 && summaries.All(x => x.NearMatchOf != null),
            Audio = audio == null
                ? null
                : new DictionaryPageAudio
                {
                    Url = audio.AudioUrl!,
                    Credit = audio.SourceCredit ?? audio.DictionaryName,
                    SourceUrl = audio.SourceUrl,
                },
            Groups = summaries
                .GroupBy(x => x.DictionaryName ?? "")
                .Select(g => new DictionaryPageGroup
                {
                    Dictionary = g.Key,
                    SourceUrl = g.Select(x => x.SourceUrl).FirstOrDefault(x => x != null),
                    Entries = g.ToList(),
                })
                .ToList(),
        };
    }

    /// <summary>The dictionary page looks up a headword with no sentence
    /// context: when the word is a headword itself, demutation guesses stay
    /// in the tap popup - where the surrounding line makes 'ass, or maybe
    /// lenited fass' worth offering - and off the ass page. Paradigm roots
    /// (smessey -> olk) are not guesses and stay.</summary>
    private List<DictionarySummary> WithoutDemutationGuesses(string word, List<DictionarySummary> summaries)
    {
        var displays = lemmaTable.DisplayLemmasFor(word);
        var self = LemmaTable.NormalizeForm(word);
        if (!displays.Any(d => LemmaTable.NormalizeForm(d) == self))
        {
            return summaries;
        }
        var paradigmRoots = lemmaTable.RootDisplayLemmasFor(word)
            .Select(LemmaTable.NormalizeForm).ToHashSet();
        var guesses = displays
            .Select(LemmaTable.NormalizeForm)
            .Where(d => d != self && !paradigmRoots.Contains(d))
            .ToHashSet();
        return summaries
            .Where(x => x.RootDepth == 0 || !guesses.Contains(LemmaTable.NormalizeForm(x.PrimaryWord)))
            .ToList();
    }

    /// <summary>Words a dictionary (or the names metadata) can answer for, within
    /// a length-scaled edit distance of the selection: distance 1 up to five
    /// letters, 2 above — distance-2 guesses on short Manx words are noise.
    /// Runs only when every other tier came back empty.</summary>
    private List<string> NearMatches(string selection)
    {
        var norm = LemmaTable.NormalizeForm(selection);
        if (norm.Length < 4 || norm.Contains(' '))
        {
            // too short to guess against; phrases have their own fallbacks
            return [];
        }
        var max = norm.Length <= 5 ? 1 : 2;
        var pool = nearMatchPool ??= BuildNearMatchPool();
        var scored = new List<(int Distance, bool DifferentInitial, string Word)>();
        for (var length = norm.Length - max; length <= norm.Length + max; length++)
        {
            if (!pool.TryGetValue(length, out var bucket))
            {
                continue;
            }
            foreach (var (candidate, word) in bucket)
            {
                var distance = BoundedEditDistance(norm, candidate, max);
                if (distance >= 1 && distance <= max)
                {
                    scored.Add((distance, candidate[0] != norm[0], word));
                }
            }
        }
        return scored
            .OrderBy(x => x.Distance)
            .ThenBy(x => x.DifferentInitial) // a wrong first letter is the rarer slip
            .ThenBy(x => x.Word, StringComparer.InvariantCultureIgnoreCase)
            .Select(x => x.Word)
            .Take(4)
            .ToList();
    }

    /// <summary>normalized length -> (normalized word, display word): every single
    /// word the Manx dictionaries answer for, plus the names supplement's display
    /// lemmas. Built on the first total-miss lookup.</summary>
    private Dictionary<int, List<(string Norm, string Word)>>? nearMatchPool;

    private Dictionary<int, List<(string Norm, string Word)>> BuildNearMatchPool()
    {
        var pool = new Dictionary<int, List<(string, string)>>();
        var seen = new HashSet<string>();
        var words = dictionaryServices
            .Where(d => d.QueryLanguages.Contains("gv"))
            .SelectMany(d => d.AllWords)
            .Concat(lemmaTable.NameDisplayLemmas);
        foreach (var word in words)
        {
            var norm = LemmaTable.NormalizeForm(word);
            if (norm.Length < 3 || norm.Contains(' ') || !seen.Add(norm))
            {
                continue;
            }
            if (!pool.TryGetValue(norm.Length, out var bucket))
            {
                pool[norm.Length] = bucket = [];
            }
            bucket.Add((norm, word));
        }
        return pool;
    }

    /// <summary>Damerau-Levenshtein (adjacent transposition = 1 edit), giving up
    /// past <paramref name="max"/>: returns max + 1 when the words are further apart</summary>
    private static int BoundedEditDistance(string a, string b, int max)
    {
        if (Math.Abs(a.Length - b.Length) > max)
        {
            return max + 1;
        }
        var prevPrev = new int[b.Length + 1];
        var prev = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            prev[j] = j;
        }
        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            var rowBest = current[0];
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                {
                    current[j] = Math.Min(current[j], prevPrev[j - 2] + 1);
                }
                rowBest = Math.Min(rowBest, current[j]);
            }
            if (rowBest > max)
            {
                return max + 1;
            }
            (prevPrev, prev, current) = (prev, current, prevPrev);
        }
        return prev[b.Length];
    }

    /// <summary>Entries synthesized from the lemma table's name metadata (the
    /// names.tsv pos column): the selection's own name at depth 0, the name a
    /// mutated spelling belongs to ('Yudah' -> Judah) at depth 1</summary>
    private List<DictionarySummary> NameSummaries(string selection)
    {
        var results = new List<DictionarySummary>();
        var seen = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        foreach (var lemmaId in lemmaTable.CandidatesFor(selection))
        {
            var nameType = lemmaTable.NameTypeOf(lemmaId);
            var display = lemmaTable.DisplayLemmaOf(lemmaId);
            if (nameType == null || display == null || !seen.Add(display))
            {
                continue;
            }
            var isSelection = LemmaTable.NormalizeForm(display) == LemmaTable.NormalizeForm(selection);
            results.Add(new DictionarySummary
            {
                PrimaryWord = display,
                Summary = NameTypeDescription(nameType),
                DictionaryName = "Proper nouns",
                RootDepth = isSelection ? 0 : 1,
            });
        }
        return results;
    }

    private static string NameTypeDescription(string nameType) => nameType switch
    {
        "personal" => "personal name",
        "place" => "place name",
        "language" => "language",
        "ethnonym" => "people",
        _ => "proper noun",
    };

    /// <summary>
    /// The selection's display lemmas, narrowed by the resolution layers when they
    /// resolve it (<see cref="LemmaResolver"/>): a reading rejected for this line —
    /// 'fer' under a prepositional 'er' — no longer seeds the root chain. Unresolved
    /// selections keep every reading, so nothing the table offers is ever lost.
    /// </summary>
    private IEnumerable<string> ResolvedDisplayLemmas(string selection, string? context)
    {
        var allowedIds = ResolvedLemmaIds(selection, context);
        return allowedIds == null
            ? lemmaTable.DisplayLemmasFor(selection)
            : lemmaTable.DisplayLemmasFor(selection, allowedIds);
    }

    /// <summary>display lemma -> the word classes of the candidate ids behind it
    /// ("bee" -> Verb when reached from row/bee.v). Displays reached through an id
    /// of unknown class stay unconstrained.</summary>
    private Dictionary<string, HashSet<string>> ExpectedPosByDisplay(string selection, string? context)
    {
        var ids = ResolvedLemmaIds(selection, context)
                  ?? (IReadOnlyCollection<string>)lemmaTable.CandidatesFor(selection);
        var result = new Dictionary<string, HashSet<string>>(StringComparer.InvariantCultureIgnoreCase);
        var unconstrained = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        foreach (var id in ids)
        {
            var display = lemmaTable.DisplayLemmaOf(id);
            if (display == null)
            {
                continue;
            }
            var pos = id[(id.LastIndexOf('.') + 1)..] switch
            {
                "v" => "Verb",
                "n" => "Noun",
                "a" => "Adjective",
                _ => null, // .x, .np and explicit ids ("bagh-1") constrain nothing
            };
            if (pos == null || id.LastIndexOf('.') < 0)
            {
                unconstrained.Add(display);
                continue;
            }
            if (!result.TryGetValue(display, out var set))
            {
                result[display] = set = [];
            }
            set.Add(pos);
        }
        foreach (var display in unconstrained)
        {
            result.Remove(display);
        }
        return result;
    }

    /// <summary>The resolved candidate ids of the selection: the form-level override
    /// first, then the context line's sidecar rows; null when unresolved</summary>
    private IReadOnlyCollection<string>? ResolvedLemmaIds(string selection, string? context)
    {
        if (lemmaTable.CandidatesFor(selection).Count < 2)
        {
            return null;
        }
        if (lemmaResolver.OverrideFor(selection) is { } overridden)
        {
            return overridden;
        }
        if (string.IsNullOrWhiteSpace(context) || !lemmaResolver.HasSidecarRows)
        {
            return null;
        }
        // the client sends the displayed line: normalizing it re-derives the exact
        // token stream the sidecar's line key was computed over at generation time
        var selectionTokens = LemmaResolver.TokenizeManx(DocumentLine.NormalizeManx(selection));
        if (selectionTokens.Count != 1)
        {
            return null;
        }
        var tokens = LemmaResolver.TokenizeManx(DocumentLine.NormalizeManx(context));
        var lineKey = LemmaResolver.LineKey(tokens);
        HashSet<string>? allowed = null;
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i] != selectionTokens[0])
            {
                continue;
            }
            var ids = lemmaResolver.SidecarFor(lineKey, i, tokens[i], includePopupTier: true);
            if (ids == null)
            {
                // an unresolved occurrence keeps every reading in play
                return null;
            }
            (allowed ??= []).UnionWith(ids);
        }
        return allowed;
    }

    /// <summary>
    /// The dictionary-coverage debug view (#dict-debug): for each line, every
    /// token with its offsets and how a tap on it would resolve.
    /// </summary>
    /// <remarks>
    /// Statuses, best first: "entry" — a dictionary lists the token itself;
    /// "root" — no direct entry, but the lemma table's root chain reaches one
    /// ('daase' -> aase); "lemma" — the lemma table knows the token but no
    /// dictionary documents any of its lemmas; "none" — unknown everywhere.
    /// </remarks>
    public List<List<TokenCoverage>> Coverage(string lang, IReadOnlyList<string> lines)
    {
        var dictionaries = dictionaryServices.Where(x => x.QueryLanguages.Contains(lang)).ToList();
        bool InDictionary(string word) => dictionaries.Any(d => d.ContainsWord(word));

        var result = new List<List<TokenCoverage>>(lines.Count);
        foreach (var line in lines)
        {
            var tokens = new List<TokenCoverage>();
            var tokenizer = new ManxTokenizer(LuceneVersion.LUCENE_48, new StringReader(line ?? ""));
            using var stream = new ManxTokenFilter(tokenizer);
            var term = stream.GetAttribute<ICharTermAttribute>();
            var offsets = stream.GetAttribute<IOffsetAttribute>();
            stream.Reset();
            while (stream.IncrementToken())
            {
                var token = term.ToString();
                string status;
                if (InDictionary(token))
                {
                    status = "entry";
                }
                else if (lemmaTable.DisplayLemmasFor(token).Concat(lemmaTable.CliticDisplayLemmasFor(token))
                         .Any(InDictionary))
                {
                    status = "root";
                }
                else if (lemmaTable.CandidatesFor(token).Count > 0
                         || lemmaTable.CliticCandidatesFor(token).Count > 0)
                {
                    status = "lemma";
                }
                else
                {
                    status = "none";
                }
                tokens.Add(new TokenCoverage
                {
                    Start = offsets.StartOffset,
                    Length = offsets.EndOffset - offsets.StartOffset,
                    Status = status,
                });
            }
            stream.End();
            result.Add(tokens);
        }
        return result;
    }

    /// <summary>
    /// The queries to attempt, most specific first: phrases from the context containing the
    /// selection (longest first), then the selection itself. Compounds are written both
    /// hyphenated and as separate words, so each candidate is also tried with hyphens
    /// exchanged for spaces (and vice versa), and with each apostrophe style.
    /// </summary>
    private static List<string> GetCandidates(string selection, string? context)
    {
        var selectionWords = Tokenize(selection);
        if (selectionWords.Count == 0)
        {
            return [];
        }

        var phrases = new List<List<string>>();
        var contextWords = Tokenize(context);
        foreach (var occurrence in FindOccurrences(contextWords, selectionWords))
        {
            for (int length = selectionWords.Count + 1; length <= MaxPhraseWords; length++)
            {
                for (int start = occurrence + selectionWords.Count - length; start <= occurrence; start++)
                {
                    if (start < 0 || start + length > contextWords.Count)
                    {
                        continue;
                    }
                    phrases.Add(contextWords.GetRange(start, length));
                }
            }
        }

        var candidates = phrases
            .OrderByDescending(x => x.Count)
            .Append(selectionWords)
            .Select(words => string.Join(" ", words))
            .SelectMany(HyphenVariants)
            .SelectMany(ApostropheVariants);

        return candidates.Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();
    }

    /// <summary>'goll-mygeayrt' is also tried as 'goll mygeayrt', and vice versa</summary>
    private static IEnumerable<string> HyphenVariants(string candidate)
    {
        yield return candidate;
        yield return candidate.Replace('-', ' ');
        yield return candidate.Replace(' ', '-');
    }

    /// <summary>The texts and dictionaries mix typewriter (') and typographic (’) apostrophes</summary>
    private static IEnumerable<string> ApostropheVariants(string candidate)
    {
        yield return candidate;
        yield return candidate.Replace('’', '\'');
        yield return candidate.Replace('\'', '’');
    }

    /// <summary>The starting indexes at which the selection occurs within the context</summary>
    private static IEnumerable<int> FindOccurrences(List<string> contextWords, List<string> selectionWords)
    {
        for (int i = 0; i + selectionWords.Count <= contextWords.Count; i++)
        {
            if (selectionWords.Select((word, offset) => (word, offset))
                .All(x => string.Equals(contextWords[i + x.offset], x.word, StringComparison.InvariantCultureIgnoreCase)))
            {
                yield return i;
            }
        }
    }

    /// <summary>
    /// The component words of a compound/contraction: 'goll-mygeayrt' -> ['goll', 'mygeayrt'],
    /// "mooad's" (#337) -> ['mooad'].
    /// </summary>
    private static List<string> GetParts(string selection)
    {
        var queried = string.Join(" ", Tokenize(selection));

        return Tokenize(selection)
            .SelectMany(x => x.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries))
            .Where(x => x.Any(char.IsLetter))
            // a lone letter is the stub of a contraction ('t' from t'eh, the possessive 's'), not a word
            .Where(x => x.Length > 1)
            // the selection itself was already queried
            .Where(x => !x.Equals(queried, StringComparison.InvariantCultureIgnoreCase))
            .Distinct(StringComparer.InvariantCultureIgnoreCase)
            .ToList();
    }

    private static List<string> Tokenize(string? text) =>
        (text ?? "")
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim(Punctuation))
            .Where(x => x.Any(char.IsLetter))
            .ToList();

    /// <summary>An entry can match more than one candidate (e.g. both hyphen variants)</summary>
    private static List<DictionarySummary> Deduplicate(IEnumerable<DictionarySummary> summaries) =>
        summaries
            .GroupBy(x => (x.PrimaryWord, x.Summary))
            .Select(x => x.First())
            .ToList();
}

/// <summary>One token of a line in the dictionary-coverage debug view</summary>
public class TokenCoverage
{
    /// <summary>Offset of the token within its line</summary>
    public int Start { get; set; }

    public int Length { get; set; }

    /// <summary>"entry" | "root" | "lemma" | "none" (see <see cref="DictionaryLookupService.Coverage"/>)</summary>
    public required string Status { get; set; }
}
