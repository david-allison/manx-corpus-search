import { useEffect, useState } from "react"
import { Link, useParams } from "react-router-dom"
import { CircularProgress } from "@mui/material"
import {
    dictionaryBrowse,
    DictionaryBrowseResponse,
    DictionaryInfo,
    dictionaryList,
} from "../api/DictionaryApi"
import { dictionaryWordUrl } from "../utils/DictionaryEntries"
import "./DictionaryBrowse.css"

const browseUrl = (dict: string, at?: string) =>
    at
        ? `/dictionary/browse/${encodeURIComponent(dict)}/${encodeURIComponent(at)}`
        : `/dictionary/browse/${encodeURIComponent(dict)}`

/** A row of links with the current one marked: the letters, and the prefixes
 * under a letter. Wears the Razor Cregeen browser's look (site-design.css
 * .dict-letters), which only the server-rendered pages load. */
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

/** The dictionary as a book you can open at a letter.
 *
 * The prefix bar is as deep as each letter needs: Cregeen's 'a' is 19 two-letter
 * groups, its 'c' 57 three-letter ones. See DictionaryBrowse.DepthFor.
 */
export const DictionaryBrowse = () => {
    const { dict = "", at } = useParams()
    const [page, setPage] = useState<DictionaryBrowseResponse | null>(null)
    const [dictionaries, setDictionaries] = useState<DictionaryInfo[]>([])
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
                    {page.prefixes.length > 1 && (
                        <Bar
                            items={page.prefixes}
                            active={page.prefix}
                            dict={page.slug}
                            ariaLabel={`Words under ${page.letter}`}
                        />
                    )}
                    <dl className="dict-browse-list">
                        {page.headwords.map((entry, index) => (
                            <div
                                className="dict-browse-row"
                                key={`${entry.word}-${index}`}
                            >
                                <dt>
                                    <Link
                                        to={dictionaryWordUrl(
                                            entry.word,
                                            page.slug,
                                        )}
                                    >
                                        {entry.word}
                                    </Link>
                                </dt>
                                <dd>{entry.gloss}</dd>
                            </div>
                        ))}
                    </dl>
                </>
            )}
        </div>
    )
}
