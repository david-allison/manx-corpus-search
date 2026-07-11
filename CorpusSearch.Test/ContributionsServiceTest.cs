using System.Collections.Generic;
using System.Linq;
using CorpusSearch.Service;
using CorpusSearch.Services;
using Newtonsoft.Json;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>
/// The contributions leaderboard is scraped from free-form manifest metadata (#347):
/// inconsistent key casing, one person under several names, and status values
/// ("Not yet") sharing keys with people. All example values below occur in OpenData.
/// </summary>
[TestFixture]
public class ContributionsServiceTest
{
    private WorkService workService = null!;
    private ContributionsService service = null!;

    [SetUp]
    public void SetUp()
    {
        workService = new WorkService();
        service = new ContributionsService(workService);
    }

    /// <summary>Documents are added via JSON, as in production: extension values are Newtonsoft tokens</summary>
    private void AddDocument(string ident, string metadataJson)
    {
        var json = $"{{\"name\": \"Document {ident}\", \"ident\": \"{ident}\", {metadataJson}}}";
        workService.AddWork(JsonConvert.DeserializeObject<OpenSourceDocument>(json)!);
    }

    private List<ContributionsService.Contributor> GetContributors() =>
        service.GetContributors().Result;

    [Test]
    public void InitialsAndNameVariantsAreOnePerson()
    {
        AddDocument("a", "\"transcribed by\": \"RT\"");
        AddDocument("b", "\"Transcriber\": \"R. Teare\"");
        AddDocument("c", "\"Translated by\": \"R. Teare (suggested translation)\"");

        var contributors = GetContributors();

        Assert.That(contributors, Has.Count.EqualTo(1));
        Assert.That(contributors[0].Name, Is.EqualTo("Rob Teare"));
        Assert.That(contributors[0].DocumentCount, Is.EqualTo(3));
        Assert.That(contributors[0].Roles, Is.EqualTo(new Dictionary<string, int>
        {
            ["Transcription"] = 2,
            ["Translation"] = 1,
        }));
    }

    [TestCase("translator")]
    [TestCase("Translator")]
    [TestCase("Translated by")]
    [TestCase("translated By")]
    public void KeyCasingVariantsAreOneRole(string key)
    {
        AddDocument("a", $"\"{key}\": \"Brian Stowell\"");

        var contributor = GetContributors().Single();
        Assert.That(contributor.Roles.Keys, Is.EqualTo(new[] { "Translation" }));
    }

    [Test]
    public void UnmappedNamesAppearVerbatim()
    {
        AddDocument("a", "\"translator\": \"Ivon Mosely\"");

        Assert.That(GetContributors().Single().Name, Is.EqualTo("Ivon Mosely"));
    }

    [TestCase("\"translator\": \"unknown\"")]
    [TestCase("\"translator\": \"unknown — likely J. Kewley\"")]
    [TestCase("\"Proofread\": \"Not yet\"")]
    [TestCase("\"Proofread\": \"English; Not yet. Manx; Not yet\"")]
    // the author wrote the text; they didn't contribute it to the corpus
    [TestCase("\"author\": \"Edward Faragher\"")]
    // citations and attribution notes are not names
    [TestCase("\"translator\": \"W.Q., Castletown; Isle of Man Examiner, Saturday, February 04 - March 04, 1899; Page: 6.\"")]
    [TestCase("\"translator\": \"The first draft is traditionally accredited to William Walker 1679-1729, from Ballaugh\"")]
    public void ValuesWhichNameNobodyCreditNobody(string metadataJson)
    {
        AddDocument("a", metadataJson);

        Assert.That(GetContributors(), Is.Empty);
    }

    // People from the era of the texts: authors and translators of the source
    // material, not volunteers who put it into the corpus
    [TestCase("\"translator\": \"Edward Faragher\"")]
    [TestCase("\"Translator\": \"E. Faragher\"")]
    [TestCase("\"translator\": \"S. Morrison.\"")]
    [TestCase("\"editor\": \"Morrison. S & Morrison. L.\"")]
    [TestCase("\"translator\": \"A.W. Moore\"")]
    [TestCase("\"translator\": \"The Rev. Hugh Stowell of Ballaugh.\"")]
    [TestCase("\"translator\": \"Isle of Man Examiner\"")]
    public void HistoricalCreditsAreNotContributors(string metadataJson)
    {
        AddDocument("a", metadataJson);

        Assert.That(GetContributors(), Is.Empty);
    }

    // the Coraa ny Gael-style keys: noun form, no "by"
    [TestCase("\"transcription\": \"R. Teare\"", "Transcription")]
    [TestCase("\"transcribed\": \"R. Teare\"", "Transcription")]
    [TestCase("\"translation\": \"R. Teare\"", "Translation")]
    [TestCase("\"translated\": \"R. Teare\"", "Translation")]
    [TestCase("\"Standardisation\": \"Robert Teare\"", "Standardisation")]
    [TestCase("\"uploaded by\": \"Rob Teare\"", "Upload")]
    public void NounFormKeysCredit(string metadataJson, string expectedRole)
    {
        AddDocument("a", metadataJson);

        var contributor = GetContributors().Single();
        Assert.That(contributor.Name, Is.EqualTo("Rob Teare"));
        Assert.That(contributor.Roles.Keys, Is.EqualTo(new[] { expectedRole }));
    }

    [TestCase("\"translated\": \"Rob Teare 2021\"", "Rob Teare")]
    [TestCase("\"translation\": \"R.Teare 2021\"", "Rob Teare")]
    [TestCase("\"transcribed\": \"Tim Swales (2025)\"", "Tim Swales")]
    [TestCase("\"transcription\": \"Rob Teare 2021 (Suggested translation)\"", "Rob Teare")]
    [TestCase("\"transcribed\": \"Walter Clarke, Ramsey (2003)\"", "Walter Clarke")]
    [TestCase("\"translated\": \"Fiona McArdle, Kirk Michael\"", "Fiona McArdle")]
    public void TrailingYearsAndQualifiersAreStripped(string metadataJson, string expected)
    {
        AddDocument("a", metadataJson);

        Assert.That(GetContributors().Single().Name, Is.EqualTo(expected));
    }

    [Test]
    public void CommaSeparatedPeopleAreEachCredited()
    {
        AddDocument("a", "\"translated\": \"Paul Rogers, Christopher Lewin\"");

        var names = GetContributors().Select(x => x.Name);
        Assert.That(names, Is.EquivalentTo(new[] { "Paul Rogers", "Christopher Lewin" }));
    }

    [Test]
    public void PublishedSourceCitationCreditsBothScholars()
    {
        AddDocument("a", "\"transcription\": \"Broderick (1981), Lewin (2014)\"");

        var names = GetContributors().Select(x => x.Name);
        Assert.That(names, Is.EquivalentTo(new[] { "George Broderick", "Christopher Lewin" }));
    }

    [Test]
    public void DigitisationCreditsBothScholars()
    {
        AddDocument("a", "\"digitisation\": \"Broderick (1981), Lewin (2014) text 9\"");

        var contributors = GetContributors();
        Assert.That(contributors.Select(x => x.Name),
            Is.EquivalentTo(new[] { "George Broderick", "Christopher Lewin" }));
        Assert.That(contributors[0].Roles.Keys, Is.EqualTo(new[] { "Digitisation" }));
    }

    [Test]
    public void CombinedRoleKeyCreditsBothRoles()
    {
        AddDocument("a", "\"transcribed & translated\": \"Rob Teare & Max Wheeler 2021\"");

        var contributors = GetContributors();
        Assert.That(contributors.Select(x => x.Name),
            Is.EquivalentTo(new[] { "Rob Teare", "Max Wheeler" }));
        Assert.That(contributors[0].Documents.Single().Roles,
            Is.EqualTo(new[] { "Transcription", "Translation" }));
    }

    [TestCase("\"transcribed\": \"Walter Clarke, Ramsey (2003); Christoper Lewin (2024)\"")]
    public void CompoundValuesCreditEachKnownPerson(string metadataJson)
    {
        AddDocument("a", metadataJson);

        var names = GetContributors().Select(x => x.Name);
        Assert.That(names, Is.EquivalentTo(new[] { "Walter Clarke", "Christopher Lewin" }));
    }

    [Test]
    public void RoleLabelsLeftBySplittingAreNotNames()
    {
        AddDocument("a", "\"translated\": \"Transcribed & Translated; R.Teare\"");

        Assert.That(GetContributors().Single().Name, Is.EqualTo("Rob Teare"));
    }

    [TestCase("\"translation\": \"accompanies original\"")]
    [TestCase("\"translation\": \"in original\"")]
    [TestCase("\"translation\": \"authors\"")]
    [TestCase("\"translated\": \"Unspecified, likely Walter Clarke, Ramsey\"")]
    [TestCase("\"translation\": \"J.J. Kneen\"")]
    [TestCase("\"translation\": \"John Gell (Introduction by F. B. Kelly)\"")]
    public void ProseAndSourceEraValuesCreditNobody(string metadataJson)
    {
        AddDocument("a", metadataJson);

        Assert.That(GetContributors(), Is.Empty);
    }

    [Test]
    public void ColonStructuredValuesOnlyCreditKnownAliases()
    {
        AddDocument("a", "\"transcribed\": \"English: © F.Coakley. Manx: R. Teare\"");

        Assert.That(GetContributors().Single().Name, Is.EqualTo("Rob Teare"));
    }

    [Test]
    public void AliasEmbeddedInAStatusValueIsCredited()
    {
        AddDocument("a", "\"Proofread\": \"English; RT. Manx; Not yet\"");

        var contributor = GetContributors().Single();
        Assert.That(contributor.Name, Is.EqualTo("Rob Teare"));
        Assert.That(contributor.Roles.Keys, Is.EqualTo(new[] { "Proofreading" }));
    }

    [Test]
    public void AmpersandCreditsEachPerson()
    {
        AddDocument("a", "\"editor\": \"Thomm Beg & Chris Sheard\"");

        var names = GetContributors().Select(x => x.Name);
        Assert.That(names, Is.EquivalentTo(new[] { "Thomm Beg", "Chris Sheard" }));
    }

    [Test]
    public void SentenceEndingPeriodIsNotANameVariant()
    {
        AddDocument("a", "\"translator\": \"Thomm Beg.\"");
        AddDocument("b", "\"translator\": \"Thomm Beg\"");

        var contributor = GetContributors().Single();
        Assert.That(contributor.Name, Is.EqualTo("Thomm Beg"));
        Assert.That(contributor.DocumentCount, Is.EqualTo(2));
    }

    [Test]
    public void MultipleRolesOnOneDocumentCountItOnce()
    {
        AddDocument("a", "\"Translator\": \"RT\", \"Transcriber\": \"RT\", \"Standardised Manx\": \"RT\"");

        var contributor = GetContributors().Single();
        Assert.That(contributor.DocumentCount, Is.EqualTo(1));
        Assert.That(contributor.Documents.Single().Roles,
            Is.EqualTo(new[] { "Standardisation", "Transcription", "Translation" }));
    }

    [Test]
    public void LeaderboardOrdersByDocumentCountThenName()
    {
        AddDocument("a", "\"translator\": \"Thomm Beg\"");
        AddDocument("b", "\"translator\": \"Thomm Beg\"");
        AddDocument("c", "\"translator\": \"G. Killey\"");
        AddDocument("d", "\"translator\": \"Brian Stowell\"");

        var names = GetContributors().Select(x => x.Name);
        Assert.That(names, Is.EqualTo(new[] { "Thomm Beg", "Brian Stowell", "G. Killey" }));
    }

    private static List<string> NamesFromLicense(string licenseText) =>
        ContributionsService.ParseLicenseCredits(licenseText)
            .SelectMany(x => ContributionsService.ExtractNames(x.Value))
            .Distinct()
            .ToList();

    [Test]
    public void LicenseSectionsAttributeWork()
    {
        var credits = ContributionsService.ParseLicenseCredits(
            "## Original Manx\n" +
            "This work was published before January 1, 1926, and is in the public domain worldwide because the author died at least 100 years ago.\n" +
            "\n" +
            "## English\n" +
            "Rob Teare 2023\n" +
            "\n" +
            "## Transcription\n" +
            "By Rob Teare 2025\n").ToList();

        Assert.That(credits, Is.EqualTo(new[]
        {
            (new[] { "Translation" }, "Rob Teare"),
            (new[] { "Transcription" }, "Rob Teare"),
        }));
    }

    [TestCase("## English\nCopyright (c) 2021 Rob Teare \n", "Rob Teare")]
    [TestCase("## Transcription\nMax Wheeler August 2021\n", "Max Wheeler")]
    [TestCase("## Transcription\nEdited and set alongside the English by Max W. Wheeler August, 2021\n", "Max Wheeler")]
    [TestCase("## English\nEdition and translation by Christopher Lewin (2025)\n", "Christopher Lewin")]
    [TestCase("## Transcription\nPhil Kelly - https://archive.gaelg.im/www.gaelg.iofm.net/ARTICLE/LQ/LTQ1.html\n", "Phil Kelly")]
    [TestCase("## English\nWith thanks to Tim Swales 2025\n", "Tim Swales")]
    [TestCase("## Transcription\nMax W. Wheeler\nRamsey, August 2021\n", "Max Wheeler")]
    [TestCase("## English\nMax W. Wheeler\nJune 2021\n", "Max Wheeler")]
    [TestCase("## English\nPhil Key / Google Translate 2024\n", "Phil Kelly")]
    public void LicenseAttributionStylesAreRecognised(string licenseText, string expected)
    {
        Assert.That(NamesFromLicense(licenseText), Is.EqualTo(new[] { expected }));
    }

    [Test]
    public void LicenseInitialsVariantsMerge()
    {
        Assert.That(NamesFromLicense("## English\nR.Teare & C.Sheard 2020\n"),
            Is.EquivalentTo(new[] { "Rob Teare", "Chris Sheard" }));
    }

    [TestCase("## Original Manx & English Summaries\nRob Teare 2023\n")] // about the source text
    [TestCase("## English\nText is licensed under the Isle of Man Open Government Licence For Public Sector Information <https://www.gov.im/about-this-site/open-government-licence/>\n")]
    [TestCase("## Transcription\nManx Language Society\n")]
    [TestCase("## English\nAttribution: Celtic League (http://www.celticleague.net/carn/)\n")]
    [TestCase("## English\nDocument provided in 2023\n")]
    [TestCase("## Manx\nWilliam Crebbin 1762\n")] // the historical translator, not corpus work
    public void LicenseBoilerplateCreditsNobody(string licenseText)
    {
        Assert.That(NamesFromLicense(licenseText), Is.Empty);
    }

    [Test]
    public void DocumentsAreListedWithNameAndRoles()
    {
        AddDocument("skeeal", "\"transcribed by\": \"RT\"");

        var document = GetContributors().Single().Documents.Single();
        Assert.That(document.Ident, Is.EqualTo("skeeal"));
        Assert.That(document.Name, Is.EqualTo("Document skeeal"));
        Assert.That(document.Roles, Is.EqualTo(new[] { "Transcription" }));
    }
}
