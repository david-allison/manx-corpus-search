using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CorpusSearch.Dependencies.Lucene;
using CorpusSearch.Service.Dictionaries;
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
    LemmaResolver lemmaResolver, SenseInventory? senseInventory = null, SenseResolver? senseResolver = null)
{
    private readonly SenseInventory senseInventory = senseInventory ?? SenseInventory.Empty;
    private readonly SenseResolver senseResolver = senseResolver ?? SenseResolver.Empty;

    /// <summary>The longest dictionary phrase (in words) we attempt to match around a selection</summary>
    private const int MaxPhraseWords = 4;

    /// <summary>Trimmed from the edges of each word; apostrophes/hyphens are word-internal in Manx</summary>
    private static readonly char[] Punctuation = ['.', ',', '?', ';', ':', '!', '(', ')', '[', ']', '{', '}', '"', '“', '”', '…'];

    /// <summary>Each marks a compound/contraction whose parts may have their own entries</summary>
    private static readonly char[] WordSeparators = ['-', '\'', '’'];

    private readonly ISearchDictionary[] dictionaryServices = dictionaryServices.ToArray();

    /// <summary>Display name -> URL slug: entries carry the name they were
    /// defined under, the page links the slug</summary>
    private readonly Dictionary<string, string> slugByDictionary = dictionaryServices
        .ToDictionary(x => x.Identifier, x => x.Slug);

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
            // a dictionary answers from its own inflected-form list, so Cregeen's
            // 'vann' ("did bless") replies to 'vannin' - a spelling the table reads
            // as Mannin. No escape if this empties the list: the reading is wrong
            // however little else there is, and the root chain below (then names,
            // parts, near matches) is what the reader actually wants
            results = results.Where(x => !IsAnotherLexeme(selection, x)).ToList();
            // the root chain of an inflected/mutated selection follows the surface
            // candidates, each hop tagged with its depth so the client can nest it
            // ('gheiney' -> 'deiney' -> 'dooinney'): the reader always gets the
            // headwords a dictionary actually lists, without them posing as
            // entries for the selection
            var seen = new HashSet<string>(candidates, StringComparer.InvariantCultureIgnoreCase);
            // each hop carries whether the table only reaches it by rule: once a
            // chain crosses an unverified link every root beyond it rests on that
            // guess, so the flag sticks for the rest of the walk
            var frontier = ResolvedDisplayLemmas(selection, context)
                .Where(x => !seen.Contains(x))
                .Select(x => (Display: x, Unverified: lemmaTable.IsUnverifiedLink(selection, x)))
                .ToList();
            // which sense of a root the chain means: the word classes of the
            // candidate ids that produced each display (row -> bee.v is the
            // verb 'bee', never the food)
            var expectedPos = ExpectedPosByDisplay(selection, context);
            for (var depth = 1; frontier.Count > 0 && depth <= 3; depth++)
            {
                seen.UnionWith(frontier.Select(x => x.Display));
                foreach (var (display, unverified) in frontier)
                {
                    var summaries = GetSummaries([display]);
                    if (depth == 1 && expectedPos.TryGetValue(display, out var expected))
                    {
                        // Phil Kelly merges homograph senses into one gloss list
                        // ('bee': food and be together): when the chain knows which
                        // sense it means, the sense-blind entry only muddies it -
                        // unless it is all there is
                        var senseCapable = summaries
                            .Where(x => x.DictionaryName != PhilKellyDictionaryService.Name)
                            .ToList();
                        if (senseCapable.Count > 0)
                        {
                            summaries = senseCapable;
                        }
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
                        summary.UnverifiedLink = unverified;
                        results.Add(summary);
                    }
                }
                // deeper hops follow paradigm links only: a mutation guess is a
                // candidate reading of the selection, not a root of the root
                frontier = frontier
                    .SelectMany(root => lemmaTable.RootDisplayLemmasFor(root.Display)
                        .Select(next => (Display: next,
                            Unverified: root.Unverified || lemmaTable.IsUnverifiedLink(root.Display, next))))
                    .Where(x => !seen.Contains(x.Display))
                    .DistinctBy(x => x.Display, StringComparer.InvariantCultureIgnoreCase)
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

        var deduplicated = Deduplicate(results);
        // a Phillips 1610 spelling has no entries of its own: every result
        // arrived through the spelling link, and the client says so up front
        var phillips = lemmaTable.PhillipsSpellingsOf(selection);
        if (phillips.Count > 0)
        {
            foreach (var summary in deduplicated)
            {
                summary.PhillipsSpellingOf ??= phillips[0];
            }
        }
        StampSenseNotes(selection, context, deduplicated);
        return deduplicated;
    }

    /// <summary>Where the clicked occurrence's sense is on record, the entry it
    /// belongs to says so — on the matching headword's summaries only, since a
    /// sense is the book's own subdivision of one entry. Layered after lemma
    /// resolution: a sense verdict only means anything once the lemma is
    /// settled (<see cref="SenseResolver.SenseFor"/> enforces the match).</summary>
    private void StampSenseNotes(string selection, string? context, List<DictionarySummary> summaries)
    {
        if (!senseResolver.HasRows || string.IsNullOrWhiteSpace(context))
        {
            return;
        }
        var resolved = ResolvedLemmaIds(selection, context)
                       ?? (IReadOnlyCollection<string>)lemmaTable.CandidatesFor(selection);
        if (resolved.Count != 1)
        {
            return;
        }
        var lemmaId = resolved.First();

        var selectionTokens = LemmaResolver.TokenizeManx(DocumentLine.NormalizeManx(selection));
        if (selectionTokens.Count != 1)
        {
            return;
        }
        var tokens = LemmaResolver.TokenizeManx(DocumentLine.NormalizeManx(context));
        var lineKey = LemmaResolver.LineKey(tokens);
        var senses = new List<SenseInventory.Sense>();
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i] != selectionTokens[0])
            {
                continue;
            }
            var found = senseResolver.SenseFor(senseInventory, lineKey, i, tokens[i], lemmaId);
            if (found == null)
            {
                // an unresolved occurrence of the same word in the line leaves
                // the claim ambiguous: say nothing rather than guess which
                return;
            }
            senses.AddRange(found);
        }
        var distinct = senses.DistinctBy(x => x.SenseId).ToList();
        if (distinct.Count != 1)
        {
            return;
        }
        var sense = distinct[0];
        var headword = LemmaTable.NormalizeForm(
            sense.EntryPath.Split(':') is { Length: 2 } path ? path[1] : sense.EntryPath);
        foreach (var summary in summaries
                     .Where(x => LemmaTable.NormalizeForm(x.PrimaryWord) == headword))
        {
            summary.SenseNote = sense.Gloss;
        }
    }

    /// <summary>The dictionaries which answer the query language, for the page's
    /// scope picker: every dictionary, not only those defining some word</summary>
    public List<DictionaryInfo> Dictionaries(string lang)
    {
        return dictionaryServices
            .Where(x => x.QueryLanguages.Contains(lang))
            .Select(x => new DictionaryInfo { Slug = x.Slug, Name = x.Identifier })
            .ToList();
    }

    /// <summary>The teanglann-style full page for a word (experimental): the
    /// lookup re-shaped into per-dictionary groups, the word's own recording
    /// pulled out as the page control, near-match suggestions marked as a tier</summary>
    /// <param name="dict">optional <see cref="ISearchDictionary.Slug"/>: scopes the
    /// page to one dictionary. An unknown slug scopes to nothing, rather than
    /// silently widening back to every dictionary</param>
    public DictionaryPage Page(string lang, string word, string? dict = null)
    {
        var summaries = WithoutDemutationGuesses(word, Lookup(lang, word));
        // read before the scoping below, and deliberately: the picker shows every
        // dictionary, so it has to be told about the ones this page is hiding
        var answering = summaries.Select(SlugOf).OfType<string>().Distinct().ToList();
        if (dict != null)
        {
            // filtered before anything else is derived: a scoped page's audio and
            // suggestion tier must describe the dictionary being shown, not the
            // ones being hidden
            summaries = summaries.Where(x => SlugOf(x) == dict).ToList();
        }
        var audio = summaries.FirstOrDefault(x =>
            x.AudioUrl != null && x.NearMatchOf == null
            && string.Equals(x.PrimaryWord, word, StringComparison.InvariantCultureIgnoreCase));
        return new DictionaryPage
        {
            Word = word,
            Answering = answering,
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
                    Slug = slugByDictionary.GetValueOrDefault(g.Key),
                    SourceUrl = g.Select(x => x.SourceUrl).FirstOrDefault(x => x != null),
                    Entries = g.ToList(),
                })
                .ToList(),
        };
    }

    /// <summary>The URL slug of the dictionary defining an entry; null when the
    /// entry names a dictionary no longer registered (a disabled source)</summary>
    private string? SlugOf(DictionarySummary summary) =>
        summary.DictionaryName == null
            ? null
            : slugByDictionary.GetValueOrDefault(summary.DictionaryName);

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

    /// <summary>
    /// A few entries for a reader mid-keystroke: every headword beginning with
    /// what they have typed, commonest first — or, when nothing does, the near
    /// spellings the total-miss page would offer, said to be that. A handful
    /// only: the box is offering a next keystroke, not an index.
    /// </summary>
    /// <param name="vocabulary">ranks the completions (a common word is the
    /// likelier errand) and greys the never-said, as every index does</param>
    public DictionarySuggestions Suggest(string query, int count, CorpusVocabulary vocabulary)
    {
        var norm = LemmaTable.NormalizeForm(query);
        if (norm.Length == 0)
        {
            return new DictionarySuggestions { Words = [] };
        }
        var pool = suggestPool ??= BuildSuggestPool();
        var matches = new List<(string Norm, string Word)>();
        for (var i = LowerBound(pool, norm);
             i < pool.Count && pool[i].Norm.StartsWith(norm, StringComparison.Ordinal);
             i++)
        {
            matches.Add(pool[i]);
        }
        if (matches.Count == 0)
        {
            return new DictionarySuggestions
            {
                Words = NearMatches(query)
                    .Take(count)
                    .Select(word => new DictionarySuggestion
                    {
                        Word = word,
                        Attested = vocabulary.IsAttested(word),
                    })
                    .ToList(),
                Fuzzy = true,
            };
        }
        return new DictionarySuggestions
        {
            Words = matches
                // the word itself leads its completions
                .OrderByDescending(x => x.Norm == norm)
                .ThenByDescending(x => vocabulary.AttestationsOf(x.Word) ?? 0)
                .ThenBy(x => x.Word, StringComparer.InvariantCultureIgnoreCase)
                .Take(count)
                .Select(x => new DictionarySuggestion
                {
                    Word = x.Word,
                    Attested = vocabulary.IsAttested(x.Word),
                })
                .ToList(),
        };
    }

    /// <summary>Every gv headword (and the names supplement's display lemmas)
    /// sorted by normalized form: the suggest box's prefix range. Phrases ride
    /// along — half of Phil Kelly is phrases, and a phrase completes like any
    /// word. Built on the first keystroke asked about.</summary>
    private List<(string Norm, string Word)>? suggestPool;

    private List<(string Norm, string Word)> BuildSuggestPool()
    {
        var byNorm = new Dictionary<string, string>();
        var words = dictionaryServices
            .Where(d => d.QueryLanguages.Contains("gv"))
            .SelectMany(d => d.AllWords)
            .Concat(lemmaTable.NameDisplayLemmas);
        foreach (var word in words)
        {
            var norm = LemmaTable.NormalizeForm(word);
            if (norm.Length == 0)
            {
                continue;
            }
            // the first book's spelling stands - except that a spelling with
            // lowercase in it beats one without, whoever came first: Kelly
            // prints its headwords in capitals, and the box should offer
            // 'mooarane', never MOOARANE, when another book writes it plainly
            if (!byNorm.TryGetValue(norm, out var kept)
                || (!kept.Any(char.IsLower) && word.Any(char.IsLower)))
            {
                byNorm[norm] = word;
            }
        }
        var pool = byNorm
            // a word only ever printed shouting is lowered
            .Select(x => (Norm: x.Key,
                Word: x.Value.Any(char.IsLower) ? x.Value : x.Value.ToLowerInvariant()))
            .ToList();
        pool.Sort((a, b) => string.CompareOrdinal(a.Norm, b.Norm));
        return pool;
    }

    /// <summary>The first pool index at or after <paramref name="norm"/> in
    /// ordinal order: where its completions start</summary>
    private static int LowerBound(List<(string Norm, string Word)> pool, string norm)
    {
        int low = 0, high = pool.Count;
        while (low < high)
        {
            var mid = (low + high) / 2;
            if (string.CompareOrdinal(pool[mid].Norm, norm) < 0)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }
        return low;
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
                // the names supplement spells most mutations by rule ('Vonaco'
                // under Monaco): the name is documented, this spelling of it is not
                UnverifiedLink = !isSelection && lemmaTable.IsUnverifiedLink(selection, display),
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
    /// <summary>
    /// Whether <paramref name="summary"/> documents a different lexeme that merely
    /// shares the selection's spelling. A dictionary's lookup set covers an entry's
    /// inflected forms, so Cregeen's 'vann' ("did bless") answers a lookup of
    /// 'vannin' — which the table lemmatises to Mannin. The two share no reading, so
    /// the entry is a homograph of the spelling, not documentation of the word.
    /// </summary>
    /// <remarks>
    /// Deliberately narrow, since a wrong drop hides a real entry: only single-word
    /// entries qualify (a phrase is matched through the line's context, not a
    /// spelling clash), never one headed by another spelling of the selection
    /// ('BILL, BILLEY'), and never when the table has no reading of either side —
    /// no opinion is not a rejection.
    /// </remarks>
    private bool IsAnotherLexeme(string selection, DictionarySummary summary)
    {
        var word = summary.PrimaryWord;
        if (word.Any(c => c is ' ' or '-' or '‑'))
        {
            return false;
        }
        var form = LemmaTable.NormalizeForm(selection);
        if (LemmaTable.NormalizeForm(word) == form
            || (summary.Words ?? []).Any(x => LemmaTable.NormalizeForm(x) == form))
        {
            return false;
        }
        var selectionIds = lemmaTable.CandidatesFor(form);
        var entryIds = lemmaTable.CandidatesFor(word);
        return selectionIds.Count > 0 && entryIds.Count > 0 && !selectionIds.Intersect(entryIds).Any();
    }

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
        // a tap tries hyphen/apostrophe variants of the selection (GetCandidates),
        // so the prediction must too: 'dy-reiltagh' is listed as 'dy reiltagh'
        bool HasEntry(string word) => HyphenVariants(word)
            .SelectMany(ApostropheVariants)
            .Distinct(StringComparer.InvariantCultureIgnoreCase)
            .Any(InDictionary);

        var result = new List<List<TokenCoverage>>(lines.Count);
        foreach (var line in lines)
        {
            var words = new List<(string Term, int Start, int End)>();
            var tokenizer = new ManxTokenizer(LuceneVersion.LUCENE_48, new StringReader(line ?? ""));
            using (var stream = new ManxTokenFilter(tokenizer))
            {
                var term = stream.GetAttribute<ICharTermAttribute>();
                var offsets = stream.GetAttribute<IOffsetAttribute>();
                stream.Reset();
                while (stream.IncrementToken())
                {
                    var token = term.ToString();
                    if (NonWordTokenFilter.IsNotAWord(token))
                    {
                        // a number or ?-marker is not a dictionary word: neither
                        // painted nor counted (the statistics stream drops these too)
                        continue;
                    }
                    words.Add((token, offsets.StartOffset, offsets.EndOffset));
                }
                stream.End();
            }

            // a tap resolves phrases from its context ("mie er bashtal" is
            // Phil Kelly's entry, "bashtal" alone is nobody's): a token inside
            // a listed phrase is covered. Longest first, as GetCandidates asks.
            var phraseCovered = new bool[words.Count];
            for (int length = MaxPhraseWords; length >= 2; length--)
            {
                for (int start = 0; start + length <= words.Count; start++)
                {
                    if (Enumerable.Range(start, length).All(i => phraseCovered[i]))
                    {
                        continue;
                    }
                    var phrase = string.Join(" ", words.Skip(start).Take(length).Select(w => w.Term));
                    if (HasEntry(phrase))
                    {
                        for (int i = start; i < start + length; i++)
                        {
                            phraseCovered[i] = true;
                        }
                    }
                }
            }

            var tokens = new List<TokenCoverage>();
            for (int i = 0; i < words.Count; i++)
            {
                var (token, start, end) = words[i];
                string status;
                if (phraseCovered[i] || HasEntry(token))
                {
                    status = "entry";
                }
                else if (lemmaTable.DisplayLemmasFor(token).Concat(lemmaTable.CliticDisplayLemmasFor(token))
                         .Any(HasEntry))
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
                    Start = start,
                    Length = end - start,
                    Status = status,
                });
            }
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
            // the selection as it came, when trimming changed it. A tap sends a
            // word with the line's punctuation stuck to it ('meenid,'), so the
            // trimmed form has to be tried - but a headword can end in a full
            // stop of its own ('a.r.e.', 'St.'), and trimming is all that stood
            // between the browse index listing those and the page finding them.
            .Append(selection.Trim())
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

/// <summary>What the look-up box offers mid-keystroke</summary>
public class DictionarySuggestions
{
    public required List<DictionarySuggestion> Words { get; set; }

    /// <summary>The words are near spellings rather than completions: nothing
    /// the books hold begins with what was typed, and the box should say so
    /// rather than pass a guess off as a match</summary>
    public bool Fuzzy { get; set; }
}

public class DictionarySuggestion
{
    public required string Word { get; set; }

    /// <summary>Whether any corpus text says it: the box greys the never-said,
    /// as every index does</summary>
    public bool Attested { get; set; }
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
