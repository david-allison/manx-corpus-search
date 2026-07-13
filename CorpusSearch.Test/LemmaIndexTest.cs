using System.IO;
using System.Linq;
using CorpusSearch.Dependencies.Lucene;
using CorpusSearch.Model;
using Lucene.Net.Index;
using Lucene.Net.Search.Spans;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>
/// The manx_lemma field (HANDOFF-lemma-search.md): lemma-id terms match inflected
/// surface forms with correct highlights, and only Manx lines feed the field.
/// Uses the vendored cregeen.tsv: 'daase' is a form of the lemma id 'aase.v'.
/// </summary>
[TestFixture]
public class LemmaIndexTest : QueryBase
{
    private const string DOC = "doc";

    private void AddLines(params DocumentLine[] lines)
    {
        luceneIndex.Add(new TestDocument(DOC, DOC_DATE), lines);
    }

    private SearchResult SearchLemma(string lemmaId)
    {
        var query = new SpanTermQuery(new Term(LuceneIndex.DOCUMENT_LEMMA_MANX, lemmaId));
        return luceneIndex.Search(DOC, query, getTranscriptData: false);
    }

    [Test]
    public void LemmaIdMatchesAnInflectedSurfaceForm()
    {
        AddLines(new DocumentLine { Manx = "Daase yn billey", English = "", CsvLineNumber = 2 });

        var line = SearchLemma("aase.v").Lines.Single();
        Assert.That(line.Manx, Is.EqualTo("Daase yn billey"));
    }

    [Test]
    public void TheLemmaHitHighlightsTheSurfaceWord()
    {
        AddLines(new DocumentLine { Manx = "Daase yn billey", English = "", CsvLineNumber = 2 });

        var line = SearchLemma("aase.v").Lines.Single();
        Assert.That(line.ManxHighlights, Is.Not.Null);
        var highlighted = line.ManxHighlights!.Select(x => line.Manx![x.Start..x.End]);
        Assert.That(highlighted, Is.EqualTo(new[] { "Daase" }));
    }

    [Test]
    public void UncoveredSurfaceTokensRemainInTheLemmaField()
    {
        // 'xyzzy' has no table entry: its surface token backs the unknown-term
        // query fallback. Covered tokens ('daase') are replaced by their ids.
        AddLines(new DocumentLine { Manx = "Daase xyzzy", English = "", CsvLineNumber = 2 });

        Assert.That(SearchLemma("xyzzy").Lines, Has.Count.EqualTo(1));
        Assert.That(SearchLemma("daase").Lines, Is.Empty);
    }

    [Test]
    public void NonManxLinesAreNotInTheLemmaField()
    {
        AddLines(new DocumentLine
        {
            Manx = "daase is an english word here",
            English = "",
            CsvLineNumber = 2,
            Language = "en",
        });

        Assert.That(SearchLemma("aase.v").Lines, Is.Empty);
    }

    // ---- the resolution layers, end to end (LemmaResolver; vendored table:
    // 'veg' is ambiguous across veg.x / beg.a / meg.n) ----

    /// <summary>A form-level override narrows what the index holds: the demutation
    /// reading no longer matches anywhere</summary>
    [Test]
    public void AnOverrideNarrowsTheIndexedCandidates()
    {
        var resolver = LemmaResolver.Load(
            new StringReader("form\tlemmaIds\nveg\tveg.x\n"), null, LemmaTable.Instance);
        luceneIndex = LuceneIndex.GetInstance(resolver);
        AddLines(new DocumentLine { Manx = "Cha nel veg aym", English = "", CsvLineNumber = 2 });

        Assert.That(SearchLemma("veg.x").Lines, Has.Count.EqualTo(1));
        Assert.That(SearchLemma("beg.a").Lines, Is.Empty);
    }

    /// <summary>A sidecar row narrows its own line only, and the surviving id still
    /// highlights the surface word</summary>
    [Test]
    public void ASidecarRowNarrowsItsLineOnly()
    {
        const string resolved = "Cha nel veg aym";
        // the line key the exporter computed: over the normalized cell's token stream
        var key = LemmaResolver.LineKey(LemmaResolver.TokenizeManx(DocumentLine.NormalizeManx(resolved)));
        var sidecar = "docId\tkey\tenglishHash\ttokenIndex\tform\tlemmaIds\ttier\thumanVerified\n"
                      + $"doc\t{key}\tx\t2\tveg\tveg.x\tindex\t0\n";
        var resolver = LemmaResolver.Load(null, new StringReader(sidecar), LemmaTable.Instance);
        luceneIndex = LuceneIndex.GetInstance(resolver);
        AddLines(
            new DocumentLine { Manx = resolved, English = "", CsvLineNumber = 2 },
            new DocumentLine { Manx = "Ta veg ayn", English = "", CsvLineNumber = 3 });

        // the demutation reading now only matches the unresolved line
        Assert.That(SearchLemma("beg.a").Lines.Single().Manx, Is.EqualTo("Ta veg ayn"));
        var lines = SearchLemma("veg.x").Lines;
        Assert.That(lines, Has.Count.EqualTo(2));
        var hit = lines.Single(x => x.Manx == resolved);
        var highlighted = hit.ManxHighlights!.Select(x => hit.Manx![x.Start..x.End]);
        Assert.That(highlighted, Is.EqualTo(new[] { "veg" }));
    }
}
