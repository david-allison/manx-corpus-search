import { useEffect, useState } from "react"
import { Link } from "react-router-dom"
import {
    dictionariesAlreadyKnown,
    DictionaryInfo,
    dictionaryList,
} from "../api/DictionaryApi"
import { dictionaryWordUrl } from "../utils/DictionaryEntries"
import "./DictionaryScope.css"

/** Which dictionary the word page is showing: every source at once, or one on
 * its own. Lists every dictionary rather than only those defining the word —
 * that Cregeen has no entry for it is itself worth being able to find out. */
export const DictionaryScope = ({
    word,
    dict,
}: {
    word: string
    dict?: string
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

    return (
        <nav className="dict-scope" aria-label="Dictionary">
            <Link
                className={dict ? "dict-scope-link" : "dict-scope-link active"}
                aria-current={dict ? undefined : "page"}
                to={dictionaryWordUrl(word)}
            >
                All dictionaries
            </Link>
            {dictionaries.map((d) => (
                <Link
                    key={d.slug}
                    className={
                        d.slug === dict
                            ? "dict-scope-link active"
                            : "dict-scope-link"
                    }
                    aria-current={d.slug === dict ? "page" : undefined}
                    to={dictionaryWordUrl(word, d.slug)}
                >
                    {d.name}
                </Link>
            ))}
        </nav>
    )
}
