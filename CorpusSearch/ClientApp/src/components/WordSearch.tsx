import { FormEvent, KeyboardEvent, useEffect, useState } from "react"
import { Link, useNavigate } from "react-router-dom"
import { dictionarySuggest, DictionarySuggestions } from "../api/DictionaryApi"
import { dictionaryWordUrl } from "../utils/DictionaryEntries"
import "./WordSearch.css"

/** The dictionary's look-up box: type a word, get its page.
 *
 * Typing offers a handful of completions beneath the box, commonest first —
 * or, when nothing the books hold begins with what was typed, near spellings,
 * said to be that. A handful only: the box is offering a next keystroke, not
 * an index. The box starts on the page's own word, which needs no completing:
 * the offers begin when the reader edits.
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

    const [suggest, setSuggest] = useState<DictionarySuggestions | null>(null)
    const [open, setOpen] = useState(false)
    const [active, setActive] = useState(-1)

    const typed = query.trim()
    // the page's own word needs no completing
    const settled = typed === (word ?? "").trim()

    useEffect(() => {
        setActive(-1)
        if (!typed || settled) {
            setSuggest(null)
            setOpen(false)
            return
        }
        const abort = new AbortController()
        // a keystroke waits for the typing to settle
        const timer = setTimeout(() => {
            dictionarySuggest(typed, abort.signal)
                .then((result) => {
                    setSuggest(result)
                    setOpen(true)
                })
                .catch((e) => {
                    // no offers is a quieter box, never a broken one
                    if (!abort.signal.aborted) console.warn(e)
                })
        }, 150)
        return () => {
            clearTimeout(timer)
            abort.abort()
        }
    }, [typed, settled])

    const pick = (target: string) => {
        setOpen(false)
        setQuery(target)
        void navigate(dictionaryWordUrl(target, dict))
    }

    const onSubmit = (event: FormEvent) => {
        event.preventDefault()
        if (open && active >= 0 && suggest?.words[active] != null) {
            pick(suggest.words[active].word)
            return
        }
        if (typed) {
            setOpen(false)
            void navigate(dictionaryWordUrl(typed, dict))
        }
    }

    const shown = open && suggest != null && suggest.words.length > 0

    const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
        if (!shown) {
            return
        }
        const count = suggest.words.length
        if (event.key === "ArrowDown") {
            event.preventDefault()
            setActive((index) => (index + 1) % count)
        } else if (event.key === "ArrowUp") {
            event.preventDefault()
            setActive((index) => (index <= 0 ? count - 1 : index - 1))
        } else if (event.key === "Escape") {
            setOpen(false)
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
            <div className="dict-search-box">
                <input
                    value={query}
                    onChange={(e) => setQuery(e.target.value)}
                    onKeyDown={onKeyDown}
                    onFocus={() => {
                        if (suggest != null && !settled) setOpen(true)
                    }}
                    onBlur={() => setOpen(false)}
                    placeholder="Look up a Manx word…"
                    aria-label="Look up a Manx word"
                    role="combobox"
                    aria-expanded={shown}
                    aria-controls="dict-search-suggest"
                    aria-activedescendant={
                        shown && active >= 0
                            ? `dict-search-suggest-${active.toString()}`
                            : undefined
                    }
                />
                {shown && (
                    <div
                        className="dict-search-suggest"
                        id="dict-search-suggest"
                        role="listbox"
                        aria-label="Suggested entries"
                    >
                        {suggest.fuzzy && (
                            <p className="dict-search-suggest-note">
                                Nothing begins with “{typed}”. Near spellings:
                            </p>
                        )}
                        {suggest.words.map((entry, index) => (
                            <button
                                type="button"
                                role="option"
                                id={`dict-search-suggest-${index.toString()}`}
                                aria-selected={index === active}
                                key={entry.word}
                                className={[
                                    "dict-search-suggest-word",
                                    index === active
                                        ? "dict-search-suggest-active"
                                        : "",
                                    entry.attested ? "" : "dict-unattested",
                                ]
                                    .filter(Boolean)
                                    .join(" ")}
                                title={
                                    entry.attested
                                        ? undefined
                                        : `${entry.word} is in no text in the corpus: a dictionary word`
                                }
                                // mousedown, not click alone: the input's blur
                                // would close the list before a click landed
                                onMouseDown={(e) => e.preventDefault()}
                                onClick={() => pick(entry.word)}
                            >
                                {entry.word}
                            </button>
                        ))}
                    </div>
                )}
            </div>
            <button type="submit">Look up</button>
        </form>
    )
}
