using CorpusSearch.Dependencies.csly;
using CorpusSearch.Model;
using CorpusSearch.Test.TestUtils;
using NUnit.Framework;
using System;
using System.Linq;

namespace CorpusSearch.Test
{
    [TestFixture]
    public class QueryBase : IDocumentStorage
    {
        protected LuceneIndex luceneIndex;
        protected SearchParser parser;
        protected DateTime DOC_DATE = new DateTime(2212, 10, 10);

        [SetUp]
        public void setUp()
        {
            luceneIndex = LuceneIndex.GetInstance();
            parser = SearchParser.GetParser();
        }

        public void AddDocument(string name, params Line[] data)
        {
            var doc = new TestDocument(name, DOC_DATE);
            AddDocument(doc, data);
        }

        protected void AddDocument(IDocument doc, params Line[] data)
        {
            luceneIndex.Add(doc, data.Select(x => new DocumentLine() { English = x.English, Manx = x.Manx }));
        }

        protected class TestDocument : IDocument
        {
            public TestDocument(string name, DateTime? dateTime)
            {
                Name = name;
                this.date = dateTime;
            }


            public string Name { get; set; }

            private readonly DateTime? date;

            public string Ident => Name;

            public DateTime? CreatedCircaStart => date;
            public DateTime? CreatedCircaEnd => date;

            public string ExternalPdfLink => null;

            public string GitHubRepo => null;

            public string RelativeCsvPath => null;

            public string Notes => null;

            public string Source => null;
        }
    }
}
