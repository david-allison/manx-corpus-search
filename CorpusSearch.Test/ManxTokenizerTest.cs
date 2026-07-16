using System.Collections.Generic;
using System.IO;
using System.Linq;
using CorpusSearch.Dependencies.Lucene;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Util;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>
/// The tokenizer's typography contract: the raw texts mix typewriter (') and
/// typographic (’) apostrophes, and both must stay word-internal and token as
/// ' — "s’liak" split at the ’ painted its halves unknown in the coverage
/// view, though the tables know s'liak.
/// </summary>
[TestFixture]
public class ManxTokenizerTest
{
    private record Token(string Term, int Start, int End);

    private static List<Token> Tokens(string text, bool preserveCase = false)
    {
        using var stream = new ManxTokenizer(LuceneVersion.LUCENE_48, new StringReader(text), preserveCase);
        var term = stream.GetAttribute<ICharTermAttribute>();
        var offset = stream.GetAttribute<IOffsetAttribute>();

        var tokens = new List<Token>();
        stream.Reset();
        while (stream.IncrementToken())
        {
            tokens.Add(new Token(term.ToString(), offset.StartOffset, offset.EndOffset));
        }
        stream.End();
        return tokens;
    }

    [Test]
    public void TypographicApostrophesAreWordInternalAndTokenAsTypewriter()
    {
        var tokens = Tokens("Son s’liak lhiam");

        Assert.That(tokens.Select(x => x.Term), Is.EqualTo(new[] { "son", "s'liak", "lhiam" }));
        // offsets stay in the raw text: the client highlights the original line
        Assert.That(tokens[1].Start, Is.EqualTo(4));
        Assert.That(tokens[1].End, Is.EqualTo(10));
    }

    [Test]
    public void EveryTypographicSingleQuoteFoldsLikeTheMapper()
    {
        // the same set NormalizationMapper folds for the indexed fields
        Assert.That(Tokens("s‘a s’a s‛a s′a").Select(x => x.Term),
            Is.EqualTo(new[] { "s'a", "s'a", "s'a", "s'a" }));
    }

    [Test]
    public void TheCasedTokenizerStillFoldsTypography()
    {
        var tokens = Tokens("S’liak", preserveCase: true);

        Assert.That(tokens.Single().Term, Is.EqualTo("S'liak"));
    }
}
