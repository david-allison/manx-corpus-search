import { useEffect, useState } from "react"
import { Link, useParams } from "react-router-dom"
import { CircularProgress } from "@mui/material"
import {
    dictionariesAlreadyKnown,
    dictionaryBrowse,
    DictionaryBrowseResponse,
    DictionaryInfo,
    dictionaryList,
} from "../api/DictionaryApi"
import { dictionaryWordUrl } from "../utils/DictionaryEntries"
import { WordSearch } from "../components/WordSearch"
import "./DictionaryBrowse.css"

const browseUrl = (dict: string, at?: string) =>
    at
        ? `/dictionary/browse/${encodeURIComponent(dict)}/${encodeURIComponent(at)}`
        : `/dictionary/browse/${encodeURIComponent(dict)}`

/** The letters, with the open one marked. Wears the Razor Cregeen browser's look
 * (site-design.css .dict-letters), which only the server-rendered pages load. */
const Bar = ({
    items,
    active,
    dict,
    ariaLabel,
}: {
    items: string[]
    active?: string | null
    dict: string
    ariaLabel: string
}) => (
    <nav className="dict-browse-bar" aria-label={ariaLabel}>
        {items.map((item) => (
            <Link
                key={item}
                to={browseUrl(dict, item)}
                className={item === active ? "active" : undefined}
                aria-current={item === active ? "page" : undefined}
            >
                {item}
            </Link>
        ))}
    </nav>
)

/** The dictionary as a book you can open at a letter: the whole letter, its
 * headwords under the three-letter prefixes they file under.
 *
 * A chapter key can come round twice, and a word can repeat, because both follow
 * the book rather than a sort. See DictionaryBrowse.Chapters.
 */
export const DictionaryBrowse = () => {
    const { dict = "", at } = useParams()
    const [page, setPage] = useState<DictionaryBrowseResponse | null>(null)
    // already known on every step after the first: the picker paints with the
    // page rather than blinking in after it
    const [dictionaries, setDictionaries] = useState<DictionaryInfo[]>(
        () => dictionariesAlreadyKnown() ?? [],
    )
    const [failed, setFailed] = useState(false)

    useEffect(() => {
        const abort = new AbortController()
        dictionaryList(abort.signal)
            .then(setDictionaries)
            .catch((e) => {
                if (!abort.signal.aborted) console.warn(e)
            })
        return () => abort.abort()
    }, [])

    useEffect(() => {
        setPage(null)
        setFailed(false)
        const abort = new AbortController()
        dictionaryBrowse(dict, at, abort.signal)
            .then(setPage)
            .catch((e) => {
                if (!abort.signal.aborted) {
                    console.warn(e)
                    setFailed(true)
                }
            })
        return () => abort.abort()
    }, [dict, at])

    return (
        <div className="dict-page">
            {/* a reader who knows the word they want should not have to find it
                in the index first. No way back to the index from here: this is
                it, and the letters are a row below. */}
            <WordSearch dict={dict} />

            {/* which dictionary is being browsed */}
            {dictionaries.length > 0 && (
                <nav className="dict-scope" aria-label="Dictionary">
                    {dictionaries.map((d) => (
                        <Link
                            key={d.slug}
                            className={
                                d.slug === dict
                                    ? "dict-scope-link active"
                                    : "dict-scope-link"
                            }
                            aria-current={d.slug === dict ? "page" : undefined}
                            to={browseUrl(d.slug)}
                        >
                            {d.name}
                        </Link>
                    ))}
                </nav>
            )}

            {failed && <p>No such dictionary.</p>}
            {!failed && page == null && (
                <div className="dict-page-loading">
                    <CircularProgress />
                </div>
            )}

            {page != null && (
                <>
                    <h1 className="dict-page-word">{page.dictionary}</h1>
                    <Bar
                        items={page.letters}
                        active={page.letter}
                        dict={page.slug}
                        ariaLabel="Letters"
                    />
                    {page.letters.length === 0 && (
                        // the JSON is downloaded on deployment: without it the
                        // dictionary is empty rather than broken
                        <p className="dict-browse-empty">
                            This dictionary has no entries loaded.
                        </p>
                    )}
                    <div
                        className="dict-browse-chapters"
                        aria-label={`Words under ${page.letter}`}
                    >
                        {/* the key repeats where the book doubles back, and a
                            word where the book prints it twice: neither is an
                            identity, so both are keyed by where they sit */}
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
                                            <Link
                                                className={
                                                    entry.attested
                                                        ? undefined
                                                        : "dict-unattested"
                                                }
                                                title={
                                                    entry.attested
                                                        ? undefined
                                                        : `${entry.word} — in no text in the corpus`
                                                }
                                                to={dictionaryWordUrl(
                                                    entry.word,
                                                    page.slug,
                                                )}
                                            >
                                                {entry.word}
                                            </Link>
                                        </span>
                                    ))}
                                </p>
                            </div>
                        ))}
                    </div>
                </>
            )}
        </div>
    )
}
