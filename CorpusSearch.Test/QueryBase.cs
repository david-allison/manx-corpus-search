using CorpusSearch.Dependencies.csly;
using CorpusSearch.Model;
using CorpusSearch.Test.TestUtils;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using CorpusSearch.Dependencies.Lucene;

namespace CorpusSearch.Test;

[TestFixture]
public class QueryBase : IDocumentStorage
{
    protected LuceneIndex luceneIndex;
    protected SearchParser parser;
    protected DateTime DOC_DATE = new(2212, 10, 10);

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

    protected class TestDocument(string name, DateTime? dateTime) : IDocument
    {
        public string Name { get; set; } = name;

        public string Ident => Name;

        public DateTime? CreatedCircaStart => dateTime;
        public DateTime? CreatedCircaEnd => dateTime;

        public string ExternalPdfLink => null;
        public string GoogleBooksId => null;

        public string GitHubRepo => null;

        public string RelativeCsvPath => null;

        public string Notes => null;

        public string Source => null;
        public string Original => "Manx";

        public IDictionary<string, object> GetAllExtensionData()
        {
            return new Dictionary<string, object>();
        }
    }
}