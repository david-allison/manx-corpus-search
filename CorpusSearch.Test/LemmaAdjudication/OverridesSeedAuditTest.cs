using System.Collections.Generic;
using System.IO;
using System.Linq;
using CorpusSearch.Dependencies.Lucene;
using NUnit.Framework;

namespace CorpusSearch.Test.LemmaAdjudication;

/// <summary>
/// The gate a form-level override must hold (DESIGN-disambiguation.md Phase 0a).
///
/// An override is a claim about the language, and the dangerous kind is the
/// row that drops a whole lexeme: `voddey` was seeded to moddey.n on 3/3
/// treebank sentences that all happened to be dogs, while the corpus splits
/// ~30 dog / ~20 foddey ("cha voddey", not long) — so every foddey line would
/// have been misfiled under the dog, context-free.
///
/// "Lexeme" is the adjudication pool's own grouping (AdjudicationExporter):
/// ids the equivalence layer classifies as one word, or sharing a display
/// headword, are one group. Same-group narrowing and class picks (jaagh.n
/// over jaagh.v) can't misfile anything; dropping another group needs real
/// evidence. Note the grouping is only as good as the equivalence layer:
/// Cregeen's own cross-reference entries (chabbyl "see cabbyl" — one horse)
/// read as two groups until a pair verdict says otherwise, so thin-evidence
/// flags include both real risks (cheer dropping keer) and unclassified
/// variant pairs. Per-line validation, not this audit, settles which is which.
///
/// The hard gate binds lemma.overrides.tsv — the ADOPTED layer production
/// consumes, tracked from manx-lemma-data HEAD daily, where a bad row ships
/// within a day. The seed is a hypothesis file that gates nothing at runtime
/// (see AdjudicationExporter on why it must not gate the pool either), so its
/// findings are reported, not failed.
/// </summary>
public class OverridesSeedAuditTest
{
    /// <summary>Mirrors LemmaSidecarGenerator.CrossLexemeMinObservations</summary>
    private const int CrossLexemeMinObservations = 10;

    [Test]
    public void AdoptedOverridesCarryRealEvidence()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Resources", "lemma.overrides.tsv");
        if (!File.Exists(path))
        {
            Assert.Pass("no lemma.overrides.tsv adopted yet: nothing to gate");
        }
        Assert.That(ThinlyEvidencedLexemeDrops(path), Is.Empty,
            "adopted cross-lexeme override rows on thin evidence — these misfile words context-free; "
            + "validate per-line before adoption:\n  ");
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
        var flagged = ThinlyEvidencedLexemeDrops(path);
        TestContext.Out.WriteLine(
            $"{flagged.Count} seed rows drop a lexeme group on <{CrossLexemeMinObservations} observations "
            + "(the Phase 2 validation worklist; unanimous per-line agreement upgrades a row, disagreement kills it):");
        flagged.ForEach(x => TestContext.Out.WriteLine($"  {x}"));
        // voddey is the proven member of this class; it must never be re-minted
        Assert.That(flagged, Has.None.StartsWith("voddey"),
            "voddey -> moddey.n is disproved by the corpus (~30 dog / ~20 foddey): remove it");
    }

    private static List<string> ThinlyEvidencedLexemeDrops(string overridesPath)
    {
        var equivalencesPath = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "Resources", "manx-lemma-data", "lemma.equivalences.seed.tsv");
        var equivalenceRoot = File.Exists(equivalencesPath)
            ? AdjudicationCommon.EquivalenceGroups(equivalencesPath)
            : id => id;

        var table = LemmaTable.Instance;
        var displayById = AdjudicationCommon.DisplayLemmaById();
        string DisplayOf(string id) => AdjudicationCommon.DisplayKey(displayById.GetValueOrDefault(id, id));

        // the pool's lexeme grouping: equivalence root, then ids sharing a
        // display headword collapse into whichever group claimed it first
        Dictionary<string, string> GroupsOf(IReadOnlyList<string> ids)
        {
            var byDisplay = new Dictionary<string, string>();
            var groups = new Dictionary<string, string>();
            foreach (var id in ids)
            {
                var root = equivalenceRoot(id);
                var display = DisplayOf(id);
                if (byDisplay.TryGetValue(display, out var aliased))
                {
                    root = aliased;
                }
                else
                {
                    byDisplay[display] = root;
                }
                groups[id] = root;
            }
            return groups;
        }

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
            // evidence reads "supporting/total"; total is the sample size the row rests on
            var evidence = columns.Length > 2 ? columns[2] : "";
            var slash = evidence.IndexOf('/');
            var total = slash > 0 && int.TryParse(evidence[(slash + 1)..], out var parsed) ? parsed : 0;
            if (total >= CrossLexemeMinObservations)
            {
                continue;
            }

            var groups = GroupsOf(table.CandidatesFor(form));
            var kept = chosen.Where(groups.ContainsKey).Select(x => groups[x]).ToHashSet();
            var dropped = groups
                .Where(x => !kept.Contains(x.Value))
                .Select(x => DisplayOf(x.Key))
                .Distinct()
                .ToList();
            if (dropped.Count > 0)
            {
                flagged.Add($"{form} -> {columns[1]} ({evidence}) drops {string.Join(",", dropped)}");
            }
        }
        return flagged;
    }
}
