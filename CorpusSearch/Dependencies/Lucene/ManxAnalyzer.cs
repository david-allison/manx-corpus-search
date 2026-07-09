using Lucene.Net.Analysis;
using Lucene.Net.Util;
using System.IO;

namespace CorpusSearch.Dependencies.Lucene;

public class ManxAnalyzer : Analyzer
{
    // the cased fields need their own components: the default (global) strategy would reuse
    // one tokenizer - with one preserveCase setting - for every field
    public ManxAnalyzer() : base(PER_FIELD_REUSE_STRATEGY)
    {
    }

    protected override TokenStreamComponents CreateComponents(string fieldName, TextReader reader)
    {
        bool preserveCase = LuceneIndex.IsCasedField(fieldName);
        Tokenizer tokenizer = new ManxTokenizer(LuceneVersion.LUCENE_48, reader, preserveCase);

        return new TokenStreamComponents(tokenizer, new ManxTokenFilter(tokenizer, preserveCase));
    }
}