import { useEffect, useState } from "react"
import {
    dictionaryHistory,
    DictionaryHistoryResponse,
} from "../api/DictionaryApi"

/** The lexeme's corpus history, fetched alongside the word's entries rather
 * than after them: the first-attestation band reads it, and it heads the page,
 * so it must not wait on the dictionary lookup to start. */
export const useWordHistory = (
    word: string | undefined,
): DictionaryHistoryResponse | null => {
    const [history, setHistory] = useState<DictionaryHistoryResponse | null>(
        null,
    )

    useEffect(() => {
        setHistory(null)
        if (!word) return
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
