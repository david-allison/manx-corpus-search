using System.IO;
using System.Linq;
using CorpusSearch.Dependencies.CsvHelper;
using CorpusSearch.Model;
using CorpusSearch.Services;
using Newtonsoft.Json;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>
/// Load-time resolution of the manifest's language contract: each line's effective
/// language resolves as line value ⇒ collection default ⇒ "gv".
/// </summary>
[TestFixture]
public class DocumentLinePreparerTest
{
    private static OpenSourceDocument Manifest(string? manxColumnLanguage = null)
        => new()
        {
            Name = "doc",
            Ident = "doc",
            ManxColumnLanguage = manxColumnLanguage,
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
    public void ManifestFieldDeserializes()
    {
        const string json = """
            {"name": "n", "ident": "i", "translated": "Rob Teare 2021", "manxColumnLanguage": "mixed"}
            """;
        var document = JsonConvert.DeserializeObject<OpenSourceDocument>(json)!;
        Assert.That(document.ManxColumnLanguage, Is.EqualTo("mixed"));
        // the new field binds to a property, not the extension data shown to users
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
