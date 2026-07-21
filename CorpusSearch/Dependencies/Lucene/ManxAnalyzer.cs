using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Core;
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
        if (LuceneIndex.IsReferenceField(fieldName))
        {
            // references keep their digits ("Psalm 23", "2.16"): letter+digit
            // tokens, lowercased - ManxTokenizer would drop the numbers
            var referenceTokenizer = new ReferenceTokenizer(LuceneVersion.LUCENE_48, reader);
            return new TokenStreamComponents(referenceTokenizer,
                new LowerCaseFilter(LuceneVersion.LUCENE_48, referenceTokenizer));
        }

        bool preserveCase = LuceneIndex.IsCasedField(fieldName);
        Tokenizer tokenizer = new ManxTokenizer(LuceneVersion.LUCENE_48, reader, preserveCase);

        TokenStream filter = new ManxTokenFilter(tokenizer, preserveCase);
        if (LuceneIndex.IsStatsField(fieldName))
        {
            // numbers and ?-markers stay searchable in the search fields, but
            // are not Manx words: the statistics stream drops them
            filter = new NonWordTokenFilter(filter);
        }
        if (LuceneIndex.IsLemmaField(fieldName) || LuceneIndex.IsSureLemmaField(fieldName))
        {
            filter = new LemmaTokenFilter(filter, LemmaTable.Instance, lemmaResolver,
                sureOnly: LuceneIndex.IsSureLemmaField(fieldName));
        }
        return new TokenStreamComponents(tokenizer, filter);
    }
}