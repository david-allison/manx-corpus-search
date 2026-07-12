using System.Collections.Generic;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.TokenAttributes;

namespace CorpusSearch.Dependencies.Lucene;

/// <summary>
/// Maps each token to its candidate lemma ids at the same position, offsets
/// untouched — so a hit on manx_lemma:aase.v highlights 'daase' in the raw line
/// (ComputeHighlights reads the term vector offsets). Ambiguity is intentional:
/// every candidate of a form is indexed, extras at a position increment of 0.
///
/// A token the table covers directly is *replaced* by its ids (queries resolve
/// such tokens the same way, so the surface term is never needed and the field
/// stays ~40% smaller). Uncovered tokens — including productive clitics, whose
/// parts' ids are injected alongside — keep their surface token, so phrase
/// positions and the unknown-term query fallback hold.
/// </summary>
public sealed class LemmaTokenFilter(TokenStream input, LemmaTable table) : TokenFilter(input)
{
    private readonly ICharTermAttribute termAtt = input.AddAttribute<ICharTermAttribute>();
    private readonly IPositionIncrementAttribute posIncrAtt = input.AddAttribute<IPositionIncrementAttribute>();

    private readonly Queue<string> pending = new();
    private State? current;

    public override bool IncrementToken()
    {
        if (pending.Count > 0)
        {
            // re-emit the original token's attributes (offsets included), as a lemma id
            RestoreState(current);
            termAtt.SetEmpty().Append(pending.Dequeue());
            posIncrAtt.PositionIncrement = 0;
            return true;
        }

        if (!m_input.IncrementToken())
        {
            return false;
        }

        var token = termAtt.ToString();
        var direct = table.CandidatesFor(token);
        if (direct.Count > 0)
        {
            for (var i = 1; i < direct.Count; i++)
            {
                pending.Enqueue(direct[i]);
            }
            if (pending.Count > 0)
            {
                current = CaptureState();
            }
            termAtt.SetEmpty().Append(direct[0]);
            return true;
        }

        foreach (var lemmaId in CliticCandidates(token))
        {
            pending.Enqueue(lemmaId);
        }
        if (pending.Count > 0)
        {
            current = CaptureState();
        }
        return true;
    }

    /// <summary>
    /// A token the table doesn't know may be a productive clitic contraction: its
    /// parts' candidates are injected (the surface token stays). `t'X`/`v'X` are
    /// present-/past-of-'bee' + X, `X'n` is X + the article 'yn'.
    /// </summary>
    private List<string> CliticCandidates(string token)
    {
        var combined = new List<string>();
        foreach (var part in CliticParts(token) ?? [])
        {
            foreach (var lemmaId in table.CandidatesFor(part))
            {
                if (!combined.Contains(lemmaId))
                {
                    combined.Add(lemmaId);
                }
            }
        }
        return combined;
    }

    private static string[]? CliticParts(string token)
    {
        if (token.Length > 2 && token.StartsWith("t'")) return ["ta", token[2..]];
        if (token.Length > 2 && token.StartsWith("v'")) return ["va", token[2..]];
        if (token.Length > 2 && token.EndsWith("'n")) return [token[..^2], "yn"];
        return null;
    }

    public override void Reset()
    {
        base.Reset();
        pending.Clear();
        current = null;
    }
}
