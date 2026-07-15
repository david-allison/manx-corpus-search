import { HighlightRange } from "../api/SearchApi"

/** How many words of context each side, where the caller doesn't say: the
 * search results' long-standing window */
const DEFAULT_WORDS = 5

/** Walks `words` words out from `from`, returning where it stopped.
 *
 * The space beside the match separates it from its context rather than standing
 * between two words of it, so it is stepped over before counting starts: going
 * forwards that happens for free (the walk tests the character after the one it
 * starts on), going backwards it has to be done by hand. Without this the
 * window is lopsided — `words` back but `words` - 1 forward.
 */
const wordsOut = (
    sample: string,
    from: number,
    words: number,
    step: -1 | 1,
): number => {
    let index = step < 0 && sample[from - 1] === " " ? from - 1 : from
    let count = 0
    let lastSpace = false
    const done = () => (step < 0 ? index <= 0 : index >= sample.length)
    while (!done() && count < words) {
        index += step
        if (sample[index] === " ") {
            if (!lastSpace) {
                count++
            }
            lastSpace = true
        } else {
            lastSpace = false
        }
    }
    return index
}

/**
 * Splits a sample line around the first highlighted match (server-computed offsets, see #40),
 * keeping up to `words` words of context on each side.
 *
 * @returns null when the server provided no highlights (e.g. English searches):
 * the caller shows the sample unhighlighted
 */
export function buildKwic(
    sample: string,
    highlights: HighlightRange[],
    words: number = DEFAULT_WORDS,
): { pre: string; match: string; post: string } | null {
    if (highlights.length === 0) {
        return null
    }
    const match = highlights[0]
    const startIndex = wordsOut(sample, match.start, words, -1)
    const endIndex = wordsOut(sample, match.end, words, 1)

    return {
        pre: sample.substring(startIndex, match.start),
        match: sample.substring(match.start, match.end),
        post: sample.substring(match.end, endIndex),
    }
}
