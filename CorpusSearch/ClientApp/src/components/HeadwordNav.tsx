import { useEffect, useState } from "react"
import {
    dictionaryNeighbours,
    DictionaryNeighboursResponse,
} from "../api/DictionaryApi"
import { dictionaryWordUrl } from "../utils/DictionaryEntries"
import { PrevNextLinks } from "./PrevNextLinks"

/** The headwords either side of this one: the dictionary as a book you turn a
 * page in.
 *
 * Scoped to one dictionary the order is that book's own. Across all of them it
 * is the union in collation order — nobody's printed order, but the only one
 * several books can share.
 */
export const HeadwordNav = ({
    word,
    dict,
}: {
    word: string
    dict?: string
}) => {
    const [near, setNear] = useState<DictionaryNeighboursResponse | null>(null)

    useEffect(() => {
        setNear(null)
        const abort = new AbortController()
        dictionaryNeighbours(word, dict, abort.signal)
            .then(setNear)
            .catch((e) => {
                if (!abort.signal.aborted) console.warn(e)
            })
        return () => abort.abort()
    }, [word, dict])

    // a word with nothing either side is not in a book we can step through
    if (near == null || (!near.previous && !near.next)) {
        return null
    }

    return (
        <PrevNextLinks
            ariaLabel="Headwords"
            previous={
                near.previous
                    ? {
                          to: dictionaryWordUrl(near.previous, dict),
                          label: near.previous,
                      }
                    : null
            }
            next={
                near.next
                    ? {
                          to: dictionaryWordUrl(near.next, dict),
                          label: near.next,
                      }
                    : null
            }
        >
            <span className="dict-page-headword-nav-word">{word}</span>
        </PrevNextLinks>
    )
}
