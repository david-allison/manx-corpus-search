import { useEffect, useState } from "react"
import { Link } from "react-router-dom"
import { dictionaryBrowse } from "../api/DictionaryApi"
import { DictionaryBooks } from "./DictionaryBooks"
import "./DictionaryLetters.css"

/** The dictionary the letters open. Cregeen is a book with an index and browses
 * as one: its longest letter is 465 headwords, a page you can read down. Phil
 * Kelly is fuller — 66,000 headwords — but it is a translation list rather than
 * a book, and its 'c' alone is 10,773, which is no page at all. The reader who
 * has not asked for a word yet is better met by the one they can browse. */
const BROWSE_DICT = "cregeen"

/** The way in for a reader who came to browse rather than to search: the books,
 * and the letters of the one this opens at.
 *
 * The books are named above the letters because the letters are one book's. They
 * were Cregeen's before too, and said so nowhere — an unlabelled A|B|C that gave
 * a reader no way to know whose index they were reading, nor that there were
 * others to read. */
export const DictionaryLetters = () => {
    const [letters, setLetters] = useState<string[]>([])

    useEffect(() => {
        const abort = new AbortController()
        dictionaryBrowse(BROWSE_DICT, undefined, abort.signal)
            .then((page) => setLetters(page.letters))
            .catch((e) => {
                if (!abort.signal.aborted) console.warn(e)
            })
        return () => abort.abort()
    }, [])

    return (
        <>
            <DictionaryBooks active={BROWSE_DICT} />
            {letters.length > 0 && (
                <nav
                    className="dict-letters-home"
                    aria-label="Browse by letter"
                >
                    {letters.map((letter) => (
                        <Link
                            key={letter}
                            to={`/dictionary/browse/${BROWSE_DICT}/${encodeURIComponent(letter)}`}
                        >
                            {letter}
                        </Link>
                    ))}
                </nav>
            )}
        </>
    )
}
