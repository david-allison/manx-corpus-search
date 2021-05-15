using CorpusSearch.Dependencies.Lucene;
using CorpusSearch.Model;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Search.Spans;
using Lucene.Net.Store;
using Lucene.Net.Util;
using MoreLinq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
        public const string DOCUMENT_NORMALIZED_ENGLISH = "english";
        public const string DOCUMENT_REAL_ENGLISH = "real_english";
        public const string DOCUMENT_CREATED_START = "created_start";
        public const string DOCUMENT_CREATED_END = "created_end";

        private IndexWriter indexWriter;

        public LuceneIndex(IndexWriter indexWriter)
        {
            this.indexWriter = indexWriter;
        }

        public static LuceneIndex GetInstance()
        {
            // Ensures index backward compatibility
            const LuceneVersion AppLuceneVersion = LuceneVersion.LUCENE_48;

            // Construct a machine-independent path for the index
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            var indexPath = Path.Combine(basePath, "luceneindex");

            // should be under a "using"
            var dir = new RAMDirectory();// FSDirectory.Open(indexPath);



            // Create an analyzer to process the text
            var analyzer = new ManxAnalyzer();

            // Create an index writer
            var indexConfig = new IndexWriterConfig(AppLuceneVersion, analyzer);
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
                
                AddField(DOCUMENT_CREATED_START, document.CreatedCircaStart?.ToString());
                AddField(DOCUMENT_CREATED_END, document.CreatedCircaEnd?.ToString());
                AddField(DOCUMENT_NOTES, line.Notes);
                AddField(DOCUMENT_PAGE, line.Page.ToString());

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
            using var reader = indexWriter.GetReader(applyAllDeletes: true);
            return Scan(reader, query);
        }


        internal SearchResult Search(string ident, SpanQuery query)
        {
            // TODO: Copied from Scan
            using var reader = indexWriter.GetReader(applyAllDeletes: true);
            var searcher = new IndexSearcher(reader);

            ISet<int> acceptDocs = GetDocsForIdent(searcher, ident);

            int totalMatches = 0;
            var spanQuery = (SpanQuery)query.Rewrite(reader);
            SpanCollection spanCollection = new();
            foreach (var leaf in reader.Leaves)
            {
                var dict = new Dictionary<Term, TermContext>();
                var spans = spanQuery.GetSpans(leaf, null, dict);

                while (spans.MoveNext())
                {
                    var docId = leaf.DocBase + spans.Doc;
                    // TODO PERF: Inefficient - should be able to use GetSpans(?, acceptDocs, ?) - need to read documents to understand it
                    if (!acceptDocs.Contains(docId))
                    {
                        continue;
                    }
                    spanCollection.Add(leaf.DocBase + spans.Doc, new Span(spans.Start, spans.End));
                    totalMatches++;
                }
            }

            var docs = spanCollection.DistinctDocuments().Select(x =>
            {
                var document = searcher.Doc(x);
                var manx = document.GetField(DOCUMENT_REAL_MANX).GetStringValue();
                var english = document.GetField(DOCUMENT_REAL_ENGLISH).GetStringValue();

                int pageAsInt = 0;
                int.TryParse(document.GetField(DOCUMENT_PAGE)?.GetStringValue(), out pageAsInt);


                string notes = document.GetField(DOCUMENT_NOTES)?.GetStringValue();
                int lineNumber = document.GetField(DOCUMENT_LINE_NUMBER)?.GetInt32Value() ?? -1;

                return new DocumentLine
                {
                    English = english,
                    Manx = manx,
                    Page = pageAsInt != 0 ? pageAsInt : null,
                    Notes = notes,
                    CsvLineNumber = lineNumber,
                };
            }).ToList();

            return new SearchResult
            {
                Lines = docs,
                TotalMatches = spanCollection.GetTotalCount(),
            };
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
            var searcher = new IndexSearcher(reader);

            int totalMatches = 0;
            var spanQuery = (SpanQuery)query.Rewrite(reader);
            SpanCollection spanCollection = new();
            foreach (var leaf in reader.Leaves)
            {
                var dict = new Dictionary<Term, TermContext>();
                var spans = spanQuery.GetSpans(leaf, null, dict);

                while (spans.MoveNext())
                {
                    spanCollection.Add(leaf.DocBase + spans.Doc, new Span(spans.Start, spans.End));
                    totalMatches++;
                }
            }

            var distinctDocuments = spanCollection.DistinctDocuments();
            var luceneDocumentCount = distinctDocuments.Count();

            var documentMapping = distinctDocuments.ToDictionary(x => x, x => reader.Document(x));

            // A collection of Lucene documents, each refers to the first sample in a distinct Corpus Document
            var corpusDocuments = documentMapping.DistinctBy(x => x.Value.GetField(DOCUMENT_IDENT)?.GetStringValue());

            // key: ident of corpus document, value: docIds of each segment
            var corpusDocumentMapping = documentMapping.ToLookup(x => x.Value.GetField(DOCUMENT_IDENT).GetStringValue(), x => x.Key);

            var samples = corpusDocuments.Select(kvp =>
            {
                var doc = kvp.Value;
                int docId = kvp.Key;

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

            var corpusDocumentCount = corpusDocuments.Count();

            return new ScanResult
            {
                NumberOfMatches = totalMatches,
                NumberOfSegments = luceneDocumentCount,
                NumberOfDocuments = corpusDocumentCount,
                DocumentResults = samples.ToList(),
            };
        }

        internal IndexWriter GetWriter()
        {
            return this.indexWriter;
        }
    }


}
