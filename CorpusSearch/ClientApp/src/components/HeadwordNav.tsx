import { ReactNode, useEffect, useState } from "react"
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
 *
 * The way out of the walk and back to the index is not here: it belongs to the
 * page rather than to the walk, and sits by the search box.
 */
export const HeadwordNav = ({
    word,
    dict,
    spoken,
    children,
}: {
    word: string
    dict?: string
    /** walk the spoken dictionary instead: the neighbours are the heard
     * words, and every step carries the walk along in its URL */
    spoken?: boolean
    /** what the walk is centred on: the page's title, when the title has nothing
     * else to distinguish it from this row */
    children: ReactNode
}) => {
    const [near, setNear] = useState<DictionaryNeighboursResponse | null>(null)
    const scope = spoken ? "spoken" : dict
    const urlOf = (to: string) =>
        spoken
            ? `${dictionaryWordUrl(to)}?nav=spoken`
            : dictionaryWordUrl(to, dict)

    useEffect(() => {
        // the step you just took must not blank this row first. This row *is*
        // what you clicked: dropping it to nothing and growing it back a moment
        // later takes the arrows out from under the cursor and slides the page
        // up into the gap, and the walk is meant to be clicked through. The
        // neighbours you stepped from ride the beat until the new ones land —
        // they are about to be replaced by their own neighbours anyway.
        const abort = new AbortController()
        dictionaryNeighbours(word, scope, abort.signal)
            .then(setNear)
            .catch((e) => {
                if (!abort.signal.aborted) console.warn(e)
            })
        return () => abort.abort()
    }, [word, scope])

    // a word with nothing either side is not in a book we can step through
    if (near == null || (!near.previous && !near.next)) {
        return null
    }

    const step = (to: string | null | undefined, attested: boolean) =>
        to
            ? {
                  to: urlOf(to),
                  label: to,
                  title: attested ? to : `${to}: in no text in the corpus`,
                  muted: !attested,
                  // a spoken step is a step to a word you can hear: the
                  // speaker at the button's inner extremity, facing the
                  // word being read
                  edge: spoken ? "🔊" : undefined,
              }
            : null

    // the skip steps carry no name: the row already holds two of them and the
    // word itself, and the arrow plus its tooltip is the whole of what they say
    const skip = (to: string | null | undefined) =>
        to
            ? {
                  to: urlOf(to),
                  label: to,
                  title: `The nearest word the corpus uses`,
              }
            : null

    // every spoken step is a heard word, so the skip-to-attested arrows have
    // nowhere to point and are not offered
    return (
        <PrevNextLinks
            ariaLabel={spoken ? "Spoken headwords" : "Headwords"}
            previous={step(near.previous, near.previousAttested)}
            next={step(near.next, near.nextAttested)}
            farPrevious={spoken ? undefined : skip(near.previousUsed)}
            farNext={spoken ? undefined : skip(near.nextUsed)}
        >
            {children}
        </PrevNextLinks>
    )
}
