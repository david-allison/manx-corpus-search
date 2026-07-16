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

    /// <summary>The vendored table loads and covers the acceptance forms</summary>
    [Test]
    public void VendoredTableLoads()
    {
        var table = LemmaTable.Instance;

        Assert.That(table.FormCount, Is.GreaterThan(39_000));
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
