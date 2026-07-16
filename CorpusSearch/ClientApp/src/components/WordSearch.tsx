import { FormEvent, useEffect, useState } from "react"
import { Link, useNavigate } from "react-router-dom"
import { dictionaryWordUrl } from "../utils/DictionaryEntries"
import "./WordSearch.css"

/** The dictionary's look-up box: type a word, get its page.
 *
 * Keeps the scope the reader is already in — a word looked up from inside
 * Cregeen opens in Cregeen, rather than quietly widening to every book.
 *
 * Shared by the word page and the index, which are the two places a reader
 * arrives wanting a different word than the one in front of them.
 */
export const WordSearch = ({
    word,
    dict,
    indexUrl,
}: {
    /** the word the box starts on: the page's own, so it can be edited rather
     * than retyped */
    word?: string
    dict?: string
    /** where "up" goes, for a page that has an up: the index the word is filed
     * in. Omitted on the index itself, which is already there */
    indexUrl?: string
}) => {
    const navigate = useNavigate()
    const [query, setQuery] = useState(word ?? "")

    // the walk changes the word under the box: what it offers to edit should be
    // the word you are looking at, not the one you arrived by
    useEffect(() => setQuery(word ?? ""), [word])

    const onSubmit = (event: FormEvent) => {
        event.preventDefault()
        const trimmed = query.trim()
        if (trimmed) {
            void navigate(dictionaryWordUrl(trimmed, dict))
        }
    }

    return (
        <form className="dict-page-search" onSubmit={onSubmit}>
            {/* the way out of the word and back to the index it is filed in. A
                page-level control, so it keeps the page's own top row rather
                than the headword walk's: that row is the walk, and stepping out
                of it is not a step in it. */}
            {indexUrl && (
                <Link
                    className="dict-page-index"
                    to={indexUrl}
                    title="Back to the index"
                    aria-label="Back to the index"
                >
                    {/* drawn, not typed, so it does not depend on how a font sets
                        an arrow: ⌃ came out small and high, being the
                        modifier-key caret, and read as a stray mark.

                        Back rather than up: the index is where a reader browsing
                        came from, and the walk's ‹ › below already mean the step
                        either side. The tooltip says where back is, because this
                        goes to the index however you arrived. */}
                    <svg viewBox="0 0 16 16" aria-hidden="true">
                        <path
                            d="M13.5 8H3M7.5 3.5 3 8l4.5 4.5"
                            fill="none"
                            stroke="currentColor"
                            strokeWidth="1.75"
                            strokeLinecap="round"
                            strokeLinejoin="round"
                        />
                    </svg>
                </Link>
            )}
            <input
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                placeholder="Look up a Manx word…"
                aria-label="Look up a Manx word"
            />
            <button type="submit">Look up</button>
        </form>
    )
}
