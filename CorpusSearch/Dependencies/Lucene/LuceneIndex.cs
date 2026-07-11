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

    public const string SUBTITLE_START = "subtitle_start";
    public const string SUBTITLE_END = "subtitle_end";

    /// <summary>Whether the field preserves case (so its analyzer must not case-fold)</summary>
    internal static bool IsCasedField(string field) => field is DOCUMENT_CASED_MANX or DOCUMENT_CASED_ENGLISH;

    private static bool IsManxField(string field) => field is DOCUMENT_NORMALIZED_MANX or DOCUMENT_CASED_MANX;

    private static bool IsEnglishField(string field) => field is DOCUMENT_NORMALIZED_ENGLISH or DOCUMENT_CASED_ENGLISH;

    private IndexReader UseReader() => indexWriter.GetReader(applyAllDeletes: true);

    public static LuceneIndex GetInstance()
    {
        // Ensures index backward compatibility
        const LuceneVersion appLuceneVersion = LuceneVersion.LUCENE_48;

        // should be under a "using"
        var dir = new RAMDirectory();// FSDirectory.Open(indexPath);

        // Create an analyzer to process the text
        var analyzer = new ManxAnalyzer();

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
            var ident = document.GetField(DOCUMENT_IDENT)?.GetStringValue() ?? "";
            if (identPool.TryGetValue(ident, out var pooled))
            {
                ident = pooled;
            }
            else
            {
                identPool.Add(ident, ident);
            }
            idents[docId] = ident;
            lineNumbers[docId] = document.GetField(DOCUMENT_LINE_NUMBER)?.GetInt32Value() ?? -1;
        }

        return new DocumentLookup(idents, lineNumbers);
    }


    internal SearchResult Search(Ident ident, SpanQuery query, bool getTranscriptData)
    {
        using var reader = UseReader();
        var searcher = new IndexSearcher(reader);

        ISet<DocId> acceptDocs = GetDocsForIdent(searcher, ident);

        var rewritten = (SpanQuery)query.Rewrite(reader);
        var spanCollection = BuildSpanCollection(rewritten, reader, AcceptDocument);

        var matchedDocs = new HashSet<DocId>(spanCollection.DistinctDocumentIds());
        var highlightTokenSpans = CollectHighlightTokenSpans(rewritten, reader, matchedDocs);
        string searchedField = rewritten.Field;

        var docs = spanCollection.DistinctDocuments().Select(x =>
        {
            var (docId, countInDoc) = x;
            var document = searcher.Doc(docId);
            var manx = document.GetField(DOCUMENT_REAL_MANX).GetStringValue();
            var english = document.GetField(DOCUMENT_REAL_ENGLISH).GetStringValue();

            string? notes = document.GetField(DOCUMENT_NOTES)?.GetStringValue();
            int lineNumber = document.GetField(DOCUMENT_LINE_NUMBER)?.GetInt32Value() ?? -1;

            var highlights = ComputeHighlights(reader, docId, searchedField,
                IsEnglishField(searchedField) ? english : manx,
                highlightTokenSpans.GetValueOrDefault(docId));

            return new DocumentLine
            {
                English = english,
                Manx = manx,
                Page = document.GetPageAsInt(),
                Notes = notes,
                CsvLineNumber = lineNumber,
                ManxHighlights = IsManxField(searchedField) ? highlights : null,
                EnglishHighlights = IsEnglishField(searchedField) ? highlights : null,
                ManxOriginal = document.GetField(DOCUMENT_ORIGINAL_MANX)?.GetStringValue(),
                EnglishOriginal = document.GetField(DOCUMENT_ORIGINAL_ENGLISH)?.GetStringValue(),
                SubStart = getTranscriptData ? document.GetField(SUBTITLE_START)?.GetDoubleValue() : null,
                SubEnd = getTranscriptData ? document.GetField(SUBTITLE_END)?.GetDoubleValue() : null,
                Speaker = getTranscriptData ? document.GetField(DOCUMENT_SPEAKER)?.GetStringValue() : null,
                MatchesInLine = countInDoc
            };
        // document order: docID order is merge-dependent, so it cannot be relied on (#303)
        }).OrderBy(x => x.CsvLineNumber).ToList();

        return new SearchResult
        {
            Lines = docs,
            TotalMatches = spanCollection.GetTotalCount(),
        };

        bool AcceptDocument(AtomicReaderContext leaf, Spans spans)
        {
            // TODO PERF: Inefficient - should be able to use GetSpans(?, acceptDocs, ?) - need to read documents to understand it
            var docId = leaf.DocBase + spans.Doc;
            return acceptDocs.Contains(docId);
        }
    }

    private static EmptySpanCollection BuildSpanCollection(SpanQuery rewritten, IndexReader reader, Func<AtomicReaderContext,Spans, bool> acceptDocument)
    {
        EmptySpanCollection spanCollection = new();
        foreach (var leaf in reader.Leaves)
        {
            var dict = new Dictionary<Term, TermContext>();
            var spans = rewritten.GetSpans(leaf, null, dict);

            while (spans.MoveNext())
            {
                if (!acceptDocument(leaf, spans))
                {
                    continue;
                }

                spanCollection.Increment(leaf.DocBase + spans.Doc);
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
            foreach (var leaf in reader.Leaves)
            {
                var dict = new Dictionary<Term, TermContext>();
                var spans = leafQuery.GetSpans(leaf, null, dict);

                while (spans.MoveNext())
                {
                    DocId docId = leaf.DocBase + spans.Doc;
                    if (!matchedDocs.Contains(docId))
                    {
                        continue;
                    }
                    if (!result.TryGetValue(docId, out var spanList))
                    {
                        result[docId] = spanList = [];
                    }
                    spanList.Add((spans.Start, spans.End));
                }
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
        var ret = searcher.Search(query, int.MaxValue).ScoreDocs.Select(x => x.Doc);
        return new HashSet<int>(ret);
    }

    public ScanResult Scan(SpanQuery query)
    {
        using var reader = UseReader();
        // tests scan without compacting; production builds the lookup in Compact()
        var lookup = _documentLookup ??= BuildDocumentLookup(reader);

        var spanQuery = (SpanQuery)query.Rewrite(reader);
        var spanCollection = BuildSpanCollection(spanQuery, reader, (_, _) => true);
        var distinctDocuments = spanCollection.DistinctDocumentIds().ToList();

        // Group the matched lines into distinct Corpus Documents via the docId lookup. The
        // sample is the first line by line number: docID order is merge-dependent, so it
        // cannot be relied on (#303)
        var corpusDocuments = new Dictionary<Ident, (DocId SampleDocId, int SampleLineNumber, int Count)>();
        foreach (var docId in distinctDocuments)
        {
            var (ident, lineNumber) = lookup.Get(docId);
            var count = spanCollection.GetCount(docId);
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

            string? maybeStartDate = doc.GetField(DOCUMENT_CREATED_START)?.GetStringValue();
            string? maybeEndDate = doc.GetField(DOCUMENT_CREATED_END)?.GetStringValue();

            DateTime? startDate = maybeStartDate != null ? DateTime.Parse(maybeStartDate) : null;
            DateTime? endDate = maybeEndDate != null ? DateTime.Parse(maybeEndDate) : null;

            var sample = doc.GetField(DOCUMENT_REAL_MANX).GetStringValue();

            return new QueryDocumentResult
            {
                Ident = kvp.Key,
                DocumentName = doc.GetField(DOCUMENT_NAME).GetStringValue(),
                Sample = sample,
                SampleHighlights = ComputeHighlights(reader, sampleDocId, spanQuery.Field, sample,
                    highlightTokenSpans.GetValueOrDefault(sampleDocId)),
                EndDate = endDate,
                StartDate = startDate,
                Count = count
            };
        });

        return new ScanResult
        {
            NumberOfMatches = spanCollection.GetTotalCount(),
            NumberOfSegments = distinctDocuments.Count,
            NumberOfDocuments = corpusDocuments.Count,
            DocumentResults = samples.ToList(),
        };
    }

    public List<DocumentLine> GetAllLines(Ident ident, bool getTranscript)
    {
        using var reader = UseReader();
        var searcher = new IndexSearcher(reader);

        TopDocs docs = searcher.Search(new TermQuery(new Term(DOCUMENT_IDENT, ident)), Int32.MaxValue);

        var fieldsToLoad = LineFields(getTranscript);

        return docs.ScoreDocs
            .Select(x => searcher.Doc(x.Doc, fieldsToLoad))
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

        var probeFields = new HashSet<string> { DOCUMENT_LINE_NUMBER, DOCUMENT_REAL_MANX, DOCUMENT_REAL_ENGLISH };
        var candidates = searcher.Search(query, int.MaxValue).ScoreDocs
            .Select(x => (x.Doc, Probe: searcher.Doc(x.Doc, probeFields)))
            .Where(x => !string.IsNullOrEmpty(x.Probe.GetField(DOCUMENT_REAL_MANX)?.GetStringValue())
                        || !string.IsNullOrEmpty(x.Probe.GetField(DOCUMENT_REAL_ENGLISH)?.GetStringValue()))
            .OrderBy(x => x.Probe.GetField(DOCUMENT_LINE_NUMBER)?.GetInt32Value() ?? -1)
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
        foreach (var scoreDoc in searcher.Search(new TermQuery(new Term(DOCUMENT_IDENT, ident)), int.MaxValue).ScoreDocs)
        {
            var line = searcher.Doc(scoreDoc.Doc, fieldsToLoad).GetField(DOCUMENT_LINE_NUMBER)?.GetInt32Value();
            if (line == null) continue;
            if (first == null || line < first) first = line;
            if (last == null || line > last) last = line;
        }

        return first == null || last == null ? null : (first.Value, last.Value);
    }

    private static HashSet<string> LineFields(bool getTranscript)
    {
        var fieldsToLoad = new HashSet<string> { DOCUMENT_REAL_MANX, DOCUMENT_REAL_ENGLISH, DOCUMENT_NOTES,
            DOCUMENT_PAGE,
            DOCUMENT_LINE_NUMBER, DOCUMENT_ORIGINAL_MANX, DOCUMENT_ORIGINAL_ENGLISH };
        if (getTranscript)
        {
            fieldsToLoad.Add(SUBTITLE_END);
            fieldsToLoad.Add(SUBTITLE_START);
            fieldsToLoad.Add(DOCUMENT_SPEAKER);
        }

        return fieldsToLoad;
    }

    private static DocumentLine ToDocumentLine(Document document, bool getTranscript) => new()
    {
        Manx = document.GetField(DOCUMENT_REAL_MANX)?.GetStringValue(),
        English = document.GetField(DOCUMENT_REAL_ENGLISH)?.GetStringValue(),
        Page = document.GetPageAsInt(),
        Notes = document.GetField(DOCUMENT_NOTES)?.GetStringValue(),
        CsvLineNumber = document.GetField(DOCUMENT_LINE_NUMBER)?.GetInt32Value() ?? -1,
        ManxOriginal = document.GetField(DOCUMENT_ORIGINAL_MANX)?.GetStringValue(),
        EnglishOriginal = document.GetField(DOCUMENT_ORIGINAL_ENGLISH)?.GetStringValue(),
        SubStart = getTranscript ? document.GetField(SUBTITLE_START)?.GetDoubleValue() : null,
        SubEnd = getTranscript ? document.GetField(SUBTITLE_END)?.GetDoubleValue() : null,
        Speaker = getTranscript ? document.GetField(DOCUMENT_SPEAKER)?.GetStringValue() : null
    };

    public long CountManxTerms()
    {
        using var reader = UseReader();
        // the total occurrence count of all terms is a stored index statistic
        var terms = MultiFields.GetTerms(reader, DOCUMENT_NORMALIZED_MANX);
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
        var terms = MultiFields.GetTerms(reader, DOCUMENT_NORMALIZED_MANX);
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
        var page = document.GetField(LuceneIndex.DOCUMENT_PAGE)?.GetStringValue();
        if (page == null)
        {
            return null;
        }

        if (!int.TryParse(page, out var pageAsInt)) return null;
        return pageAsInt != 0 ? pageAsInt : null;
    }
}