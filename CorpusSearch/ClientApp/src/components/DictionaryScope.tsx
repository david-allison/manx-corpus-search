import { useEffect, useRef, useState } from "react"
import { Link } from "react-router-dom"
import {
    dictionariesAlreadyKnown,
    DictionaryInfo,
    dictionaryList,
} from "../api/DictionaryApi"
import { dictionaryWordUrl } from "../utils/DictionaryEntries"
import "./DictionaryScope.css"

/** Which dictionary the word page is showing: every source at once, or one on
 * its own.
 *
 * Lists every dictionary rather than only those defining the word — that Cregeen
 * has no entry for it is itself worth being able to find out — but the ones with
 * nothing to show are greyed, so finding it out costs no clicks. Greyed, not
 * hidden or disabled: the answer is "not in this book", and a reader is entitled
 * to go and see the empty page for themselves.
 *
 * `answering` is absent until the page arrives. Nothing is greyed until then:
 * greying every link and lifting it a moment later would read as a fault.
 */
export const DictionaryScope = ({
    word,
    dict,
    answering,
}: {
    word: string
    dict?: string
    answering?: string[] | null
}) => {
    // already known on every step after the first: the picker paints with the
    // page rather than arriving after it
    const [dictionaries, setDictionaries] = useState<DictionaryInfo[] | null>(
        dictionariesAlreadyKnown,
    )

    useEffect(() => {
        if (dictionaries != null) {
            return
        }
        const abort = new AbortController()
        dictionaryList(abort.signal)
            .then(setDictionaries)
            .catch((e) => {
                if (!abort.signal.aborted) console.warn(e)
            })
        return () => abort.abort()
    }, [dictionaries])

    // the chosen book must be seen to be chosen: on a phone the row scrolls
    // sideways, and the active tab may sit past its edge
    const nav = useRef<HTMLElement>(null)
    useEffect(() => {
        nav.current
            ?.querySelector(".dict-scope-link.active")
            ?.scrollIntoView({ block: "nearest", inline: "nearest" })
    }, [dict, dictionaries])

    // the picker offers nothing until it knows what there is to pick, but it
    // holds its row: coming back with a height would shove the page down
    if (dictionaries == null) {
        return (
            <nav className="dict-scope dict-scope-waiting" aria-hidden="true" />
        )
    }
    if (dictionaries.length === 0) {
        return null
    }

    /** A link to a page that would have nothing on it. "All dictionaries" is
     * empty only when every one of them is. */
    const isEmpty = (slug?: string): boolean =>
        answering != null &&
        (slug == null ? answering.length === 0 : !answering.includes(slug))

    const linkTo = (slug: string | undefined, name: string) => {
        // a phone has no room for "Manx to English" twice over: the direction
        // shortens to M2E there, and the full name stays for a mouse and for
        // a screen reader alike
        const short = name
            .replace("Manx to English", "M2E")
            .replace("English to Manx", "E2M")
        return (
            <Link
                key={slug ?? ""}
                className={[
                    "dict-scope-link",
                    slug === dict ? "active" : "",
                    isEmpty(slug) ? "dict-scope-empty" : "",
                ]
                    .filter(Boolean)
                    .join(" ")}
                aria-current={slug === dict ? "page" : undefined}
                aria-label={short === name ? undefined : name}
                // the grey is a colour, and a colour is not something every reader
                // gets: the link says the same thing in words
                title={
                    isEmpty(slug)
                        ? `Nothing for “${word}” in ${name}`
                        : undefined
                }
                to={dictionaryWordUrl(word, slug)}
                // the same word through another book's lens: a view switch, so it
                // replaces rather than stacks — Back should leave the word, not
                // replay the tabs
                replace
            >
                {short === name ? (
                    name
                ) : (
                    <>
                        <span className="dict-scope-name-full">{name}</span>
                        <span
                            className="dict-scope-name-short"
                            aria-hidden="true"
                        >
                            {short}
                        </span>
                    </>
                )}
            </Link>
        )
    }

    return (
        <nav className="dict-scope" aria-label="Dictionary">
            {linkTo(undefined, "All dictionaries")}
            {dictionaries.map((d) => linkTo(d.slug, d.name))}
        </nav>
    )
}
