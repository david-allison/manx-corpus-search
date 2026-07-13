using Lucene.Net.Analysis;
using Lucene.Net.Util;
using System.IO;

namespace CorpusSearch.Dependencies.Lucene;

public class ManxAnalyzer : Analyzer
{
    private readonly LemmaResolver lemmaResolver;

    // the cased fields need their own components: the default (global) strategy would reuse
    // one tokenizer - with one preserveCase setting - for every field
    public ManxAnalyzer(LemmaResolver? lemmaResolver = null) : base(PER_FIELD_REUSE_STRATEGY)
    {
        this.lemmaResolver = lemmaResolver ?? LemmaResolver.Instance;
    }

    protected override TokenStreamComponents CreateComponents(string fieldName, TextReader reader)
    {
        bool preserveCase = LuceneIndex.IsCasedField(fieldName);
        Tokenizer tokenizer = new ManxTokenizer(LuceneVersion.LUCENE_48, reader, preserveCase);

        TokenStream filter = new ManxTokenFilter(tokenizer, preserveCase);
        if (LuceneIndex.IsLemmaField(fieldName))
        {
            filter = new LemmaTokenFilter(filter, LemmaTable.Instance, lemmaResolver);
        }
        return new TokenStreamComponents(tokenizer, filter);
    }
}