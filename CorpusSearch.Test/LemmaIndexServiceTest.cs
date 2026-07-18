using System.Collections.Generic;
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
            "gheiney\tdeiney.n\tdeiney\tdemutated\ts.\te gheiney\t"));

        var deiney = service.Tree("dooinney")!.Groups.Single().Forms.Single();
        Assert.Multiple(() =>
        {
            Assert.That(deiney.Form, Is.EqualTo("deiney"));
            var eGheiney = deiney.Groups!.Single().Forms.Single();
            Assert.That(eGheiney.Form, Is.EqualTo("e gheiney"));
            var gheiney = eGheiney.Groups!.Single().Forms.Single();
            Assert.That(gheiney.Form, Is.EqualTo("gheiney"));
            Assert.That(eGheiney.Groups!.Single().LinkType, Is.EqualTo("demutated"));
            Assert.That(deiney.Via, Is.Null);
        });
    }

    /// <summary>Cregeen's entry 'e haaght' is itself the particle phrase: a
    /// particle row filing under it would say the entry over again, count
    /// and all, and is not drawn</summary>
    [Test]
    public void AParticleRowUnderItsOwnPhraseIsNotDrawn()
    {
        var service = Service(Table(
            "aaght\taaght.n\taaght\tself\ts. m.\taaght\t",
            "e haaght\taaght.n\taaght\tself\ts.\te haaght\t",
            "haaght\taaght.n\taaght\tparticle\ts.\te haaght\t"));

        var groups = service.Tree("aaght")!.Groups;

        Assert.Multiple(() =>
        {
            var entry = groups.Single(g => g.LinkType == "self").Forms.Single();
            Assert.That(entry.Form, Is.EqualTo("e haaght"));
            // nothing beneath it: the phrase row would only echo it
            Assert.That(entry.Groups, Is.Null);
            Assert.That(groups.Any(g => g.LinkType == "particle"), Is.False);
        });
    }

    /// <summary>'deiney' is inflected AND plural of dooinney — two links in
    /// the tables, one word to the reader: one row, under the best-ranked
    /// group, the other way named on it</summary>
    [Test]
    public void AFormLinkedTwoWaysDrawsOnce()
    {
        var service = Service(Table(
            "dooinney\tdooinney.n\tdooinney\tself\ts. m.\tdooinney\t",
            "deiney\tdooinney.n\tdooinney\tinflected\ts. m.\tdooinney\t",
            "deiney\tdooinney.n\tdooinney\tplural\ts. m.\tdeiney\t"));

        var groups = service.Tree("dooinney")!.Groups;

        Assert.Multiple(() =>
        {
            var row = groups.Single().Forms.Single();
            Assert.That(groups.Single().LinkType, Is.EqualTo("inflected"));
            Assert.That(row.Form, Is.EqualTo("deiney"));
            Assert.That(row.AlsoLinkedAs, Is.EqualTo(new[] { "plural" }));
        });
    }

    /// <summary>A lemma no text uses still stands in a book: the index names
    /// the book ("cregeen"), as the tree does, or the grey reads as a
    /// phantom. The corpus speaks for an attested lemma itself.</summary>
    [Test]
    public void TheIndexNamesTheBookBehindANeverSaidLemma()
    {
        var tsv = "form\tlemmaId\tlemma\tlinkType\tpos\tvia\tnote\n"
                  + "aase\taase.n\taase\tself\ts. m.\taase\t\n"
                  + "aalin\taalin.a\taalin\tself\ta.\taalin\t";
        var table = LemmaTable.Load([((TextReader)new StringReader(tsv), "cregeen")]);
        var vocabulary = new CorpusVocabulary(table);
        vocabulary.Init([("aase", 5)]);
        var service = new LemmaIndexService(table, vocabulary);

        var words = service.Index(null).Chapters.SelectMany(x => x.Words).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(words.Single(x => x.Word == "aalin").Source, Is.EqualTo("cregeen"));
            Assert.That(words.Single(x => x.Word == "aalin").Attested, Is.False);
            Assert.That(words.Single(x => x.Word == "aase").Source, Is.Null);
        });
    }

    /// <summary>Phillips spells the bare form, not the phrase: 'gene'
    /// derives through gheiney, so it hangs off gheiney's own row. The
    /// phrase row is a leaf — a phrase hosts nothing — and the guess row
    /// survives beside it exactly when it has a family to carry.</summary>
    [Test]
    public void TheFormsFamilyHangsOffTheFormNotThePhrase()
    {
        var service = Service(Table(
            "dooinney\tdooinney.n\tdooinney\tself\ts. m.\tdooinney\t",
            "gheiney\tdooinney.n\tdooinney\tparticle\ts.\te gheiney\t",
            "gheiney\tdooinney.n\tdooinney\tdemutated\ts. m.\te gheiney\t",
            "gene\tdooinney.n\tdooinney\tphillips\ts.\tgheiney\tphillips-1610"));

        var groups = service.Tree("dooinney")!.Groups;

        Assert.Multiple(() =>
        {
            // the particle phrase attests the mutation, so the bare row is
            // no longer merely "possible": it files under Mutations
            Assert.That(groups.Select(x => x.LinkType),
                Is.EqualTo(new[] { "mutation", "particle" }));
            var bare = groups.Single(x => x.LinkType == "mutation").Forms.Single();
            Assert.That(bare.Form, Is.EqualTo("gheiney"));
            Assert.That(bare.Groups!.Single().LinkType, Is.EqualTo("phillips"));
            Assert.That(bare.Groups!.Single().Forms.Single().Form, Is.EqualTo("gene"));
            var phrase = groups.Single(x => x.LinkType == "particle").Forms.Single();
            Assert.That(phrase.Via, Is.EqualTo("e gheiney"));
            Assert.That(phrase.Groups, Is.Null);
        });
    }

    /// <summary>The same, nested: under Cregeen's entry 'e gheiney' the
    /// particle row is an echo and is not drawn, but its print-standing
    /// remains — the bare form's row reads Mutations, not Possible</summary>
    [Test]
    public void APrintedPhraseUpgradesTheGuessBeneathIt()
    {
        var service = Service(Table(
            "deiney\tdeiney.n\tdeiney\tself\ts.\tdeiney\t",
            "e gheiney\tdeiney.n\tdeiney\tself\ts.\te gheiney\t",
            "gheiney\tdeiney.n\tdeiney\tparticle\ts.\te gheiney\t",
            "gheiney\tdeiney.n\tdeiney\tdemutated\ts. m.\te gheiney\t",
            "gene\tdeiney.n\tdeiney\tphillips\ts.\tgheiney\tphillips-1610"));

        var entry = service.Tree("deiney")!
            .Groups.Single(g => g.LinkType == "self").Forms.Single();

        Assert.Multiple(() =>
        {
            Assert.That(entry.Form, Is.EqualTo("e gheiney"));
            Assert.That(entry.Groups!.Select(g => g.LinkType),
                Is.EqualTo(new[] { "mutation" }));
            var bare = entry.Groups!.Single().Forms.Single();
            Assert.That(bare.Form, Is.EqualTo("gheiney"));
            Assert.That(bare.Groups!.Single().Forms.Single().Form, Is.EqualTo("gene"));
        });
    }

    /// <summary>A lone childless guess beside the form's particle row says
    /// nothing the phrase does not: it is dropped, not demoted</summary>
    [Test]
    public void AGuessYieldsToARealLinkBesideIt()
    {
        var service = Service(Table(
            "dooinney\tdooinney.n\tdooinney\tself\ts. m.\tdooinney\t",
            "gheiney\tdooinney.n\tdooinney\tparticle\ts.\te gheiney\t",
            "gheiney\tdooinney.n\tdooinney\tdemutated\ts. m.\te gheiney\t"));

        var groups = service.Tree("dooinney")!.Groups;

        Assert.Multiple(() =>
        {
            var row = groups.Single().Forms.Single();
            Assert.That(groups.Single().LinkType, Is.EqualTo("particle"));
            Assert.That(row.Via, Is.EqualTo("e gheiney"));
            // dropped, not demoted: the guess is not worth naming beside print
            Assert.That(row.AlsoLinkedAs, Is.Null);
        });
    }

    /// <summary>The bare spelling rides after any particle at once, so its
    /// count answers for all of them together: a particle row is counted by
    /// its phrase, and only its phrase</summary>
    [Test]
    public void AParticleRowIsCountedByItsPhrase()
    {
        var table = Table(
            "dooinney\tdooinney.n\tdooinney\tself\ts. m.\tdooinney\t",
            "gheiney\tdooinney.n\tdooinney\tparticle\ts.\te gheiney\t");
        var vocabulary = new CorpusVocabulary(table);
        // 'gheiney' is said three times, but after this particle only once
        vocabulary.Init([("ta", 5), ("e", 5), ("gheiney", 3)]);
        vocabulary.ScanPhrases(["e gheiney"],
            ["ta e gheiney ayn", "gheiney as gheiney"]);
        var service = new LemmaIndexService(table, vocabulary);

        var row = service.Tree("dooinney")!
            .Groups.Single(g => g.LinkType == "particle").Forms.Single();

        Assert.Multiple(() =>
        {
            Assert.That(row.Via, Is.EqualTo("e gheiney"));
            Assert.That(row.Attestations, Is.EqualTo(1));
            Assert.That(row.Attested, Is.True);
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
    /// letters is not claimed. And the family reads back up: the compound names
    /// the prefix it is written with.</summary>
    [Test]
    public void APrefixAndItsFamilyReadBothWays()
    {
        var service = Service(Table(
            "aa\taa.a\taa-\tself\ta.\taa-\t",
            "aa ghiennaghtyn\taa-ghiennaghtyn.n\taa-ghiennaghtyn\tself\ts. m.\taa-ghiennaghtyn\t",
            "aase\taase.n\taase\tself\ts. m.\taase\t"));

        var group = service.Tree("aa-")!.Groups.Single();
        var compound = service.Tree("aa-ghiennaghtyn")!;
        Assert.Multiple(() =>
        {
            Assert.That(group.LinkType, Is.EqualTo("prefixed"));
            Assert.That(group.Forms.Single().Form, Is.EqualTo("aa-ghiennaghtyn"));
            // ...and back up
            var parent = compound.Parents!.Single();
            Assert.That(parent.Lemma, Is.EqualTo("aa-"));
            Assert.That(parent.LinkTypes, Is.EqualTo(new[] { "prefixed" }));
            // a word merely starting with the letters claims no prefix
            Assert.That(service.Tree("aase")!.Parents, Is.Null);
        });
    }

    /// <summary>Spelling, not the lemma table, is a prefix's whole relationship,
    /// so the family takes in whatever is written with it: the corpus coins
    /// compounds no book lists ('aa-chroo' is Wilson's, not Cregeen's), and the
    /// books print compounds the table never linked. A member the table never
    /// linked is derived and marked so; where two sources write the same word,
    /// the table's spelling stands, and a word only ever printed in Kelly's
    /// capitals is lowered.</summary>
    [Test]
    public void APrefixFamilyTakesTheBooksAndTheCorpusIn()
    {
        var table = Table(
            "aa\taa.a\taa-\tself\ta.\taa-\t",
            "aa ghiennaghtyn\taa-ghiennaghtyn.n\taa-ghiennaghtyn\tself\ts. m.\taa-ghiennaghtyn\t");
        var vocabulary = new CorpusVocabulary(table);
        vocabulary.Init([("aa-chroo", 2), ("aase", 40)]);
        var service = new LemmaIndexService(table, vocabulary,
            [new StubDictionary(["AA-CHOORSAL", "Aa-ghiennaghtyn", "aa-chummey eddin", "aase"])]);

        var family = service.Tree("aa-")!.Groups.Single().Forms;
        Assert.Multiple(() =>
        {
            // the book's compound, the corpus's coinage, the table's row.
            // 'aase', said and printed both, claims nothing (no hyphen, no
            // prefix), and the phrase opening 'aa-chummey' is no compound
            Assert.That(family.Select(x => x.Form),
                Is.EqualTo(new[] { "aa-choorsal", "aa-chroo", "aa-ghiennaghtyn" }));
            // the derived members say so; the table's own row does not
            Assert.That(family.Select(x => x.Unverified),
                Is.EqualTo(new[] { true, true, false }));
            Assert.That(family.Single(x => x.Form == "aa-chroo").Attestations, Is.EqualTo(2));
        });
    }

    /// <summary>A book that answers for its word list and nothing else: all a
    /// prefix's family asks of a dictionary</summary>
    private sealed class StubDictionary(string[] words) : ISearchDictionary
    {
        public string Identifier => "Stub";
        public string Slug => "stub";
        public List<string> QueryLanguages => ["gv"];
        public bool LinkToDictionary => false;
        public IEnumerable<DictionarySummary> GetSummaries(string query, bool basic = false) => [];
        public bool ContainsWord(string word) => words.Contains(word);
        public IEnumerable<string> AllWords => words;
        public IReadOnlyList<string> Headwords => words;
    }

    /// <summary>Every link a tree draws downward reads back up from the other
    /// end: dooinney shows deiney, so deiney names dooinney</summary>
    [Test]
    public void ALemmaNamesTheLemmasItHangsOff()
    {
        var service = Service(Table(
            "dooinney\tdooinney.n\tdooinney\tself\ts. m.\tdooinney\t",
            "deiney\tdooinney.n\tdooinney\tinflected\ts. m.\tdooinney\t",
            "deiney\tdooinney.n\tdooinney\tplural\ts. m.\tdeiney\t",
            "e gheiney\tdeiney.n\tdeiney\tself\ts.\te gheiney\t"));

        var parent = service.Tree("deiney")!.Parents!.Single();
        Assert.Multiple(() =>
        {
            Assert.That(parent.Lemma, Is.EqualTo("dooinney"));
            Assert.That(parent.LinkTypes, Is.EqualTo(new[] { "inflected", "plural" }));
            // the top of the family claims nothing above it
            Assert.That(service.Tree("dooinney")!.Parents, Is.Null);
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
