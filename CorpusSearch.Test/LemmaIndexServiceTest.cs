using System.IO;
using System.Linq;
using CorpusSearch.Dependencies.Lucene;
using CorpusSearch.Service;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>
/// The lemma index: every lemma the tables link a form to, one letter at a time,
/// and one lemma's form tree.
/// </summary>
[TestFixture]
public class LemmaIndexServiceTest
{
    private static LemmaTable Table(params string[] rows)
    {
        var tsv = "form\tlemmaId\tlemma\tlinkType\tpos\tvia\tnote\n" + string.Join("\n", rows);
        using var reader = new StringReader(tsv);
        return LemmaTable.Load(reader);
    }

    /// <summary>No corpus behind it, so nothing is greyed: these tests are about
    /// the index and the tree, not about which spellings a text happens to use</summary>
    private static LemmaIndexService Service(LemmaTable table) =>
        new(table, new CorpusVocabulary(table));

    [Test]
    public void TheIndexReadsInCollationOrderThoughTheTableDoesNot()
    {
        // generator order: 'yn nah' before everything, a name in capitals,
        // a hyphenated lemma filing among the plain ones
        var service = Service(Table(
            "yn nah\tnah.n\tnah\tself\ts. f.\tyn nah\tdemutation-unvalidated",
            "doolish\tdoolish.np\tDoolish\tself\tnp. place\tDoolish\t",
            "aa aase\taa-aase.n\taa-aase\tself\ts. m.\taa-aase\t",
            "aase\taase.n\taase\tself\ts. m.\taase\t"));

        var page = service.Index(null);
        Assert.Multiple(() =>
        {
            Assert.That(page.Letters, Is.EqualTo(new[] { "A", "D", "N" }));
            Assert.That(page.Letter, Is.EqualTo("A"));
            Assert.That(page.Chapters.SelectMany(x => x.Words).Select(x => x.Word),
                Is.EqualTo(new[] { "aa-aase", "aase" }));
        });
    }

    [Test]
    public void TheIndexOpensAtTheLetterAsked()
    {
        var service = Service(Table(
            "aase\taase.n\taase\tself\ts. m.\taase\t",
            "doolish\tdoolish.np\tDoolish\tself\tnp. place\tDoolish\t"));

        var page = service.Index("d");
        Assert.Multiple(() =>
        {
            Assert.That(page.Letter, Is.EqualTo("D"));
            Assert.That(page.Chapters.SelectMany(x => x.Words).Select(x => x.Word),
                Is.EqualTo(new[] { "Doolish" }));
        });
    }

    /// <summary>cregeen.tsv carries two transcription artifacts ('≈',
    /// "[s'tammyltee"): an index by letter has no shelf for them, and neither
    /// deserves a punctuation "letter" in the bar. Their trees stay reachable
    /// by URL.</summary>
    [Test]
    public void ALemmaStartingWithNoLetterIsNotFiled()
    {
        var table = Table(
            "≈\t≈.n\t≈\tself\ts. f.\t≈\t",
            "scaanyn\t≈.n\t≈\tinflected\ts. f.\t≈\t",
            "aase\taase.n\taase\tself\ts. m.\taase\t");
        var service = Service(table);

        var page = service.Index(null);
        Assert.Multiple(() =>
        {
            Assert.That(page.Letters, Is.EqualTo(new[] { "A" }));
            Assert.That(page.Chapters.SelectMany(x => x.Words).Select(x => x.Word),
                Is.EqualTo(new[] { "aase" }));
            // the tree itself still answers
            Assert.That(service.Tree("≈"), Is.Not.Null);
        });
    }

    [Test]
    public void AnEmptyTableIsAnEmptyIndexNotAnError()
    {
        var page = Service(Table()).Index(null);
        Assert.Multiple(() =>
        {
            Assert.That(page.Letters, Is.Empty);
            Assert.That(page.Letter, Is.Null);
        });
    }

    /// <summary>The tree's groups read in a fixed order — the lexeme's own
    /// headwords, the paradigm, then the rule-made and historical links — not the
    /// order the file happens to list them</summary>
    [Test]
    public void TheTreeGroupsFormsByLinkTypeInReadingOrder()
    {
        var service = Service(Table(
            "aase\taase.v\taase\tself\tv.\taase\t",
            "dwyne\taase.v\taase\tphillips\tv.\taase\tphillips-1610",
            "haase\taase.v\taase\tmutation\tv.\taase\tgenerated-lenition",
            "aaseagh\taase.v\taase\tinflected\tv.\taase\t",
            "daase\taase.v\taase\tself\tv.\tdaase\t"));

        var tree = service.Tree("aase")!;
        Assert.Multiple(() =>
        {
            Assert.That(tree.Lemma, Is.EqualTo("aase"));
            Assert.That(tree.Groups.Select(x => x.LinkType),
                Is.EqualTo(new[] { "self", "inflected", "mutation", "phillips" }));
            Assert.That(tree.Groups.Single(x => x.LinkType == "mutation").Forms.Single().Unverified,
                Is.True);
            Assert.That(tree.Groups.Single(x => x.LinkType == "self").Forms.Single().Form,
                Is.EqualTo("daase"));
        });
    }

    /// <summary>The vocab supplement's rows are hand-asserted: the root itself
    /// renders as a guess, the way the popup's unverifiedLink does</summary>
    [Test]
    public void AHandAssertedLemmaIsAGuessAtTheRoot()
    {
        var service = Service(Table(
            "peiagh\tpeiagh.n\tpeiagh\tself\ts.\tpeiagh\tmodern-variant unverified",
            "peiaghyn\tpeiagh.n\tpeiagh\tinflected\ts.\tpeiagh\tunverified"));

        var tree = service.Tree("peiagh")!;
        Assert.Multiple(() =>
        {
            Assert.That(tree.Unverified, Is.True);
            Assert.That(tree.Groups.Single().Forms.Single().Unverified, Is.True);
        });
    }

    /// <summary>The via column is the tree's depth: 'pyaghyn' inflects the
    /// variant 'pyagh', not peiagh itself, and the tree hangs it there</summary>
    [Test]
    public void AFormDerivingThroughAnotherNestsUnderIt()
    {
        var service = Service(Table(
            "peiagh\tpeiagh.n\tpeiagh\tself\ts.\tpeiagh\t",
            "peiaghyn\tpeiagh.n\tpeiagh\tinflected\ts.\tpeiagh\t",
            "pyagh\tpeiagh.n\tpeiagh\tvariant\ts.\tpyagh\t",
            "pyaghyn\tpeiagh.n\tpeiagh\tinflected\ts.\tpyagh\t"));

        var tree = service.Tree("peiagh")!;
        var inflected = tree.Groups.Single(x => x.LinkType == "inflected");
        var pyagh = tree.Groups.Single(x => x.LinkType == "variant").Forms.Single();
        Assert.Multiple(() =>
        {
            // only the lemma's own inflection at the root; the variant's nests
            Assert.That(inflected.Forms.Single().Form, Is.EqualTo("peiaghyn"));
            Assert.That(pyagh.Groups!.Single().Forms.Single().Form, Is.EqualTo("pyaghyn"));
        });
    }

    /// <summary>A form heading a lexeme of its own carries that lexeme's tree:
    /// Cregeen enters 'e gheiney' under a lemma 'deiney', which is itself an
    /// inflection of dooinney — and 'gheiney' derives via the printed 'e
    /// gheiney', so the whole chain draws: dooinney -> deiney -> e gheiney ->
    /// gheiney</summary>
    [Test]
    public void AFormHeadingItsOwnLexemeNestsItsTree()
    {
        var service = Service(Table(
            "dooinney\tdooinney.n\tdooinney\tself\ts. m.\tdooinney\t",
            "deiney\tdooinney.n\tdooinney\tinflected\ts. m.\tdooinney\t",
            "e gheiney\tdeiney.n\tdeiney\tself\ts.\te gheiney\t",
            "gheiney\tdeiney.n\tdeiney\tparticle\ts.\te gheiney\t"));

        var deiney = service.Tree("dooinney")!.Groups.Single().Forms.Single();
        Assert.Multiple(() =>
        {
            Assert.That(deiney.Form, Is.EqualTo("deiney"));
            var eGheiney = deiney.Groups!.Single().Forms.Single();
            Assert.That(eGheiney.Form, Is.EqualTo("e gheiney"));
            var gheiney = eGheiney.Groups!.Single().Forms.Single();
            Assert.That(gheiney.Form, Is.EqualTo("gheiney"));
            Assert.That(eGheiney.Groups!.Single().LinkType, Is.EqualTo("particle"));
        });
    }

    /// <summary>A demutation guess is a leaf, never a doorway: fee's guessed
    /// 'ee' must not import the whole family of *to eat* into a tree about
    /// weaving — the same hop <see cref="LemmaTable.RootDisplayLemmasFor"/>
    /// refuses</summary>
    [Test]
    public void ADemutationGuessDoesNotImportTheOtherLexeme()
    {
        var service = Service(Table(
            "fee\tfee.v\tfee\tself\tv.\tfee\t",
            "ee\tfee.v\tfee\tdemutated\tv.\tfee\t",
            "ee\tee.v\tee\tself\tv.\tee\t",
            "eeym\tee.v\tee\tinflected\tv.\tee\t"));

        var ee = service.Tree("fee")!.Groups.Single(x => x.LinkType == "demutated")
            .Forms.Single();
        Assert.Multiple(() =>
        {
            Assert.That(ee.Form, Is.EqualTo("ee"));
            Assert.That(ee.Groups, Is.Null);
        });
    }

    /// <summary>fee inflects to feeagh and feeagh pluralizes to fee — a directed
    /// cycle the print itself creates (LemmaLinkCycleTest). Each form is drawn
    /// once: the second meeting is a leaf, not a circle.</summary>
    [Test]
    public void ABookTrueCycleClosesAsALeaf()
    {
        var service = Service(Table(
            "fee\tfee.v\tfee\tself\tv.\tfee\t",
            "feeagh\tfee.v\tfee\tinflected\tv.\tfee\t",
            "feeagh\tfeeagh.n\tfeeagh\tself\ts. m.\tfeeagh\t",
            "fee\tfeeagh.n\tfeeagh\tplural\ts. m.\tfee\t"));

        var feeagh = service.Tree("fee")!.Groups.Single().Forms.Single();
        Assert.Multiple(() =>
        {
            Assert.That(feeagh.Form, Is.EqualTo("feeagh"));
            // feeagh's own lexeme nests, and its 'fee' closes the loop as a leaf
            var loop = feeagh.Groups!.Single().Forms.Single();
            Assert.That(loop.Form, Is.EqualTo("fee"));
            Assert.That(loop.Groups, Is.Null);
            // ...and the other way round
            Assert.That(service.Tree("feeagh")!.Groups.Single().Forms.Single().Form,
                Is.EqualTo("fee"));
        });
    }

    [Test]
    public void AnUnknownLemmaHasNoTree()
    {
        Assert.That(Service(Table()).Tree("xyzzy"), Is.Null);
    }

    /// <summary>A prefix is spelled into its family: Cregeen prints 'aa-' as a
    /// headword and twenty compounds written with it, and its tree gathers them
    /// — by spelling, never by rule, so a word merely starting with the same
    /// letters is not claimed</summary>
    [Test]
    public void APrefixGathersTheLemmasSpelledWithIt()
    {
        var service = Service(Table(
            "aa\taa.a\taa-\tself\ta.\taa-\t",
            "aa ghiennaghtyn\taa-ghiennaghtyn.n\taa-ghiennaghtyn\tself\ts. m.\taa-ghiennaghtyn\t",
            "aase\taase.n\taase\tself\ts. m.\taase\t"));

        var group = service.Tree("aa-")!.Groups.Single();
        Assert.Multiple(() =>
        {
            Assert.That(group.LinkType, Is.EqualTo("prefixed"));
            Assert.That(group.Forms.Single().Form, Is.EqualTo("aa-ghiennaghtyn"));
        });
    }

    /// <summary>A form Cregeen prints but no text uses can say so: the source
    /// rides the node — and only an attested link's, since a guess has nothing
    /// but the generator behind it</summary>
    [Test]
    public void TheTreeNamesTheBookBehindAVerifiedLink()
    {
        using var reader = new StringReader(
            "form\tlemmaId\tlemma\tlinkType\tpos\tvia\tnote\n"
            + "aase\taase.n\taase\tself\ts. m.\taase\t\n"
            + "aaseyn\taase.n\taase\tinflected\ts. m.\taase\t\n"
            + "haase\taase.n\taase\tmutation\ts. m.\taase\tgenerated-lenition\n");
        var table = LemmaTable.Load([(reader, "cregeen")]);

        var tree = Service(table).Tree("aase")!;
        var byForm = tree.Groups.SelectMany(x => x.Forms).ToDictionary(x => x.Form);
        Assert.Multiple(() =>
        {
            Assert.That(tree.Source, Is.EqualTo("cregeen"));
            Assert.That(byForm["aaseyn"].Source, Is.EqualTo("cregeen"));
            Assert.That(byForm["haase"].Source, Is.Null);
        });
    }

    /// <summary>Each node counts how often the corpus says it, by its spelling</summary>
    [Test]
    public void TheTreeCountsEachSpellingsAttestations()
    {
        var table = Table(
            "dooinney\tdooinney.n\tdooinney\tself\ts. m.\tdooinney\t",
            "deiney\tdooinney.n\tdooinney\tinflected\ts. m.\tdooinney\t");
        var vocabulary = new CorpusVocabulary(table);
        vocabulary.Init([("dooinney", 4L), ("deiney", 2L)]);
        var tree = new LemmaIndexService(table, vocabulary).Tree("dooinney")!;

        Assert.Multiple(() =>
        {
            Assert.That(tree.Attestations, Is.EqualTo(4));
            Assert.That(tree.Groups.Single().Forms.Single().Attestations, Is.EqualTo(2));
        });
    }

    /// <summary>The tree greys by the spelling itself, never the lemma hop: the
    /// corpus saying 'deiney' does not make 'dooinneyyn' a word a text uses</summary>
    [Test]
    public void AFormIsGreyedByItsOwnSpelling()
    {
        var table = Table(
            "dooinney\tdooinney.n\tdooinney\tself\ts. m.\tdooinney\t",
            "deiney\tdooinney.n\tdooinney\tinflected\ts. m.\tdooinney\t",
            "gheiney\tdooinney.n\tdooinney\tmutation\ts. m.\tdooinney\tgenerated-lenition");
        var vocabulary = new CorpusVocabulary(table);
        vocabulary.Init([("deiney", 3L)]);
        var tree = new LemmaIndexService(table, vocabulary).Tree("dooinney")!;

        var byForm = tree.Groups.SelectMany(x => x.Forms).ToDictionary(x => x.Form);
        Assert.Multiple(() =>
        {
            // no text spells the headword itself: the root is greyed even
            // though its inflection attests the lexeme
            Assert.That(tree.Attested, Is.False);
            Assert.That(byForm["deiney"].Attested, Is.True);
            Assert.That(byForm["gheiney"].Attested, Is.False);
        });
    }
}
