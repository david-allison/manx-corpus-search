using System.IO;
using System.Linq;
using CorpusSearch.Dependencies.Lucene;
using CorpusSearch.Model;
using CorpusSearch.Service;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>
/// The sense layer (DESIGN-disambiguation.md Phase 4): a display refinement
/// over settled lemmas, never a recall mechanism. The inventory names the
/// discriminable printed senses of a lemma id; the sidecar says which of them
/// one occurrence was read as; nothing here reaches the search index.
/// </summary>
public class SenseLayerTest
{
    private const string Inventory =
        "senseId\tlemmaId\tdict\tentryPath\tgloss\n"
        + "foddey.a#1\tfoddey.a\tcregeen\tcregeen:foddey\tfar, at a great distance\n"
        + "foddey.a#2\tfoddey.a\tcregeen\tcregeen:foddey\tlong, of time\n";

    private static SenseInventory LoadInventory(string text = Inventory) =>
        SenseInventory.Load(new StringReader(text));

    private static string KeyOf(string line) =>
        LemmaResolver.LineKey(LemmaResolver.TokenizeManx(DocumentLine.NormalizeManx(line)));

    private static SenseResolver LoadResolver(SenseInventory inventory, params string[] rows) =>
        SenseResolver.Load(new StringReader(
            "docId\tkey\tenglishHash\ttokenIndex\tform\tsenseIds\ttier\thumanVerified\n"
            + string.Join("", rows.Select(x => x + "\n"))), inventory);

    /// <summary>Every vendored sense row must round-trip against the data it
    /// points into: its lemmaId known to the table, its cregeen entryPath a
    /// printed headword, its gloss non-empty. A cregeen-nvh edit or a table
    /// rekey that strands a row fails here, not silently in the popup.</summary>
    [Test]
    public void TheVendoredInventoryRoundTrips()
    {
        var inventory = SenseInventory.Instance;
        Assume.That(inventory.Count, Is.GreaterThan(0), "no vendored senses.tsv");
        var entries = Service.Dictionaries.CregeenDictionaryService.GetEntries();
        Assume.That(entries, Is.Not.Empty, "cregeen.json not present");

        var headwords = new System.Collections.Generic.HashSet<string>(
            System.StringComparer.OrdinalIgnoreCase);
        void Walk(System.Collections.Generic.IEnumerable<Model.Dictionary.CregeenEntry> level)
        {
            foreach (var entry in level)
            {
                if (entry.Words.FirstOrDefault() is { } word)
                {
                    headwords.Add(word);
                }
                Walk(entry.SafeChildren);
            }
        }
        Walk(entries);
        var knownIds = LemmaAdjudication.AdjudicationCommon.DisplayLemmaById();

        Assert.Multiple(() =>
        {
            foreach (var sense in inventory.All)
            {
                Assert.That(knownIds, Does.ContainKey(sense.LemmaId),
                    $"{sense.SenseId}: lemma id unknown to the table");
                Assert.That(sense.Gloss, Is.Not.Empty, $"{sense.SenseId}: empty gloss");
                if (sense.Dictionary != "cregeen")
                {
                    continue;
                }
                var path = sense.EntryPath.Split(':');
                Assert.That(path, Has.Length.EqualTo(2), $"{sense.SenseId}: malformed entryPath");
                // set-side Contains: the set folds case (Erin/erin), and
                // NUnit's Does.Contain would compare case-sensitively
                Assert.That(headwords.Contains(path[1]), Is.True,
                    $"{sense.SenseId}: entryPath names no printed entry ({sense.EntryPath})");
            }
        });
    }

    [Test]
    public void TheInventoryKnowsItsSenses()
    {
        var inventory = LoadInventory();

        Assert.Multiple(() =>
        {
            Assert.That(inventory.SensesOf("foddey.a").Select(x => x.SenseId),
                Is.EqualTo(new[] { "foddey.a#1", "foddey.a#2" }));
            // the common case: no rows, the implicit whole-entry sense
            Assert.That(inventory.SensesOf("moddey.n"), Is.Empty);
            Assert.That(inventory.SenseOf("foddey.a#2")!.Gloss, Is.EqualTo("long, of time"));
        });
    }

    [Test]
    public void AMisshapenSenseIdIsVersionSkewAndIsDropped()
    {
        // the id must be its own lemma's: anything else is a stale artifact
        var inventory = LoadInventory(Inventory + "moddey.n#1\tfoddey.a\tcregeen\tcregeen:moddey\ta dog\n");

        Assert.That(inventory.Count, Is.EqualTo(2));
    }

    [Test]
    public void AnOccurrenceAnswersWithItsRecordedSense()
    {
        var inventory = LoadInventory();
        var line = "dy voddey beayn y ree";
        var resolver = LoadResolver(inventory, $"Doc\t{KeyOf(line)}\thash\t1\tvoddey\tfoddey.a#2\tpopup\t0");

        var senses = resolver.SenseFor(inventory, KeyOf(line), 1, "voddey", "foddey.a");

        Assert.That(senses!.Single().Gloss, Is.EqualTo("long, of time"));
    }

    [Test]
    public void ASenseOfAnotherLemmaSaysNothing()
    {
        // the layers are ordered: if the lemma layer read the token as the dog,
        // a foddey sense row is a disagreement, and the validated layer wins
        var inventory = LoadInventory();
        var line = "dy voddey beayn y ree";
        var resolver = LoadResolver(inventory, $"Doc\t{KeyOf(line)}\thash\t1\tvoddey\tfoddey.a#2\tpopup\t0");

        Assert.That(resolver.SenseFor(inventory, KeyOf(line), 1, "voddey", "moddey.n"), Is.Null);
    }

    [Test]
    public void ARowNamingAnUnknownSenseIsDropped()
    {
        var inventory = LoadInventory();
        var resolver = LoadResolver(inventory, "Doc\tsomekey\thash\t0\tfoddey\tfoddey.a#9\tpopup\t0");

        Assert.That(resolver.HasRows, Is.False);
    }

    [Test]
    public void TheFormGuardsAgainstRowCorruption()
    {
        var inventory = LoadInventory();
        var line = "dy voddey beayn y ree";
        var resolver = LoadResolver(inventory, $"Doc\t{KeyOf(line)}\thash\t1\tvoddey\tfoddey.a#2\tpopup\t0");

        Assert.That(resolver.SenseFor(inventory, KeyOf(line), 1, "beayn", "foddey.a"), Is.Null);
    }
}
