using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CorpusSearch.Dependencies;
using CorpusSearch.Dependencies.Lucene;
using CorpusSearch.Model;

namespace CorpusSearch.Service;

/// <summary>
/// The word page's attestation walk (experimental): the corpus documents using a
/// lexeme, oldest first, and every use of it within one of them.
///
/// Unlike <see cref="DictionaryHistoryService"/>, which scans spelling by spelling,
/// this asks the lemma field (<see cref="LuceneIndex.DOCUMENT_LEMMA_MANX"/>) for the
/// lexeme itself: one query, no form budget to truncate the cluster, and the
/// resolver's per-line decisions already applied, so a line it read as another word
/// does not ride along.
///
/// Where the lemma table knows no lexeme for the word it falls back to that scan
/// instead (<see cref="ReadingsIn"/>). The table is built from Cregeen and J Kelly
/// and does not cover even those: 'angaish' is one of J Kelly's own headwords and
/// has no lexeme here. Walking only the lemma field left the page counting 63 uses
/// in the band and offering no way to read one.
/// </summary>
public class DictionaryAttestationService(
    Searcher searcher, LemmaTable lemmaTable, WorkService workService)
{
    /// <summary>The lemma ids whose uses the walk should show. A word that is a
    /// headword itself keeps only its own lexeme: walking 'ass' (out) must not step
    /// through the demutation guess fass, the way
    /// <see cref="DictionaryHistoryService.LemmaReadingsFor"/> keeps their timelines
    /// apart.
    ///
    /// None at all for an affix: its lexeme is keyed by the bare word it is
    /// spelled like, so asking the lemma field for 'an-' answers with 'an'. It is
    /// walked by <see cref="Affix.CorpusQuery"/> instead — the words carrying it,
    /// which is what attests it.</summary>
    internal static IReadOnlyList<string> LemmaIdsFor(LemmaTable table, string word)
    {
        if (Affix.Is(word))
        {
            return [];
        }
        var candidates = table.CandidatesFor(word);
        var self = LemmaTable.NormalizeForm(word);
        var own = candidates
            .Where(id => LemmaTable.NormalizeForm(table.DisplayLemmaOf(id) ?? "") == self)
            .ToList();
        return own.Count > 0 ? own : candidates;
    }

    /// <summary>The readings of <paramref name="lemmaIds"/> narrowed to the one
    /// displaying as <paramref name="lemma"/> — the walk's per-reading tabs ask a
    /// reading at a time. Unchanged when nothing (or nothing the word has) is
    /// asked: a stale tab in a URL must not blank the section.</summary>
    private IReadOnlyList<string> ReadingOf(IReadOnlyList<string> lemmaIds, string? lemma)
    {
        if (lemma == null)
        {
            return lemmaIds;
        }
        var wanted = LemmaTable.NormalizeForm(lemma);
        var reading = lemmaIds
            .Where(id => LemmaTable.NormalizeForm(lemmaTable.DisplayLemmaOf(id) ?? "") == wanted)
            .ToList();
        return reading.Count > 0 ? reading : lemmaIds;
    }

    /// <summary>The display lemma <paramref name="lemmaIds"/> share, when they do:
    /// what a filtered walk says it walked. Null over several readings — the
    /// unfiltered walk of an ambiguous word names no one reading.</summary>
    private string? WalkedLemma(IReadOnlyList<string> lemmaIds)
    {
        var displays = lemmaIds.Select(lemmaTable.DisplayLemmaOf).OfType<string>().Distinct().ToList();
        return displays.Count == 1 ? displays[0] : null;
    }

    /// <summary>The documents attesting the word, oldest first: its lexeme's, or
    /// its spelling's where the table knows no lexeme.</summary>
    /// <param name="lemma">optional display lemma: one reading's documents, for
    /// the walk's tabs ('vee' is bee or mee, and each tab walks one of them)</param>
    public DictionaryAttestations Attestations(string word, string? lemma = null)
    {
        var lemmaIds = LemmaIdsFor(lemmaTable, word);
        var walkedIds = ReadingOf(lemmaIds, lemma);
        var scan = ScanFor(word, walkedIds);

        // an undated document cannot take a place in a chronological walk, but
        // dropping it silently would understate the word's use: it is counted instead
        var undated = scan.DocumentResults.Where(x => x.StartDate == null).ToList();

        return new DictionaryAttestations
        {
            Word = word,
            // every reading, whatever was walked: the tabs stay put while one is open
            Lemmas = lemmaIds.Select(lemmaTable.DisplayLemmaOf).OfType<string>().Distinct().ToList(),
            Lemma = WalkedLemma(walkedIds),
            Documents = scan.DocumentResults
                .Where(x => x.StartDate != null)
                .OrderBy(x => x.StartDate)
                .ThenBy(x => x.Ident)
                .Select(x => new AttestationDocument
                {
                    Ident = x.Ident,
                    Title = x.DocumentName,
                    Year = x.StartDate!.Value.Year,
                    // the scan counts span matches, and an OR over several
                    // readings scores one token once per reading claiming it:
                    // only a lone term can be counted from here without
                    // reading four uses of 'vee' where there is one. A spelling
                    // scan (no readings at all) is one term too.
                    Uses = walkedIds.Count <= 1 ? x.Count : null,
                })
                .ToList(),
            UndatedDocuments = undated.Count,
        };
    }

    /// <summary>The documents using the word: its lexeme's, or — where the lemma
    /// table knows no lexeme — the ones the query below turns up in, which is what
    /// the first-seen band counts by.</summary>
    private ScanResult ScanFor(string word, IReadOnlyList<string> lemmaIds)
    {
        if (lemmaIds.Count > 0)
        {
            return searcher.ScanLemma(lemmaIds);
        }
        try
        {
            return searcher.Scan(CorpusQueryFor(word));
        }
        catch (Exception)
        {
            // a headword the query grammar cannot parse has no walk, which is
            // where it started: never fatal to the page
            return new ScanResult();
        }
    }

    /// <summary>What to ask the corpus for a word with no lexeme to ask for: the
    /// spelling, or — for an affix — the words carrying it, since an affix is
    /// attested by those and never on its own (see <see cref="Affix"/>)</summary>
    private static string CorpusQueryFor(string word) =>
        Affix.Is(word) ? Affix.CorpusQuery(word) : word;

    /// <summary>How many lines of one reading the walk shows before deferring to the
    /// document itself: a text like the Psalms uses a common word over a hundred
    /// times, and the walk is a taste of the evidence, not a concordance</summary>
    private const int MaxLinesPerLemma = 2;

    /// <summary>Where a lexeme is actually used: one entry per surface word, so a
    /// line saying it twice counts twice. The highlights are the query's own spans,
    /// so their offsets identify an occurrence across readings — a token two
    /// readings both claim is one use of the word, not two.</summary>
    private static IEnumerable<(int Line, int Start)> Occurrences(SearchResult result) =>
        result.Lines.SelectMany(line => line.ManxHighlights is { Count: > 0 }
            ? line.ManxHighlights.Select(h => (line.CsvLineNumber, h.Start))
            : [(line.CsvLineNumber, -1)]);

    /// <summary>The word class a lemma id names ("jaagh.v" → "v"), or null where it
    /// names none: an id may carry no class at all ("ny-adv", "bagh-1").</summary>
    private static string? ClassOf(string lemmaId)
    {
        var dot = lemmaId.LastIndexOf('.');
        return dot < 0 ? null : lemmaId[(dot + 1)..];
    }

    /// <summary>The word classes naming a row's readings: jaagh.n and jaagh.v → n, v,
    /// which is what tells one reading of a headword from another where the headword
    /// itself cannot. Empty unless every id names one — a list missing a reading would
    /// read as the row's whole story — so a row that is a spelling rather than a
    /// reading (a null id) has none, which is right: nothing has said what class it is.
    /// </summary>
    private static List<string> ClassesOf(IEnumerable<string?> lemmaIds)
    {
        var classes = lemmaIds.Select(id => id == null ? null : ClassOf(id)).ToList();
        return classes.Contains(null) ? [] : classes.OfType<string>().Distinct().ToList();
    }

    /// <summary>A query the walk found the word by, and the name its uses are filed
    /// under. <paramref name="LemmaId"/> is null for the spelling fallback: there is
    /// no reading there, only the word as it is written.</summary>
    private sealed record Reading(string? LemmaId, string Lemma, SearchResult Result);

    /// <summary>What found the word in one document, and what each found.</summary>
    private List<Reading> ReadingsIn(string word, string ident, string? lemma)
    {
        var lemmaIds = ReadingOf(LemmaIdsFor(lemmaTable, word), lemma);
        if (lemmaIds.Count > 0)
        {
            // one query per reading: a span query says which lines matched, never
            // which of its OR'd terms did, and an ambiguous word has two or three
            // readings at most. A line the resolver left ambiguous answers to each
            // of them, and is counted under each: that it could be either is the
            // fact, not a bug.
            return lemmaIds
                .Select(id => (Id: id, Result: searcher.SearchLemma(ident, [id])))
                .Where(x => x.Result is { Lines.Count: > 0 })
                .Select(x => new Reading(x.Id, lemmaTable.DisplayLemmaOf(x.Id) ?? x.Id, x.Result!))
                .ToList();
        }
        // no lexeme to ask for, so ask the corpus directly — the scan the
        // first-seen band above the walk has always used, and whose count the walk
        // otherwise leaves the reader unable to see a single line of. For an affix
        // that is the words carrying it; for anything else, the spelling.
        //
        // Weaker evidence, knowingly: this cannot apply the resolver's per-line
        // decisions, so an ambiguous spelling brings the other lexeme's lines with
        // it. Nothing is being confused that the table could have told apart — it
        // has no reading for this word at all — and the row is filed under the
        // headword rather than under a lexeme it cannot name.
        try
        {
            var found = searcher.SearchWork(ident, CorpusQueryFor(word),
                SearchOptions.Default, returnTranscriptData: false);
            return found is { Lines.Count: > 0 } ? [new Reading(null, word, found)] : [];
        }
        catch (Exception)
        {
            // a headword the query grammar cannot parse shows no uses, never an error
            return [];
        }
    }

    /// <summary>Every use of the word in one document, grouped by the reading each
    /// line was resolved to, with the surface words highlighted. Null when the
    /// document does not attest it.</summary>
    /// <param name="lemma">optional display lemma: one reading's uses, matching the
    /// walk tab the step was opened from</param>
    public async Task<AttestationLines?> InDocument(string word, string ident, string? lemma = null)
    {
        var matched = ReadingsIn(word, ident, lemma);
        if (matched.Count == 0)
        {
            return null;
        }
        var readings = matched
            .Select(x => new
            {
                x.LemmaId,
                x.Result,
                x.Lemma,
                Uses = Occurrences(x.Result).ToHashSet(),
            })
            .ToList();
        var groups = readings
            .GroupBy(x => x.Lemma)
            // readings a reader could not tell apart are one row. 'jaagh' is smoke
            // (jaagh.n) or the verb (jaagh.v): both display as "jaagh", so a line the
            // resolver left as either answers to both queries and would head two rows
            // reading the same. That the use is either of them is one fact about one
            // word, and the row names both readings to say so — twin rows only show a
            // repeat. Readings claiming *different* words stay apart: there the
            // resolver did decide, and the rows differ by more than their labels.
            .SelectMany(lemma => lemma
                .GroupBy(x => x.Uses, HashSet<(int Line, int Start)>.CreateSetComparer())
                .Select(row => new AttestationLemmaGroup
                {
                    LemmaIds = row.Select(x => x.LemmaId).OfType<string>().ToList(),
                    Lemma = lemma.Key,
                    Classes = ClassesOf(row.Select(x => x.LemmaId)),
                    // the row's readings claim the same uses, so the count is that
                    // set's: one term's spans cannot overlap each other
                    Count = row.Key.Count,
                    // and the same uses are the same lines: any reading's serve
                    Lines = row.First().Result.Lines
                        .OrderBy(line => line.CsvLineNumber)
                        .Take(MaxLinesPerLemma)
                        .ToList(),
                }))
            // the reading a text uses most leads: a one-off demutation guess should
            // not head a document that is really about the other word
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Lemma)
            .ToList();
        var document = await workService.ByIdent(ident);
        return new AttestationLines
        {
            Ident = ident,
            Title = document.Name,
            Year = document.CreatedCircaStart?.Year,
            Lemma = WalkedLemma(matched.Select(x => x.LemmaId).OfType<string>().ToList()),
            // the union, not the sum: a word the resolver left ambiguous is claimed
            // by every reading, and is still one use of it
            UseCount = matched.SelectMany(x => Occurrences(x.Result)).Distinct().Count(),
            Groups = groups,
        };
    }
}

/// <summary>The corpus documents attesting a word, oldest first</summary>
public class DictionaryAttestations
{
    public required string Word { get; set; }
    /// <summary>The word's display lemmas, every reading whatever was walked: the
    /// walk's tabs, which must stay put while one of them is open. Empty where the
    /// lemma table knows no lexeme for the word, in which case the walk is of its
    /// spelling and there is no lexeme to name</summary>
    public required List<string> Lemmas { get; set; }
    /// <summary>The one reading <see cref="Documents"/> walks, where it is one:
    /// the asked reading, or an unambiguous word's own. Null for the unfiltered
    /// walk of an ambiguous word, and for a spelling walk.</summary>
    public string? Lemma { get; set; }
    public required List<AttestationDocument> Documents { get; set; }
    /// <summary>Documents attesting the lexeme which carry no date: they cannot be
    /// placed in the walk, so they are counted beside it rather than dropped</summary>
    public int UndatedDocuments { get; set; }
}

/// <summary>A step in the walk</summary>
public class AttestationDocument
{
    public required string Ident { get; set; }
    public required string Title { get; set; }
    public int Year { get; set; }

    /// <summary>
    /// Uses of the lexeme, where the scan can be trusted to count them: a word
    /// with one reading is one query term, so each use is matched once.
    ///
    /// Null for an ambiguous word, whose readings are OR'd — a token carrying
    /// several of them is matched once per reading, and 'vee' (four readings)
    /// would read four times too high. Those are counted from the highlight
    /// offsets instead, one document at a time, as
    /// <see cref="AttestationLines.UseCount"/>.
    /// </summary>
    public int? Uses { get; set; }
}

public class AttestationLines
{
    public required string Ident { get; set; }
    public required string Title { get; set; }
    public int? Year { get; set; }
    /// <summary>The one reading the groups answer for, where it is one — matching
    /// <see cref="DictionaryAttestations.Lemma"/>, so a step can be told to belong
    /// to the tab it was opened from. Null over several readings, and for a
    /// spelling walk.</summary>
    public string? Lemma { get; set; }
    /// <summary>Uses of the lexeme in the document: surface words, not lines, and
    /// counted once each however many readings claim them</summary>
    public int UseCount { get; set; }
    /// <summary>The document's uses, split by the reading they resolved to, commonest
    /// reading first. A word with one reading has one group.</summary>
    public required List<AttestationLemmaGroup> Groups { get; set; }
}

/// <summary>The uses of one reading of a word within a document — or of several
/// readings, where nothing on the page could tell them apart</summary>
public class AttestationLemmaGroup
{
    /// <summary>The readings the row stands for ("beg.a"): distinguishes homographs
    /// the display lemma cannot. More than one where they share that lemma and claim
    /// the very same words — the document's use of the word is genuinely either.
    ///
    /// Empty where the row is a spelling rather than a reading: the lemma table
    /// knows no lexeme for the word, so the walk scanned what it is written as.</summary>
    public required List<string> LemmaIds { get; set; }
    /// <summary>The headword a reader would look up ("beg"), or the spelling that was
    /// scanned where <see cref="LemmaIds"/> is empty</summary>
    public required string Lemma { get; set; }
    /// <summary>The word classes of <see cref="LemmaIds"/> ("n", "v"), for naming a
    /// reading the headword alone does not. Empty where an id names no class.</summary>
    public required List<string> Classes { get; set; }
    /// <summary>Uses the row claims, across the whole document rather than only the
    /// lines <see cref="Lines"/> shows. A word the resolver left ambiguous between
    /// two headwords is claimed by each, so these do not sum to
    /// <see cref="AttestationLines.UseCount"/>.</summary>
    public int Count { get; set; }
    public required List<DocumentLine> Lines { get; set; }
}
