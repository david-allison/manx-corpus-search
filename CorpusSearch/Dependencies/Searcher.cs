using CorpusSearch.Dependencies.csly;
using CorpusSearch.Dependencies.Lucene;
using CorpusSearch.Model;
using Lucene.Net.Index;
using Lucene.Net.Search.Spans;
using sly.parser;
using System;
using System.Collections.Generic;
using System.Linq;
using CorpusSearch.Dependencies.csly.Model;

namespace CorpusSearch.Dependencies;

public class Searcher(LuceneIndex luceneIndex, SearchParser parser)
{
    // responsible for converting from a 

    internal SearchResult SearchWork(string ident, string query, SearchOptions options, bool returnTranscriptData)
    {
        // Detect '*' on the normalized*query to handle '.*'.
        // Intended for good faith use, not security hardening.
        var normalizedQuery = GetTerm(query, options);
        if (normalizedQuery.Length > 0 && normalizedQuery.All(x => x == '*'))
        {
            return new SearchResult
            {
                // reference-only rows (chapter headings) count as content: they render
                // as section headings even though both text cells are empty
                Lines = luceneIndex.GetAllLines(ident, returnTranscriptData)
                    .Where(x => !string.IsNullOrEmpty(x.Manx) || !string.IsNullOrEmpty(x.English)
                                || !string.IsNullOrEmpty(x.Reference)).ToList(),
                TotalMatches = null,
            };
        }

        // parse the string into a Result<Expression>
        var parsed = parser.Parse(query);

        var (spanQuery, referenceQuery) = BuildQueries(query, parsed, options);
        return luceneIndex.Search(ident, spanQuery, returnTranscriptData, referenceQuery);
    }

    /// <summary>
    /// The text-field query plus the verse/chapter reference side-query (Manx
    /// searches only), so "Thessalonians" or "2.16" still finds referenced lines.
    /// A query the grammar cannot parse at all ("2.16" lexes as a number plus a
    /// fragment) is retried as a literal term: the per-field term builders
    /// normalize its punctuation away, which is strictly better than the error
    /// the parse used to throw.
    /// </summary>
    private (SpanQuery Query, SpanQuery? ReferenceQuery) BuildQueries(string rawQuery,
        ParseResult<ExpressionToken, Expression> parsed, SearchOptions options)
    {
        if (parsed.IsOk && parsed.Result != null)
        {
            return (ToSpanQuery(parsed.Result, options), ReferenceQueryOrNull(parsed, options));
        }
        var literalOptions = options with { IgnoreHyphens = true };
        var literal = ManxTermQuery(rawQuery, literalOptions);
        return (literal, ReferenceTermOrNull(rawQuery, literalOptions));
    }

    private static SpanQuery? ReferenceTermOrNull(string rawQuery, SearchOptions options)
    {
        if (options.SearchType != SearchType.Manx)
        {
            return null;
        }
        try
        {
            return ManxTermQuery(rawQuery, ReferenceOptions(options));
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// The same query against the verse/chapter reference field, run beside a Manx
    /// search so "Thessalonians" or "2.16" still finds referenced lines. Null when
    /// the side-query cannot be built: reference matching must never break search.
    /// </summary>
    private SpanQuery? ReferenceQueryOrNull(ParseResult<ExpressionToken, Expression> parsed, SearchOptions options)
    {
        if (options.SearchType != SearchType.Manx)
        {
            return null;
        }
        try
        {
            return ToSpanQuery(parsed, ReferenceOptions(options));
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static SearchOptions ReferenceOptions(SearchOptions options) => options with
    {
        SearchType = SearchType.Reference,
        CaseSensitive = false,
        NormalizeDiacritics = false,
        IgnoreHyphens = true, // "2 16" queries as an ordered phrase
    };

    /// <summary>
    /// Returns all lines for a provided document
    /// </summary>
    /// <param name="ident">The ID of the document</param>
    internal List<DocumentLine> GetAllLines(string ident)
    {
        return luceneIndex.GetAllLines(ident, getTranscript: false);
    }

    /// <summary>
    /// The lines of a document with a CsvLineNumber in [start, end]: the first
    /// <paramref name="limit"/> of them, or the last if <paramref name="fromEnd"/>.
    /// Expands the context around a search result (#286).
    /// </summary>
    internal (List<DocumentLine> Lines, int TotalInRange) GetLines(string ident, int start, int end, int limit,
        bool fromEnd, bool getTranscript)
    {
        return luceneIndex.GetLines(ident, start, end, limit, fromEnd, getTranscript);
    }

    /// <summary>The first and last CsvLineNumber of a document; null if the document has no lines</summary>
    internal (int First, int Last)? GetLineNumberRange(string ident)
    {
        return luceneIndex.GetLineNumberRange(ident);
    }

    /// <summary>Corpus-wide lines carrying one of the canonical reference
    /// <paramref name="keys"/>, or any verse under <paramref name="chapterPrefix"/>:
    /// the verse-alignment lookup.</summary>
    internal List<LuceneIndex.VerseAlignmentLine> GetVerseAlignment(
        IReadOnlyCollection<string> keys, string? chapterPrefix)
    {
        return luceneIndex.GetVerseAlignment(keys, chapterPrefix);
    }

    /// <summary>
    /// Every corpus line whose token resolves to one of <paramref name="lemmaIds"/>,
    /// as one query against <see cref="LuceneIndex.DOCUMENT_LEMMA_MANX"/> rather than a
    /// scan per surface spelling. The field carries the lemma ids the resolver settled on
    /// at each token's position, so 'aase.v' matches 'daase' and highlights the surface
    /// word: the cluster is neither truncated by a form budget nor widened by spellings
    /// the resolver already ruled out on that line.
    /// </summary>
    /// <remarks>The query grammar has no field syntax, so the field is reachable only
    /// from here — see CorpusSearch.Test/LemmaIndexTest for what it matches</remarks>
    private static SpanQuery? LemmaQuery(IReadOnlyCollection<string> lemmaIds)
    {
        var terms = lemmaIds
            .Select(id => (SpanQuery)new SpanTermQuery(new Term(LuceneIndex.DOCUMENT_LEMMA_MANX, id)))
            .ToArray();
        // an ambiguous word means every reading it could be: 'veg' is veg.x or beg.a
        return terms.Length switch
        {
            0 => null,
            1 => terms[0],
            _ => new SpanOrQuery(terms),
        };
    }

    /// <summary>The corpus documents attesting a lexeme, with their dates and counts.
    /// Empty when the word has no lemma reading to search for.</summary>
    public ScanResult ScanLemma(IReadOnlyCollection<string> lemmaIds)
    {
        var query = LemmaQuery(lemmaIds);
        return query == null ? new ScanResult() : luceneIndex.Scan(query);
    }

    /// <summary>Every use of a lexeme within one document, surface words highlighted</summary>
    internal SearchResult? SearchLemma(string ident, IReadOnlyCollection<string> lemmaIds)
    {
        var query = LemmaQuery(lemmaIds);
        return query == null ? null : luceneIndex.Search(ident, query, getTranscriptData: false);
    }

    public ScanResult Scan(string query)
    {
        return Scan(query, SearchOptions.Default);
    }

    public ScanResult Scan(string query, SearchOptions searchOptions)
    {
        // parse the string into a Result<Expression>
        var parsed = parser.Parse(query);

        var (spanQuery, referenceQuery) = BuildQueries(query, parsed, searchOptions);
        return luceneIndex.Scan(spanQuery, referenceQuery);
    }


    private SpanQuery ToSpanQuery(ParseResult<ExpressionToken, Expression> parsed, SearchOptions searchOptions)
    {
        if (!parsed.IsOk || parsed.Result == null)
        {
            throw new ArgumentException("failed to parse query: " + string.Join(",", parsed.Errors.Select(x => x.ErrorMessage)));
        }

        return ToSpanQuery(parsed.Result, searchOptions);
    }



    private SpanQuery ToSpanQuery(Expression result, SearchOptions searchOptions)
    {
        SpanQuery ToSpanQueryInner(Expression res) => ToSpanQuery(res, searchOptions);
        SpanQuery ToManx(string value) => ManxTermQuery(value, searchOptions);

        switch (result)
        {
            case StringExpression s:
                return ToManx(s.Term);
            case OrExpression or:
            {
                var left = ToSpanQueryInner(or.Left);
                var right = ToSpanQueryInner(or.Right);
                return new SpanOrQuery(left, right);
            }
            case AndExpression and:
            {
                var left = ToSpanQueryInner(and.Left);
                var right = ToSpanQueryInner(and.Right);
                // TODO: Might be something better than an infinite slop
                return new SpanNearQuery([left, right], int.MaxValue, false);
            }
            case AdjacentWordExpression e:
                if (searchOptions.IgnoreHyphens)
                {
                    var atoms = e.Words.SelectMany(w => SplitAtoms(GetTerm(w, searchOptions))).ToList();
                    if (atoms.Count > 0)
                    {
                        return HyphenAgnosticQuery(atoms, searchOptions);
                    }
                }
                var queries = e.Words.Select(ToManx);
                return new SpanNearQuery(queries.ToArray(), 0, true);
            case NotExpression e:
                // This also feels inefficient: we want a "not" for the document, and the easiest way seems to be a "not near the span"
                SpanQuery l = ToSpanQueryInner(e.Left);
                SpanQuery r = ToSpanQueryInner(e.Right);
                var notNear = new SpanNearQuery([l, r], int.MaxValue, false);
                return new SpanNotQuery(l, notNear);
            case WrappedExpression w:
                return ToSpanQueryInner(w.Wrapped);
            default:
                throw new NotImplementedException(result.GetType().ToString());
        }
    }

    private static SpanQuery ManxTermQuery(string value, SearchOptions searchOptions)
    {
        var normalizedTerm = GetTerm(value, searchOptions);
        if (searchOptions.IgnoreHyphens)
        {
            var atoms = SplitAtoms(normalizedTerm);
            if (atoms.Length > 0)
            {
                return HyphenAgnosticQuery(atoms, searchOptions);
            }
        }
        return SingleTokenQuery(normalizedTerm, searchOptions, ignoreHyphens: false);
    }

    /// <summary>
    /// A query where hyphens, spaces and joined words are interchangeable: the atoms of
    /// 'lhiam-lhiat' (or of 'lhiam lhiat') match 'lhiam-lhiat', 'lhiam lhiat' and 'lhiamlhiat'.
    /// </summary>
    /// <param name="atoms">the hyphen/space-separated parts of the query, in order</param>
    private static SpanQuery HyphenAgnosticQuery(IReadOnlyList<string> atoms, SearchOptions searchOptions)
    {
        // one alternative per way of regrouping adjacent atoms into tokens:
        // [lhiam, lhiat] => the token 'lhiamlhiat' (which also matches 'lhiam-lhiat', see
        // ManxQuery) or the phrase 'lhiam lhiat'
        var alternatives = Segmentations(atoms)
            .Select(tokens => tokens.Count == 1
                ? TokenQuery(tokens[0])
                : new SpanNearQuery(tokens.Select(TokenQuery).ToArray(), 0, true))
            .ToArray();
        return alternatives.Length == 1 ? alternatives[0] : new SpanOrQuery(alternatives);

        SpanQuery TokenQuery(string token) => SingleTokenQuery(token, searchOptions, ignoreHyphens: true);
    }

    /// <summary>Every way of regrouping adjacent atoms into tokens (2^(n-1) for n atoms)</summary>
    private static List<List<string>> Segmentations(IReadOnlyList<string> atoms)
    {
        // guard against pathological queries: only the fully-split and fully-joined forms
        const int maxAtomsForFullExpansion = 5;
        if (atoms.Count > maxAtomsForFullExpansion)
        {
            return [atoms.ToList(), [string.Concat(atoms)]];
        }

        var result = new List<List<string>>();
        for (int joinMask = 0; joinMask < 1 << (atoms.Count - 1); joinMask++)
        {
            var tokens = new List<string>();
            var current = atoms[0];
            for (int i = 1; i < atoms.Count; i++)
            {
                if ((joinMask & (1 << (i - 1))) != 0)
                {
                    current += atoms[i];
                }
                else
                {
                    tokens.Add(current);
                    current = atoms[i];
                }
            }
            tokens.Add(current);
            result.Add(tokens);
        }
        return result;
    }

    /// <summary>Splits a normalized term on hyphens (and any spaces normalization introduced)</summary>
    private static string[] SplitAtoms(string normalizedTerm) =>
        normalizedTerm.Split(['-', ' '], StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// 'Did you mean' candidates for a query which found nothing (#158): index terms which
    /// differ only in hyphenation ('lumlane' => 'lum-lane'), plus the space-separated form
    /// of a hyphenated query. Callers should drop candidates without matches.
    /// </summary>
    public List<string> GetHyphenAlternates(string query, SearchOptions searchOptions)
    {
        var parsed = parser.Parse(query);
        if (!parsed.IsOk || parsed.Result == null)
        {
            return [];
        }

        var words = PlainWords(parsed.Result);
        if (words == null)
        {
            return [];
        }

        var atoms = words.SelectMany(w => SplitAtoms(GetTerm(w, searchOptions))).ToList();
        // a wildcard matches far more of the vocabulary than hyphen variants
        if (atoms.Count == 0 || atoms.Any(a => a.Any(c => c is '*' or '_' or '+' or '?')))
        {
            return [];
        }

        var termKey = GetTermKey(searchOptions);
        var automaton = ManxQuery.BuildAutomaton(new Term(termKey, string.Concat(atoms)), ignoreHyphens: true);
        var alternates = luceneIndex.GetMatchingTerms(termKey, automaton, limit: 5);
        if (atoms.Count > 1)
        {
            alternates.Add(string.Join(" ", atoms));
        }

        var normalizedQuery = GetTerm(query, searchOptions);
        return alternates.Where(x => x != normalizedQuery).Distinct().ToList();
    }

    /// <summary>The words of a plain word/phrase query; null for operators etc.</summary>
    private static List<string>? PlainWords(Expression expression) => expression switch
    {
        StringExpression s => [s.Term],
        AdjacentWordExpression e => e.Words.ToList(),
        _ => null,
    };

    private static SpanQuery SingleTokenQuery(string normalizedTerm, SearchOptions searchOptions, bool ignoreHyphens)
    {
        Term term = new Term(GetTermKey(searchOptions), normalizedTerm);
        if (searchOptions.NormalizeDiacritics)
        {
            var manx = new ManxQuery(term, ignoreHyphens);
            return new SpanMultiTermQueryWrapper<ManxQuery>(manx);
        }
        else if (ExtendedWildcardQuery.AppliesTo(normalizedTerm))
        {
            return new SpanMultiTermQueryWrapper<ExtendedWildcardQuery>(new ExtendedWildcardQuery(term));
        }
        else
        {
            // TODO: This does not handle trailing question marks and some forms of dashes
            return new SpanTermQuery(term);
        }
    }

    private static string GetTerm(string value, SearchOptions searchOptions)
    {
        switch (searchOptions.SearchType)
        {
            case SearchType.Manx: return DocumentLine.NormalizeManx(value, allowQuestionMark: true, preserveCase: searchOptions.CaseSensitive);
            case SearchType.English: return DocumentLine.NormalizeEnglish(value, allowQuestionMark: true, preserveCase: searchOptions.CaseSensitive);
            case SearchType.Reference: return NormalizeReference(value);
            default: throw new ArgumentException("Unhandlded case: " + searchOptions.SearchType);
        }
    }

    /// <summary>Mirrors <see cref="Lucene.ReferenceTokenizer"/>: letter/digit runs,
    /// lowercased — "2.16" and "Psalm 23" keep their numbers</summary>
    private static string NormalizeReference(string value)
    {
        return System.Text.RegularExpressions.Regex.Replace(
            value.ToLowerInvariant(), @"[^\p{L}\p{N}]+", " ").Trim();
    }

    private static string GetTermKey(SearchOptions searchOptions)
    {
        switch (searchOptions.SearchType)
        {
            case SearchType.Manx:
                return searchOptions.CaseSensitive ? LuceneIndex.DOCUMENT_CASED_MANX : LuceneIndex.DOCUMENT_NORMALIZED_MANX;
            case SearchType.English:
                return searchOptions.CaseSensitive ? LuceneIndex.DOCUMENT_CASED_ENGLISH : LuceneIndex.DOCUMENT_NORMALIZED_ENGLISH;
            case SearchType.Reference:
                return LuceneIndex.DOCUMENT_REFERENCE;
            default: throw new ArgumentException("Unhandlded case: " + searchOptions.SearchType);
        }
    }

    internal void OnAllDocumentsAdded()
    {
        luceneIndex.Compact();
    }

    internal void AddDocument(IDocument document, IEnumerable<DocumentLine> data)
    {
        luceneIndex.Add(document, data);
    }

    public List<(string, long)> QueryTermFrequency()
    {
        return luceneIndex.GetTermFrequencyList();
    }
        
    public long CountManxTerms() => luceneIndex.CountManxTerms();
}