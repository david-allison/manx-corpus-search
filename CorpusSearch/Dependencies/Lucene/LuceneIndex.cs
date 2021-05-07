using Codex_API.Dependencies.Lucene;
using Codex_API.Model;
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

namespace Codex_API
{
    public class LuceneIndex
    {
        private const string DOCUMENT_NAME = "name";
        private const string DOCUMENT_IDENT = "ident";

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

        public void Add(IDocument document, IEnumerable<Startup.DocumentLine> data)
        {
            var fieldType = new FieldType(TextField.TYPE_STORED);
            fieldType.StoreTermVectorPositions = true;
            fieldType.StoreTermVectors = true;
            fieldType.StoreTermVectorOffsets = true;

            foreach (var line in data)
            {
                var doc = new Document
                {
                    // StringField indexes but doesn't tokenize
                    new StringField(DOCUMENT_NAME, document.Name, Field.Store.YES),
                    new StringField(DOCUMENT_IDENT, document.Ident, Field.Store.YES),
                    new Field("manx", line.NormalizedManx , fieldType)
                };

                indexWriter.AddDocument(doc);
            }

            indexWriter.Flush(triggerMerge: false, applyAllDeletes: false);
        }

        public ScanResult Scan(SpanQuery query)
        {
            using var reader = indexWriter.GetReader(applyAllDeletes: true);
            return Scan(reader, query);
        }

        public static ScanResult Scan(IndexReader reader, SpanQuery query)
        {
            var searcher = new IndexSearcher(reader);
            var hits = searcher.Search(query, int.MaxValue).ScoreDocs;


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

            var distinctDocuments = hits.Select(x => x.Doc).Distinct();
            var luceneDocumentCount = distinctDocuments.Count();

            var corpusDocumentCount = distinctDocuments.Select(x => reader.Document(x)).Select(x => x.GetField(DOCUMENT_NAME)).Distinct().Count();

            return new ScanResult
            {
                NumberOfMatches = totalMatches,
                NumberOfSegments = luceneDocumentCount,
                NumberOfDocuments = corpusDocumentCount,
            };
        }

        internal IndexWriter GetWriter()
        {
            return this.indexWriter;
        }
    }


}
