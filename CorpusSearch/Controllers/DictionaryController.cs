using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CorpusSearch.Service;
using Microsoft.AspNetCore.Mvc;

namespace CorpusSearch.Controllers;

/// <summary>
/// Unstable API
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DictionaryController(
    DictionaryLookupService lookupService, DictionaryHistoryService historyService,
    DictionaryAttestationService attestationService, DictionaryBrowseService browseService,
    LemmaIndexService lemmaIndexService, CorpusVocabulary vocabulary,
    DictionaryStatsService statsService)
{
    /// <summary>The front page's coverage numbers: what share of the corpus the
    /// books, the recordings and the lemma table can answer for. Counts, with
    /// the audio trio null until the recordings are read.</summary>
    [HttpGet("stats")]
    public DictionaryStats Stats() => statsService.Stats();

    /// <summary>
    /// Returns diction
    /// </summary>
    /// <param name="lang"></param>
    /// <param name="word"></param>
    /// <param name="context">optional: the text surrounding the word, used to match phrases/idioms</param>
    /// <returns></returns>
    [HttpGet]
    public IEnumerable<DictionarySummary> Get([FromQuery] string lang, [FromQuery] string word, [FromQuery] string? context = null)
    {
        return lookupService.Lookup(lang, word, context);
    }

    /// <summary>
    /// A few completions for the look-up box, commonest first — or, when
    /// nothing the books hold begins with what was typed, near spellings said
    /// to be that. Capped small: the box offers a next keystroke, not an index.
    /// </summary>
    [HttpGet("suggest")]
    public DictionarySuggestions Suggest([FromQuery] string q = "", [FromQuery] int count = 6)
    {
        return lookupService.Suggest(q, Math.Clamp(count, 1, 10), vocabulary);
    }

    /// <summary>
    /// The teanglann-style full page for a word (experimental): per-dictionary
    /// groups, the word's own recording, near-match suggestions as a tier.
    /// </summary>
    /// <param name="dict">optional dictionary slug ("cregeen"): scopes the page
    /// to one dictionary, as /dictionary/in/{dict}/{word} does</param>
    [HttpGet("page")]
    public DictionaryPage Page([FromQuery] string lang, [FromQuery] string word, [FromQuery] string? dict = null)
    {
        var page = lookupService.Page(lang, word, dict);
        // the lookup is about the books; whether a text ever says the word is the
        // corpus's business, and the page only carries the answer — including
        // "not yet", which a phrase gets until the corpus has been read for it
        page.Attested = vocabulary.Attestation(word);
        return page;
    }

    /// <summary>
    /// The dictionaries answering a query language, for the page's scope picker.
    /// </summary>
    [HttpGet("dictionaries")]
    public IEnumerable<DictionaryInfo> Dictionaries([FromQuery] string lang)
    {
        return lookupService.Dictionaries(lang);
    }

    /// <summary>
    /// The lexeme's corpus history (experimental): earliest attestation, the
    /// spelling cluster, use by decade, traditional/revived split, cognates.
    /// </summary>
    [HttpGet("history")]
    public DictionaryHistory History([FromQuery] string lang, [FromQuery] string word)
    {
        return historyService.History(lang, word);
    }

    /// <summary>
    /// The corpus documents attesting a word's lexeme, oldest first: the word
    /// page's attestation walk (experimental).
    /// </summary>
    /// <param name="lemma">optional display lemma: one reading's documents, for
    /// the walk's per-reading tabs</param>
    [HttpGet("attestations")]
    public DictionaryAttestations Attestations([FromQuery] string word, [FromQuery] string? lemma = null)
    {
        return attestationService.Attestations(word, lemma);
    }

    /// <summary>
    /// Every use of a word's lexeme within one document, for the walk's current step.
    /// </summary>
    /// <param name="lemma">optional display lemma: one reading's uses, matching
    /// the tab the step was opened from</param>
    [HttpGet("attestations/{ident}")]
    public async Task<ActionResult<AttestationLines>> AttestationsInDocument(
        string ident, [FromQuery] string word, [FromQuery] string? lemma = null)
    {
        var lines = await attestationService.InDocument(word, ident, lemma);
        return lines == null ? new NotFoundResult() : lines;
    }

    /// <summary>
    /// One page of a dictionary's index: the letters, one letter's prefix bar,
    /// and the headwords under a prefix.
    /// </summary>
    /// <param name="dict">the dictionary's slug ("cregeen")</param>
    /// <param name="at">a letter ("a") or a prefix ("aal"); the first letter when
    /// it names neither</param>
    [HttpGet("browse")]
    public ActionResult<DictionaryBrowsePage> Browse([FromQuery] string dict, [FromQuery] string? at = null)
    {
        var page = browseService.Page(dict, at);
        return page == null ? new NotFoundResult() : page;
    }

    /// <summary>
    /// A handful of a dictionary's entries spanning corpus use, unordered — a
    /// couple common, some middling, one no text says: an invitation to open
    /// the book anywhere rather than at A.
    /// </summary>
    [HttpGet("samples")]
    public ActionResult<List<DictionarySample>> Samples([FromQuery] string dict, [FromQuery] int count = 6)
    {
        var samples = browseService.Samples(dict, count);
        return samples == null ? new NotFoundResult() : samples;
    }

    /// <summary>
    /// One page of the lemma index: every lemma the tables link a form to, one
    /// letter at a time, in the browse page's shape.
    /// </summary>
    /// <param name="at">a letter ("a") or a prefix; the first letter when it
    /// names neither</param>
    [HttpGet("lemmas")]
    public DictionaryBrowsePage Lemmas([FromQuery] string? at = null)
    {
        return lemmaIndexService.Index(at);
    }

    /// <summary>
    /// One lemma's form tree: the forms the tables link to it, grouped by link
    /// type, each marked for corpus attestation and unverified links.
    /// </summary>
    [HttpGet("lemma")]
    public ActionResult<LemmaTreePage> Lemma([FromQuery] string lemma)
    {
        var page = lemmaIndexService.Tree(lemma);
        return page == null ? new NotFoundResult() : page;
    }

    /// <summary>
    /// The headwords either side of a word: stepping through a dictionary the way
    /// you turn a page.
    /// </summary>
    /// <param name="dict">optional slug: one book's own order. Without it, the
    /// union across every dictionary, in collation order</param>
    [HttpGet("neighbours")]
    public DictionaryNeighbours Neighbours([FromQuery] string word, [FromQuery] string? dict = null)
    {
        return browseService.Neighbours(dict, word);
    }

    /// <summary>
    /// The dictionary-coverage debug view: per-token dictionary/lemma status
    /// for each posted line (the client's dictionary debug mode).
    /// </summary>
    [HttpPost("coverage")]
    public CoverageResponse Coverage([FromBody] CoverageRequest request)
    {
        // a debug endpoint, but still bounded: the client chunks its requests
        var lines = request.Lines.Take(500).ToList();
        return new CoverageResponse { Lines = lookupService.Coverage(request.Lang, lines) };
    }

    public class CoverageRequest
    {
        public required string Lang { get; set; }
        public required List<string> Lines { get; set; }
    }

    public class CoverageResponse
    {
        public required List<List<TokenCoverage>> Lines { get; set; }
    }
}
