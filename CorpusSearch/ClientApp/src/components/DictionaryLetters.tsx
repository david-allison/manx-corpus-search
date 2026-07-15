import { useEffect, useState } from "react"
import { Link } from "react-router-dom"
import { dictionaryBrowse } from "../api/DictionaryApi"
import "./DictionaryLetters.css"

/** The dictionary the letters open. Phil Kelly is the fullest word list the
 * site has — 66,000 headwords against Cregeen's 3,396 — so it is the one worth
 * offering to someone who has not asked for a word yet. */
const BROWSE_DICT = "phil-kelly"

/** A|B|C with no word looked up: the way into the dictionary for a reader who
 * came to browse rather than to search. */
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

    if (letters.length === 0) {
        return null
    }

    return (
        <nav className="dict-letters-home" aria-label="Browse by letter">
            {letters.map((letter) => (
                <Link
                    key={letter}
                    to={`/dictionary/browse/${BROWSE_DICT}/${encodeURIComponent(letter)}`}
                >
                    {letter}
                </Link>
            ))}
        </nav>
    )
}
