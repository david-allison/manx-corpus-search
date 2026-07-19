import { ReactNode, useEffect, useRef, useState } from "react"
import {
    dictionaryNeighbours,
    DictionaryNeighboursResponse,
} from "../api/DictionaryApi"
import { dictionaryWordUrl } from "../utils/DictionaryEntries"
import { PrevNextLinks } from "./PrevNextLinks"

/** How many windows either side ride along with each answer: a reader can tap
 * this many steps ahead of the network before the walk has to catch up, and
 * the re-centring below means they never do at a walking pace. */
const SPAN = 5

/** The headwords either side of this one: the dictionary as a book you turn a
 * page in.
 *
 * Scoped to one dictionary the order is that book's own. Across all of them it
 * is the union in collation order — nobody's printed order, but the only one
 * several books can share.
 *
 * The walk is meant to be tapped through faster than its answers arrive, so
 * three things keep the arrows live between them: every answer brings the
 * complete windows of the pages either side (SPAN of them each way, stepped
 * through from memory and re-centred as the reader nears the edge), every
 * window seen is remembered (a step back must not wait on the network), and a
 * step ahead of all of it re-points what it can — the arrow back is the page
 * just left. Without this the row rode out each step showing the *last*
 * page's neighbours, whose "next" was the page already on screen: a tap on it
 * went nowhere, and a reader walking briskly was stopped once per fetch.
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
    const scope = spoken ? "spoken" : dict
    // the windows this walk has seen or prefetched, by scope and word: the
    // headword lists are fixed at deploy, so a window once fetched is final
    const seen = useRef(new Map<string, DictionaryNeighboursResponse>())
    const inFlight = useRef(new Set<string>())
    // the last answered step: what the optimistic shift and the stale hold
    // below are read off while this word's own window is on its way
    const [settled, setSettled] = useState<{
        key: string
        word: string
        near: DictionaryNeighboursResponse
    } | null>(null)

    useEffect(() => {
        const keyOf = (w: string) => `${scope ?? ""}|${w}`

        // one answer seeds the whole span: the word's own window and the
        // complete windows either side, arrows and skips included
        const seed = (near: DictionaryNeighboursResponse) => {
            seen.current.set(keyOf(near.word), near)
            for (const window of near.nearby ?? []) {
                seen.current.set(keyOf(window.word), window)
            }
        }

        // re-centres the span on this word, in the background: fired from a
        // cache hit whose neighbour is missing — the span's edge is near, and
        // the reader must not reach it before the next span lands
        const extend = () => {
            const k = keyOf(word)
            if (inFlight.current.has(k)) {
                return
            }
            inFlight.current.add(k)
            dictionaryNeighbours(word, scope, undefined, SPAN)
                .then(seed)
                .catch(() => undefined)
                .finally(() => inFlight.current.delete(k))
        }

        const k = keyOf(word)
        const cached = seen.current.get(k)
        if (cached) {
            setSettled((previous) =>
                previous?.key === k ? previous : { key: k, word, near: cached },
            )
            if (
                [cached.previous, cached.next].some(
                    (target) =>
                        target != null && !seen.current.has(keyOf(target)),
                )
            ) {
                extend()
            }
            return
        }
        const abort = new AbortController()
        dictionaryNeighbours(word, scope, abort.signal, SPAN)
            .then((near) => {
                seed(near)
                setSettled({ key: k, word, near })
            })
            .catch((e) => {
                if (!abort.signal.aborted) console.warn(e)
            })
        return () => abort.abort()
    }, [word, scope])

    // this word's window: its own if known — the effect above may not have
    // settled a prefetched answer yet, so the cache is read directly — else
    // what the step it came by already knew, else the row it left riding
    // (a jump from the search box has nothing to shift; the stale row holds
    // the space, exactly as it did before the answer beat it here)
    const cached = seen.current.get(`${scope ?? ""}|${word}`)
    const near =
        cached ??
        (settled == null
            ? null
            : settled.near.next === word
              ? // stepped forward: the way back is the page just left. What
                // lies ahead is genuinely unknown, and the slot holds empty
                // for the beat rather than pointing somewhere wrong.
                {
                    word,
                    attested: true,
                    previous: settled.word,
                    previousAttested: settled.near.attested,
                    nextAttested: false,
                }
              : settled.near.previous === word
                ? {
                      word,
                      attested: true,
                      next: settled.word,
                      nextAttested: settled.near.attested,
                      previousAttested: false,
                  }
                : settled.near)

    const urlOf = (to: string) =>
        spoken
            ? `${dictionaryWordUrl(to)}?nav=spoken`
            : dictionaryWordUrl(to, dict)

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
