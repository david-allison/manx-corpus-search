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
    public void SurfaceTokensRemainInTheLemmaField()
    {
        AddLines(new DocumentLine { Manx = "Daase yn billey", English = "", CsvLineNumber = 2 });

        Assert.That(SearchLemma("daase").Lines, Has.Count.EqualTo(1));
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
}
