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
    /// None at all for an affix (<see cref="LemmaTable.IsAffix"/>): there is no
    /// walking a prefix through the texts, because no text says one.</summary>
    internal static IReadOnlyList<string> LemmaIdsFor(LemmaTable table, string word)
    {
        if (LemmaTable.IsAffix(word))
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

    /// <summary>The documents attesting the word's lexeme, oldest first.</summary>
    public DictionaryAttestations Attestations(string word)
    {
        var lemmaIds = LemmaIdsFor(lemmaTable, word);
        var scan = searcher.ScanLemma(lemmaIds);

        // an undated document cannot take a place in a chronological walk, but
        // dropping it silently would understate the word's use: it is counted instead
        var undated = scan.DocumentResults.Where(x => x.StartDate == null).ToList();

        return new DictionaryAttestations
        {
            Word = word,
            Lemmas = lemmaIds.Select(lemmaTable.DisplayLemmaOf).OfType<string>().Distinct().ToList(),
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
                    // only a lone reading can be counted from here without
                    // reading four uses of 'vee' where there is one
                    Uses = lemmaIds.Count == 1 ? x.Count : null,
                })
                .ToList(),
            UndatedDocuments = undated.Count,
        };
    }

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
    /// read as the row's whole story.</summary>
    private static List<string> ClassesOf(IEnumerable<string> lemmaIds)
    {
        var classes = lemmaIds.Select(ClassOf).ToList();
        return classes.Contains(null) ? [] : classes.OfType<string>().Distinct().ToList();
    }

    /// <summary>Every use of the word's lexeme in one document, grouped by the reading
    /// each line was resolved to, with the surface words highlighted. Null when the
    /// document does not attest it.</summary>
    public async Task<AttestationLines?> InDocument(string word, string ident)
    {
        var lemmaIds = LemmaIdsFor(lemmaTable, word);
        // one query per reading: a span query says which lines matched, never which
        // of its OR'd terms did, and an ambiguous word has two or three readings at
        // most. A line the resolver left ambiguous answers to each of them, and is
        // counted under each: that it could be either is the fact, not a bug.
        var matched = lemmaIds
            .Select(id => (Id: id, Result: searcher.SearchLemma(ident, [id])))
            .Where(x => x.Result is { Lines.Count: > 0 })
            .ToList();
        if (matched.Count == 0)
        {
            return null;
        }
        var readings = matched
            .Select(x => new
            {
                x.Id,
                x.Result,
                Lemma = lemmaTable.DisplayLemmaOf(x.Id) ?? x.Id,
                Uses = Occurrences(x.Result!).ToHashSet(),
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
                    LemmaIds = row.Select(x => x.Id).ToList(),
                    Lemma = lemma.Key,
                    Classes = ClassesOf(row.Select(x => x.Id)),
                    // the row's readings claim the same uses, so the count is that
                    // set's: one term's spans cannot overlap each other
                    Count = row.Key.Count,
                    // and the same uses are the same lines: any reading's serve
                    Lines = row.First().Result!.Lines
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
            // the union, not the sum: a word the resolver left ambiguous is claimed
            // by every reading, and is still one use of it
            UseCount = matched.SelectMany(x => Occurrences(x.Result!)).Distinct().Count(),
            Groups = groups,
        };
    }
}

/// <summary>The corpus documents attesting a lexeme, oldest first</summary>
public class DictionaryAttestations
{
    public required string Word { get; set; }
    /// <summary>The display lemmas being walked; empty when the lemma table does not
    /// know the word, in which case there is nothing to walk</summary>
    public required List<string> Lemmas { get; set; }
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
    /// the very same words — the document's use of the word is genuinely either.</summary>
    public required List<string> LemmaIds { get; set; }
    /// <summary>The headword a reader would look up ("beg")</summary>
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
