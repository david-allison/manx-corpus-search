using System.IO;
using System.Linq;
using CorpusSearch.Dependencies.CsvHelper;
using CorpusSearch.Model;
using CorpusSearch.Services;
using Newtonsoft.Json;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>
/// Load-time honouring of the manifest's speaker-code and language contract:
/// inline speaker codes move into Speaker, and each line's effective language
/// resolves as line value ⇒ collection default ⇒ "gv".
/// </summary>
[TestFixture]
public class DocumentLinePreparerTest
{
    private static OpenSourceDocument Manifest(string? manxColumnLanguage = null, params string[] inlineSpeakerCodes)
        => new()
        {
            Name = "doc",
            Ident = "doc",
            ManxColumnLanguage = manxColumnLanguage,
            InlineSpeakerCodes = inlineSpeakerCodes.Length == 0 ? null : inlineSpeakerCodes.ToList(),
        };

    private static DocumentLine Prepared(Document document, DocumentLine line)
    {
        DocumentLinePreparer.Prepare(document, [line]);
        return line;
    }

    [Test]
    public void LanguageDefaultsToManx()
    {
        var line = Prepared(Manifest(), new DocumentLine { Manx = "ta" });
        Assert.That(line.Language, Is.EqualTo("gv"));
        Assert.That(line.IsManxLanguage, Is.True);
    }

    [Test]
    public void ManifestDefaultAppliesToUnmarkedLines()
    {
        var line = Prepared(Manifest(manxColumnLanguage: "en"), new DocumentLine { Manx = "the cat" });
        Assert.That(line.Language, Is.EqualTo("en"));
        Assert.That(line.IsManxLanguage, Is.False);
    }

    [Test]
    public void LineValueBeatsManifestDefault()
    {
        var line = Prepared(Manifest(manxColumnLanguage: "mixed"), new DocumentLine { Manx = "ta", Language = "gv" });
        Assert.That(line.Language, Is.EqualTo("gv"));
        Assert.That(line.IsManxLanguage, Is.True);
    }

    [Test]
    public void LanguageValuesAreNormalized()
    {
        var line = Prepared(Manifest(), new DocumentLine { Manx = "the cat", Language = " EN " });
        Assert.That(line.Language, Is.EqualTo("en"));

        var blank = Prepared(Manifest(manxColumnLanguage: "la"), new DocumentLine { Manx = "", Language = "  " });
        Assert.That(blank.Language, Is.EqualTo("la"));
    }

    [Test]
    public void SpeakerCodeMovesToSpeakerField()
    {
        var line = Prepared(Manifest(null, "NM", "WR"), new DocumentLine { Manx = "NM. Ta fys aym" });
        Assert.That(line.Speaker, Is.EqualTo("NM"));
        Assert.That(line.Manx, Is.EqualTo("Ta fys aym"));
    }

    [TestCase("nm: ta", "ta")]
    [TestCase("NM ta", "ta")]
    [TestCase("  NM.ta", "ta")]
    public void ColonBareAndLowercaseMarkersAreStripped(string manx, string expected)
    {
        var line = Prepared(Manifest(null, "NM"), new DocumentLine { Manx = manx });
        Assert.That(line.Speaker, Is.EqualTo("NM"));
        Assert.That(line.Manx, Is.EqualTo(expected));
    }

    [Test]
    public void CodeOnlyCellBecomesEmpty()
    {
        var line = Prepared(Manifest(null, "NM"), new DocumentLine { Manx = "NM." });
        Assert.That(line.Speaker, Is.EqualTo("NM"));
        Assert.That(line.Manx, Is.EqualTo(""));
    }

    [Test]
    public void CodeMustBeAWholeWord()
    {
        var line = Prepared(Manifest(null, "Q"), new DocumentLine { Manx = "Quirk as Juan" });
        Assert.That(line.Speaker, Is.Null);
        Assert.That(line.Manx, Is.EqualTo("Quirk as Juan"));
    }

    [Test]
    public void NoDeclaredCodesMeansNoStripping()
    {
        var line = Prepared(Manifest(), new DocumentLine { Manx = "NM. Ta fys aym" });
        Assert.That(line.Speaker, Is.Null);
        Assert.That(line.Manx, Is.EqualTo("NM. Ta fys aym"));
    }

    [Test]
    public void FilledSpeakerColumnIsKept()
    {
        var line = Prepared(Manifest(null, "NM"), new DocumentLine { Manx = "NM. Ta", Speaker = "Ned Maddrell" });
        Assert.That(line.Speaker, Is.EqualTo("Ned Maddrell"));
        Assert.That(line.Manx, Is.EqualTo("Ta"));
    }

    [Test]
    public void LongerCodeWinsOverItsPrefix()
    {
        var line = Prepared(Manifest(null, "J", "JTK"), new DocumentLine { Manx = "JTK: ta" });
        Assert.That(line.Speaker, Is.EqualTo("JTK"));
        Assert.That(line.Manx, Is.EqualTo("ta"));
    }

    [Test]
    public void ManifestFieldsDeserialize()
    {
        const string json = """
            {"name": "n", "ident": "i", "translated": "Rob Teare 2021",
             "inlineSpeakerCodes": ["NM", "Q"], "manxColumnLanguage": "mixed"}
            """;
        var document = JsonConvert.DeserializeObject<OpenSourceDocument>(json)!;
        Assert.That(document.InlineSpeakerCodes, Is.EqualTo(new[] { "NM", "Q" }));
        Assert.That(document.ManxColumnLanguage, Is.EqualTo("mixed"));
        // the new fields bind to properties, not the extension data shown to users
        Assert.That(document.ExtensionData.Keys, Is.EquivalentTo(new[] { "translated" }));
    }

    /// <summary>The CSV contract: the sparse column is ManxColumnLanguage, like the
    /// manifest field it overrides — not a bare "Language"</summary>
    [Test]
    public void LanguageIsReadFromTheManxColumnLanguageCsvColumn()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path,
                "English,Manx,ManxColumnLanguage\n" +
                "I know,Ta fys aym,\n" +
                "untranslated,the cat,en\n");

            var lines = CsvHelperUtils.LoadCsv(path);

            // a blank cell reads as "": Prepare() later replaces it with the default
            Assert.That(lines.Select(x => x.Language), Is.EqualTo(new[] { "", "en" }));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
