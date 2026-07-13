using System.Collections.Generic;
using System.IO;
using System.Linq;
using CorpusSearch.Dependencies.Lucene;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Util;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>
/// The lemma injection contract (HANDOFF-lemma-search.md): candidates appear at
/// the original token's position (increment 0) with its offsets untouched, so
/// term-vector highlighting maps a lemma hit back onto the surface word.
/// </summary>
[TestFixture]
public class LemmaTokenFilterTest
{
    private record Token(string Term, int PosIncr, int Start, int End);

    private static LemmaTable Table(params string[] rows)
    {
        var tsv = "form\tlemmaId\tlemma\tlinkType\tpos\tvia\tnote\n" + string.Join("\n", rows);
        using var reader = new StringReader(tsv);
        return LemmaTable.Load(reader);
    }

    private static string Row(string form, string lemmaId) => $"{form}\t{lemmaId}\tx\tself\tx\t{form}\t";

    private static List<Token> Tokens(string text, LemmaTable table, LemmaResolver? resolver = null)
    {
        var tokenizer = new ManxTokenizer(LuceneVersion.LUCENE_48, new StringReader(text));
        using var stream = new LemmaTokenFilter(new ManxTokenFilter(tokenizer), table, resolver);
        var term = stream.GetAttribute<ICharTermAttribute>();
        var posIncr = stream.GetAttribute<IPositionIncrementAttribute>();
        var offset = stream.GetAttribute<IOffsetAttribute>();

        var tokens = new List<Token>();
        stream.Reset();
        while (stream.IncrementToken())
        {
            tokens.Add(new Token(term.ToString(), posIncr.PositionIncrement, offset.StartOffset, offset.EndOffset));
        }
        stream.End();
        return tokens;
    }

    [Test]
    public void ACoveredTokenIsReplacedByItsCandidate()
    {
        var tokens = Tokens("daase eh", Table(Row("daase", "aase.v")));

        Assert.That(tokens.Select(x => (x.Term, x.PosIncr)), Is.EqualTo(new[]
        {
            ("aase.v", 1),
            ("eh", 1),
        }));
    }

    [Test]
    public void EveryCandidateOfAnAmbiguousFormIsEmitted()
    {
        var table = Table(Row("aase", "aase.n"), Row("aase", "aase.v"), Row("aase", "faase.a"));

        var tokens = Tokens("aase", table);
        Assert.That(tokens.Select(x => (x.Term, x.PosIncr)), Is.EqualTo(new[]
        {
            ("aase.n", 1),
            ("aase.v", 0),
            ("faase.a", 0),
        }));
    }

    [Test]
    public void LemmaTokensKeepTheSurfaceOffsets()
    {
        // "my daase" - 'daase' spans offsets 3..8
        var tokens = Tokens("my daase", Table(Row("daase", "aase.n"), Row("daase", "aase.v")));

        var lemmas = tokens.Where(x => x.Term.StartsWith("aase.")).ToList();
        Assert.That(lemmas.Select(x => (x.Start, x.End)), Is.EqualTo(new[] { (3, 8), (3, 8) }));
    }

    [Test]
    public void UnknownTokensPassThroughAlone()
    {
        Assert.That(Tokens("xyzzy", Table()).Select(x => x.Term), Is.EqualTo(new[] { "xyzzy" }));
    }

    [Test]
    public void HyphenatedTokenResolvesViaItsSpacedForm()
    {
        // the tokenizer keeps 'aa-aase' as one token; the table's form is 'aa aase'
        var terms = Tokens("aa-aase", Table(Row("aa aase", "aa-aase.n"))).Select(x => x.Term);

        Assert.That(terms, Is.EqualTo(new[] { "aa-aase.n" }));
    }

    [Test]
    public void PresentOfBeeCliticFallsBackToItsParts()
    {
        var table = Table(Row("ta", "bee.v"), Row("ayn", "ayn.x"));

        var terms = Tokens("t'ayn", table).Select(x => x.Term);
        Assert.That(terms, Is.EqualTo(new[] { "t'ayn", "bee.v", "ayn.x" }));
    }

    [Test]
    public void PastOfBeeCliticFallsBackToItsParts()
    {
        var table = Table(Row("va", "bee.v"), Row("ayn", "ayn.x"));

        var terms = Tokens("v'ayn", table).Select(x => x.Term);
        Assert.That(terms, Is.EqualTo(new[] { "v'ayn", "bee.v", "ayn.x" }));
    }

    [Test]
    public void ArticleCliticFallsBackToItsParts()
    {
        var table = Table(Row("shoh", "shoh.x"), Row("yn", "yn.d"));

        var terms = Tokens("shoh'n", table).Select(x => x.Term);
        Assert.That(terms, Is.EqualTo(new[] { "shoh'n", "shoh.x", "yn.d" }));
    }

    [Test]
    public void ATableRowForTheContractionWinsOverTheCliticSplit()
    {
        // v'aym is a form in its own right: no fallback to va + aym
        var table = Table(Row("v'aym", "v'aym.x"), Row("va", "bee.v"), Row("aym", "aym.x"));

        var terms = Tokens("v'aym", table).Select(x => x.Term);
        Assert.That(terms, Is.EqualTo(new[] { "v'aym.x" }));
    }

    [Test]
    public void PartsSharedBetweenCliticHalvesAreEmittedOnce()
    {
        var table = Table(Row("yn", "yn.d"));

        // 'yn'n' is contrived: both halves resolve to yn.d, emitted once
        var terms = Tokens("yn'n", table).Select(x => x.Term);
        Assert.That(terms, Is.EqualTo(new[] { "yn'n", "yn.d" }));
    }

    // ---- the resolution layers (LemmaResolver) ----

    private static LemmaResolver Resolver(LemmaTable table, string? overrides = null, string? sidecar = null)
    {
        return LemmaResolver.Load(
            overrides == null ? null : new StringReader(overrides),
            sidecar == null ? null : new StringReader(sidecar),
            table);
    }

    /// <summary>A sidecar row for the line <paramref name="text"/> tokenizes to</summary>
    private static string SidecarRow(string text, int tokenIndex, string form, string lemmaIds,
        string tier = "index")
    {
        var key = LemmaResolver.LineKey(LemmaResolver.TokenizeManx(text));
        return "docId\tkey\tenglishHash\ttokenIndex\tform\tlemmaIds\ttier\thumanVerified\n"
               + $"doc\t{key}\tx\t{tokenIndex}\t{form}\t{lemmaIds}\t{tier}\t0\n";
    }

    [Test]
    public void AFormLevelOverrideNarrowsTheEmission()
    {
        var table = Table(Row("veg", "veg.x"), Row("veg", "beg.a"));
        var resolver = Resolver(table, overrides: "form\tlemmaIds\nveg\tveg.x\n");

        var tokens = Tokens("veg", table, resolver);
        Assert.That(tokens.Select(x => (x.Term, x.PosIncr)), Is.EqualTo(new[] { ("veg.x", 1) }));
    }

    [Test]
    public void ASidecarRowNarrowsOnlyItsOwnLine()
    {
        var table = Table(Row("veg", "veg.x"), Row("veg", "beg.a"), Row("moddey", "moddey.n"));
        var resolver = Resolver(table, sidecar: SidecarRow("veg moddey", tokenIndex: 0, "veg", "veg.x"));

        Assert.That(Tokens("veg moddey", table, resolver).Select(x => (x.Term, x.PosIncr)), Is.EqualTo(new[]
        {
            ("veg.x", 1),
            ("moddey.n", 1),
        }));
        // a different line: its key misses, and every candidate is emitted
        Assert.That(Tokens("veg cabbyl", table, resolver).Select(x => (x.Term, x.PosIncr)), Is.EqualTo(new[]
        {
            ("veg.x", 1),
            ("beg.a", 0),
            ("cabbyl", 1),
        }));
    }

    [Test]
    public void APopupTierRowDoesNotNarrowTheIndex()
    {
        var table = Table(Row("veg", "veg.x"), Row("veg", "beg.a"));
        var resolver = Resolver(table, sidecar: SidecarRow("veg", tokenIndex: 0, "veg", "veg.x", tier: "popup"));

        Assert.That(Tokens("veg", table, resolver).Select(x => x.Term), Is.EqualTo(new[] { "veg.x", "beg.a" }));
    }

    [Test]
    public void ARowPointingAtADifferentTokenIsIgnored()
    {
        // the row's index lands on 'moddey', not the 'veg' it claims: no narrowing
        var table = Table(Row("veg", "veg.x"), Row("veg", "beg.a"), Row("moddey", "moddey.n"));
        var resolver = Resolver(table, sidecar: SidecarRow("veg moddey", tokenIndex: 1, "veg", "veg.x"));

        Assert.That(Tokens("veg moddey", table, resolver).Select(x => x.Term),
            Is.EqualTo(new[] { "veg.x", "beg.a", "moddey.n" }));
    }

    /// <summary>The buffered (sidecar-active) path keeps the streaming contract:
    /// surface offsets on the injected ids, and the clitic fallback</summary>
    [Test]
    public void TheBufferedPathKeepsOffsetsAndClitics()
    {
        var table = Table(Row("daase", "aase.n"), Row("daase", "aase.v"), Row("ta", "bee.v"), Row("ayn", "ayn.x"));
        // an unrelated row, so the buffered path runs without narrowing anything
        var resolver = Resolver(table, sidecar: SidecarRow("nagh vel", tokenIndex: 0, "daase", "aase.n"));

        var lemmas = Tokens("my daase", table, resolver).Where(x => x.Term.StartsWith("aase.")).ToList();
        Assert.That(lemmas.Select(x => (x.Start, x.End)), Is.EqualTo(new[] { (3, 8), (3, 8) }));

        Assert.That(Tokens("t'ayn", table, resolver).Select(x => x.Term),
            Is.EqualTo(new[] { "t'ayn", "bee.v", "ayn.x" }));
    }
}
