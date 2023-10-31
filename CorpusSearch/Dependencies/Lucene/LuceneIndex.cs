using CorpusSearch.Dependencies.Lucene;
using CorpusSearch.Model;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Search.Spans;
using Lucene.Net.Store;
using Lucene.Net.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CorpusSearch;
using Document = Lucene.Net.Documents.Document;
using LuceneDocument = Lucene.Net.Documents.Document;

namespace CorpusSearch
{
    public class LuceneIndex
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
        public const string DOCUMENT_CREATED_START = "created_start";
        public const string DOCUMENT_CREATED_END = "created_end";

        public const string SUBTITLE_START = "subtitle_start";
        public const string SUBTITLE_END = "subtitle_end";
        
        private IndexWriter indexWriter;

        public LuceneIndex(IndexWriter indexWriter)
        {
            this.indexWriter = indexWriter;
        }
        
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
            var indexConfig = new IndexWriterConfig(appLuceneVersion, analyzer);
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

            foreach (var line in data)
            {
                var doc = new LuceneDocument
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

                };

                void AddField(string key, string value)
                {
                    // ArgumentNullException if the value is null
                    if (value == null) { return; }
                    doc.Add(new StringField(key, value, Field.Store.YES));
                }

                AddField(DOCUMENT_ORIGINAL_ENGLISH, line.EnglishOriginal);
                AddField(DOCUMENT_ORIGINAL_MANX, line.ManxOriginal);
                AddField(DOCUMENT_CREATED_START, document.CreatedCircaStart?.ToString());
                AddField(DOCUMENT_CREATED_END, document.CreatedCircaEnd?.ToString());
                AddField(DOCUMENT_NOTES, line.Notes);
                AddField(DOCUMENT_PAGE, line.Page.ToString());

                if (line.SubStart != null)
                {
                    doc.Add(new DoubleField(SUBTITLE_START, line.SubStart.Value, Field.Store.YES));
                }
                if (line.SubEnd != null)
                {
                    doc.Add(new DoubleField(SUBTITLE_END, line.SubEnd.Value, Field.Store.YES));
                }
                
                indexWriter.AddDocument(doc);
            }

            indexWriter.Flush(triggerMerge: false, applyAllDeletes: false);
        }

        public void Compact()
        {
            indexWriter.ForceMerge(1);
        }

        public ScanResult Scan(SpanQuery query)
        {
            using var reader = UseReader();
            return Scan(reader, query);
        }


        internal SearchResult Search(string ident, SpanQuery query, bool getTranscriptData)
        {
            using var reader = UseReader();
            var searcher = new IndexSearcher(reader);

            ISet<int> acceptDocs = GetDocsForIdent(searcher, ident);

            bool AcceptDocument(AtomicReaderContext leaf, Spans spans)
            {
                // TODO PERF: Inefficient - should be able to use GetSpans(?, acceptDocs, ?) - need to read documents to understand it
                var docId = leaf.DocBase + spans.Doc;
                return acceptDocs.Contains(docId);
            }
            var spanCollection = BuildSpanCollection(query, reader, AcceptDocument);

            var docs = spanCollection.DistinctDocuments().Select(x =>
            {
                var (docId, countInDoc) = x;
                var document = searcher.Doc(docId);
                var manx = document.GetField(DOCUMENT_REAL_MANX).GetStringValue();
                var english = document.GetField(DOCUMENT_REAL_ENGLISH).GetStringValue();

                string notes = document.GetField(DOCUMENT_NOTES)?.GetStringValue();
                int lineNumber = document.GetField(DOCUMENT_LINE_NUMBER)?.GetInt32Value() ?? -1;

                return new DocumentLine
                {
                    English = english,
                    Manx = manx,
                    Page = document.GetPageAsInt(),
                    Notes = notes,
                    CsvLineNumber = lineNumber,
                    ManxOriginal = document.GetField(DOCUMENT_ORIGINAL_MANX)?.GetStringValue(),
                    EnglishOriginal = document.GetField(DOCUMENT_ORIGINAL_ENGLISH)?.GetStringValue(),
                    SubStart = getTranscriptData ? document.GetField(SUBTITLE_START)?.GetDoubleValue() : null,
                    SubEnd = getTranscriptData ? document.GetField(SUBTITLE_END)?.GetDoubleValue() : null,
                    MatchesInLine = countInDoc
                };
            }).ToList();

            return new SearchResult
            {
                Lines = docs,
                TotalMatches = spanCollection.GetTotalCount(),
            };
        }

        private static EmptySpanCollection BuildSpanCollection(Query query, IndexReader reader, Func<AtomicReaderContext,Spans, bool> acceptDocument)
        {
            var spanQuery = (SpanQuery)query.Rewrite(reader);
            EmptySpanCollection spanCollection = new();
            foreach (var leaf in reader.Leaves)
            {
                var dict = new Dictionary<Term, TermContext>();
                var spans = spanQuery.GetSpans(leaf, null, dict);

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

        private ISet<int> GetDocsForIdent(IndexSearcher searcher, string ident)
        {
            var query = new TermQuery(new Term(DOCUMENT_IDENT, ident));

            // TODO: See if IBits will help here? 
            var ret = searcher.Search(query, int.MaxValue).ScoreDocs.Select(x => x.Doc);
            return new HashSet<int>(ret);
        }

        public static ScanResult Scan(IndexReader reader, SpanQuery query)
        {
            var spanQuery = (SpanQuery)query.Rewrite(reader);
            var spanCollection = BuildSpanCollection(spanQuery, reader, (_, _) => true);
            var distinctDocuments = spanCollection.DistinctDocumentIds().ToList();

            var documentMapping = distinctDocuments.ToDictionary(x => x, reader.Document);

            // A collection of Lucene documents, each refers to the first sample in a distinct Corpus Document
            var corpusDocuments = documentMapping.DistinctBy(x => x.Value.GetField(DOCUMENT_IDENT)?.GetStringValue()).ToList();

            // key: ident of corpus document, value: docIds of each segment
            var corpusDocumentMapping = documentMapping.ToLookup(x => x.Value.GetField(DOCUMENT_IDENT).GetStringValue(), x => x.Key);

            var samples = corpusDocuments.Select(kvp =>
            {
                var doc = kvp.Value;

                string maybeStartDate = doc.GetField(DOCUMENT_CREATED_START)?.GetStringValue();
                string maybeEndDate = doc.GetField(DOCUMENT_CREATED_END)?.GetStringValue();

                DateTime? startDate = maybeStartDate != null ? DateTime.Parse(maybeStartDate) : null;
                DateTime? endDate = maybeEndDate != null ? DateTime.Parse(maybeEndDate) : null;

                var ident = doc.GetField(DOCUMENT_IDENT).GetStringValue();

                return new QueryDocumentResult
                {
                    Ident = ident,
                    DocumentName = doc.GetField(DOCUMENT_NAME).GetStringValue(),
                    Sample = doc.GetField(DOCUMENT_REAL_MANX).GetStringValue(),
                    EndDate = endDate,
                    StartDate = startDate,
                    Count = corpusDocumentMapping[ident].Sum(docIdForIdent => spanCollection.GetCount(docIdForIdent))
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

        public List<DocumentLine> GetAllLines(string ident, bool getTranscript)
        {
            using var reader = UseReader();
            var searcher = new IndexSearcher(reader);
            
            TopDocs docs = searcher.Search(new TermQuery(new Term(DOCUMENT_IDENT, ident)), Int32.MaxValue);

            var fieldsToLoad = new HashSet<string> { DOCUMENT_REAL_MANX, DOCUMENT_REAL_ENGLISH, DOCUMENT_NOTES,
                DOCUMENT_PAGE,
                DOCUMENT_LINE_NUMBER, DOCUMENT_ORIGINAL_MANX, DOCUMENT_ORIGINAL_ENGLISH };
            if (getTranscript)
            {
                fieldsToLoad.Add(SUBTITLE_END);
                fieldsToLoad.Add(SUBTITLE_START);
            }
            
            return docs.ScoreDocs
                .Select(x => searcher.Doc(x.Doc, fieldsToLoad))
                .Select(x => new DocumentLine
            {
                Manx = x.GetField(DOCUMENT_REAL_MANX)?.GetStringValue(),
                English = x.GetField(DOCUMENT_REAL_ENGLISH)?.GetStringValue(),
                Page = x.GetPageAsInt(),
                Notes = x.GetField(DOCUMENT_NOTES)?.GetStringValue(),
                CsvLineNumber = x.GetField(DOCUMENT_LINE_NUMBER)?.GetInt32Value() ?? -1,
                ManxOriginal = x.GetField(DOCUMENT_ORIGINAL_MANX)?.GetStringValue(),
                EnglishOriginal = x.GetField(DOCUMENT_ORIGINAL_ENGLISH)?.GetStringValue(),
                SubStart = getTranscript ? x.GetField(SUBTITLE_START)?.GetDoubleValue() : null,
                SubEnd = getTranscript ? x.GetField(SUBTITLE_END)?.GetDoubleValue() : null
            }).ToList();
        }

        public long CountManxTerms()
        {
            // TODO: Probably inefficient
            using var reader = UseReader();
            var query = new SpanMultiTermQueryWrapper<ExtendedWildcardQuery>(new ExtendedWildcardQuery(new Term( DOCUMENT_NORMALIZED_MANX, "*"))); 
            int totalMatches = 0;
            var spanQuery = (SpanQuery)query.Rewrite(reader);
            foreach (var leaf in reader.Leaves)
            {
                var dict = new Dictionary<Term, TermContext>();
                var spans = spanQuery.GetSpans(leaf, null, dict);

                while (spans.MoveNext())
                {
                    totalMatches++;
                }
            }

            return totalMatches;
        }
        
        public List<(string, long)> GetTermFrequencyList()
        {
            var termList = new List<(string, long)>();
            
            using var reader = UseReader();
            var terms = MultiFields.GetTerms(reader, DOCUMENT_NORMALIZED_MANX);
            foreach (var term in terms)
            {
                termList.Add((term.Term.Utf8ToString(), term.TotalTermFreq));
            }

            return termList;
        }
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
