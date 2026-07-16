import { useEffect, useState } from "react"
import {
    dictionaryHistory,
    DictionaryHistoryResponse,
} from "../api/DictionaryApi"

/** The lexeme's corpus history, fetched alongside the word's entries rather
 * than after them: the first-attestation band reads it, and it heads the page,
 * so it must not wait on the dictionary lookup to start.
 *
 * The word before's history stays until this word's lands, and is not blanked
 * first: the band is tall, and dropping it between one headword and the next
 * takes a chunk out of the middle of the page. `word` on the response says whose
 * it is — the caller fades what has not caught up yet. */
export const useWordHistory = (
    word: string | undefined,
): DictionaryHistoryResponse | null => {
    const [history, setHistory] = useState<DictionaryHistoryResponse | null>(
        null,
    )

    useEffect(() => {
        if (!word) {
            setHistory(null)
            return
        }
        const abort = new AbortController()
        dictionaryHistory(word, abort.signal)
            .then(setHistory)
            .catch((e) => {
                if (!abort.signal.aborted) console.warn(e)
            })
        return () => abort.abort()
    }, [word])

    return history
}
