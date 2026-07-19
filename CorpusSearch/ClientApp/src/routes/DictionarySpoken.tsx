import { useEffect, useRef, useState } from "react"
import { Link, useSearchParams } from "react-router-dom"
import { CircularProgress } from "@mui/material"
import { DictionaryBrowseResponse, spokenIndex } from "../api/DictionaryApi"
import { WordSearch } from "../components/WordSearch"
import { useActiveInRow } from "../hooks/useActiveInRow"
import { useDictionaryHead } from "../hooks/useDictionaryHead"
import "./DictionaryBrowse.css"
import "./DictionaryLemma.css"

export const spokenIndexUrl = (at?: string | null) =>
    at
        ? `/dictionary/spoken?at=${encodeURIComponent(at)}`
        : "/dictionary/spoken"

/** The letters, with the open one marked: the browse page's bar, over the
 * heard words */
const Bar = ({
    items,
    active,
    ariaLabel,
}: {
    items: string[]
    active?: string | null
    ariaLabel: string
}) => {
    const nav = useRef<HTMLElement>(null)
    useActiveInRow(nav, active)
    return (
        <nav className="dict-browse-bar" aria-label={ariaLabel} ref={nav}>
            {items.map((item) => (
                <Link
                    key={item}
                    to={spokenIndexUrl(item)}
                    className={item === active ? "active" : undefined}
                    aria-current={item === active ? "page" : undefined}
                >
                    {item}
                </Link>
            ))}
        </nav>
    )
}

/** The spoken dictionary at /dictionary/spoken: every word the recordings say
 * that some book answers for, one letter at a time. Each word's own page
 * carries the recording to jump into, so the listing greys nothing and
 * vouches for nothing - being heard is the ticket in. */
export const DictionarySpoken = () => {
    const [params] = useSearchParams()
    const at = params.get("at")
    const [page, setPage] = useState<DictionaryBrowseResponse | null>(null)
    // the server reads the recordings behind its boot: a 404 is "not yet",
    // not "never", and the page says so
    const [unread, setUnread] = useState(false)
    const [failed, setFailed] = useState(false)

    useDictionaryHead("Heard spoken")

    useEffect(() => {
        window.scrollTo(0, 0)
    }, [at])

    useEffect(() => {
        setPage(null)
        setUnread(false)
        setFailed(false)
        const abort = new AbortController()
        spokenIndex(at, abort.signal)
            .then((result) =>
                result == null ? setUnread(true) : setPage(result),
            )
            .catch((e) => {
                if (!abort.signal.aborted) {
                    console.warn(e)
                    setFailed(true)
                }
            })
        return () => abort.abort()
    }, [at])

    return (
        <div className="dict-page">
            <WordSearch />
            {failed && <p>Something went wrong. Try again.</p>}
            {unread && (
                <p>The recordings are still being read. Try again shortly.</p>
            )}
            {!failed && !unread && page == null && (
                <div className="dict-page-loading">
                    <CircularProgress />
                </div>
            )}

            {page != null && (
                <>
                    <h1 className="dict-page-word">
                        🔊 Heard spoken
                        <span className="attest-experimental">
                            experimental &amp; incomplete
                        </span>
                    </h1>
                    <p className="dict-lemma-note">
                        Every word a recording says that the dictionaries answer
                        for. Open one to read its entry and hear it.
                    </p>
                    <Bar
                        items={page.letters}
                        active={page.letter}
                        ariaLabel="Letters"
                    />
                    <div
                        className="dict-browse-chapters"
                        aria-label={`Heard words under ${page.letter}`}
                    >
                        {page.chapters.map((chapter, index) => (
                            <div
                                className="dict-browse-chapter"
                                key={`${chapter.key}-${index}`}
                            >
                                <span className="dict-browse-key">
                                    {chapter.key}
                                </span>
                                <p className="dict-browse-words">
                                    {chapter.words.map((entry, position) => (
                                        <span key={`${entry.word}-${position}`}>
                                            {position > 0 && (
                                                <span
                                                    className="dict-browse-sep"
                                                    aria-hidden="true"
                                                >
                                                    {" · "}
                                                </span>
                                            )}
                                            {/*the walk rides along: the page's
                                               back/forward steps the heard
                                               words, not the whole union*/}
                                            <Link
                                                to={`/dictionary/${encodeURIComponent(
                                                    entry.word,
                                                )}?nav=spoken`}
                                            >
                                                {entry.word}
                                            </Link>
                                        </span>
                                    ))}
                                </p>
                            </div>
                        ))}
                    </div>

                    {/* the letters again, as the browse repeats them: a reader
                        at the foot of a long letter can turn without scrolling
                        back up for the bar */}
                    {page.letters.length > 0 && (
                        <Bar
                            items={page.letters}
                            active={page.letter}
                            ariaLabel="Letters, again"
                        />
                    )}
                </>
            )}
        </div>
    )
}
