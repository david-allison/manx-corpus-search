using Codex_API.Dependencies.csly;
using Codex_API.Model;
using Codex_API.Test.TestUtils;
using Lucene.Net.Index;
using NUnit.Framework;
using System.Linq;
using static Codex_API.LuceneIndex;

namespace Codex_API.Test
{
    [TestFixture]
    public class QueryBase : IDocumentStorage
    {
        protected LuceneIndex luceneIndex;
        protected SearchParser parser;

        [SetUp]
        public void setUp()
        {
            luceneIndex = LuceneIndex.GetInstance();
            parser = SearchParser.GetParser();
        }

        public void AddDocument(string name, params Line[] data)
        {
            luceneIndex.Add(new TestDocument(name), data.Select(x => new Startup.DocumentLine() { English = x.English, Manx = x.Manx }));
        }


        private class TestDocument : IDocument
        {
            public TestDocument(string name)
            {
                Name = name;
            }

            public string Name { get; set; }
            public string Ident => Name;
        }
    }
}
