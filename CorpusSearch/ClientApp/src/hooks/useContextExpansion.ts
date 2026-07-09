import { useEffect, useMemo, useState } from "react"
import {
    fetchLines,
    SearchWorkResponse,
    SearchWorkResult,
} from "../api/SearchWorkApi"

/** Lines revealed per click of an expander (#286) */
export const CONTEXT_CHUNK = 5
/** A middle gap spanning at most this many CSV lines is expanded with a single click */
export const SMALL_GAP = CONTEXT_CHUNK * 2

export type ContextGap = {
    /** the first hidden CSV line number */
    start: number
    /** the last hidden CSV line number */
    end: number
    /** where the gap sits relative to the results: the edges only expand in one direction */
    position: "leading" | "middle" | "trailing"
    /** a fetch for this gap is in flight: ignore further clicks */
    loading: boolean
}

/**
 * "down" continues from the line above the gap; "up" reveals the lines just above the line
 * below it; "all" reveals the whole gap (small gaps only)
 */
export type ExpandDirection = "up" | "down" | "all"

export type TableEntry =
    | {
          type: "line"
          line: SearchWorkResult
          /** a context line revealed by an expander, rather than a search result */
          isContext: boolean
          /** ordinal among the displayed lines (used for row striping) */
          index: number
      }
    | { type: "gap"; gap: ContextGap }

/**
 * The hidden regions of an in-document search: between results more than one CSV line apart,
 * and between the results and the bounds of the document.
 *
 * A jump in csvLineNumber does not always hide anything (a record may span several raw CSV
 * lines): such a gap resolves itself on the first click, when the server reports the range
 * as empty.
 */
export const deriveGaps = (response: SearchWorkResponse): ContextGap[] => {
    // a '*' search already returns every line
    if (response.totalMatches == null) {
        return []
    }
    const results = response.results
    if (results.length == 0) {
        return []
    }

    const gaps: ContextGap[] = []
    const first = response.firstLineNumber
    if (first != null && results[0].csvLineNumber > first) {
        gaps.push({
            start: first,
            end: results[0].csvLineNumber - 1,
            position: "leading",
            loading: false,
        })
    }
    for (let i = 1; i < results.length; i++) {
        const previous = results[i - 1].csvLineNumber
        const next = results[i].csvLineNumber
        if (next - previous > 1) {
            gaps.push({
                start: previous + 1,
                end: next - 1,
                position: "middle",
                loading: false,
            })
        }
    }
    const last = response.lastLineNumber
    const lastResult = results[results.length - 1].csvLineNumber
    if (last != null && lastResult < last) {
        gaps.push({
            start: lastResult + 1,
            end: last,
            position: "trailing",
            loading: false,
        })
    }
    return gaps
}

type ExpansionState = {
    contextLines: SearchWorkResult[]
    gaps: ContextGap[]
}

/**
 * 'Expand context' within a document search (#286): tracks the gaps between the returned
 * lines, fetches hidden lines on demand and merges them into the displayed rows.
 *
 * Returns the rows to display (lines interleaved with gap markers, in document order) and
 * the expand action. Expansion is disabled without a `docIdent`.
 */
export const useContextExpansion = (
    response: SearchWorkResponse,
    docIdent?: string,
) => {
    const initialState = (): ExpansionState => ({
        contextLines: [],
        gaps: docIdent ? deriveGaps(response) : [],
    })
    const [state, setState] = useState<ExpansionState>(initialState)

    // a new search discards the expansion
    useEffect(() => {
        setState({
            contextLines: [],
            gaps: docIdent ? deriveGaps(response) : [],
        })
    }, [response, docIdent])

    const expand = (gap: ContextGap, direction: ExpandDirection) => {
        if (gap.loading || docIdent == null) {
            return
        }
        const isSame = (g: ContextGap) =>
            g.start == gap.start && g.end == gap.end
        setState((s) => ({
            ...s,
            gaps: s.gaps.map((g) => (isSame(g) ? { ...g, loading: true } : g)),
        }))

        const span = gap.end - gap.start + 1
        const limit = direction == "all" ? span : Math.min(CONTEXT_CHUNK, span)
        fetchLines({
            docIdent,
            start: gap.start,
            end: gap.end,
            limit,
            fromEnd: direction == "up",
        })
            .then((data) => {
                setState((s) => {
                    const revealed = data.lines.map((x) => x.csvLineNumber)
                    const gaps = s.gaps.flatMap((g): ContextGap[] => {
                        if (!isSame(g)) {
                            return [g]
                        }
                        if (
                            data.totalInRange <= limit ||
                            revealed.length == 0
                        ) {
                            return [] // nothing left hidden
                        }
                        return direction == "up"
                            ? [
                                  {
                                      ...g,
                                      end: Math.min(...revealed) - 1,
                                      loading: false,
                                  },
                              ]
                            : [
                                  {
                                      ...g,
                                      start: Math.max(...revealed) + 1,
                                      loading: false,
                                  },
                              ]
                    })
                    // belt-and-braces: gap arithmetic should already prevent duplicates
                    const known = new Set([
                        ...response.results.map((x) => x.csvLineNumber),
                        ...s.contextLines.map((x) => x.csvLineNumber),
                    ])
                    return {
                        contextLines: [
                            ...s.contextLines,
                            ...data.lines.filter(
                                (x) => !known.has(x.csvLineNumber),
                            ),
                        ],
                        gaps,
                    }
                })
            })
            .catch((e: unknown) => {
                console.warn(e)
                setState((s) => ({
                    ...s,
                    gaps: s.gaps.map((g) =>
                        isSame(g) ? { ...g, loading: false } : g,
                    ),
                }))
            })
    }

    const entries = useMemo((): TableEntry[] => {
        const results = response.results
        if (state.contextLines.length == 0 && state.gaps.length == 0) {
            // nothing to expand: preserve the server's ordering exactly
            return results.map((line, index) => ({
                type: "line",
                line,
                isContext: false,
                index,
            }))
        }

        const rows = [
            ...results.map((line) => ({ line, isContext: false })),
            ...state.contextLines.map((line) => ({ line, isContext: true })),
        ].sort((a, b) => a.line.csvLineNumber - b.line.csvLineNumber)
        const gaps = [...state.gaps].sort((a, b) => a.start - b.start)

        // interleave: a gap's lines are all hidden, so it sorts before any later row
        const merged: TableEntry[] = []
        let gapIndex = 0
        let lineIndex = 0
        for (const row of rows) {
            while (
                gapIndex < gaps.length &&
                gaps[gapIndex].start < row.line.csvLineNumber
            ) {
                merged.push({ type: "gap", gap: gaps[gapIndex] })
                gapIndex++
            }
            merged.push({ type: "line", ...row, index: lineIndex })
            lineIndex++
        }
        for (; gapIndex < gaps.length; gapIndex++) {
            merged.push({ type: "gap", gap: gaps[gapIndex] })
        }
        return merged
    }, [response, state])

    return { entries, expand }
}
