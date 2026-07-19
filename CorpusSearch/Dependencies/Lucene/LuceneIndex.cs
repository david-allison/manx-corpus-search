using System;
using System.Collections.Generic;
using System.Linq;
using CorpusSearch.Model;
using CorpusSearch.Utils;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Search.Spans;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Lucene.Net.Util.Automaton;
using Document = Lucene.Net.Documents.Document;
// Lucene's index of a line across the whole index, 0..MaxDoc-1: e.g. 73412.
// This is not necessarily contiguous within a corpus document.
// Note: 'document' refers to a Lucene document.
using DocId = int;
// names a corpus document (DOCUMENT_IDENT): e.g. "MatthewGospel1748"
using Ident = string;

namespace CorpusSearch.Dependencies.Lucene;

public class LuceneIndex(IndexWriter indexWriter)
{
    public const string DOCUMENT_NAME = "name";
    public const string DOCUMENT_IDENT = "ident";
    public const string DOCUMENT_NOTES = "notes";
    public const string DOCUMENT_PAGE = "page";
    public const string DOCUMENT_LINE_NUMBER = "line_number";
    public const string DOCUMENT_NORMALIZED_MANX = "manx";
    public const string DOCUMENT_REAL_MANX = "real_manx";
    public const string DOCUMENT_ORIGINAL_MANX = "original_manx";
    public const string DOCUMENT_NORMALIZED_ENGLISH = "english";
    public const string DOCUMENT_REAL_ENGLISH = "real_english";
    public const string DOCUMENT_ORIGINAL_ENGLISH = "original_english";
    /// <summary>Case-preserving <see cref="DOCUMENT_NORMALIZED_MANX"/>: targeted by case-sensitive queries (#19)</summary>
    public const string DOCUMENT_CASED_MANX = "manx_cased";
    /// <summary>Case-preserving <see cref="DOCUMENT_NORMALIZED_ENGLISH"/>: targeted by case-sensitive queries (#19)</summary>
    public const string DOCUMENT_CASED_ENGLISH = "english_cased";
    public const string DOCUMENT_CREATED_START = "created_start";
    public const string DOCUMENT_CREATED_END = "created_end";
    public const string DOCUMENT_SPEAKER = "speaker";
    /// <summary>The line's language when the Manx column is not Manx (e.g. "en" for an
    /// untranslated row); absent on Manx lines. Not queried yet: ready for a search filter.</summary>
    public const string DOCUMENT_LANGUAGE = "language";
    /// <summary><see cref="DOCUMENT_NORMALIZED_MANX"/> restricted to lines whose Manx column
    /// is really Manx: Manx-language statistics (<see cref="CountManxTerms"/>,
    /// <see cref="GetTermFrequencyList"/>) read this field, so untranslated rows and speaker
    /// codes don't count as Manx. Term frequencies only.</summary>
    public const string DOCUMENT_MANX_GV = "manx_gv";
    /// <summary><see cref="DOCUMENT_NORMALIZED_MANX"/> with candidate lemma ids injected at
    /// each token's position (<see cref="LemmaTokenFilter"/>): a query on manx_lemma:aase.v
    /// matches 'daase' and highlights it, via the preserved surface offsets. Only fed for
    /// Manx lines, so lemma statistics stay clean. Queried and highlighted, never read back.</summary>
    public const string DOCUMENT_LEMMA_MANX = "manx_lemma";

    /// <summary>The line's verse/chapter reference ("MS 1 Thessalonians 2.16"): metadata
    /// like <see cref="DOCUMENT_SPEAKER"/>, but tokenized (digit-preserving analyzer) so
    /// references stay searchable without polluting the Manx token stream.</summary>
    public const string DOCUMENT_REFERENCE = "reference";

    /// <summary>The line's canonical "book.chapter[.verse]" reference key ("psalms.23.1"):
    /// the cross-version identity of a verse, exact-matched (a single untokenized term)
    /// by the verse-alignment lookups. See <see cref="Model.CanonicalReference"/>.</summary>
    public const string DOCUMENT_CANONICAL_REFERENCE = "canonical_reference";

    public const string SUBTITLE_START = "subtitle_start";
    public const string SUBTITLE_END = "subtitle_end";

    /// <summary>Whether the field preserves case (so its analyzer must not case-fold)</summary>
    internal static bool IsCasedField(string field) => field is DOCUMENT_CASED_MANX or DOCUMENT_CASED_ENGLISH;

    /// <summary>Whether the field's tokens append candidate lemma ids (<see cref="LemmaTokenFilter"/>)</summary>
    internal static bool IsLemmaField(string field) => field is DOCUMENT_LEMMA_MANX;

    /// <summary>Whether the field feeds the Manx-language statistics (<see cref="NonWordTokenFilter"/>)</summary>
    internal static bool IsStatsField(string field) => field is DOCUMENT_MANX_GV;

    /// <summary>Whether the field holds verse/chapter references (digit-preserving analyzer)</summary>
    internal static bool IsReferenceField(string field) => field is DOCUMENT_REFERENCE;

    private static bool IsManxField(string field) =>
        field is DOCUMENT_NORMALIZED_MANX or DOCUMENT_CASED_MANX or DOCUMENT_LEMMA_MANX;

    private static bool IsEnglishField(string field) => field is DOCUMENT_NORMALIZED_ENGLISH or DOCUMENT_CASED_ENGLISH;

    private IndexReader UseReader() => indexWriter.GetReader(applyAllDeletes: true);

    public static LuceneIndex GetInstance(LemmaResolver? lemmaResolver = null)
    {
        // Ensures index backward compatibility
        const LuceneVersion appLuceneVersion = LuceneVersion.LUCENE_48;

        // should be under a "using"
        var dir = new RAMDirectory();// FSDirectory.Open(indexPath);

        // Create an analyzer to process the text
        var analyzer = new ManxAnalyzer(lemmaResolver);

        // Create an index writer
        var indexConfig = new IndexWriterConfig(appLuceneVersion, analyzer)
        {
            // startup indexes in parallel (#303): one indexing thread per core (default: 8),
            // and a RAM buffer large enough that concurrent writers don't constantly flush
            // (16MB default, shared across all threads) - the index lives in RAM regardless
            MaxThreadStates = Environment.ProcessorCount,
            RAMBufferSizeMB = 64,
        };
        var writer = new IndexWriter(dir, indexConfig);
        return new LuceneIndex(writer);
    }

    public void Add(IDocument document, IEnumerable<DocumentLine> data)
    {
        var fieldType = new FieldType(TextField.TYPE_STORED)
        {
            StoreTermVectorPositions = true,
            StoreTermVectors = true,
            StoreTermVectorOffsets = true
        };

        // the cased fields are only queried, never read back: no need to store the text
        var casedFieldType = new FieldType(TextField.TYPE_NOT_STORED)
        {
            StoreTermVectorPositions = true,
            StoreTermVectors = true,
            StoreTermVectorOffsets = true
        };

        // the statistics field only sums term frequencies: no positions, vectors or storage
        var statsFieldType = new FieldType(TextField.TYPE_NOT_STORED)
        {
            IndexOptions = IndexOptions.DOCS_AND_FREQS,
        };

        foreach (var line in data)
        {
            var doc = new Document
            {
                // StringField indexes but doesn't tokenize
                new StringField(DOCUMENT_NAME, document.Name, Field.Store.YES),
                new StringField(DOCUMENT_IDENT, document.Ident, Field.Store.YES),
                new StringField(DOCUMENT_REAL_MANX, line.Manx, Field.Store.YES),
                new StringField(DOCUMENT_REAL_ENGLISH, line.English, Field.Store.YES),
                new Int32Field(DOCUMENT_LINE_NUMBER, line.CsvLineNumber, Field.Store.YES),
                new Field(DOCUMENT_NORMALIZED_MANX, line.NormalizedManx , fieldType),
                // TODO: Confirm that the analyzer that we use is also appropriate for English
                new Field(DOCUMENT_NORMALIZED_ENGLISH, line.NormalizedEnglish, fieldType),
                new Field(DOCUMENT_CASED_MANX, line.NormalizedManxCased, casedFieldType),
                new Field(DOCUMENT_CASED_ENGLISH, line.NormalizedEnglishCased, casedFieldType),

            };


            AddField(DOCUMENT_ORIGINAL_ENGLISH, line.EnglishOriginal);
            AddField(DOCUMENT_ORIGINAL_MANX, line.ManxOriginal);
            AddField(DOCUMENT_CREATED_START, document.CreatedCircaStart?.ToString());
            AddField(DOCUMENT_CREATED_END, document.CreatedCircaEnd?.ToString());
            AddField(DOCUMENT_NOTES, line.Notes);
            AddField(DOCUMENT_PAGE, line.Page.ToString());
            AddField(DOCUMENT_SPEAKER, line.Speaker);
            if (!string.IsNullOrWhiteSpace(line.Reference))
            {
                // tokenized and stored: searchable via its own analyzer, returned for display
                doc.Add(new TextField(DOCUMENT_REFERENCE, line.Reference, Field.Store.YES));
            }
            AddField(DOCUMENT_CANONICAL_REFERENCE, line.CanonicalReference);

            // non-Manx lines stay searchable (the manx field above), but only Manx lines
            // feed the Manx statistics and the lemma field
            if (line.IsManxLanguage)
            {
                // the stats text drops mid-text scripture citations; the searchable
                // fields above keep them (display/analysed identity for highlights)
                doc.Add(new Field(DOCUMENT_MANX_GV, line.NormalizedStatsManx, statsFieldType));
                // same text as the manx field: the analyzer injects the lemma ids
                doc.Add(new Field(DOCUMENT_LEMMA_MANX, line.NormalizedManx, casedFieldType));
            }
            else
            {
                AddField(DOCUMENT_LANGUAGE, line.Language);
            }

            line.SubStart?.Let(start => doc.Add(new DoubleField(SUBTITLE_START, start, Field.Store.YES)));
            line.SubEnd?.Let(end => doc.Add(new DoubleField(SUBTITLE_END, end, Field.Store.YES)));

            indexWriter.AddDocument(doc);
            continue;

            void AddField(string key, string? value)
            {
                // ArgumentNullException if the value is null
                value?.Let(val => doc.Add(new StringField(key, val, Field.Store.YES)));
            }
        }
    }

    public void Compact()
    {
        indexWriter.ForceMerge(1);
        using var reader = UseReader();
        _documentLookup = BuildDocumentLookup(reader);
    }

    /// <summary>
    /// docId -> (document ident, line number) for the whole index, so <see cref="Scan"/> can
    /// group matched lines into corpus documents without loading each line's stored document
    /// (the dominant cost when a common word matches tens of thousands of lines).
    /// </summary>
    private sealed class DocumentLookup(Ident[] idents, int[] lineNumbers)
    {
        /// <summary>The corpus document ident and CsvLineNumber of the line with this docId</summary>
        public (Ident Ident, int LineNumber) Get(DocId docId) => (idents[docId], lineNumbers[docId]);
    }

    private DocumentLookup? _documentLookup;

    private static DocumentLookup BuildDocumentLookup(IndexReader reader)
    {
        var idents = new Ident[reader.MaxDoc];
        var lineNumbers = new int[reader.MaxDoc];
        // one shared string instance per document: ~800 idents across ~100k lines
        var identPool = new Dictionary<Ident, Ident>();
        var fields = new HashSet<string> { DOCUMENT_IDENT, DOCUMENT_LINE_NUMBER };
        for (DocId docId = 0; docId < reader.MaxDoc; docId++)
        {
            var document = reader.Document(docId, fields);
            // every line is indexed with its document's ident (see Add)
            var ident = document.GetString(DOCUMENT_IDENT) ?? "";
            if (identPool.TryGetValue(ident, out var pooled))
            {
                ident = pooled;
            }
            else
            {
                identPool.Add(ident, ident);
            }
            idents[docId] = ident;
            lineNumbers[docId] = document.GetInt32(DOCUMENT_LINE_NUMBER) ?? -1;
        }

        return new DocumentLookup(idents, lineNumbers);
    }


    internal SearchResult Search(Ident ident, SpanQuery query, bool getTranscriptData,
        SpanQuery? referenceQuery = null)
    {
        using var reader = UseReader();
        var searcher = new IndexSearcher(reader);

        ISet<DocId> acceptDocs = GetDocsForIdent(searcher, ident);

        var rewritten = (SpanQuery)query.Rewrite(reader);
        // TODO PERF: Inefficient - should be able to use GetSpans(?, acceptDocs, ?) - need to read documents to understand it
        var spanCollection = BuildSpanCollection(rewritten, reader, acceptDocs.Contains);

        var matchedDocs = new HashSet<DocId>(spanCollection.DistinctDocumentIds());
        var highlightTokenSpans = CollectHighlightTokenSpans(rewritten, reader, matchedDocs);
        string searchedField = rewritten.Field;

        // lines matched only through their verse/chapter reference ride along,
        // without text highlights (the match is in the reference, not the text)
        var referenceMatches = new List<(DocId, int)>();
        if (referenceQuery != null)
        {
            var referenceRewritten = (SpanQuery)referenceQuery.Rewrite(reader);
            var referenceCollection = BuildSpanCollection(referenceRewritten, reader, acceptDocs.Contains);
            referenceMatches = referenceCollection.DistinctDocuments()
                .Where(x => !matchedDocs.Contains(x.Item1))
                .ToList();
        }

        var docs = spanCollection.DistinctDocuments().Concat(referenceMatches).Select(x =>
        {
            var (docId, countInDoc) = x;
            var document = searcher.Doc(docId);
            var manx = document.RequireString(DOCUMENT_REAL_MANX);
            var english = document.RequireString(DOCUMENT_REAL_ENGLISH);

            string? notes = document.GetString(DOCUMENT_NOTES);
            int lineNumber = document.GetInt32(DOCUMENT_LINE_NUMBER) ?? -1;

            var highlights = matchedDocs.Contains(docId)
                ? ComputeHighlights(reader, docId, searchedField,
                    IsEnglishField(searchedField) ? english : manx,
                    highlightTokenSpans.GetValueOrDefault(docId))
                : null;

            return new DocumentLine
            {
                English = english,
                Manx = manx,
                Page = document.GetPageAsInt(),
                Notes = notes,
                CsvLineNumber = lineNumber,
                ManxHighlights = IsManxField(searchedField) ? highlights : null,
                EnglishHighlights = IsEnglishField(searchedField) ? highlights : null,
                ManxOriginal = document.GetString(DOCUMENT_ORIGINAL_MANX),
                EnglishOriginal = document.GetString(DOCUMENT_ORIGINAL_ENGLISH),
                SubStart = getTranscriptData ? document.GetDouble(SUBTITLE_START) : null,
                SubEnd = getTranscriptData ? document.GetDouble(SUBTITLE_END) : null,
                Speaker = document.GetString(DOCUMENT_SPEAKER),
                Reference = document.GetString(DOCUMENT_REFERENCE),
                CanonicalReference = document.GetString(DOCUMENT_CANONICAL_REFERENCE),
                Language = document.GetString(DOCUMENT_LANGUAGE),
                MatchesInLine = countInDoc
            };
        // document order: docID order is merge-dependent, so it cannot be relied on (#303)
        }).OrderBy(x => x.CsvLineNumber).ToList();

        return new SearchResult
        {
            Lines = docs,
            TotalMatches = spanCollection.GetTotalCount() + referenceMatches.Sum(x => x.Item2),
        };
    }

    private static EmptySpanCollection BuildSpanCollection(SpanQuery rewritten, IndexReader reader, Func<DocId, bool> acceptDocument)
    {
        EmptySpanCollection spanCollection = new();
        foreach (var (docId, _, _) in rewritten.EnumerateSpans(reader))
        {
            if (acceptDocument(docId))
            {
                spanCollection.Increment(docId);
            }
        }

        return spanCollection;
    }

    /// <summary>
    /// The token positions to highlight in each matched document.
    /// A second walk over the spans: <see cref="BuildSpanCollection"/> counts matches of the
    /// full query, this collects the positions of each term/phrase of it (see
    /// <see cref="SpanQueryHighlightExtractor"/>) in the documents which matched.
    /// </summary>
    private static Dictionary<DocId, List<(int Start, int End)>> CollectHighlightTokenSpans(
        SpanQuery rewritten, IndexReader reader, ISet<DocId> matchedDocs)
    {
        var result = new Dictionary<DocId, List<(int Start, int End)>>();
        if (matchedDocs.Count == 0)
        {
            return result;
        }

        foreach (var leafQuery in SpanQueryHighlightExtractor.GetHighlightLeaves(rewritten))
        {
            foreach (var (docId, start, end) in leafQuery.EnumerateSpans(reader))
            {
                if (!matchedDocs.Contains(docId))
                {
                    continue;
                }
                if (!result.TryGetValue(docId, out var spanList))
                {
                    result[docId] = spanList = [];
                }
                spanList.Add((start, end));
            }
        }
        return result;
    }

    /// <summary>
    /// Converts the matched token positions of one document to character ranges of the raw text:
    /// token position -> character offsets in the normalized field text (via the term vector)
    /// -> character offsets in the raw text (via <see cref="MappedText"/>).
    /// </summary>
    /// <returns>ranges ordered by start, overlaps merged; null if nothing can be highlighted</returns>
    private static List<HighlightRange>? ComputeHighlights(IndexReader reader, DocId docId, string field,
        string rawText, List<(int Start, int End)>? tokenSpans)
    {
        if (tokenSpans == null || tokenSpans.Count == 0 || string.IsNullOrEmpty(rawText))
        {
            return null;
        }

        var positionOffsets = TermVectorOffsetReader.GetPositionOffsets(reader, docId, field);
        if (positionOffsets == null)
        {
            return null;
        }

        MappedText normalized = IsEnglishField(field)
            ? NormalizationMapper.PaddedEnglish(rawText, preserveCase: IsCasedField(field))
            : NormalizationMapper.PaddedManx(rawText, preserveCase: IsCasedField(field));

        var ranges = new List<HighlightRange>();
        foreach (var (start, end) in tokenSpans)
        {
            // the offsets of a span's first and last token bound the matched text
            if (!positionOffsets.TryGetValue(start, out var first)
                || !positionOffsets.TryGetValue(end - 1, out var last))
            {
                continue;
            }
            var range = normalized.MapRangeToSource(first.Start, last.End);
            if (range != null)
            {
                ranges.Add(new HighlightRange(range.Value.Start, range.Value.End));
            }
        }

        return ranges.Count != 0 ? MergeOverlapping(ranges) : null;
    }

    private static List<HighlightRange> MergeOverlapping(List<HighlightRange> ranges)
    {
        ranges.Sort((a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : a.End.CompareTo(b.End));
        var merged = new List<HighlightRange> { ranges[0] };
        foreach (var range in ranges.Skip(1))
        {
            var previous = merged[^1];
            if (range.Start < previous.End)
            {
                if (range.End > previous.End)
                {
                    merged[^1] = previous with { End = range.End };
                }
            }
            else
            {
                merged.Add(range);
            }
        }
        return merged;
    }

    private ISet<DocId> GetDocsForIdent(IndexSearcher searcher, Ident ident)
    {
        var query = new TermQuery(new Term(DOCUMENT_IDENT, ident));

        // TODO: See if IBits will help here?
        return new HashSet<DocId>(searcher.AllDocIds(query));
    }

    /// <param name="checkTranscriptTimings">also answer, per recording, whether
    /// the matched lines say when they are spoken (<see cref="QueryDocumentResult.Timed"/>):
    /// the attestation walk's audio link wants a recording it can jump into.
    /// Off by default — the answer costs a stored-field read per matched line
    /// of every recording, which a Home page scan has no use for.</param>
    public ScanResult Scan(SpanQuery query, SpanQuery? referenceQuery = null,
        bool checkTranscriptTimings = false)
    {
        using var reader = UseReader();
        // tests scan without compacting; production builds the lookup in Compact()
        var lookup = _documentLookup ??= BuildDocumentLookup(reader);

        var spanQuery = (SpanQuery)query.Rewrite(reader);
        var spanCollection = BuildSpanCollection(spanQuery, reader, _ => true);
        var distinctDocuments = spanCollection.DistinctDocumentIds().ToList();

        // lines matched only through their verse/chapter reference count too
        var referenceCounts = new Dictionary<DocId, int>();
        if (referenceQuery != null)
        {
            var referenceRewritten = (SpanQuery)referenceQuery.Rewrite(reader);
            var referenceCollection = BuildSpanCollection(referenceRewritten, reader, _ => true);
            var textMatched = new HashSet<DocId>(distinctDocuments);
            foreach (var (docId, count) in referenceCollection.DistinctDocuments())
            {
                if (!textMatched.Contains(docId))
                {
                    referenceCounts[docId] = count;
                }
            }
        }

        // Group the matched lines into distinct Corpus Documents via the docId lookup. The
        // sample is the first line by line number: docID order is merge-dependent, so it
        // cannot be relied on (#303)
        var corpusDocuments = new Dictionary<Ident, (DocId SampleDocId, int SampleLineNumber, int Count)>();
        // the matched lines by corpus document, kept only when the timing
        // question will be asked of them: a big scan matches too many to hold
        var matchedLines = checkTranscriptTimings ? new Dictionary<Ident, List<DocId>>() : null;
        foreach (var docId in distinctDocuments.Concat(referenceCounts.Keys))
        {
            var (ident, lineNumber) = lookup.Get(docId);
            if (matchedLines != null)
            {
                if (!matchedLines.TryGetValue(ident, out var lines))
                {
                    matchedLines[ident] = lines = [];
                }
                lines.Add(docId);
            }
            var count = referenceCounts.TryGetValue(docId, out var referenceCount)
                ? referenceCount
                : spanCollection.GetCount(docId);
            if (corpusDocuments.TryGetValue(ident, out var existing))
            {
                corpusDocuments[ident] = lineNumber < existing.SampleLineNumber
                    ? (docId, lineNumber, existing.Count + count)
                    : (existing.SampleDocId, existing.SampleLineNumber, existing.Count + count);
            }
            else
            {
                corpusDocuments[ident] = (docId, lineNumber, count);
            }
        }

        // The sample displayed on the Home page is always the Manx text: only highlight it when Manx was searched
        var sampleDocIds = new HashSet<DocId>(corpusDocuments.Values.Select(x => x.SampleDocId));
        var highlightTokenSpans = IsManxField(spanQuery.Field)
            ? CollectHighlightTokenSpans(spanQuery, reader, sampleDocIds)
            : [];

        var samples = corpusDocuments.Select(kvp =>
        {
            var (sampleDocId, _, count) = kvp.Value;
            // only the sample line's stored document is loaded: one per corpus document
            var doc = reader.Document(sampleDocId);

            DateTime? startDate = doc.GetDateTime(DOCUMENT_CREATED_START);
            DateTime? endDate = doc.GetDateTime(DOCUMENT_CREATED_END);

            var sample = doc.RequireString(DOCUMENT_REAL_MANX);
            var name = doc.RequireString(DOCUMENT_NAME);

            return new QueryDocumentResult
            {
                Ident = kvp.Key,
                DocumentName = name,
                Sample = sample,
                SampleHighlights = ComputeHighlights(reader, sampleDocId, spanQuery.Field, sample,
                    highlightTokenSpans.GetValueOrDefault(sampleDocId)),
                EndDate = endDate,
                StartDate = startDate,
                Timed = TimedOrNull(reader, name, matchedLines?[kvp.Key]),
                Count = count
            };
        });

        return new ScanResult
        {
            NumberOfMatches = spanCollection.GetTotalCount() + referenceCounts.Values.Sum(),
            NumberOfSegments = distinctDocuments.Count,
            NumberOfDocuments = corpusDocuments.Count,
            DocumentResults = samples.ToList(),
        };
    }

    private static readonly HashSet<string> SubtitleStartOnly = [SUBTITLE_START];

    /// <summary>Whether the matched lines of a recording (the 🎥 name, as the
    /// Audio tag reads it) say when they are spoken. A transcript may carry its
    /// clock on some lines and not others (Skeealyn Vannin Disk 1 Track 2), so
    /// only *these* lines can answer for the word being jumpable-to. Null for
    /// print, and when the scan was not asked: reading every matched line of
    /// the Psalms to answer a question nobody asks of a book would be waste.</summary>
    private static bool? TimedOrNull(IndexReader reader, string name, List<DocId>? lines) =>
        lines != null && name.StartsWith("🎥")
            ? lines.Any(docId =>
                reader.Document(docId, SubtitleStartOnly).GetDouble(SUBTITLE_START) != null)
            : null;

    public List<DocumentLine> GetAllLines(Ident ident, bool getTranscript)
    {
        using var reader = UseReader();
        var searcher = new IndexSearcher(reader);

        return searcher.AllDocs(new TermQuery(new Term(DOCUMENT_IDENT, ident)), LineFields(getTranscript))
            .Select(x => ToDocumentLine(x, getTranscript))
            // document order: docID order is merge-dependent, so it cannot be relied on (#303)
            .OrderBy(x => x.CsvLineNumber)
            .ToList();
    }

    /// <summary>
    /// The non-blank lines of a document with a CsvLineNumber in [start, end], in document order:
    /// the first <paramref name="limit"/> of them, or the last if <paramref name="fromEnd"/>.
    /// Expands the context around a search result (#286).
    /// </summary>
    /// <returns>the lines, and the count in the range before the limit was applied: if it is no
    /// more than the limit, the range is exhausted</returns>
    public (List<DocumentLine> Lines, int TotalInRange) GetLines(Ident ident, int start, int end, int limit,
        bool fromEnd, bool getTranscript)
    {
        using var reader = UseReader();
        var searcher = new IndexSearcher(reader);

        var query = new BooleanQuery
        {
            { new TermQuery(new Term(DOCUMENT_IDENT, ident)), Occur.MUST },
            { NumericRangeQuery.NewInt32Range(DOCUMENT_LINE_NUMBER, start, end, minInclusive: true, maxInclusive: true), Occur.MUST },
        };

        var probeFields = new HashSet<string>
            { DOCUMENT_LINE_NUMBER, DOCUMENT_REAL_MANX, DOCUMENT_REAL_ENGLISH, DOCUMENT_REFERENCE };
        var candidates = searcher.AllDocIds(query)
            .Select(docId => (Doc: docId, Probe: searcher.Doc(docId, probeFields)))
            // reference-only rows (chapter headings) count as content
            .Where(x => !string.IsNullOrEmpty(x.Probe.GetString(DOCUMENT_REAL_MANX))
                        || !string.IsNullOrEmpty(x.Probe.GetString(DOCUMENT_REAL_ENGLISH))
                        || !string.IsNullOrEmpty(x.Probe.GetString(DOCUMENT_REFERENCE)))
            .OrderBy(x => x.Probe.GetInt32(DOCUMENT_LINE_NUMBER) ?? -1)
            .ToList();

        var window = fromEnd ? candidates.Skip(Math.Max(0, candidates.Count - limit)) : candidates.Take(limit);
        var fieldsToLoad = LineFields(getTranscript);
        var lines = window
            .Select(x => ToDocumentLine(searcher.Doc(x.Doc, fieldsToLoad), getTranscript))
            .ToList();
        return (lines, candidates.Count);
    }

    /// <summary>The first and last CsvLineNumber of a document; null if the document has no lines</summary>
    public (int First, int Last)? GetLineNumberRange(Ident ident)
    {
        using var reader = UseReader();
        var searcher = new IndexSearcher(reader);
        var fieldsToLoad = new HashSet<string> { DOCUMENT_LINE_NUMBER };

        int? first = null, last = null;
        foreach (var docId in searcher.AllDocIds(new TermQuery(new Term(DOCUMENT_IDENT, ident))))
        {
            var line = searcher.Doc(docId, fieldsToLoad).GetInt32(DOCUMENT_LINE_NUMBER);
            if (line == null) continue;
            if (first == null || line < first) first = line;
            if (last == null || line > last) last = line;
        }

        return first == null || last == null ? null : (first.Value, last.Value);
    }

    /// <summary>A line of some document carrying a canonical reference the
    /// verse-alignment lookup asked for: the same verse, in another translation.</summary>
    public sealed record VerseAlignmentLine(string DocumentIdent, string DocumentName, DateTime? Created,
        DocumentLine Line);

    /// <summary>
    /// Every line in the corpus whose canonical reference is one of
    /// <paramref name="keys"/> — or, when <paramref name="chapterPrefix"/> is given,
    /// any verse under it ("psalms.23." finds psalms.23.1..6). Exact terms on the
    /// untokenized <see cref="DOCUMENT_CANONICAL_REFERENCE"/> field.
    /// </summary>
    public List<VerseAlignmentLine> GetVerseAlignment(IReadOnlyCollection<string> keys, string? chapterPrefix)
    {
        var query = new BooleanQuery();
        foreach (var key in keys)
        {
            query.Add(new TermQuery(new Term(DOCUMENT_CANONICAL_REFERENCE, key)), Occur.SHOULD);
        }
        if (chapterPrefix != null)
        {
            query.Add(new PrefixQuery(new Term(DOCUMENT_CANONICAL_REFERENCE, chapterPrefix)), Occur.SHOULD);
        }

        var fieldsToLoad = LineFields(getTranscript: false);
        fieldsToLoad.Add(DOCUMENT_NAME);
        fieldsToLoad.Add(DOCUMENT_IDENT);
        fieldsToLoad.Add(DOCUMENT_CREATED_START);

        using var reader = UseReader();
        var searcher = new IndexSearcher(reader);
        return searcher.AllDocs(query, fieldsToLoad)
            .Select(x => new VerseAlignmentLine(
                x.GetString(DOCUMENT_IDENT) ?? "",
                x.GetString(DOCUMENT_NAME) ?? "",
                DateTime.TryParse(x.GetString(DOCUMENT_CREATED_START), out var created)
                    ? created
                    : null,
                ToDocumentLine(x, getTranscript: false)))
            .ToList();
    }

    private static HashSet<string> LineFields(bool getTranscript)
    {
        var fieldsToLoad = new HashSet<string> { DOCUMENT_REAL_MANX, DOCUMENT_REAL_ENGLISH, DOCUMENT_NOTES,
            DOCUMENT_PAGE,
            DOCUMENT_LINE_NUMBER, DOCUMENT_ORIGINAL_MANX, DOCUMENT_ORIGINAL_ENGLISH,
            DOCUMENT_SPEAKER, DOCUMENT_REFERENCE, DOCUMENT_CANONICAL_REFERENCE, DOCUMENT_LANGUAGE };
        if (getTranscript)
        {
            fieldsToLoad.Add(SUBTITLE_END);
            fieldsToLoad.Add(SUBTITLE_START);
        }

        return fieldsToLoad;
    }

    private static DocumentLine ToDocumentLine(Document document, bool getTranscript) => new()
    {
        Manx = document.GetString(DOCUMENT_REAL_MANX),
        English = document.GetString(DOCUMENT_REAL_ENGLISH),
        Page = document.GetPageAsInt(),
        Notes = document.GetString(DOCUMENT_NOTES),
        CsvLineNumber = document.GetInt32(DOCUMENT_LINE_NUMBER) ?? -1,
        ManxOriginal = document.GetString(DOCUMENT_ORIGINAL_MANX),
        EnglishOriginal = document.GetString(DOCUMENT_ORIGINAL_ENGLISH),
        SubStart = getTranscript ? document.GetDouble(SUBTITLE_START) : null,
        SubEnd = getTranscript ? document.GetDouble(SUBTITLE_END) : null,
        Speaker = document.GetString(DOCUMENT_SPEAKER),
        Reference = document.GetString(DOCUMENT_REFERENCE),
        CanonicalReference = document.GetString(DOCUMENT_CANONICAL_REFERENCE),
        Language = document.GetString(DOCUMENT_LANGUAGE)
    };

    public long CountManxTerms()
    {
        using var reader = UseReader();
        // the total occurrence count of all terms is a stored index statistic
        var terms = MultiFields.GetTerms(reader, DOCUMENT_MANX_GV);
        return terms == null ? 0 : Math.Max(0, terms.SumTotalTermFreq);
    }
        
    /// <summary>
    /// Index terms accepted by <paramref name="automaton"/>: e.g. 'lum-lane' for the
    /// hyphen-tolerant automaton of 'lumlane'. Used for 'did you mean' suggestions (#158).
    /// </summary>
    public List<string> GetMatchingTerms(string field, Automaton automaton, int limit)
    {
        var ret = new List<string>();

        using var reader = UseReader();
        var terms = MultiFields.GetTerms(reader, field);
        if (terms == null)
        {
            return ret;
        }

        var termsEnum = new CompiledAutomaton(automaton).GetTermsEnum(terms);
        while (ret.Count < limit && termsEnum.MoveNext())
        {
            ret.Add(termsEnum.Term.Utf8ToString());
        }

        return ret;
    }

    public List<(string, long)> GetTermFrequencyList()
    {
        var termList = new List<(string, long)>();

        using var reader = UseReader();
        var terms = MultiFields.GetTerms(reader, DOCUMENT_MANX_GV);
        if (terms == null)
        {
            // no documents were loaded: don't crash on startup
            return termList;
        }
        foreach (var term in terms)
        {
            termList.Add((term.Term.Utf8ToString(), term.TotalTermFreq));
        }

        return termList;
    }
}

public static class DocumentExtensions
{
    public static int? GetPageAsInt(this Document document)
    {
        var page = document.GetString(LuceneIndex.DOCUMENT_PAGE);
        if (page == null)
        {
            return null;
        }

        if (!int.TryParse(page, out var pageAsInt)) return null;
        return pageAsInt != 0 ? pageAsInt : null;
    }
}