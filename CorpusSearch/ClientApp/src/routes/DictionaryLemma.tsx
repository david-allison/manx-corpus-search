import { useEffect, useState } from "react"
import { Link, useParams, useSearchParams } from "react-router-dom"
import { CircularProgress } from "@mui/material"
import { DictionaryBrowseResponse, lemmaIndex } from "../api/DictionaryApi"
import { LemmaTree, lemmaIndexUrl, lemmaTreeUrl } from "../components/LemmaTree"
import { WordSearch } from "../components/WordSearch"
import "./DictionaryBrowse.css"
import "./DictionaryLemma.css"

/** The letters, with the open one marked: the browse page's bar, over lemmas */
const Bar = ({
    items,
    active,
    ariaLabel,
}: {
    items: string[]
    active?: string | null
    ariaLabel: string
}) => (
    <nav className="dict-browse-bar" aria-label={ariaLabel}>
        {items.map((item) => (
            <Link
                key={item}
                to={lemmaIndexUrl(item)}
                className={item === active ? "active" : undefined}
                aria-current={item === active ? "page" : undefined}
            >
                {item}
            </Link>
        ))}
    </nav>
)

/** One letter of the lemma index, whole: the same adaptive chapters the
 * dictionary browse files its headwords under, over every lemma the tables
 * link a form to. */
const LemmaIndex = ({ at }: { at?: string | null }) => {
    const [page, setPage] = useState<DictionaryBrowseResponse | null>(null)
    const [failed, setFailed] = useState(false)

    useEffect(() => {
        setPage(null)
        setFailed(false)
        const abort = new AbortController()
        lemmaIndex(at, abort.signal)
            .then(setPage)
            .catch((e) => {
                if (!abort.signal.aborted) {
                    console.warn(e)
                    setFailed(true)
                }
            })
        return () => abort.abort()
    }, [at])

    return (
        <>
            {failed && <p>Something went wrong. Try again.</p>}
            {!failed && page == null && (
                <div className="dict-page-loading">
                    <CircularProgress />
                </div>
            )}

            {page != null && (
                <>
                    <h1 className="dict-page-word">Lemmas</h1>
                    <p className="dict-lemma-note">
                        The words the corpus search groups spellings under,
                        whatever form a text uses. Open one for its forms.
                    </p>
                    <Bar
                        items={page.letters}
                        active={page.letter}
                        ariaLabel="Letters"
                    />
                    {page.letters.length === 0 && (
                        // the lemma tables are a submodule: without them the
                        // index is empty rather than broken
                        <p className="dict-browse-empty">
                            No lemmas are loaded.
                        </p>
                    )}
                    <div
                        className="dict-browse-chapters dict-lemma-chapters"
                        aria-label={`Lemmas under ${page.letter}`}
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
                                                to={lemmaTreeUrl(entry.word)}
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
        </>
    )
}

/** The lemma pages at /dictionary/lemma: the index of every lemma without a
 * path segment, one lemma's form tree with one. */
export const DictionaryLemma = () => {
    const { lemma } = useParams()
    const [params] = useSearchParams()
    const at = params.get("at")

    return (
        <div className="dict-page">
            {/* a reader who knows the word they want should not have to find
                it in the index first */}
            <WordSearch indexUrl={lemma ? lemmaIndexUrl(lemma) : undefined} />
            {lemma ? <LemmaTree lemma={lemma} /> : <LemmaIndex at={at} />}
        </div>
    )
}
