using System.Linq;
using CorpusSearch.Model;
using CorpusSearch.Services;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>
/// The structured half of verse references: opaque Reference strings resolve to
/// canonical "book.chapter[.verse]" keys — the cross-version identity a verse
/// shares across every translation of it.
/// </summary>
[TestFixture]
public class ReferenceResolverTest
{
    private static OpenSourceDocument Doc(string? referenceBook = null) => new()
    {
        Name = "doc",
        Ident = "doc",
        ReferenceBook = referenceBook,
    };

    private static string?[] Resolved(OpenSourceDocument document, params string[] references)
    {
        var lines = references.Select(r => new DocumentLine { Manx = "ta", Reference = r }).ToList();
        ReferenceResolver.Resolve(document, lines);
        return lines.Select(l => l.CanonicalReference).ToArray();
    }

    // --- the book registry ---

    [TestCase("Mian", "matthew")]
    [TestCase("Psalmyn", "psalms")]
    [TestCase("Ashlish", "revelation")]
    [TestCase("Jannoo", "acts")]
    [TestCase("I. Reeaghyn", "1-kings")]
    [TestCase("II. Recortyssyn", "2-chronicles")]
    [TestCase("III. Ean", "3-john")]
    [TestCase("Arrane Solomon", "song-of-solomon")]
    [TestCase("Raaghyn Creeney", "proverbs")]
    [TestCase("Screeuyn Yamys", "james")]
    public void TheManxBibleNamesAreKnown(string name, string id)
    {
        Assert.That(BibleBooks.Find(name)?.Id, Is.EqualTo(id));
    }

    [TestCase("Gen.", "genesis")]
    [TestCase("Jud.", "judges")]
    [TestCase("Jude", "jude")]
    [TestCase("1 Thess.", "1-thessalonians")]
    [TestCase("Phil.", "philippians")]
    [TestCase("Philem.", "philemon")]
    [TestCase("H[a]b.", "habakkuk")]
    [TestCase("Ecclesiast[es]", "ecclesiastes")]
    [TestCase("psalm", "psalms")]
    public void CitationAbbreviationsAreKnown(string name, string id)
    {
        Assert.That(BibleBooks.Find(name)?.Id, Is.EqualTo(id));
    }

    [TestCase("Methodist Hymn Book")]
    [TestCase("Aght Giare")]
    [TestCase("")]
    [TestCase(null)]
    public void NamesOutsideTheCanonAreNot(string? name)
    {
        Assert.That(BibleBooks.Find(name), Is.Null);
    }

    // --- self-contained forms ---

    [Test]
    public void AColonVerseResolvesThroughTheManxBookName()
    {
        Assert.That(Resolved(Doc(), "Mian:1:1"), Is.EqualTo(new[] { "matthew.1.1" }));
    }

    [Test]
    public void AColonVerseBookMayContainSpaces()
    {
        Assert.That(Resolved(Doc(), "Arrane Solomon:2:3"), Is.EqualTo(new[] { "song-of-solomon.2.3" }));
    }

    [TestCase("MS 1 Thessalonians 2.16", "1-thessalonians.2.16")]
    [TestCase("Jud. xii. 6", "judges.12.6")]
    [TestCase("Luke xiii, 16:", "luke.13.16")]
    [TestCase("2 Kings xi. 2", "2-kings.11.2")]
    [TestCase("H[a]b. ii. 11:", "habakkuk.2.11")]
    [TestCase("Zech. xi. 16:", "zechariah.11.16")]
    [TestCase("Methodist Hymn Book, lx. 5", null)]
    [TestCase("Hymn 54:", null)]
    [TestCase("title struck out", null)]
    public void ACitationResolvesWithoutAnyContext(string citation, string? expected)
    {
        Assert.That(Resolved(Doc(), citation), Is.EqualTo(new[] { expected }));
    }

    [Test]
    public void ACitationVerseRangeKeepsItsFirstVerse()
    {
        Assert.That(ReferenceResolver.TryParseCitation("Rom. i. 29-30")?.Key, Is.EqualTo("romans.1.29"));
    }

    /// <summary>Cregeen's house abbreviations (Psl., Pro., Ez.) and his prefixed
    /// citations (See Exod. ...) resolve; his OED/EDD references and Apocrypha
    /// citations must not</summary>
    [TestCase("Psl. xlv. 5", "psalms.45.5")]
    [TestCase("Pro. xxi. 15", "proverbs.21.15")]
    [TestCase("Ez. xiii. 2", "ezekiel.13.2")]
    [TestCase("See Exod. iii. 14", "exodus.3.14")]
    [TestCase("Cf. Acts xxvii. 29", "acts.27.29")]
    [TestCase("2 King xi. 2", "2-kings.11.2")]
    [TestCase("OED squat 1725, 4", null)]
    [TestCase("Ecclesiasticus xliv. 9", null)]
    public void CregeensCitationStylesResolve(string citation, string? expected)
    {
        Assert.That(ReferenceResolver.TryParseCitation(citation)?.Key, Is.EqualTo(expected));
    }

    // --- headings and the context they open ---

    [Test]
    public void AHeadingOpensTheChapterItsVerseNumbersCountIn()
    {
        var keys = Resolved(Doc(referenceBook: "Matthew"), "CAB. II.", "1", "2");
        Assert.That(keys, Is.EqualTo(new[] { "matthew.2", "matthew.2.1", "matthew.2.2" }));
    }

    [Test]
    public void ANewHeadingReplacesTheChapter()
    {
        var keys = Resolved(Doc(referenceBook: "Matthew"), "CAB. I.", "3", "CAB. II.", "3");
        Assert.That(keys, Is.EqualTo(new[] { "matthew.1", "matthew.1.3", "matthew.2", "matthew.2.3" }));
    }

    [Test]
    public void APsalmHeadingNamesItsOwnBook()
    {
        var keys = Resolved(Doc(), "PSALM 23", "2");
        Assert.That(keys, Is.EqualTo(new[] { "psalms.23", "psalms.23.2" }));
    }

    [TestCase("Beatus vir qui non abiit. psal. 1")]
    [TestCase("Psal. 1. Beatus vir, qui non abiit")]
    public void AnIncipitHeadingReadsThePsalmNumberFromEitherEnd(string incipit)
    {
        var keys = Resolved(Doc(), incipit, "2");
        Assert.That(keys, Is.EqualTo(new[] { "psalms.1", "psalms.1.2" }));
    }

    /// <summary>Aght Giare's section numbers, Wilsons' paragraph numbers: no heading
    /// ever opened a chapter, so the numbers claim no scripture identity</summary>
    [Test]
    public void ANumberWithoutContextStaysUnresolved()
    {
        Assert.That(Resolved(Doc(), "12", "13"), Is.EqualTo(new string?[] { null, null }));
    }

    /// <summary>Acocrypha: "Cab. I" headings with no manifest book — the chapter is
    /// known but whose it is isn't, so nothing resolves</summary>
    [Test]
    public void AHeadingWithoutABookResolvesNothing()
    {
        Assert.That(Resolved(Doc(), "Cab. I", "3"), Is.EqualTo(new string?[] { null, null }));
    }

    /// <summary>A page-number or citation must not become the chapter that following
    /// bare numbers count verses in</summary>
    [Test]
    public void ACitationDoesNotLeakIntoFollowingNumbers()
    {
        var keys = Resolved(Doc(), "MS 1 Thessalonians 2.16", "3");
        Assert.That(keys, Is.EqualTo(new[] { "1-thessalonians.2.16", null }));
    }

    /// <summary>The resolver only writes CanonicalReference: Reference and the Manx
    /// text stay untouched, so token streams and statistics cannot move</summary>
    [Test]
    public void OnlyTheCanonicalReferenceIsWritten()
    {
        var line = new DocumentLine { Manx = "ta", Reference = "Mian:1:1" };
        ReferenceResolver.Resolve(Doc(), [line]);
        Assert.Multiple(() =>
        {
            Assert.That(line.CanonicalReference, Is.EqualTo("matthew.1.1"));
            Assert.That(line.Reference, Is.EqualTo("Mian:1:1"));
            Assert.That(line.Manx, Is.EqualTo("ta"));
        });
    }

    // --- the key round-trips for the alignment lookups ---

    [Test]
    public void TheKeyRoundTripsWithADisplayName()
    {
        var reference = CanonicalReference.TryParseKey("1-thessalonians.2.16");
        Assert.Multiple(() =>
        {
            Assert.That(reference?.Display, Is.EqualTo("1 Thessalonians 2:16"));
            Assert.That(reference?.ChapterKey, Is.EqualTo("1-thessalonians.2"));
            Assert.That(reference?.Key, Is.EqualTo("1-thessalonians.2.16"));
        });
    }

    [Test]
    public void AChapterKeyRoundTripsToo()
    {
        var reference = CanonicalReference.TryParseKey("psalms.23");
        Assert.Multiple(() =>
        {
            Assert.That(reference?.Display, Is.EqualTo("Psalms 23"));
            Assert.That(reference?.Verse, Is.Null);
        });
    }

    [TestCase("psalms")]
    [TestCase("psalms.0")]
    [TestCase("psalms.23.0")]
    [TestCase("narnia.1.1")]
    [TestCase("psalms.23.1.1")]
    [TestCase(null)]
    public void MalformedKeysDoNotParse(string? key)
    {
        Assert.That(CanonicalReference.TryParseKey(key), Is.Null);
    }
}

/// <summary>Scripture citations found inside dictionary definition text, for the
/// corpus links the client renders (see <see cref="VerseCitations"/>).</summary>
[TestFixture]
public class VerseCitationsTest
{
    [Test]
    public void FindsTheCitationsInRunningProse()
    {
        var found = VerseCitations.FindAll(
            "beeal ny giattey, the entering of the gate; Jud. xii. 6. See also Hos. x. 4.");
        Assert.That(found?.Select(x => (x.Text, x.Key)), Is.EqualTo(new[]
        {
            ("Jud. xii. 6", "judges.12.6"),
            ("Hos. x. 4", "hosea.10.4"),
        }));
    }

    /// <summary>Kelly writes arabic chapters with a comma: Ps. 45, 12</summary>
    [Test]
    public void KellyStyleArabicChaptersResolve()
    {
        var found = VerseCitations.FindAll("as in Ps. 45, 12 it is written");
        Assert.That(found?.Single().Key, Is.EqualTo("psalms.45.12"));
    }

    [Test]
    public void ARepeatedCitationIsListedOnce()
    {
        var found = VerseCitations.FindAll("Jud. xii. 6; and again Jud. xii. 6.");
        Assert.That(found, Has.Count.EqualTo(1));
    }

    /// <summary>OED/EDD references and Apocrypha citations are not corpus verses</summary>
    [TestCase("OED squat 1725, 4")]
    [TestCase("Ecclesiasticus xliv. 9")]
    [TestCase("no citations here at all")]
    [TestCase("")]
    [TestCase(null)]
    public void NonScriptureTextYieldsNothing(string? text)
    {
        Assert.That(VerseCitations.FindAll(text), Is.Null);
    }

    [Test]
    public void EditorialBracketsResolve()
    {
        var found = VerseCitations.FindAll("H[a]b. ii. 11 makes the stones cry out");
        Assert.That(found?.Single().Key, Is.EqualTo("habakkuk.2.11"));
    }

    /// <summary>The Homilies' parenthesized asides leave the statistics text:
    /// citations with verse lists, ranges and trailing periods included</summary>
    [TestCase("son e vyghin (Rom. ii. 4) as e ghrayse", "son e vyghin   as e ghrayse")]
    [TestCase("goo yn Chiarn (Isa. xlv. 24, 25.)", "goo yn Chiarn  ")]
    [TestCase("myr te scruit (Heb. x.25.) ayns shen", "myr te scruit   ayns shen")]
    [TestCase("(Ps. cxxxix. 1–12.) Ta'n Chiarn toiggal", "  Ta'n Chiarn toiggal")]
    [TestCase("blein ny ghaa (Mian xxii. 37) roish shen", "blein ny ghaa   roish shen")]
    public void AParenthesizedCitationLeavesTheStatsText(string manx, string expected)
    {
        Assert.That(VerseCitations.Strip(manx), Is.EqualTo(expected));
    }

    /// <summary>Coyrle Sodjey's bare mid-text citations leave the stats text too</summary>
    [TestCase("son Pardoon son ooilley nyn beccaghyn, Rom. v. 10. as bee eh",
        "son Pardoon son ooilley nyn beccaghyn,   as bee eh")]
    [TestCase("Yn Screeuyn: Rom. xii. 1-5.", "Yn Screeuyn:  ")]
    [TestCase("Veih Rom. viii. 15. Ta shin er gheddyn", "Veih   Ta shin er gheddyn")]
    public void ABareCitationLeavesTheStatsText(string manx, string expected)
    {
        Assert.That(VerseCitations.Strip(manx), Is.EqualTo(expected));
    }

    /// <summary>Ordinary parentheticals are prose, not citations</summary>
    [TestCase("v'eh er ny ruggey (myr shen) ayns Sostyn")]
    [TestCase("yn vlein (1745) haink eh")]
    [TestCase("gyn parentheses erbee")]
    [TestCase("va Job 7 laa as 7 oie ny host")]
    [TestCase("")]
    [TestCase(null)]
    public void ProseParentheticalsStayInTheStatsText(string? manx)
    {
        Assert.That(VerseCitations.Strip(manx), Is.Null);
    }
}
