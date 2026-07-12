using System.Collections.Generic;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.TokenAttributes;

namespace CorpusSearch.Dependencies.Lucene;

/// <summary>
/// Injects each token's candidate lemma ids as extra tokens at the same position:
/// 'daase' emits 'daase' then 'aase.v' with a position increment of 0 and the
/// original token's offsets untouched — so a hit on manx_lemma:aase.v highlights
/// 'daase' in the raw line (ComputeHighlights reads the term vector offsets).
/// Ambiguity is intentional: every candidate of a form is indexed.
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

        foreach (var lemmaId in CandidatesOf(termAtt.ToString()))
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
    /// The token's candidates; a token the table doesn't know may be a productive
    /// clitic contraction, and falls back to its parts' candidates:
    /// `t'X`/`v'X` are present-/past-of-'bee' + X, `X'n` is X + the article 'yn'.
    /// </summary>
    private IEnumerable<string> CandidatesOf(string token)
    {
        var direct = table.CandidatesFor(token);
        if (direct.Count > 0)
        {
            return direct;
        }

        var parts = CliticParts(token);
        if (parts == null)
        {
            return [];
        }
        var combined = new List<string>();
        foreach (var part in parts)
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
