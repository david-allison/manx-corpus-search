using Lucene.Net.Analysis;
using Lucene.Net.Util;
using System.IO;

namespace CorpusSearch.Dependencies.Lucene
{
    public class ManxAnalyzer : Analyzer
    {
        protected override TokenStreamComponents CreateComponents(string fieldName, TextReader reader)
        {
            Tokenizer tokenizer = new ManxTokenizer(LuceneVersion.LUCENE_48, reader);

            new ManxTokenFilter(tokenizer);

            return new TokenStreamComponents(tokenizer, new ManxTokenFilter(tokenizer));
        }
    }
}
