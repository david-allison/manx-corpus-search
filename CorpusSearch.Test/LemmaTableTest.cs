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

    [Test]
    public void LemmaIdsAreRecognised()
    {
        var table = Table("aase\taase.v\taase\tself\tv.\taase\t");

        Assert.That(table.IsLemmaId("aase.v"), Is.True);
        Assert.That(table.IsLemmaId("aase"), Is.False);
    }

    /// <summary>The vendored table loads and covers the acceptance forms</summary>
    [Test]
    public void VendoredTableLoads()
    {
        var table = LemmaTable.Instance;

        Assert.That(table.FormCount, Is.GreaterThan(39_000));
        Assert.That(table.CandidatesFor("daase"), Does.Contain("aase.v"));
        Assert.That(table.CandidatesFor("aaseyn"), Does.Contain("aase.n"));
        // 'aase' also carries the mutation candidates which n'aase resolves to,
        // so a lemma query for 'aase' reaches lines containing n'aase
        Assert.That(table.CandidatesFor("aase"), Does.Contain("n'aase.v"));
        Assert.That(table.CandidatesFor("n'aase"), Does.Contain("n'aase.v"));
    }
}
