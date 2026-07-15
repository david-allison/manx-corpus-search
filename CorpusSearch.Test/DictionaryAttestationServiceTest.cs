using System;
using System.Linq;
using CorpusSearch.Dependencies;
using CorpusSearch.Dependencies.Lucene;
using CorpusSearch.Model;
using CorpusSearch.Service;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>
/// The word page's attestation walk: the documents using a lexeme, oldest first,
/// and every use of it inside one, split by the reading each line resolved to.
///
/// Runs against the vendored cregeen.tsv rather than a fixture table, because the
/// index is built with <see cref="LemmaTable.Instance"/> whatever the service is
/// handed: a fixture would only agree with the lemma field by coincidence.
/// 'daase' is a form of aase.v; 'veg' heads veg.x while also being a lenition of
/// beg.a; 'vee' heads nothing and is a lenition of four lexemes.
/// </summary>
[TestFixture]
public class DictionaryAttestationServiceTest : QueryBase
{
    private WorkService workService = null!;

    [SetUp]
    public void SetUpWorkService()
    {
        workService = new WorkService();
    }

    private DictionaryAttestationService Service()
    {
        return new DictionaryAttestationService(
            new Searcher(luceneIndex, parser), LemmaTable.Instance, workService);
    }

    private void Add(string ident, DateTime? date, params string[] manxLines)
    {
        var document = new TestDocument(ident, date);
        workService.AddWork(document);
        luceneIndex.Add(document, manxLines.Select((manx, i) =>
            new DocumentLine { Manx = manx, English = "", CsvLineNumber = i + 2 }));
    }

    private void AddDated(string ident, int year, params string[] manxLines) =>
        Add(ident, new DateTime(year, 1, 1), manxLines);

    [Test]
    public void DocumentsAreWalkedOldestFirst()
    {
        AddDated("Later", 1819, "Daase yn billey");
        AddDated("Earlier", 1748, "Ta mee aase");

        var walk = Service().Attestations("aase");

        Assert.That(walk.Documents.Select(x => x.Ident), Is.EqualTo(new[] { "Earlier", "Later" }));
        Assert.That(walk.Documents[0].Year, Is.EqualTo(1748));
    }

    /// <summary>The walk shows each step's use count as you arrive, so it must
    /// come with the document list rather than a query later</summary>
    [Test]
    public void ALoneReadingIsCountedWithTheDocumentList()
    {
        AddDated("Doc", 1748, "Ta mee aase", "Daase eh");

        // 'daase' has one reading, so its scan is one term: each use matched once
        var walk = Service().Attestations("daase");

        Assert.That(walk.Documents.Single().Uses, Is.EqualTo(2));
    }

    /// <summary>...but an ambiguous word's readings are OR'd, and a token that
    /// several of them claim is matched once per reading: 'vee' would read four
    /// times too high, so the walk says nothing rather than something wrong
    /// (AttestationLines.UseCount counts those from the offsets instead)</summary>
    [Test]
    public void AnAmbiguousWordIsNotCountedFromTheScan()
    {
        AddDated("Doc", 1748, "Ta mee vee ayn");

        var walk = Service().Attestations("vee");

        Assert.That(walk.Documents.Single().Uses, Is.Null);
    }

    /// <summary>The whole point of walking the lemma field rather than the
    /// spelling: an inflected form is the same word as its headword</summary>
    [Test]
    public void AnInflectedSpellingIsWalkedAsItsHeadword()
    {
        AddDated("Lenited", 1748, "Daase yn billey");

        Assert.That(Service().Attestations("aase").Documents.Single().Ident,
            Is.EqualTo("Lenited"));
        // and looking up the inflected form walks the same lexeme
        Assert.That(Service().Attestations("daase").Documents.Single().Ident,
            Is.EqualTo("Lenited"));
    }

    /// <summary>An undated document cannot take a place in a chronological walk,
    /// but must not vanish from the count either</summary>
    [Test]
    public void UndatedDocumentsAreCountedRatherThanWalked()
    {
        AddDated("Dated", 1748, "Ta mee aase");
        Add("Undated", null, "Daase eh", "Aase eh");

        var walk = Service().Attestations("aase");

        Assert.Multiple(() =>
        {
            Assert.That(walk.Documents.Select(x => x.Ident), Is.EqualTo(new[] { "Dated" }));
            Assert.That(walk.UndatedDocuments, Is.EqualTo(1));
        });
    }

    /// <summary>'daase' carries only the verb, so the verb's group holds both
    /// lines while the noun's holds one: each reading shows what it claims</summary>
    [Test]
    public void EveryUseInADocumentIsReturnedInLineOrder()
    {
        AddDated("Doc", 1748, "Ta mee aase", "Cha nel eh", "Daase yn billey");

        var found = Service().InDocument("aase", "Doc").Result!;
        var verb = found.Groups.Single(x => x.LemmaIds.Contains("aase.v"));

        Assert.Multiple(() =>
        {
            Assert.That(found.Year, Is.EqualTo(1748));
            // two uses; the line between them uses neither form, and neither is
            // counted twice for being claimed by both the noun and the verb
            Assert.That(found.UseCount, Is.EqualTo(2));
            Assert.That(verb.Lines.Select(x => x.Manx),
                Is.EqualTo(new[] { "Ta mee aase", "Daase yn billey" }));
            Assert.That(verb.Lines.Select(x => x.CsvLineNumber), Is.Ordered);
        });
    }

    [Test]
    public void TheSurfaceWordIsHighlighted()
    {
        AddDated("Doc", 1748, "Daase yn billey");

        var line = Service().InDocument("aase", "Doc").Result!
            .Groups.Single(x => x.LemmaIds.Contains("aase.v")).Lines.Single();

        var highlighted = line.ManxHighlights!.Select(x => line.Manx![x.Start..x.End]);
        Assert.That(highlighted, Is.EqualTo(new[] { "Daase" }));
    }

    /// <summary>The walk is a taste of the evidence, not a concordance: a text
    /// using a word a hundred times must not make the section unreadable</summary>
    [Test]
    public void ThePreviewIsCappedButTheCountIsNot()
    {
        AddDated("Doc", 1748, Enumerable.Repeat("Ta mee aase", 20).ToArray());

        var group = Service().InDocument("aase", "Doc").Result!
            .Groups.Single(x => x.LemmaIds.Contains("aase.v"));

        Assert.Multiple(() =>
        {
            Assert.That(group.Count, Is.EqualTo(20));
            Assert.That(group.Lines, Has.Count.EqualTo(2));
        });
    }

    /// <summary>'aase' is both a noun (growth) and a verb (to grow), and this line
    /// is genuinely either: the readings display the same headword and claim the
    /// same word, so two rows would print the reader one quote twice with nothing
    /// to tell them apart, under a document reporting a single use. One row, and
    /// it names both readings — that the word is either of them is the fact.</summary>
    [Test]
    public void ReadingsClaimingTheSameWordAreOneRowNamingBoth()
    {
        AddDated("Doc", 1748, "Ta mee aase");

        var found = Service().InDocument("aase", "Doc").Result!;
        var row = found.Groups.Single();

        Assert.Multiple(() =>
        {
            Assert.That(row.LemmaIds, Is.EquivalentTo(new[] { "aase.n", "aase.v" }));
            Assert.That(row.Classes, Is.EquivalentTo(new[] { "n", "v" }));
            // the use the document itself reports, not one of them per reading
            Assert.That(row.Count, Is.EqualTo(1));
            Assert.That(found.UseCount, Is.EqualTo(1));
        });
    }

    /// <summary>...but readings claiming different words stay apart: there the
    /// resolver did decide, and rows differing by more than their label are two
    /// facts. 'daase' carries only the verb, so the verb claims both lines and the
    /// noun one — and each keeps its class, since 'aase' alone tells neither row
    /// from the other.</summary>
    [Test]
    public void ReadingsClaimingDifferentWordsStayApartAndKeepTheirClass()
    {
        AddDated("Doc", 1748, "Ta mee aase", "Daase yn billey");

        var groups = Service().InDocument("aase", "Doc").Result!.Groups;

        Assert.Multiple(() =>
        {
            Assert.That(groups.Select(x => x.Lemma), Is.EqualTo(new[] { "aase", "aase" }));
            // the commoner reading leads
            Assert.That(groups.Select(x => x.Count), Is.EqualTo(new[] { 2, 1 }));
            Assert.That(groups.Select(x => string.Join(",", x.Classes)),
                Is.EqualTo(new[] { "v", "n" }));
        });
    }

    /// <summary>A use is a surface word, not a line: a line saying the word twice
    /// is two uses of it</summary>
    [Test]
    public void ALineUsingTheWordTwiceCountsTwice()
    {
        AddDated("Doc", 1748, "Daase as daase reesht");

        var found = Service().InDocument("aase", "Doc").Result!;

        Assert.That(found.UseCount, Is.EqualTo(2));
        Assert.That(found.Groups.Single(x => x.LemmaIds.Contains("aase.v")).Lines, Has.Count.EqualTo(1));
    }

    /// <summary>An ambiguous word is split by what each line was read as, so the
    /// reader can tell one lexeme from another rather than meeting them
    /// interleaved. 'vee' is a lenition of bee, mee and their homographs.</summary>
    [Test]
    public void AnAmbiguousWordIsGroupedByReading()
    {
        AddDated("Doc", 1748, "Ta mee vee ayn");

        var groups = Service().InDocument("vee", "Doc").Result!.Groups;

        Assert.Multiple(() =>
        {
            // every reading the table offers 'vee' is accounted for by a row —
            // one of its own, or one shared with a reading it cannot be told from
            Assert.That(groups.SelectMany(x => x.LemmaIds),
                Is.EquivalentTo(DictionaryAttestationService.LemmaIdsFor(LemmaTable.Instance, "vee")));
            Assert.That(groups.Select(x => x.Lemma).Distinct(), Is.EquivalentTo(new[] { "bee", "mee" }));
        });
    }

    /// <summary>A word the lemma table does not know has no lexeme to walk: it
    /// must return nothing rather than every document in the corpus</summary>
    [Test]
    public void AnUnknownWordWalksNothing()
    {
        AddDated("Doc", 1748, "Ta mee aase");

        var walk = Service().Attestations("xyzzy");

        Assert.Multiple(() =>
        {
            Assert.That(walk.Lemmas, Is.Empty);
            Assert.That(walk.Documents, Is.Empty);
        });
    }

    [Test]
    public void ADocumentNotAttestingTheWordIsNotFound()
    {
        AddDated("Doc", 1748, "Cha nel eh");

        Assert.That(Service().InDocument("aase", "Doc").Result, Is.Null);
    }

    /// <summary>Mirrors DictionaryHistoryService.LemmaReadingsFor: a word which
    /// heads its own lexeme keeps only that one. 'veg' is a lenition of beg.a,
    /// but it also heads veg.x — walking it must not step through beg.a's
    /// documents as though they were the same word.</summary>
    [Test]
    public void AHeadwordKeepsOnlyItsOwnLexeme()
    {
        Assert.That(DictionaryAttestationService.LemmaIdsFor(LemmaTable.Instance, "veg"),
            Is.EqualTo(new[] { "veg.x" }));
    }

    /// <summary>...but a form which heads nothing offers every reading: 'vee' is
    /// only ever a lenition, and the walk cannot decide of what without a line to
    /// read it in</summary>
    [Test]
    public void AnAmbiguousNonHeadwordWalksEveryReading()
    {
        var ids = DictionaryAttestationService.LemmaIdsFor(LemmaTable.Instance, "vee");

        Assert.That(ids, Has.Count.GreaterThan(1));
        Assert.That(ids, Does.Contain("bee.v"));
        Assert.That(ids, Does.Contain("mee.n"));
    }
}
