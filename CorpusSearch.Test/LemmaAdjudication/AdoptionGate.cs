using System;
using System.Collections.Generic;
using System.Linq;

namespace CorpusSearch.Test.LemmaAdjudication;

/// <summary>
/// The rule a form-level override row must pass before it may be adopted into
/// lemma.overrides.tsv — the layer production consumes, context-free, for every
/// occurrence of the form (DESIGN-disambiguation.md Phase 2).
///
/// A row that narrows within one lexeme group (a class pick, or choosing the
/// radical entry over Cregeen's mutated cross-reference) cannot misfile a word:
/// it passes on any evidence. A row that DROPS a lexeme group is a claim about
/// the language, and the treebank is a sample of it:
///
/// - voddey was decisive at 3/3 and wrong (the corpus splits ~30/~20): small
///   unanimous samples prove nothing about a mutation collision, so 10
///   observations is the floor.
/// - hug is 137/150 (UD reads it as its own word 13 times): a real minority
///   reading survives any sample size, so a majority is not enough. Unanimity
///   is required; 98% is accepted only at 50+ observations, where the tail is
///   more plausibly annotation noise than a second word.
/// - "N/N LLM-unanimous" rows were judged per corpus occurrence with the line
///   and its English in view: that IS the full-corpus validation, and 10+
///   unanimous occurrences adopt.
/// </summary>
public static class AdoptionGate
{
    public const int MinObservations = 10;
    private const int NearUnanimousFloor = 50;
    private const double NearUnanimousShare = 0.98;

    public sealed record Evidence(int Supporting, int Total, bool LlmUnanimous);

    /// <summary>Parses "137/150", "323/323 LLM-unanimous"; null where unreadable</summary>
    public static Evidence? Parse(string evidence)
    {
        var head = evidence.Split(' ')[0];
        var slash = head.IndexOf('/');
        if (slash <= 0
            || !int.TryParse(head[..slash], out var supporting)
            || !int.TryParse(head[(slash + 1)..], out var total))
        {
            return null;
        }
        return new Evidence(supporting, total, evidence.Contains("LLM-unanimous", StringComparison.Ordinal));
    }

    /// <summary>Why the row may not drop a lexeme group; null when it may</summary>
    public static string? RefusalOf(Evidence? evidence)
    {
        if (evidence == null)
        {
            return "unreadable evidence";
        }
        if (evidence.Total < MinObservations)
        {
            return $"only {evidence.Total} observations (voddey passed 3/3 and was wrong)";
        }
        if (evidence.LlmUnanimous)
        {
            // every corpus occurrence was judged in context and agreed
            return null;
        }
        if (evidence.Supporting == evidence.Total)
        {
            return null;
        }
        var share = evidence.Supporting / (double)evidence.Total;
        if (evidence.Total >= NearUnanimousFloor && share >= NearUnanimousShare)
        {
            return null;
        }
        return $"a real minority reading ({evidence.Supporting}/{evidence.Total}): hug reads cur 137/150 and the 13 are genuine";
    }

    /// <summary>The display headwords the row's choice would silence: displays
    /// whose every unchosen id is neither kept nor equivalence-vouched by a
    /// chosen one. Empty for a harmless narrowing.
    ///
    /// The pool's grouping is NOT reused here: it also merges ids that share a
    /// display, and that bridge is exactly how `hug -> cur.v` once slipped
    /// through — hug.v (the suppletive past the equivalence rightly ties to
    /// cur.v) shares its display with hug.x, Cregeen's preposition "to", which
    /// no verdict vouches. A display survives narrowing when a chosen id wears
    /// it (aggle.n keeps 'aggle' whatever happens to aggle.v: class picks are
    /// harmless), or when the equivalence layer says its ids are the chosen
    /// word by another spelling (faarkey.n vouches 'aarkey'). Anything else
    /// dropped is a word this row would erase from its own page.</summary>
    public static List<string> DroppedDisplays(IReadOnlyList<string> candidates, string[] chosen,
        Func<string, string> equivalenceRoot, Dictionary<string, string> displayById)
    {
        string DisplayOf(string id) => AdjudicationCommon.DisplayKey(displayById.GetValueOrDefault(id, id));

        var chosenSet = chosen.ToHashSet();
        var chosenRoots = chosen.Select(equivalenceRoot).ToHashSet();
        var chosenDisplays = chosen.Select(DisplayOf).ToHashSet();
        return candidates
            .Where(id => !chosenSet.Contains(id) && !chosenRoots.Contains(equivalenceRoot(id)))
            .Select(DisplayOf)
            .Where(display => !chosenDisplays.Contains(display))
            .Distinct()
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
    }
}
