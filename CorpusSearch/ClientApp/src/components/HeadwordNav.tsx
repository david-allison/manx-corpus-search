import { ReactNode, useEffect, useState } from "react"
import {
    dictionaryNeighbours,
    DictionaryNeighboursResponse,
} from "../api/DictionaryApi"
import { dictionaryWordUrl } from "../utils/DictionaryEntries"
import { PrevNextLinks } from "./PrevNextLinks"

/** The index a word with no scope goes back to. A page of every dictionary at
 * once has no one index behind it, and Cregeen is the one that browses as a
 * book: it is what /dictionary opens too. See DictionaryLetters. */
const INDEX_DICT = "cregeen"

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
    children,
}: {
    word: string
    dict?: string
    /** what the walk is centred on: the page's title, when the title has nothing
     * else to distinguish it from this row */
    children: ReactNode
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

    const step = (to: string | null | undefined, attested: boolean) =>
        to
            ? {
                  to: dictionaryWordUrl(to, dict),
                  label: to,
                  title: attested ? to : `${to} — in no text in the corpus`,
                  muted: !attested,
              }
            : null

    // the skip steps carry no name: the row already holds two of them and the
    // word itself, and the arrow plus its tooltip is the whole of what they say
    const skip = (to: string | null | undefined) =>
        to
            ? {
                  to: dictionaryWordUrl(to, dict),
                  label: to,
                  title: `${to} — the nearest word the corpus uses`,
              }
            : null

    // the letter this word is filed under, without having to work out which:
    // browse takes a whole word for its 'at' and opens the letter it starts,
    // folding ç to c the way the books do
    const index = {
        to: `/dictionary/browse/${encodeURIComponent(dict ?? INDEX_DICT)}/${encodeURIComponent(word)}`,
        label: "Index",
        title: "Back to the index",
    }

    return (
        <PrevNextLinks
            ariaLabel="Headwords"
            up={index}
            previous={step(near.previous, near.previousAttested)}
            next={step(near.next, near.nextAttested)}
            farPrevious={skip(near.previousUsed)}
            farNext={skip(near.nextUsed)}
        >
            {children}
        </PrevNextLinks>
    )
}
