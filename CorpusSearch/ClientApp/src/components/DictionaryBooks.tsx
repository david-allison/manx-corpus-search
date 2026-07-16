import { useEffect, useState } from "react"
import { Link } from "react-router-dom"
import {
    dictionariesAlreadyKnown,
    DictionaryInfo,
    dictionaryList,
} from "../api/DictionaryApi"
import "./DictionaryScope.css"

/** The books, as a row of links into each one's index.
 *
 * Wears DictionaryScope's look, and deliberately: that picker points the same
 * row of names at a word, this one at an index, and a reader should not have to
 * learn the control twice.
 *
 * There is no "all dictionaries" here, unlike on a word page. A word can be
 * looked up in every book at once because the answers stack; an index cannot be,
 * because the books disagree about the order — Cregeen files 'faar-y-chaagh'
 * among the 'caa' words, and collating the union would print none of them as
 * their book does. See ISearchDictionary.Headwords.
 *
 * Paints from what is already known where it can: the list is fixed at deploy,
 * and the row must not blink in after the page. */
export const DictionaryBooks = ({ active }: { active?: string }) => {
    const [dictionaries, setDictionaries] = useState<DictionaryInfo[]>(
        () => dictionariesAlreadyKnown() ?? [],
    )

    useEffect(() => {
        const abort = new AbortController()
        dictionaryList(abort.signal)
            .then(setDictionaries)
            .catch((e) => {
                if (!abort.signal.aborted) console.warn(e)
            })
        return () => abort.abort()
    }, [])

    if (dictionaries.length === 0) {
        return null
    }

    return (
        <nav className="dict-scope" aria-label="Dictionary">
            {dictionaries.map((d) => (
                <Link
                    key={d.slug}
                    className={
                        d.slug === active
                            ? "dict-scope-link active"
                            : "dict-scope-link"
                    }
                    aria-current={d.slug === active ? "page" : undefined}
                    to={`/dictionary/browse/${encodeURIComponent(d.slug)}`}
                >
                    {d.name}
                </Link>
            ))}
        </nav>
    )
}
