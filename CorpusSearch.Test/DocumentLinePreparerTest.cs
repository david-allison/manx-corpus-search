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

    private static OpenSourceDocument ReferenceManifest(params string[] inlineReferences)
        => new()
        {
            Name = "doc",
            Ident = "doc",
            InlineReferences = inlineReferences.ToList(),
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
             "inlineSpeakerCodes": ["NM", "Q"], "manxColumnLanguage": "mixed",
             "referenceBook": "Matthew"}
            """;
        var document = JsonConvert.DeserializeObject<OpenSourceDocument>(json)!;
        Assert.That(document.InlineSpeakerCodes, Is.EqualTo(new[] { "NM", "Q" }));
        Assert.That(document.ManxColumnLanguage, Is.EqualTo("mixed"));
        Assert.That(document.ReferenceBook, Is.EqualTo("Matthew"));
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

    /// <summary>[MS 1 Thessalonians 2.16] Text... - the citation becomes line
    /// metadata and leaves the Manx token stream</summary>
    [Test]
    public void ABracketedCitationBecomesTheReference()
    {
        var line = Prepared(ReferenceManifest("bracketed-citation"),
            new DocumentLine { Manx = "[MS 1 Thessalonians 2.16] Liettal shin vei loayrt" });

        Assert.That(line.Reference, Is.EqualTo("MS 1 Thessalonians 2.16"));
        Assert.That(line.Manx, Is.EqualTo("Liettal shin vei loayrt"));
    }

    [Test]
    public void ABracketedVerseNumberBecomesTheReference()
    {
        var line = Prepared(ReferenceManifest("bracketed-number"),
            new DocumentLine { Manx = "[3] Ta'n Chiarn er my hroggal" });

        Assert.That(line.Reference, Is.EqualTo("3"));
        Assert.That(line.Manx, Is.EqualTo("Ta'n Chiarn er my hroggal"));
    }

    /// <summary>bracketed-number must not swallow citations or recording events</summary>
    [Test]
    public void ABracketedNumberFormatIgnoresNonNumbers()
    {
        var line = Prepared(ReferenceManifest("bracketed-number"),
            new DocumentLine { Manx = "[laughs] as eisht" });

        Assert.That(line.Reference, Is.Null);
        Assert.That(line.Manx, Is.EqualTo("[laughs] as eisht"));
    }

    /// <summary>1 Lioar Sheeloghe... - a bare verse number opens the cell</summary>
    [Test]
    public void ALeadingVerseNumberBecomesTheReference()
    {
        var line = Prepared(ReferenceManifest("leading-number"),
            new DocumentLine { Manx = "1 Lioar Sheeloghe Yeesey Creest, Mac Ghavid. " });

        Assert.That(line.Reference, Is.EqualTo("1"));
        Assert.That(line.Manx, Is.EqualTo("Lioar Sheeloghe Yeesey Creest, Mac Ghavid. "));
    }

    /// <summary>8. Baniít ta yn dwyne... - the Phillips psalter's verse
    /// numbers carry a period</summary>
    [Test]
    public void AVerseNumberWithAPeriodBecomesTheReference()
    {
        var line = Prepared(ReferenceManifest("leading-number"),
            new DocumentLine { Manx = "8. Baniít ta yn dwyne" });

        Assert.Multiple(() =>
        {
            Assert.That(line.Reference, Is.EqualTo("8"));
            Assert.That(line.Manx, Is.EqualTo("Baniít ta yn dwyne"));
        });
    }

    /// <summary>Beatus vir qui non abiit. psal. 1. - the whole cell is a Latin
    /// incipit heading: reference-only, so the Latin leaves the Manx stream</summary>
    [Test]
    public void ALatinIncipitHeadingBecomesAReferenceOnlyRow()
    {
        var line = Prepared(ReferenceManifest("incipit-psalm-heading"),
            new DocumentLine { Manx = "Beatus vir qui non abiit. psal. 1." });

        Assert.Multiple(() =>
        {
            Assert.That(line.Reference, Is.EqualTo("Beatus vir qui non abiit. psal. 1"));
            Assert.That(line.Manx, Is.Empty);
        });
    }

    /// <summary>Psal. 1. Beatus vir, qui non abiit. - the 1765 psalter puts the
    /// psalm number before the incipit instead of after it</summary>
    [Test]
    public void AnIncipitHeadingMayLeadWithThePsalmNumber()
    {
        var line = Prepared(ReferenceManifest("incipit-psalm-heading"),
            new DocumentLine { Manx = "Psal. 1. Beatus vir, qui non abiit." });

        Assert.Multiple(() =>
        {
            Assert.That(line.Reference, Is.EqualTo("Psal. 1. Beatus vir, qui non abiit"));
            Assert.That(line.Manx, Is.Empty);
        });
    }

    /// <summary>An ordinary verse cell must not read as a leading incipit</summary>
    [Test]
    public void AnIncipitFormatLeavesVerseCellsAlone()
    {
        var line = Prepared(ReferenceManifest("incipit-psalm-heading", "leading-number"),
            new DocumentLine { Manx = "2. Agh ta e yeearree ayns leigh yn Chiarn" });

        Assert.Multiple(() =>
        {
            Assert.That(line.Reference, Is.EqualTo("2"));
            Assert.That(line.Manx, Is.EqualTo("Agh ta e yeearree ayns leigh yn Chiarn"));
        });
    }

    /// <summary>End to end through Prepare: extraction hands the resolver its
    /// Reference strings, and the canonical keys come out the other side</summary>
    [Test]
    public void PreparedLinesCarryCanonicalReferences()
    {
        var document = ReferenceManifest("heading-line", "leading-number");
        document.ReferenceBook = "Matthew";
        var heading = new DocumentLine { Manx = "CAB. II." };
        var verse = new DocumentLine { Manx = "1 As tra rug Yeesey ayns Bethlehem" };
        DocumentLinePreparer.Prepare(document, [heading, verse]);

        Assert.Multiple(() =>
        {
            Assert.That(heading.CanonicalReference, Is.EqualTo("matthew.2"));
            Assert.That(verse.CanonicalReference, Is.EqualTo("matthew.2.1"));
        });
    }

    /// <summary>"19 ¶ " - the KJV pilcrow rides with the number, and the English
    /// column repeats the marker: both cells come out clean</summary>
    [Test]
    public void APilcrowRidesWithTheVerseNumberInBothColumns()
    {
        var line = Prepared(ReferenceManifest("leading-number"), new DocumentLine
        {
            Manx = "19 ¶ Eisht Joseph e sheshey, va dooinney cairagh",
            English = "19 ¶ Then Joseph her husband, being a just man",
        });

        Assert.Multiple(() =>
        {
            Assert.That(line.Reference, Is.EqualTo("19"));
            Assert.That(line.Manx, Is.EqualTo("Eisht Joseph e sheshey, va dooinney cairagh"));
            Assert.That(line.English, Is.EqualTo("Then Joseph her husband, being a just man"));
        });
    }

    /// <summary>Genesis:1:1. Text... - the P Kelly Bible import's verse markers</summary>
    [Test]
    public void AColonVerseMarkerBecomesTheReference()
    {
        var line = Prepared(ReferenceManifest("colon-verse"),
            new DocumentLine { Manx = "Genesis:1:1. Ayns y toshiaght chroo Jee niau as thalloo." });

        Assert.That(line.Reference, Is.EqualTo("Genesis:1:1"));
        Assert.That(line.Manx, Is.EqualTo("Ayns y toshiaght chroo Jee niau as thalloo."));
    }

    [Test]
    public void AColonVerseBookNameMayContainSpaces()
    {
        var line = Prepared(ReferenceManifest("colon-verse"),
            new DocumentLine { Manx = "Arrane Solomon:2:3. Myr y billey-ooyl" });

        Assert.That(line.Reference, Is.EqualTo("Arrane Solomon:2:3"));
        Assert.That(line.Manx, Is.EqualTo("Myr y billey-ooyl"));
    }

    /// <summary>A whole-cell chapter heading becomes a reference-only row</summary>
    [Test]
    public void AHeadingLineBecomesAReferenceOnlyRow()
    {
        var line = Prepared(ReferenceManifest("heading-line"),
            new DocumentLine { Manx = "CAB. II." });

        Assert.That(line.Reference, Is.EqualTo("CAB. II."));
        Assert.That(line.Manx, Is.Empty);
    }

    [Test]
    public void AHeadingFormatLeavesOrdinaryLinesAlone()
    {
        var line = Prepared(ReferenceManifest("heading-line"),
            new DocumentLine { Manx = "Cabdil dy row ayn" });

        Assert.That(line.Reference, Is.Null);
        Assert.That(line.Manx, Is.EqualTo("Cabdil dy row ayn"));
    }

    [Test]
    public void AFilledReferenceColumnWinsOverTheInlineMarker()
    {
        var line = Prepared(ReferenceManifest("bracketed-number"),
            new DocumentLine { Manx = "[3] Text", Reference = "Psalm 23:3" });

        Assert.That(line.Reference, Is.EqualTo("Psalm 23:3"));
        Assert.That(line.Manx, Is.EqualTo("Text"));
    }

    /// <summary>An undeclared manifest leaves every line untouched: the no-op default</summary>
    [Test]
    public void NoDeclaredFormatsIsANoOp()
    {
        var line = Prepared(Manifest(),
            new DocumentLine { Manx = "[MS 1 Thessalonians 2.16] Liettal shin" });

        Assert.That(line.Reference, Is.Null);
        Assert.That(line.Manx, Is.EqualTo("[MS 1 Thessalonians 2.16] Liettal shin"));
    }

    /// <summary>Dramatic speakers arrive bracketed ([SATAN] in Pargeiys Caillit)</summary>
    [Test]
    public void ABracketedSpeakerCodeIsExtracted()
    {
        var line = Prepared(Manifest(null, "SATAN", "GOD"),
            new DocumentLine { Manx = "[SATAN] Cre'n ynnyd shoh" });

        Assert.That(line.Speaker, Is.EqualTo("SATAN"));
        Assert.That(line.Manx, Is.EqualTo("Cre'n ynnyd shoh"));
    }

    /// <summary>A mid-text citation ("(Rom. ii. 4)" in the Homilies) leaves the
    /// statistics text on every document - no manifest needed, the book registry
    /// is the disambiguator - while the displayed Manx keeps it</summary>
    [Test]
    public void AMidTextCitationLeavesTheStatisticsText()
    {
        var line = Prepared(Manifest(),
            new DocumentLine { Manx = "son e vyghin (Rom. ii. 4) as e ghrayse" });

        Assert.Multiple(() =>
        {
            Assert.That(line.Manx, Is.EqualTo("son e vyghin (Rom. ii. 4) as e ghrayse"));
            Assert.That(line.StatsManx, Is.EqualTo("son e vyghin   as e ghrayse"));
            Assert.That(line.NormalizedStatsManx, Does.Not.Contain("rom"));
        });
    }

    [Test]
    public void AnOrdinaryLineHasNoSeparateStatsText()
    {
        var line = Prepared(Manifest(), new DocumentLine { Manx = "Ta fys aym (dy jarroo)" });

        Assert.That(line.StatsManx, Is.Null);
        Assert.That(line.NormalizedStatsManx, Is.EqualTo(line.NormalizedManx));
    }
}
