using System.Collections.Generic;
using System.IO;
using CorpusSearch.Dependencies.Lucene;
using NUnit.Framework;

namespace CorpusSearch.Test.LemmaAdjudication;

/// <summary>
/// The gate a form-level override must hold (<see cref="AdoptionGate"/>;
/// DESIGN-disambiguation.md Phase 0a/2).
///
/// An override is a claim about the language, and the dangerous kind is the
/// row that drops a whole lexeme: `voddey` was seeded to moddey.n on 3/3
/// treebank sentences that all happened to be dogs, while the corpus splits
/// ~30 dog / ~20 foddey ("cha voddey", not long) — so every foddey line would
/// have been misfiled under the dog, context-free. `hug` shows the other
/// failure: 137/150 looks decisive, but the 13 are a genuine second word.
///
/// The hard gate binds lemma.overrides.tsv — the ADOPTED layer production
/// consumes, tracked from manx-lemma-data HEAD daily, where a bad row ships
/// within a day. The seed is a hypothesis file that gates nothing at runtime
/// (see AdjudicationExporter on why it must not gate the pool either), so its
/// findings are reported, not failed.
/// </summary>
public class OverridesSeedAuditTest
{
    [Test]
    public void AdoptedOverridesCarryRealEvidence()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Resources", "lemma.overrides.tsv");
        if (!File.Exists(path))
        {
            Assert.Pass("no lemma.overrides.tsv adopted yet: nothing to gate");
        }
        Assert.That(RefusedLexemeDrops(path), Is.Empty,
            "adopted cross-lexeme override rows the gate refuses — these misfile words context-free; "
            + "validate per-line instead of adopting:\n  ");
    }

    [Test]
    public void SeedFindingsAreReported()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "Resources", "manx-lemma-data", "lemma.overrides.seed.tsv");
        if (!File.Exists(path))
        {
            Assert.Ignore("lemma.overrides.seed.tsv not present (manx-lemma-data submodule not initialised)");
        }
        var flagged = RefusedLexemeDrops(path);
        TestContext.Out.WriteLine(
            $"{flagged.Count} seed rows drop a lexeme group on evidence the adoption gate refuses "
            + "(the Phase 2 validation worklist; unanimous per-line agreement upgrades a row, disagreement kills it):");
        flagged.ForEach(x => TestContext.Out.WriteLine($"  {x}"));
        // voddey is the proven member of this class; it must never be re-minted
        Assert.That(flagged, Has.None.StartsWith("voddey"),
            "voddey -> moddey.n is disproved by the corpus (~30 dog / ~20 foddey): remove it");
    }

    /// <summary>The file's lexeme-dropping rows whose evidence
    /// <see cref="AdoptionGate"/> refuses, with the reason</summary>
    private static List<string> RefusedLexemeDrops(string overridesPath)
    {
        var equivalencesPath = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "Resources", "manx-lemma-data", "lemma.equivalences.seed.tsv");
        var equivalenceRoot = File.Exists(equivalencesPath)
            ? AdjudicationCommon.EquivalenceGroups(equivalencesPath)
            : id => id;

        var table = LemmaTable.Instance;
        var displayById = AdjudicationCommon.DisplayLemmaById();

        var flagged = new List<string>();
        foreach (var line in File.ReadLines(overridesPath))
        {
            if (line.StartsWith('#') || line.StartsWith("form\t") || string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            var columns = line.Split('\t');
            var form = columns[0];
            var chosen = columns[1].Split(',');
            var evidence = columns.Length > 2 ? columns[2] : "";

            var dropped = AdoptionGate.DroppedDisplays(
                table.CandidatesFor(form), chosen, equivalenceRoot, displayById);
            if (dropped.Count == 0)
            {
                continue;
            }
            var refusal = AdoptionGate.RefusalOf(AdoptionGate.Parse(evidence));
            if (refusal != null)
            {
                flagged.Add($"{form} -> {columns[1]} ({evidence}) drops {string.Join(",", dropped)}: {refusal}");
            }
        }
        return flagged;
    }
}
