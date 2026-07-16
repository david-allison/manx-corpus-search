using System.IO;
using CorpusSearch.Dependencies.Lucene;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>
/// The lemma table contract (HANDOFF-lemma-search.md): lookup normalization must
/// mirror cregeen.tsv's `form` column, and every candidate of an ambiguous form
/// is returned.
/// </summary>
[TestFixture]
public class LemmaTableTest
{
    private static LemmaTable Table(params string[] rows)
    {
        var tsv = "form\tlemmaId\tlemma\tlinkType\tpos\tvia\tnote\n" + string.Join("\n", rows);
        using var reader = new StringReader(tsv);
        return LemmaTable.Load(reader);
    }

    [TestCase("Daase", "daase")]
    [TestCase("çhengey", "chengey")]
    [TestCase("t’ayn", "t'ayn")]
    [TestCase("aa-aase", "aa aase")]
    [TestCase("aa - aase", "aa aase")]
    [TestCase("my  chione ", "my chione")]
    [TestCase("jee.", "jee")]
    [TestCase("'sy", "sy")]
    [TestCase("aase.v", "aase.v")] // an internal dot survives: lemma ids pass through
    [TestCase("Benreïn", "benrein")] // combining marks are display conventions
    [TestCase("mârish", "marish")]
    [TestCase("bleïn", "blein")]
    public void NormalizeFormMirrorsTheFormColumn(string input, string expected)
    {
        Assert.That(LemmaTable.NormalizeForm(input), Is.EqualTo(expected));
    }

    [Test]
    public void AmbiguousFormReturnsEveryCandidate()
    {
        var table = Table(
            "aase\taase.n\taase\tself\ts. m.\taase\t",
            "aase\taase.v\taase\tself\tv.\taase\t",
            "aase\tfaase.a\tfaase\tdemutated\ta.\taase\t");

        Assert.That(table.CandidatesFor("aase"), Is.EqualTo(new[] { "aase.n", "aase.v", "faase.a" }));
    }

    [Test]
    public void RepeatedPairsAreOneCandidate()
    {
        var table = Table(
            "ynnah\tnah.n\tnah\tself\ts. f.\tyn nah\tcollapsed",
            "ynnah\tnah.n\tnah\tuniverbated\ts. f.\tyn nah\t");

        Assert.That(table.CandidatesFor("ynnah"), Is.EqualTo(new[] { "nah.n" }));
    }

    [Test]
    public void VocabSupplementRowsAreUnverifiedLinks()
    {
        // the vocab supplement tags every row unverified, additively beside
        // any print reading; the note column holds space-separated tags, so
        // a second tag must not hide the unverified one
        var table = Table(
            "pyaghyn\tpy.n\tpy\tinflected\ts. f.\tpy\t",
            "pyaghyn\tpeiagh.n\tpeiagh\tinflected\ts.\tpyagh\tunverified",
            "pheiagh\tpeiagh.n\tpeiagh\tmutation\ts.\tpeiagh\tmodern-variant unverified");

        Assert.That(table.CandidatesFor("pyaghyn"), Is.EqualTo(new[] { "py.n", "peiagh.n" }));
        Assert.That(table.IsUnverifiedLink("pyaghyn", "peiagh"), Is.True);
        Assert.That(table.IsUnverifiedLink("pheiagh", "peiagh"), Is.True);
        Assert.That(table.IsUnverifiedLink("pyaghyn", "py"), Is.False);
    }

    [Test]
    public void LookupNormalizesItsInput()
    {
        var table = Table("aa aase\taa-aase.n\taa-aase\tself\ts. m.\taa-aase\t");

        Assert.That(table.CandidatesFor("Aa-Aase"), Is.EqualTo(new[] { "aa-aase.n" }));
    }

    [Test]
    public void UnknownFormsHaveNoCandidates()
    {
        Assert.That(Table().CandidatesFor("xyzzy"), Is.Empty);
    }

    /// <summary>Why Affix has to read the headword's spelling: the fold really
    /// does lose the hyphen, so 'an-' and 'an' key the same row and nothing after
    /// this point can tell the prefix from the word</summary>
    [Test]
    public void AnAffixAndItsBareWordKeyTheSameRow()
    {
        Assert.That(LemmaTable.NormalizeForm("an-"), Is.EqualTo("an"));
    }

    [Test]
    public void LemmaIdsAreRecognised()
    {
        var table = Table("aase\taase.v\taase\tself\tv.\taase\t");

        Assert.That(table.IsLemmaId("aase.v"), Is.True);
        Assert.That(table.IsLemmaId("aase"), Is.False);
    }

    [TestCase("t'ayn", "ta")]
    [TestCase("v'ayn", "va")]
    public void BeeCliticContractionCombinesItsParts(string form, string beeForm)
    {
        var table = Table(
            $"{beeForm}\tbee.v\tbee\tinflected\tv.\t{beeForm}\t",
            "ayn\tayn.x\tayn\tself\tx\tayn\t");

        Assert.That(table.CliticCandidatesFor(form), Is.EqualTo(new[] { "bee.v", "ayn.x" }));
        Assert.That(table.CliticDisplayLemmasFor(form), Is.EqualTo(new[] { "bee", "ayn" }));
    }

    [Test]
    public void ArticleCliticContractionCombinesItsParts()
    {
        var table = Table(
            "shoh\tshoh.x\tshoh\tself\tx\tshoh\t",
            "yn\tyn.d\tyn\tself\td\tyn\t");

        Assert.That(table.CliticCandidatesFor("shoh'n"), Is.EqualTo(new[] { "shoh.x", "yn.d" }));
    }

    [Test]
    public void CliticLookupNormalizesItsInput()
    {
        var table = Table(
            "ta\tbee.v\tbee\tinflected\tv.\tta\t",
            "ayn\tayn.x\tayn\tself\tx\tayn\t");

        // curly apostrophe and case are normalized before the pattern match
        Assert.That(table.CliticCandidatesFor("T’ayn"), Is.EqualTo(new[] { "bee.v", "ayn.x" }));
    }

    [Test]
    public void NonCliticFormsHaveNoCliticCandidates()
    {
        var table = Table("ta\tbee.v\tbee\tinflected\tv.\tta\t");

        Assert.That(table.CliticCandidatesFor("aase"), Is.Empty);
        Assert.That(table.CliticCandidatesFor("ta"), Is.Empty); // no clitic pattern: direct lookup's job
    }

    /// <summary>The file order is the generator's, not an alphabet's ('yn nah' is
    /// row 2 of the real table): the accessor is what sorts</summary>
    [Test]
    public void AllDisplayLemmasAreSortedThoughTheFileIsNot()
    {
        var table = Table(
            "yn nah\tnah.n\tnah\tself\ts. f.\tyn nah\tdemutation-unvalidated",
            "aase\taase.n\taase\tself\ts. m.\taase\t",
            "daaney\tdaaney.a\tdaaney\tself\ta.\tdaaney\t");

        Assert.That(table.AllDisplayLemmas, Is.EqualTo(new[] { "aase", "daaney", "nah" }));
    }

    /// <summary>A form is linked once per way it hangs off the lemma: 'deiney' is
    /// both `inflected` and `plural` of dooinney, and that is two links, but a
    /// repeated row is not</summary>
    [Test]
    public void LinksOfCollectsAFormOncePerLinkType()
    {
        var table = Table(
            "dooinney\tdooinney.n\tdooinney\tself\ts. m.\tdooinney\t",
            "deiney\tdooinney.n\tdooinney\tinflected\ts. m.\tdooinney\t",
            "deiney\tdooinney.n\tdooinney\tplural\ts. m.\tdeiney\t",
            "deiney\tdooinney.n\tdooinney\tplural\ts. m.\tdeiney\t");

        Assert.That(table.LinksOf("dooinney")!.Links, Is.EquivalentTo(new[]
        {
            new LemmaLink("inflected", "deiney", false),
            new LemmaLink("plural", "deiney", false),
        }));
    }

    /// <summary>The lemma's own row draws no branch, but a hand-asserted one (the
    /// vocab supplement) makes the root itself a guess</summary>
    [Test]
    public void TheLemmasOwnRowDrawsNoBranchAndCanMakeTheRootAGuess()
    {
        var table = Table(
            "peiagh\tpeiagh.n\tpeiagh\tself\ts.\tpeiagh\tmodern-variant unverified",
            "peiaghyn\tpeiagh.n\tpeiagh\tinflected\ts.\tpeiagh\tunverified");

        var links = table.LinksOf("peiagh")!;
        Assert.Multiple(() =>
        {
            Assert.That(links.SelfUnverified, Is.True);
            Assert.That(links.Links, Is.EqualTo(new[] { new LemmaLink("inflected", "peiaghyn", true) }));
        });
    }

    [Test]
    public void APrintedSelfRowKeepsTheRootVerified()
    {
        Assert.That(Table("aase\taase.n\taase\tself\ts. m.\taase\t")
            .LinksOf("aase")!.SelfUnverified, Is.False);
    }

    /// <summary>Cregeen prints 'daase' as an entry of its own and the table files
    /// it under aase as `self`: another headword of the lexeme is a link the tree
    /// shows, unlike the lemma's own row</summary>
    [Test]
    public void AnotherHeadwordOfTheLexemeIsASelfLink()
    {
        var table = Table(
            "aase\taase.v\taase\tself\tv.\taase\t",
            "daase\taase.v\taase\tself\tv.\tdaase\t");

        Assert.That(table.LinksOf("aase")!.Links,
            Is.EqualTo(new[] { new LemmaLink("self", "daase", false) }));
    }

    /// <summary>A pair the print attests anywhere stays verified, however many
    /// rules also produce it — as <see cref="LemmaTable.IsUnverifiedLink"/> has it</summary>
    [Test]
    public void ALinkAnyRowAttestsStaysVerified()
    {
        var table = Table(
            "pheiagh\tpeiagh.n\tpeiagh\tmutation\ts.\tpeiagh\tgenerated-lenition",
            "pheiagh\tpeiagh.n\tpeiagh\tmutation\ts.\tpeiagh\t");

        Assert.That(table.LinksOf("peiagh")!.Links,
            Is.EqualTo(new[] { new LemmaLink("mutation", "pheiagh", false) }));
    }

    /// <summary>The lookup folds ('Aa-Aase' -> 'aa aase'), the answer keeps the
    /// printed spelling</summary>
    [Test]
    public void LinksOfNormalizesItsInputAndKeepsThePrintedSpelling()
    {
        var table = Table("aa aase\taa-aase.n\taa-aase\tself\ts. m.\taa-aase\t");

        Assert.That(table.LinksOf("Aa-Aase")!.Lemma, Is.EqualTo("aa-aase"));
    }

    [Test]
    public void AnUnknownLemmaHasNoLinks()
    {
        Assert.That(Table().LinksOf("xyzzy"), Is.Null);
    }

    /// <summary>The vendored table loads and covers the acceptance forms</summary>
    [Test]
    public void VendoredTableLoads()
    {
        var table = LemmaTable.Instance;

        Assert.That(table.FormCount, Is.GreaterThan(39_000));
        Assert.That(table.AllDisplayLemmas, Has.Count.GreaterThan(15_000));
        // the Phillips supplement's 1610 spellings hang off the classical lemma
        Assert.That(table.LinksOf("dooinney")!.Links,
            Does.Contain(new LemmaLink("phillips", "dwyne", false)));
        Assert.That(table.CandidatesFor("daase"), Does.Contain("aase.v"));
        Assert.That(table.CandidatesFor("aaseyn"), Does.Contain("aase.n"));
        // n'aase (the er n'aase participle) shares aase's lemma id, so a
        // lemma query for 'aase' reaches lines containing n'aase
        Assert.That(table.CandidatesFor("n'aase"), Does.Contain("aase.v"));
    }

    /// <summary>The names supplement loads beside the main table: candidates merge
    /// per form, and a bridge entry repeating a (form, lemmaId) pair stays one
    /// candidate with the main table's display</summary>
    [Test]
    public void ASupplementMergesIntoOneTable()
    {
        using var cregeen = new StringReader(
            "form\tlemmaId\tlemma\tlinkType\n"
            + "creest\tcreest.n\tCreest\tself\n"
            + "veg\tveg.x\tveg\tself\n");
        using var names = new StringReader(
            "form\tlemmaId\tlemma\tlinkType\tpos\n"
            + "creest\tcreest.n\tCreest\tself\tnp. personal\n" // bridge: same id
            + "chreest\tcreest.n\tCreest\tmutation\tnp. personal\n"
            + "doolish\tdoolish.np\tDoolish\tself\tnp. place\n");
        var table = LemmaTable.Load([cregeen, names]);

        Assert.That(table.CandidatesFor("creest"), Is.EqualTo(new[] { "creest.n" }));
        Assert.That(table.CandidatesFor("chreest"), Is.EqualTo(new[] { "creest.n" }));
        Assert.That(table.DisplayLemmasFor("chreest"), Is.EqualTo(new[] { "Creest" }));
        // the generated mutation is root-eligible: tapping Chreest offers Creest
        Assert.That(table.RootDisplayLemmasFor("chreest"), Is.EqualTo(new[] { "Creest" }));
        Assert.That(table.CandidatesFor("doolish"), Is.EqualTo(new[] { "doolish.np" }));
        Assert.That(table.CandidatesFor("veg"), Is.EqualTo(new[] { "veg.x" }));
    }
}
