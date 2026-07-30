using System.Collections.Generic;
using System.Linq;
using CorpusSearch.Model.Dictionary;
using NUnit.Framework;
using static CorpusSearch.Test.LemmaAdjudication.SenseInventoryGenerator;

namespace CorpusSearch.Test.LemmaAdjudication;

public class SenseInventoryGeneratorTest
{
    private static CregeenEntry Entry(string word, string? pos, string gloss, params CregeenEntry[] children) =>
        new()
        {
            Words = [word],
            EntryHtml = "",
            HeadingHtml = "",
            Definition = gloss,
            PartsOfSpeech = pos == null ? [] : [pos],
            Children = children.ToList(),
        };

    private static Dictionary<string, IReadOnlyList<string>> Ids(
        params (string Word, string[] Ids)[] entries) =>
        entries.ToDictionary(x => x.Word, x => (IReadOnlyList<string>)x.Ids);

    /// <summary>An id with two printed entries gets ordinals in book order;
    /// an id with one keeps its implicit whole-entry sense</summary>
    [Test]
    public void TwoSensesMintAndOneDoesNot()
    {
        var senses = new[]
        {
            new PrintedSense("aase", ["Noun"], "growth"),
            new PrintedSense("aase", ["Noun"], "a second crop"),
            new PrintedSense("aase", ["Verb"], "grow"),
        };
        var (minted, skim) = Mint(senses, Ids(("aase", ["aase.n", "aase.v"])), new HashSet<string>());

        Assert.Multiple(() =>
        {
            Assert.That(minted.Select(x => x.SenseId), Is.EqualTo(new[] { "aase.n#1", "aase.n#2" }));
            Assert.That(minted[0].Gloss, Is.EqualTo("growth"));
            Assert.That(minted[0].EntryPath, Is.EqualTo("cregeen:aase"));
            Assert.That(skim, Is.Empty);
        });
    }

    /// <summary>Curated rows are finer than the print (foddey.a#2 splits one
    /// printed entry): the generator never mints over them</summary>
    [Test]
    public void AnIdWithCuratedRowsIsSkippedAndFlagged()
    {
        var senses = new[]
        {
            new PrintedSense("foddey", ["Adjective"], "far, at a great distance"),
            new PrintedSense("foddey", ["Adjective"], "remote, distant, foreign"),
        };
        var (minted, skim) = Mint(senses, Ids(("foddey", ["foddey.a"])),
            new HashSet<string> { "foddey.a" });

        Assert.Multiple(() =>
        {
            Assert.That(minted, Is.Empty);
            Assert.That(skim.Single().Reason, Is.EqualTo("existing-rows"));
        });
    }

    /// <summary>A sense with no printed class still attaches when the word
    /// has only one id - and is reported, so a mislabel is seen</summary>
    [Test]
    public void ASingleIdTakesTheUnlabelledSense()
    {
        var senses = new[]
        {
            new PrintedSense("er-lhieu", [], "in their opinion"),
            new PrintedSense("er-lhieu", [], "they think"),
        };
        var (minted, skim) = Mint(senses, Ids(("er-lhieu", ["er-lhieu.x"])), new HashSet<string>());

        Assert.Multiple(() =>
        {
            Assert.That(minted.Select(x => x.SenseId), Is.EqualTo(new[] { "er-lhieu.x#1", "er-lhieu.x#2" }));
            Assert.That(minted.Select(x => x.Flag), Is.All.EqualTo("single-id-attach"));
            Assert.That(skim.Count(x => x.Reason == "single-id-attach"), Is.EqualTo(2));
        });
    }

    /// <summary>A pair one letter-slip apart whose content words never meet:
    /// the fold will not guess, so both rows carry the reviewer's flag</summary>
    [Test]
    public void ALetterSlipPairIsFlaggedNearIdentical()
    {
        var senses = new[]
        {
            new PrintedSense("sheiltynys", ["Noun"], "imagination"),
            new PrintedSense("sheiltynys", ["Noun"], "immagination"),
        };
        var (minted, _) = Mint(senses, Ids(("sheiltynys", ["sheiltynys.n"])), new HashSet<string>());
        Assert.Multiple(() =>
        {
            Assert.That(minted, Has.Count.EqualTo(2));
            Assert.That(minted.Select(x => x.Flag), Is.All.EqualTo("near-identical"));
        });
    }

    /// <summary>Several ids and no matching class is a human's call</summary>
    [Test]
    public void AnUnmatchedSenseAmongSeveralIdsGoesToTheSkim()
    {
        var senses = new[] { new PrintedSense("çhiu", ["Adverb"], "thickly") };
        var (minted, skim) = Mint(senses, Ids(("çhiu", ["çhiu.a", "çhiu.n"])), new HashSet<string>());

        Assert.Multiple(() =>
        {
            Assert.That(minted, Is.Empty);
            Assert.That(skim.Single().Reason, Is.EqualTo("pos-unmatched"));
        });
    }

    /// <summary>A sense with no gloss cannot discriminate anything: it never
    /// mints, and never drags a real pair below the minting bar</summary>
    [Test]
    public void EmptyGlossesDoNotMint()
    {
        var senses = new[]
        {
            new PrintedSense("eabbey", ["Verb"], ""),
            new PrintedSense("eabbey", ["Verb"], "attempting"),
        };
        var (minted, _) = Mint(senses, Ids(("eabbey", ["eabbey.v"])), new HashSet<string>());
        Assert.That(minted, Is.Empty);
    }

    /// <summary>Two top-level printings of the same entry fold to one</summary>
    [Test]
    public void DoublePrintedEntriesFoldToOneSense()
    {
        var senses = PrintedSensesOf([
            Entry("aa-aase", "Noun", "second growth"),
            Entry("aa-aase", "Noun", "second growth"),
        ]);
        Assert.That(senses.Count(x => x.Headword == "aa-aase"), Is.EqualTo(1));
    }

    /// <summary>Children that conjugate and mutate the parent ('e edjag' his
    /// feather) say the same thing through a possessive: they fold, and the
    /// id keeps its one implicit sense</summary>
    [Test]
    public void MutationChildrenFoldIntoTheParentSense()
    {
        var senses = new[]
        {
            new PrintedSense("fedjag", ["Noun"], "a feather"),
            new PrintedSense("e edjag", ["Noun"], "his feather"),
            new PrintedSense("nyn vedjag", ["Noun"], "your, &c. feather"),
        };
        var ids = Ids(("fedjag", ["fedjag.n"]), ("e edjag", ["fedjag.n"]), ("nyn vedjag", ["fedjag.n"]));
        var (minted, _) = Mint(senses, ids, new HashSet<string>());
        Assert.That(minted, Is.Empty);
    }

    /// <summary>A phrase child with a reading of its own ('dy cheilley'
    /// together) mints beside the parent: a reading with no senseId is one
    /// the sidecar can never assign to a token in context</summary>
    [Test]
    public void APhraseChildKeepsItsOwnReading()
    {
        var senses = new[]
        {
            new PrintedSense("cheilley", ["Pronoun"], "‘one another’"),
            new PrintedSense("dy cheilley", ["Adverb"], "together, joined"),
        };
        var ids = Ids(("cheilley", ["cheilley.x"]), ("dy cheilley", ["cheilley.x"]));
        var (minted, _) = Mint(senses, ids, new HashSet<string>());
        Assert.That(minted.Select(x => (x.SenseId, x.EntryPath)), Is.EqualTo(new[]
        {
            ("cheilley.x#1", "cregeen:cheilley"),
            ("cheilley.x#2", "cregeen:dy cheilley"),
        }));
    }

    /// <summary>A self row keys by its FORM: Cregeen headwords the lenited
    /// 'cheilley' under keeill, and its id is keilley.a - looking the ids up
    /// by lemma display would leave only cheilley.x, and 'of the church'
    /// would park on the reciprocal pronoun</summary>
    [Test]
    public void SelfRowsKeyByFormNotLemmaDisplay()
    {
        var ids = SelfIdsByForm(
        [
            "form\tlemmaId\tlemma\tlinkType\tpos\tvia\tnote",
            "cheilley\tcheilley.x\tcheilley\tself\tpro.\tcheilley\t",
            "cheilley\tkeilley.a\tkeilley\tself\ta. d.\tcheilley\t",
            "cheilley\tkeilley.a\tkeilley\tdemutated\ta. d.\tcheilley\t",
        ]);
        Assert.That(ids["cheilley"], Is.EqualTo(new[] { "cheilley.x", "keilley.a" }));

        var senses = new[]
        {
            new PrintedSense("cheilley", ["Pronoun"], "one another"),
            new PrintedSense("cheilley", ["Adjective"], "of the church"),
        };
        var (minted, skim) = Mint(senses, ids, new HashSet<string>());
        Assert.Multiple(() =>
        {
            // each sense lands on its own lexeme: nothing to discriminate
            Assert.That(minted, Is.Empty);
            Assert.That(skim, Is.Empty);
        });
    }

    /// <summary>The two printings of a compound differ by a hyphen or a
    /// trailing semicolon: one sense, not a minted pair</summary>
    [Test]
    public void NearDuplicateGlossesAreOneSense()
    {
        var senses = new[]
        {
            new PrintedSense("aa-oe", ["Noun"], "a great grand child"),
            new PrintedSense("aa-oe", ["Noun"], "a great grandchild;"),
        };
        var (minted, _) = Mint(senses, Ids(("aa-oe", ["aa-oe.n"])), new HashSet<string>());
        Assert.That(minted, Is.Empty);
    }

    /// <summary>The second printing truncates the first's gloss ("discord,
    /// division;" under the prefix entry): one sense, the fuller text</summary>
    [Test]
    public void ATruncatedReprintingFoldsIntoTheFullerGloss()
    {
        var senses = new[]
        {
            new PrintedSense("anvea", ["Noun"], "discord, division, strife, perplexity"),
            new PrintedSense("anvea", ["Noun"], "discord, division;"),
        };
        var (minted, _) = Mint(senses, Ids(("anvea", ["anvea.n"])), new HashSet<string>());
        Assert.That(minted, Is.Empty);
    }

    /// <summary>The second printing prepends a label instead ("pl. principal
    /// fathers"): the suffix fold catches what the prefix fold cannot</summary>
    [Test]
    public void ALabelPrefixedReprintingFoldsToo()
    {
        var senses = new[]
        {
            new PrintedSense("ard-ayraghyn", ["Noun"], "principal fathers, chief fathers;"),
            new PrintedSense("ard-ayraghyn", ["Noun"], "pl. principal fathers, chief fathers;"),
        };
        var (minted, _) = Mint(senses, Ids(("ard-ayraghyn", ["ard-ayraghyn.n"])), new HashSet<string>());
        Assert.That(minted, Is.Empty);
    }

    /// <summary>An apparatus gloss is two readings in one string: whether its
    /// printings are one sense is a human's call, not a spelling rule's</summary>
    [Test]
    public void ApparatusGlossesGoToTheSkimNotTheInventory()
    {
        var senses = new[]
        {
            new PrintedSense("anlheil", ["Noun"], "<unable> [inability] to move about"),
            new PrintedSense("anlheil", ["Noun"], "helplessness of the body"),
        };
        var (minted, skim) = Mint(senses, Ids(("anlheil", ["anlheil.n"])), new HashSet<string>());
        Assert.Multiple(() =>
        {
            Assert.That(minted, Is.Empty);
            Assert.That(skim.Single().Reason, Is.EqualTo("apparatus-gloss"));
        });
    }

    /// <summary>The same verb glossed across its inflections ("lifting" /
    /// "to lift" / "hath reared") is one reading: stemmed content words
    /// overlap, and the fuller gloss survives</summary>
    [Test]
    public void InflectionGlossesFoldIntoOneReading()
    {
        var senses = new[]
        {
            new PrintedSense("troggal", ["Verb"], "lifting, rearing, training, building;"),
            new PrintedSense("dy hroggal", ["Verb"], "to lift, rear, build, train, &c"),
            new PrintedSense("er droggal", ["Verb"], "hath, &c. reared, lifted, trained, built, or raised"),
        };
        var ids = Ids(("troggal", ["troggal.v"]), ("dy hroggal", ["troggal.v"]), ("er droggal", ["troggal.v"]));
        var (minted, _) = Mint(senses, ids, new HashSet<string>());
        Assert.That(minted, Is.Empty);
    }

    /// <summary>A one-letter spelling slip between the printings (crudled /
    /// curdled) folds on the surviving shared words</summary>
    [Test]
    public void ASpellingSlipBetweenPrintingsFolds()
    {
        var senses = new[]
        {
            new PrintedSense("bainney clabbagh", [], "crudled or sour milk"),
            new PrintedSense("bainney clabbagh", [], "curdled or sour milk"),
        };
        var (minted, _) = Mint(senses, Ids(("bainney clabbagh", ["bainney-clabbagh.x"])), new HashSet<string>());
        Assert.That(minted, Is.Empty);
    }

    [TestCase("Noun", "n")]
    [TestCase("Verb", "v")]
    [TestCase("Adjective", "a")]
    [TestCase("Adverb", "x")]
    [TestCase("Pronoun", "x")]
    public void PrintedClassesFileUnderTheTableSuffixes(string pos, string expected)
    {
        Assert.That(SuffixOf([pos]), Is.EqualTo(expected));
    }

    [Test]
    public void NoSingleClassMapsToNoSuffix()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SuffixOf([]), Is.Null);
            Assert.That(SuffixOf(["Noun", "Verb"]), Is.Null);
        });
    }

    [Test]
    public void GlossesAreOneLineAndCapped()
    {
        Assert.Multiple(() =>
        {
            Assert.That(GlossOf("growth,\n  second \t crop"), Is.EqualTo("growth, second crop"));
            Assert.That(GlossOf(null), Is.EqualTo(""));
            var longGloss = GlossOf(string.Join(" ", Enumerable.Repeat("word", 40)));
            Assert.That(longGloss, Has.Length.LessThanOrEqualTo(80));
            Assert.That(longGloss, Does.EndWith("…"));
        });
    }
}
