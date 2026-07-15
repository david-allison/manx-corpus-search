using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CorpusSearch.Dependencies.Lucene;
using CorpusSearch.Model;
using CorpusSearch.Service;
using NUnit.Framework;

namespace CorpusSearch.Test;

public class DictionaryLookupServiceTest
{
    private static readonly LemmaTable NoLemmas = LemmaTable.Load(new StringReader("form\tlemmaId\tlemma\n"));

    private static DictionaryLookupService Service(params string[] words)
    {
        return new DictionaryLookupService([new FakeDictionary(words)], NoLemmas, LemmaResolver.Empty);
    }

    private static List<string> Lookup(DictionaryLookupService service, string selection, string? context = null)
    {
        return service.Lookup("gv", selection, context).Select(x => x.PrimaryWord).ToList();
    }

    [Test]
    public void MatchesASingleWord()
    {
        var service = Service("goll", "mygeayrt");
        Assert.That(Lookup(service, "goll"), Is.EqualTo(new[] { "goll" }));
    }

    [Test]
    public void MatchIsCaseInsensitive()
    {
        var service = Service("goll");
        Assert.That(Lookup(service, "Goll"), Is.EqualTo(new[] { "goll" }));
    }

    [Test]
    public void PunctuationIsTrimmed()
    {
        var service = Service("goll");
        Assert.That(Lookup(service, "(goll,"), Is.EqualTo(new[] { "goll" }));
    }

    [Test]
    public void ApostrophesAreKept()
    {
        var service = Service("mygeayrt y mo'ee");
        Assert.That(Lookup(service, "mygeayrt y mo'ee"), Is.EqualTo(new[] { "mygeayrt y mo'ee" }));
    }

    /// <summary>The test case from #337</summary>
    [Test]
    public void PossessiveWithoutAnEntryFallsBackToTheWord()
    {
        var service = Service("mooad");
        Assert.That(Lookup(service, "mooad's"), Is.EqualTo(new[] { "mooad" }));
        Assert.That(Lookup(service, "MOOAD'S"), Is.EqualTo(new[] { "mooad" }));
        Assert.That(Lookup(service, "mooad’s"), Is.EqualTo(new[] { "mooad" }));
    }

    [Test]
    public void ContractionWithoutAnEntryFallsBackToItsParts()
    {
        // the lone 't' (of 'ta') is not returned: a single letter is a contraction stub, not a word
        var service = Service("eh", "goll");
        Assert.That(Lookup(service, "t'eh goll"), Is.EqualTo(new[] { "eh", "goll" }));
    }

    [Test]
    public void WordWithItsOwnEntryDoesNotFallBackToItsParts()
    {
        // the emphatic -'s is a real suffix ('my chree's'): the exact entry is sufficient
        var service = Service("chree's", "chree");
        Assert.That(Lookup(service, "chree's"), Is.EqualTo(new[] { "chree's" }));
    }

    [Test]
    public void ApostropheStylesAreInterchangeable()
    {
        // Kelly writes typographic apostrophes ('B’ODDEY'), Cregeen typewriter ones ('mo'ee')
        var service = Service("b’oddey", "mo'ee");
        Assert.That(Lookup(service, "b'oddey"), Is.EqualTo(new[] { "b’oddey" }));
        Assert.That(Lookup(service, "mo’ee"), Is.EqualTo(new[] { "mo'ee" }));
    }

    [Test]
    public void CompoundWithPossessiveFallsBackToItsWords()
    {
        var service = Service("goll", "mygeayrt");
        Assert.That(Lookup(service, "goll-mygeayrt's"), Is.EqualTo(new[] { "goll", "mygeayrt" }));
    }

    [Test]
    public void UnknownWordReturnsNothing()
    {
        var service = Service("goll");
        Assert.That(Lookup(service, "braew"), Is.Empty);
    }

    /// <summary>The test case from #135</summary>
    [Test]
    public void CompoundWithoutAnEntryFallsBackToItsParts()
    {
        var service = Service("goll", "mygeayrt");
        Assert.That(Lookup(service, "goll-mygeayrt"), Is.EqualTo(new[] { "goll", "mygeayrt" }));
    }

    [Test]
    public void HyphenatedSelectionMatchesASpacedEntry()
    {
        var service = Service("goll mygeayrt", "goll", "mygeayrt");
        Assert.That(Lookup(service, "goll-mygeayrt"), Is.EqualTo(new[] { "goll mygeayrt" }));
    }

    [Test]
    public void SpacedSelectionMatchesAHyphenatedEntry()
    {
        var service = Service("lieh-cheead");
        Assert.That(Lookup(service, "lieh cheead"), Is.EqualTo(new[] { "lieh-cheead" }));
    }

    [Test]
    public void SelectionExpandsToAPhraseFromTheContext()
    {
        var service = Service("goll mygeayrt", "goll");
        var result = Lookup(service, "goll", context: "v'eh goll mygeayrt y valley");
        // the phrase is the more specific match: it is returned first
        Assert.That(result, Is.EqualTo(new[] { "goll mygeayrt", "goll" }));
    }

    [Test]
    public void PhraseFromContextMatchesAcrossPunctuation()
    {
        var service = Service("goll mygeayrt");
        var result = Lookup(service, "mygeayrt", context: "t'eh goll mygeayrt, dy jarroo");
        Assert.That(result, Is.EqualTo(new[] { "goll mygeayrt" }));
    }

    [Test]
    public void ContextDoesNotMatchPhrasesTheSelectionIsNotPartOf()
    {
        var service = Service("goll mygeayrt");
        Assert.That(Lookup(service, "valley", context: "v'eh goll mygeayrt y valley"), Is.Empty);
    }

    [Test]
    public void MultiWordSelectionIsMatchedDirectly()
    {
        var service = Service("dy hroggal", "dy", "hroggal");
        // the parts are not returned: the phrase entry is sufficient
        Assert.That(Lookup(service, "dy hroggal"), Is.EqualTo(new[] { "dy hroggal" }));
    }

    [Test]
    public void MultiWordSelectionFallsBackToItsWords()
    {
        var service = Service("dy", "hroggal");
        Assert.That(Lookup(service, "dy hroggal"), Is.EqualTo(new[] { "dy", "hroggal" }));
    }

    /// <summary>
    /// Kelly heads some entries with variant forms ('EEN, YN'), so looking up 'yn' also matches
    /// them. Entries headed by the query itself must come first.
    /// </summary>
    [Test]
    public void EntriesHeadedByTheQueryAreReturnedFirst()
    {
        var service = new DictionaryLookupService([new FakeDictionary(["EEN", "YN"], ["YN"])], NoLemmas,
            LemmaResolver.Empty);
        Assert.That(Lookup(service, "yn"), Is.EqualTo(new[] { "YN", "EEN" }));
    }

    [Test]
    public void PhrasesFromTheContextStillOutrankTheHeadedEntry()
    {
        // specificity wins over the headword: the phrase match is more useful than the exact entry
        var service = new DictionaryLookupService([new FakeDictionary(["goll mygeayrt"], ["goll", "gholl"])], NoLemmas,
            LemmaResolver.Empty);
        var result = Lookup(service, "goll", context: "v'eh goll mygeayrt y valley");
        Assert.That(result, Is.EqualTo(new[] { "goll mygeayrt", "goll" }));
    }

    [Test]
    public void DuplicateMatchesAreRemoved()
    {
        // one entry known under both forms: both hyphen variants of the query resolve to it
        var service = new DictionaryLookupService(dictionaryServices: [new FakeDictionary(new Dictionary<string, string>
        {
            ["goll-mygeayrt"] = "goll mygeayrt",
            ["goll mygeayrt"] = "goll mygeayrt",
        })], lemmaTable: NoLemmas, lemmaResolver: LemmaResolver.Empty);
        Assert.That(Lookup(service, "goll-mygeayrt"), Is.EqualTo(new[] { "goll mygeayrt" }));
    }

    [Test]
    public void EmptySelectionReturnsNothing()
    {
        var service = Service("goll");
        Assert.That(Lookup(service, " ", context: "goll mygeayrt"), Is.Empty);
    }

    private static LemmaTable Lemmas(params (string Form, string Lemma)[] rows)
    {
        var tsv = "form\tlemmaId\tlemma\tlinkType\n"
                  + string.Join("\n", rows.Select(r => $"{r.Form}\t{r.Lemma}.x\t{r.Lemma}\tinflected"));
        return LemmaTable.Load(new StringReader(tsv));
    }

    [Test]
    public void AnInflectedSelectionOffersItsRoot()
    {
        // 'daase' has no entry of its own: the reader gets the root's entry
        var service = new DictionaryLookupService([new FakeDictionary(["aase"])],
            Lemmas(("daase", "aase")), LemmaResolver.Empty);

        Assert.That(Lookup(service, "daase"), Is.EqualTo(new[] { "aase" }));
    }

    [Test]
    public void TheExactEntryStaysAheadOfTheRoot()
    {
        var service = new DictionaryLookupService([new FakeDictionary(["daase", "aase"])],
            Lemmas(("daase", "aase")), LemmaResolver.Empty);

        Assert.That(Lookup(service, "daase"), Is.EqualTo(new[] { "daase", "aase" }));
    }

    /// <summary>A root the table only reaches by rule is flagged, so the popup can
    /// say so; one the print attests anywhere is not — and the flag sticks for the
    /// rest of a chain that crossed it</summary>
    [Test]
    public void ARuleDerivedRootIsMarkedUnverified()
    {
        var tsv = "form\tlemmaId\tlemma\tlinkType\tpos\tvia\tnote\n"
                  // a generated lenition: no page lists 'vonaco' at all
                  + "vonaco\tmonaco.n\tmonaco\tmutation\ts. f.\tmonaco\tgenerated-lenition\n"
                  // a root of the generated root: inherits the guess it hangs on
                  + "monaco\tprincipality.n\tprincipality\tinflected\ts. f.\tmonaco\t\n"
                  // the print lists 'daase' under 'aase' itself
                  + "daase\taase.v\taase\tinflected\tv.\taase\t\n"
                  // a particle strip restates a printed headword ("e gheiney"):
                  // transcription, not derivation
                  + "gheiney\tdeiney.n\tdeiney\tparticle\ts.\te gheiney\t\n";
        var service = new DictionaryLookupService(
            [new FakeDictionary("monaco", "aase", "principality", "deiney")],
            LemmaTable.Load(new StringReader(tsv)), LemmaResolver.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(service.Lookup("gv", "vonaco").Select(x => (x.PrimaryWord, x.UnverifiedLink)),
                Is.EqualTo(new[] { ("monaco", true), ("principality", true) }));
            Assert.That(service.Lookup("gv", "daase").Select(x => (x.PrimaryWord, x.UnverifiedLink)),
                Is.EqualTo(new[] { ("aase", false) }));
            Assert.That(service.Lookup("gv", "gheiney").Select(x => (x.PrimaryWord, x.UnverifiedLink)),
                Is.EqualTo(new[] { ("deiney", false) }));
        });
    }

    /// <summary>A pair the print attests stays verified however many rules also
    /// reach it: the attested row wins wherever it appears</summary>
    [Test]
    public void AnAttestedRowOutranksAGeneratedOneForTheSameLink()
    {
        var tsv = "form\tlemmaId\tlemma\tlinkType\tpos\tvia\tnote\n"
                  + "vannin\tmannin.n\tmannin\tmutation\ts. f.\tmannin\tgenerated-lenition\n"
                  + "vannin\tmannin.n\tmannin\tinflected\ts. f.\tmannin\t\n";
        var service = new DictionaryLookupService([new FakeDictionary("mannin")],
            LemmaTable.Load(new StringReader(tsv)), LemmaResolver.Empty);

        Assert.That(service.Lookup("gv", "vannin").Single().UnverifiedLink, Is.False);
    }

    /// <summary>The vannin case: Cregeen lists 'vannin' among vann's conjugation,
    /// so a lookup of it answers with "did bless" — but the table lemmatises the
    /// spelling to Mannin. The entry documents a homograph, not this word</summary>
    [Test]
    public void AnEntrySharingOnlyTheSpellingIsDropped()
    {
        var tsv = "form\tlemmaId\tlemma\tlinkType\tpos\tvia\tnote\n"
                  + "vannin\tmannin.n\tMannin\toverride\ts. f.\tMannin\t\n"
                  + "vann\tbann.v\tbann\tself\tv.\tvann\t\n"
                  + "mannin\tmannin.n\tMannin\tself\ts. f.\tMannin\t\n";
        // 'vann' answers for 'vannin' the way a dictionary's inflected-form list does
        var cregeen = new FakeDictionary(new Dictionary<string, string>
        {
            ["vannin"] = "vann",
            ["vann"] = "vann",
            ["mannin"] = "Mannin",
        });
        var service = new DictionaryLookupService([cregeen], LemmaTable.Load(new StringReader(tsv)),
            LemmaResolver.Empty);

        Assert.That(Lookup(service, "vannin"), Is.EqualTo(new[] { "Mannin" }));
    }

    /// <summary>The drop only applies where the table reads the two as different
    /// lexemes: a plural answering from its entry ('biljin' -> BILLEY) shares the
    /// selection's reading and stays</summary>
    [Test]
    public void AnEntryTheSelectionSharesAReadingWithIsKept()
    {
        var tsv = "form\tlemmaId\tlemma\tlinkType\tpos\tvia\tnote\n"
                  + "biljin\tbilley.n\tbilley\tinflected\ts. m.\tbilley\t\n"
                  + "billey\tbilley.n\tbilley\tself\ts. m.\tbilley\t\n";
        var service = new DictionaryLookupService(
            [new FakeDictionary(new Dictionary<string, string> { ["biljin"] = "BILLEY" })],
            LemmaTable.Load(new StringReader(tsv)), LemmaResolver.Empty);

        Assert.That(Lookup(service, "biljin"), Is.EqualTo(new[] { "BILLEY" }));
    }

    /// <summary>Root-derived entries carry their hop depth so the popup can nest
    /// them; entries for the selection itself stay at depth 0</summary>
    [Test]
    public void RootEntriesCarryTheirDepth()
    {
        var service = new DictionaryLookupService([new FakeDictionary(["daase", "aase"])],
            Lemmas(("daase", "aase")), LemmaResolver.Empty);

        var results = service.Lookup("gv", "daase");
        Assert.That(results.Select(x => (x.PrimaryWord, x.RootDepth)),
            Is.EqualTo(new[] { ("daase", 0), ("aase", 1) }));
    }

    /// <summary>A root's own root is walked too: 'gheiney' (mutated plural) ->
    /// 'deiney' (the plural's entry) -> 'dooinney' (the singular)</summary>
    [Test]
    public void TheRootChainIsWalked()
    {
        var service = new DictionaryLookupService([new FakeDictionary(["deiney", "dooinney"])],
            Lemmas(("gheiney", "deiney"), ("deiney", "dooinney")), LemmaResolver.Empty);

        var results = service.Lookup("gv", "gheiney");
        Assert.That(results.Select(x => (x.PrimaryWord, x.RootDepth)),
            Is.EqualTo(new[] { ("deiney", 1), ("dooinney", 2) }));
    }

    private static LemmaResolver Resolver(LemmaTable table, string? overrides = null, string? sidecar = null)
    {
        return LemmaResolver.Load(
            overrides == null ? null : new StringReader(overrides),
            sidecar == null ? null : new StringReader(sidecar),
            table);
    }

    /// <summary>The sidecar line key of a displayed line, as the popup recomputes it</summary>
    private static string KeyOf(string context)
    {
        return LemmaResolver.LineKey(LemmaResolver.TokenizeManx(DocumentLine.NormalizeManx(context)));
    }

    private static string SidecarRow(string context, int tokenIndex, string form, string lemmaIds,
        string tier = "index")
    {
        return "docId\tkey\tenglishHash\ttokenIndex\tform\tlemmaIds\ttier\thumanVerified\n"
               + $"doc\t{KeyOf(context)}\tx\t{tokenIndex}\t{form}\t{lemmaIds}\t{tier}\t0\n";
    }

    /// <summary>A form-level override suppresses the rejected reading's root entry:
    /// 'er' resolved to the preposition no longer offers 'fer' (man)</summary>
    [Test]
    public void AnOverrideDropsTheRejectedRootReading()
    {
        var table = Lemmas(("er", "er"), ("er", "fer"));
        var unresolved = new DictionaryLookupService([new FakeDictionary(["er", "fer"])], table, LemmaResolver.Empty);
        Assert.That(Lookup(unresolved, "er"), Is.EqualTo(new[] { "er", "fer" }));

        var resolver = Resolver(table, overrides: "form\tlemmaIds\tudEvidence\ner\ter.x\t12/12\n");
        var service = new DictionaryLookupService([new FakeDictionary(["er", "fer"])], table, resolver);
        Assert.That(Lookup(service, "er"), Is.EqualTo(new[] { "er" }));
    }

    /// <summary>A sidecar resolution demotes readings on its own line only: the
    /// line's key is recomputed from the context the client sends</summary>
    [Test]
    public void ASidecarRowDemotesTheReadingOnItsLineOnly()
    {
        var table = Lemmas(("er", "er"), ("er", "fer"));
        const string context = "Moddey er y dreeym.";
        var resolver = Resolver(table, sidecar: SidecarRow(context, tokenIndex: 1, "er", "er.x"));
        var service = new DictionaryLookupService([new FakeDictionary(["er", "fer"])], table, resolver);

        Assert.That(Lookup(service, "er", context), Is.EqualTo(new[] { "er" }));
        // a different line, and no context at all: the row does not apply
        Assert.That(Lookup(service, "er", "Cabbyl er y clieau."), Is.EqualTo(new[] { "er", "fer" }));
        Assert.That(Lookup(service, "er"), Is.EqualTo(new[] { "er", "fer" }));
    }

    /// <summary>tier=popup rows are display-only, but the popup is display</summary>
    [Test]
    public void APopupTierRowAlsoDemotes()
    {
        var table = Lemmas(("er", "er"), ("er", "fer"));
        const string context = "Moddey er y dreeym.";
        var resolver = Resolver(table, sidecar: SidecarRow(context, tokenIndex: 1, "er", "er.x", tier: "popup"));
        var service = new DictionaryLookupService([new FakeDictionary(["er", "fer"])], table, resolver);

        Assert.That(Lookup(service, "er", context), Is.EqualTo(new[] { "er" }));
    }

    private static LemmaTable Names()
    {
        return LemmaTable.Load(new StringReader(
            "form\tlemmaId\tlemma\tlinkType\tpos\n"
            + "solomon\tsolomon.np\tSolomon\tself\tnp. personal\n"
            + "yudah\tjudah.np\tJudah\tmutation\tnp. personal\n"
            + "judah\tjudah.np\tJudah\tself\tnp. personal\n"
            + "doolish\tdoolish.np\tDoolish\tself\tnp. place\n"));
    }

    /// <summary>No dictionary lists Bible names: the names supplement's metadata
    /// still identifies the tapped word</summary>
    [Test]
    public void AnEntrylessNameShowsAsAProperNoun()
    {
        var service = new DictionaryLookupService([new FakeDictionary(Array.Empty<string>())], Names(), LemmaResolver.Empty);

        var results = service.Lookup("gv", "Solomon");
        Assert.That(results.Select(x => (x.PrimaryWord, x.Summary, x.DictionaryName, x.RootDepth)),
            Is.EqualTo(new[] { ("Solomon", "personal name", "Proper nouns", 0) }));
    }

    /// <summary>A mutated spelling identifies the name it belongs to, nested like
    /// a root entry</summary>
    [Test]
    public void AMutatedEntrylessNameIdentifiesItsName()
    {
        var service = new DictionaryLookupService([new FakeDictionary(Array.Empty<string>())], Names(), LemmaResolver.Empty);

        var results = service.Lookup("gv", "Yudah");
        Assert.That(results.Select(x => (x.PrimaryWord, x.Summary, x.RootDepth)),
            Is.EqualTo(new[] { ("Judah", "personal name", 1) }));
    }

    /// <summary>A real dictionary entry wins: the synthesized name is only a
    /// fallback for an otherwise empty popup</summary>
    [Test]
    public void ADictionaryEntrySuppressesTheProperNounFallback()
    {
        var service = new DictionaryLookupService([new FakeDictionary("doolish")], Names(), LemmaResolver.Empty);

        var results = service.Lookup("gv", "Doolish");
        Assert.That(results.Select(x => x.DictionaryName), Is.EqualTo(new[] { "Fake" }));
    }

    /// <summary>A total miss falls back to entries for near spellings, tagged as
    /// suggestions ("did you mean")</summary>
    [Test]
    public void AMissSuggestsNearSpellings()
    {
        var service = Service("moddey", "cabbyl");

        var results = service.Lookup("gv", "modey");
        Assert.That(results.Select(x => (x.PrimaryWord, x.NearMatchOf)),
            Is.EqualTo(new[] { ("moddey", "moddey") }));
    }

    /// <summary>A misspelled name suggests the name: its entry is the proper-noun
    /// metadata, since no dictionary lists it</summary>
    [Test]
    public void AMisspelledNameSuggestsTheName()
    {
        var service = new DictionaryLookupService([new FakeDictionary(Array.Empty<string>())], Names(),
            LemmaResolver.Empty);

        var results = service.Lookup("gv", "Soloman");
        Assert.That(results.Select(x => (x.PrimaryWord, x.Summary, x.NearMatchOf)),
            Is.EqualTo(new[] { ("Solomon", "personal name", "Solomon") }));
    }

    /// <summary>Distance-2 guesses on short words are noise: up to five letters
    /// only one edit is tolerated, and tiny selections never guess</summary>
    [Test]
    public void ShortWordsBarelyGuess()
    {
        var service = Service("moddey");
        Assert.That(Lookup(service, "moey"), Is.Empty); // two edits on four letters
        Assert.That(Lookup(service, "mod"), Is.Empty); // too short to guess against

        // exact and part matches never reach the suggestion tier
        var parts = Service("goll", "mygeayrt");
        Assert.That(parts.Lookup("gv", "goll-mygeayrt").Select(x => x.NearMatchOf),
            Is.All.Null);
    }

    /// <summary>The selection appearing twice with only one occurrence resolved:
    /// the unresolved occurrence keeps every reading in play</summary>
    [Test]
    public void AnUnresolvedOccurrenceKeepsEveryReading()
    {
        var table = Lemmas(("er", "er"), ("er", "fer"));
        const string context = "Er my hie er y dreeym.";
        var resolver = Resolver(table, sidecar: SidecarRow(context, tokenIndex: 0, "er", "er.x"));
        var service = new DictionaryLookupService([new FakeDictionary(["er", "fer"])], table, resolver);

        Assert.That(Lookup(service, "er", context), Is.EqualTo(new[] { "er", "fer" }));
    }

    private static LemmaTable BeeTable()
    {
        return LemmaTable.Load(new StringReader(
            "form\tlemmaId\tlemma\tlinkType\n"
            + "row\trow.v\trow\tself\n"
            + "row\tbee.v\tbee\tirregular\n"
            + "bee\tbee.v\tbee\tself\n"
            + "bee\tbee.n\tbee\tself\n"));
    }

    /// <summary>row belongs to bee the verb: the chain drops the food sense</summary>
    [Test]
    public void TheRootChainKeepsOnlyTheMeantSense()
    {
        var service = new DictionaryLookupService([new FakeDictionary(
            ("row", "Verb", "wast thou"), ("bee", "Verb", "be, will be"), ("bee", "Noun", "meat, food"))],
            BeeTable(), LemmaResolver.Empty);

        var results = service.Lookup("gv", "row");
        Assert.That(results.Select(x => (x.PrimaryWord, x.Summary)), Is.EqualTo(new[]
        {
            ("row", "wast thou"),
            ("bee", "be, will be"),
        }));
    }

    /// <summary>Entries without a declared class survive the sense filter</summary>
    [Test]
    public void UndeclaredWordClassesAreNeverFiltered()
    {
        var service = new DictionaryLookupService([new FakeDictionary(
            ("row", "Verb", "wast thou"), ("bee", "", "be, will be"), ("bee", "", "meat, food"))],
            BeeTable(), LemmaResolver.Empty);

        Assert.That(service.Lookup("gv", "row"), Has.Count.EqualTo(3));
    }

    /// <summary>A filter that would empty the list loses: everything stays</summary>
    [Test]
    public void AnEmptyingSenseFilterIsAbandoned()
    {
        var service = new DictionaryLookupService([new FakeDictionary(
            ("row", "Verb", "wast thou"), ("bee", "Noun", "meat, food"))],
            BeeTable(), LemmaResolver.Empty);

        // the only 'bee' entry is the noun: better than showing nothing
        Assert.That(service.Lookup("gv", "row").Select(x => x.Summary),
            Is.EqualTo(new[] { "wast thou", "meat, food" }));
    }

    /// <summary>The popup labels each entry with the dictionary it came from (#51)</summary>
    [Test]
    public void SummariesNameTheirDictionary()
    {
        var service = Service("goll");
        var result = service.Lookup("gv", "goll").Single();
        Assert.That(result.DictionaryName, Is.EqualTo("Fake"));
    }

    [Test]
    public void CitationMarkersInContextAreIgnored()
    {
        var service = Service("goll mygeayrt");
        Assert.That(Lookup(service, "goll", context: "v'eh goll [1] mygeayrt"), Is.EqualTo(new[] { "goll mygeayrt" }));
    }

    [Test]
    public void CoverageClassifiesEachToken()
    {
        var table = LemmaTable.Load(new StringReader(
            "form\tlemmaId\tlemma\tlinkType\n"
            + "daase\taase.v\taase\tinflected\n"
            + "aase\taase.v\taase\tself\n"
            + "ghow\tgow.v\tgow\tinflected\n"));
        var service = new DictionaryLookupService([new FakeDictionary("moddey", "aase")], table, LemmaResolver.Empty);

        var coverage = service.Coverage("gv", ["Moddey, daase ghow xyzzy"]);

        // moddey: its own entry; daase: the root chain reaches aase's entry;
        // ghow: the table knows it but no dictionary does; xyzzy: unknown
        Assert.That(coverage[0].Select(x => x.Status),
            Is.EqualTo(new[] { "entry", "root", "lemma", "none" }));
        Assert.That(coverage[0].Select(x => (x.Start, x.Length)),
            Is.EqualTo(new[] { (0, 6), (8, 5), (14, 4), (19, 5) }));
    }

    [Test]
    public void CoverageResolvesClitics()
    {
        var table = LemmaTable.Load(new StringReader(
            "form\tlemmaId\tlemma\tlinkType\naase\taase.v\taase\tself\n"));
        var service = new DictionaryLookupService([new FakeDictionary("aase")], table, LemmaResolver.Empty);

        var coverage = service.Coverage("gv", ["T'aase"]);

        Assert.That(coverage[0].Select(x => x.Status), Is.EqualTo(new[] { "root" }));
    }

    /// <summary>The corpus writes 'dy-reiltagh' where the dictionary lists
    /// 'dy reiltagh'; a tap resolves it through the hyphen variants, so the
    /// coverage prediction must agree</summary>
    [Test]
    public void CoverageTriesTheTapPathsHyphenVariants()
    {
        var service = new DictionaryLookupService([new FakeDictionary("dy reiltagh")], NoLemmas,
            LemmaResolver.Empty);

        var coverage = service.Coverage("gv", ["dy-reiltagh"]);

        Assert.That(coverage[0].Select(x => x.Status), Is.EqualTo(new[] { "entry" }));
    }

    [Test]
    public void PageGroupsEntriesByDictionary()
    {
        var service = Service("goll", "mygeayrt");

        var page = service.Page("gv", "goll");

        Assert.Multiple(() =>
        {
            Assert.That(page.Word, Is.EqualTo("goll"));
            Assert.That(page.IsSuggestionTier, Is.False);
            Assert.That(page.Audio, Is.Null);
            Assert.That(page.Groups, Has.Count.EqualTo(1));
            Assert.That(page.Groups[0].Dictionary, Is.EqualTo("Fake"));
            Assert.That(page.Groups[0].Entries.Single().PrimaryWord, Is.EqualTo("goll"));
        });
    }

    [Test]
    public void PageGroupsCarryTheDictionarySlug()
    {
        var service = Service("goll");

        var page = service.Page("gv", "goll");

        // the client scopes on the slug: the display name is prose and churns
        Assert.That(page.Groups.Single().Slug, Is.EqualTo("fake"));
    }

    [Test]
    public void PageScopedToADictionaryDropsTheOthers()
    {
        var service = new DictionaryLookupService(
            [new FakeDictionary("Cregeen", ["goll"]), new FakeDictionary("Kelly", ["goll"])],
            NoLemmas, LemmaResolver.Empty);

        var page = service.Page("gv", "goll", dict: "cregeen");

        Assert.That(page.Groups.Select(x => x.Dictionary), Is.EqualTo(new[] { "Cregeen" }));
    }

    /// <summary>A slug no dictionary answers to scopes to nothing: silently
    /// widening back to every dictionary would show a page the URL never asked
    /// for, and read as though the named dictionary held those entries</summary>
    [Test]
    public void PageScopedToAnUnknownDictionaryIsEmpty()
    {
        var service = Service("goll");

        var page = service.Page("gv", "goll", dict: "no-such-dictionary");

        Assert.That(page.Groups, Is.Empty);
    }

    [Test]
    public void DictionariesListsEveryDictionaryForTheLanguage()
    {
        var service = new DictionaryLookupService(
            [new FakeDictionary("Cregeen", ["goll"]), new FakeDictionary("Kelly", ["mygeayrt"])],
            NoLemmas, LemmaResolver.Empty);

        // every dictionary, not only those defining the word being viewed
        Assert.That(service.Dictionaries("gv").Select(x => x.Slug),
            Is.EqualTo(new[] { "cregeen", "kelly" }));
    }

    /// <summary>Phil Kelly merges homograph senses into one gloss list, so when
    /// the chain knows which sense it means (row -> bee.v, the verb), the
    /// sense-blind entry stands aside for the sense-capable dictionaries</summary>
    [Test]
    public void PhilKellyStandsAsideWhenTheChainKnowsTheSense()
    {
        var table = LemmaTable.Load(new StringReader(
            "form\tlemmaId\tlemma\tlinkType\tpos\n" +
            "row\tbee.v\tbee\tirregular\tv.\n" +
            "bee\tbee.v\tbee\tself\tv.\n" +
            "bee\tbee.n\tbee\tself\ts.\n"));
        var service = new DictionaryLookupService(
            [
                new FakeDictionary("Fake", ["bee"]),
                new FakeDictionary(CorpusSearch.Service.Dictionaries.PhilKellyDictionaryService.Name, ["bee"]),
            ],
            table, LemmaResolver.Empty);

        var chain = service.Lookup("gv", "row").Where(x => x.RootDepth == 1).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(chain.Select(x => x.DictionaryName), Does.Contain("Fake"));
            Assert.That(chain.Select(x => x.DictionaryName),
                Does.Not.Contain(CorpusSearch.Service.Dictionaries.PhilKellyDictionaryService.Name));
        });
    }

    /// <summary>A Phillips 1610 spelling reaches its entries through the
    /// spelling link: every summary says so, so the client can explain the
    /// hop instead of implying a dictionary lists the 1610 form</summary>
    [Test]
    public void APhillipsSpellingIsExplained()
    {
        var table = LemmaTable.Load(new StringReader(
            "form\tlemmaId\tlemma\tlinkType\tpos\tvia\tnote\n" +
            "dooinney\tdooinney.n\tdooinney\tself\ts. m.\tdooinney\t\n" +
            "dwyne\tdooinney.n\tdooinney\tphillips\ts. m.\tdooinney\tphillips-1610\n"));
        var service = new DictionaryLookupService([new FakeDictionary("dooinney")],
            table, LemmaResolver.Empty);

        var viaPhillips = service.Lookup("gv", "dwyne");
        Assert.That(viaPhillips, Is.Not.Empty);
        Assert.That(viaPhillips, Has.All.Property(nameof(DictionarySummary.PhillipsSpellingOf))
            .EqualTo("dooinney"));

        var direct = service.Lookup("gv", "dooinney");
        Assert.That(direct, Has.All.Property(nameof(DictionarySummary.PhillipsSpellingOf)).Null);
    }

    /// <summary>The page looks up a headword without context: 'ass' (out)
    /// must not offer the demutation guess fass; a word that is not a
    /// headword itself keeps its guesses (they are all the reader has)</summary>
    [Test]
    public void PageDropsDemutationGuessesForAHeadword()
    {
        var table = LemmaTable.Load(new StringReader(
            "form\tlemmaId\tlemma\tlinkType\n" +
            "ass\tass.x\tass\tself\n" +
            "ass\tfass.v\tfass\tdemutated\n" +
            "vow\tfow.v\tfow\tdemutated\n"));
        var service = new DictionaryLookupService([new FakeDictionary("ass", "fass", "fow")],
            table, LemmaResolver.Empty);

        var assPage = service.Page("gv", "ass");
        Assert.That(assPage.Groups.SelectMany(g => g.Entries).Select(x => x.PrimaryWord),
            Is.EqualTo(new[] { "ass" }));

        var vowPage = service.Page("gv", "vow");
        Assert.That(vowPage.Groups.SelectMany(g => g.Entries).Select(x => x.PrimaryWord),
            Does.Contain("fow"));
    }

    [Test]
    public void PageMarksTheSuggestionTier()
    {
        // 'golla' matches nothing; 'golly' is one edit away
        var service = Service("golly");

        var page = service.Page("gv", "golla");

        Assert.Multiple(() =>
        {
            Assert.That(page.IsSuggestionTier, Is.True);
            Assert.That(page.Groups.SelectMany(x => x.Entries),
                Has.All.Property(nameof(DictionarySummary.NearMatchOf)).Not.Null);
        });
    }

    private class FakeDictionary : ISearchDictionary
    {
        private readonly List<(List<string> Words, string PrimaryWord)> entries;
        private readonly string identifier = "Fake";

        public FakeDictionary(params string[] words) : this(words.Select(x => new[] { x }).ToArray()) { }

        /// <summary>A named dictionary: identifier-sensitive rules (the Phil
        /// Kelly sense-blind demotion) need more than one</summary>
        public FakeDictionary(string identifier, string[] words) : this(words)
        {
            this.identifier = identifier;
        }

        /// <summary>Each entry is its word list, headed by the primary word: ["EEN", "YN"] is the entry 'EEN, YN'</summary>
        public FakeDictionary(params string[][] entryWords)
        {
            entries = entryWords.Select(words => (words.ToList(), words[0])).ToList();
        }

        public FakeDictionary(Dictionary<string, string> wordToPrimaryWord)
        {
            entries = wordToPrimaryWord.Select(x => (new List<string> { x.Key }, x.Value)).ToList();
        }

        /// <summary>Entries with declared word classes, for sense filtering</summary>
        public FakeDictionary(params (string Word, string Pos, string Gloss)[] posEntries)
        {
            entries = posEntries.Select(x => (new List<string> { x.Word }, x.Word)).ToList();
            this.posEntries = posEntries.ToList();
        }

        private readonly List<(string Word, string Pos, string Gloss)>? posEntries;

        public string Identifier => identifier;
        public string Slug => identifier.ToLowerInvariant();
        public IReadOnlyList<string> Headwords => entries.Select(x => x.PrimaryWord).ToList();
        public List<string> QueryLanguages => ["gv"];
        public bool LinkToDictionary => false;

        public IEnumerable<DictionarySummary> GetSummaries(string query, bool basic = false)
        {
            if (posEntries != null)
            {
                foreach (var (word, pos, gloss) in posEntries.Where(e =>
                             string.Equals(e.Word, query, StringComparison.InvariantCultureIgnoreCase)))
                {
                    yield return new DictionarySummary
                    {
                        PrimaryWord = word,
                        Summary = gloss,
                        PartsOfSpeech = pos.Length > 0 ? [pos] : null,
                    };
                }
                yield break;
            }
            foreach (var (_, primaryWord) in entries.Where(e => e.Words.Contains(query, StringComparer.InvariantCultureIgnoreCase)))
            {
                yield return new DictionarySummary { PrimaryWord = primaryWord, Summary = $"definition of {primaryWord}" };
            }
        }

        public IEnumerable<string> AllWords => entries.SelectMany(e => e.Words);

        public bool ContainsWord(string word) =>
            entries.Any(e => e.Words.Contains(word, StringComparer.InvariantCultureIgnoreCase));
    }
}
