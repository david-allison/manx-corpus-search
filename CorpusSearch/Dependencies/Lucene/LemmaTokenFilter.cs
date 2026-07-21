using System.Collections.Generic;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.TokenAttributes;

namespace CorpusSearch.Dependencies.Lucene;

/// <summary>
/// Maps each token to its candidate lemma ids at the same position, offsets
/// untouched — so a hit on manx_lemma:aase.v highlights 'daase' in the raw line
/// (ComputeHighlights reads the term vector offsets). Ambiguity is intentional:
/// every candidate of a form is indexed, extras at a position increment of 0 —
/// unless a resolution layer (<see cref="LemmaResolver"/>) narrows the set:
/// a form-level override first, then the line's sidecar row, then all
/// candidates. Queries expand a form to all candidates, and the resolved ids
/// are a subset, so narrowing sharpens precision without a query-side change.
///
/// A token the table covers directly is *replaced* by its ids (queries resolve
/// such tokens the same way, so the surface term is never needed and the field
/// stays ~40% smaller). Uncovered tokens — including productive clitics, whose
/// parts' ids are injected alongside — keep their surface token, so phrase
/// positions and the unknown-term query fallback hold.
///
/// Sidecar rows are keyed by a hash of the whole line's token stream, so when
/// any exist the line is buffered before the first token is emitted; with no
/// sidecar (or none loaded) the filter streams token by token as before.
///
/// In <paramref name="sureOnly"/> mode (the manx_lemma_sure field) only settled
/// readings are emitted: a covered token whose resolved id set names a single
/// display lemma. An ambiguous token (voddey: moddey or foddey), an uncovered
/// token, and a clitic's parts say nothing here — the field exists so a count
/// can be asserted rather than offered, and only serves lemma-id queries.
/// Skipped tokens leave position holes; offsets, and so highlights, hold.
/// </summary>
public sealed class LemmaTokenFilter(TokenStream input, LemmaTable table, LemmaResolver? resolver = null,
    bool sureOnly = false)
    : TokenFilter(input)
{
    private readonly ICharTermAttribute termAtt = input.AddAttribute<ICharTermAttribute>();
    private readonly IPositionIncrementAttribute posIncrAtt = input.AddAttribute<IPositionIncrementAttribute>();
    private readonly LemmaResolver resolver = resolver ?? LemmaResolver.Empty;

    private readonly Queue<string> pending = new();
    private State? current;

    // the buffered (sidecar) path: every token's attributes captured up front
    private readonly Queue<(State State, string Term, bool AtPreviousPosition)> buffer = new();
    private bool buffered;

    public override bool IncrementToken()
    {
        return resolver.HasSidecarRows || sureOnly ? BufferedIncrement() : StreamingIncrement();
    }

    /// <summary>Whether the ids leave no doubt which word this is: they all
    /// display as one headword (jaagh.n and jaagh.v are both jaagh)</summary>
    private bool SingleDisplayLemma(IReadOnlyList<string> ids)
    {
        var display = table.DisplayLemmaOf(ids[0]);
        if (display == null)
        {
            return false;
        }
        for (var i = 1; i < ids.Count; i++)
        {
            if (table.DisplayLemmaOf(ids[i]) != display)
            {
                return false;
            }
        }
        return true;
    }

    private bool StreamingIncrement()
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
            var ids = resolver.OverrideFor(token) ?? direct;
            for (var i = 1; i < ids.Count; i++)
            {
                pending.Enqueue(ids[i]);
            }
            if (pending.Count > 0)
            {
                current = CaptureState();
            }
            termAtt.SetEmpty().Append(ids[0]);
            return true;
        }

        // a token the table doesn't know may be a productive clitic contraction:
        // its parts' candidates are injected (the surface token stays)
        foreach (var lemmaId in table.CliticCandidatesFor(token))
        {
            pending.Enqueue(lemmaId);
        }
        if (pending.Count > 0)
        {
            current = CaptureState();
        }
        return true;
    }

    private bool BufferedIncrement()
    {
        if (!buffered)
        {
            BufferLine();
            buffered = true;
        }
        if (buffer.Count == 0)
        {
            return false;
        }
        var (state, term, atPreviousPosition) = buffer.Dequeue();
        RestoreState(state);
        termAtt.SetEmpty().Append(term);
        if (atPreviousPosition)
        {
            posIncrAtt.PositionIncrement = 0;
        }
        // sure-only mode skips tokens outright, leaving holes in the position
        // sequence: harmless, since this field is only counted and highlighted
        return true;
    }

    private void BufferLine()
    {
        var states = new List<State>();
        var tokens = new List<string>();
        while (m_input.IncrementToken())
        {
            states.Add(CaptureState());
            tokens.Add(termAtt.ToString());
        }

        var lineKey = LemmaResolver.LineKey(tokens);
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            var direct = table.CandidatesFor(token);
            if (direct.Count > 0)
            {
                var ids = resolver.OverrideFor(token)
                          ?? resolver.SidecarFor(lineKey, i, token, includePopupTier: false)
                          ?? direct;
                if (sureOnly && !SingleDisplayLemma(ids))
                {
                    continue;
                }
                buffer.Enqueue((states[i], ids[0], false));
                for (var j = 1; j < ids.Count; j++)
                {
                    buffer.Enqueue((states[i], ids[j], true));
                }
                continue;
            }
            if (sureOnly)
            {
                continue;
            }
            buffer.Enqueue((states[i], token, false));
            foreach (var lemmaId in table.CliticCandidatesFor(token))
            {
                buffer.Enqueue((states[i], lemmaId, true));
            }
        }
    }

    public override void Reset()
    {
        base.Reset();
        pending.Clear();
        current = null;
        buffer.Clear();
        buffered = false;
    }
}
