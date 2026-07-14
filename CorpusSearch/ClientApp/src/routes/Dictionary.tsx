import { FormEvent, useEffect, useState } from "react"
import { Link, useNavigate, useParams } from "react-router-dom"
import { CircularProgress } from "@mui/material"
import {
    dictionaryPage,
    DictionaryPageResponse,
    Summary,
} from "../api/DictionaryApi"
import { headingFor } from "../components/DictionaryLookupModal"
import { DefinitionText, GrammarLabel } from "../components/GrammarAbbr"
import {
    getMultidictLookupWord,
    MultidictLink,
} from "../components/MultidictLink"
import { WordHistory } from "../components/WordHistory"
import "./Dictionary.css"

/** "learnmanx.com" from the source URL, for the audio credit's second line */
const hostOf = (url: string): string => {
    try {
        return new URL(url).hostname.replace(/^www\./, "")
    } catch {
        return ""
    }
}

const Entry = ({ word, summary }: { word: string; summary: Summary }) => (
    <div
        className={
            summary.rootDepth
                ? "dict-page-entry dict-page-root-entry"
                : "dict-page-entry"
        }
        style={
            summary.rootDepth
                ? { marginLeft: 20 * summary.rootDepth }
                : undefined
        }
    >
        {summary.rootDepth ? (
            <span className="dict-page-root-connector" aria-label="root form">
                {"↳ "}
            </span>
        ) : null}
        <strong>
            {summary.rootDepth
                ? summary.primaryWord
                : headingFor(word, summary)}
        </strong>
        <GrammarLabel label={summary.grammarLabel} />
        {": "}
        <DefinitionText text={summary.summary} />
        {summary.plurals?.length ? (
            <span className="dict-page-plural">
                {" — "}
                <abbr className="dict-abbr" title="plural">
                    pl.
                </abbr>{" "}
                {summary.plurals.join(", ")}
            </span>
        ) : null}
        {summary.audioUrl && (
            <button
                className="dict-page-audio"
                aria-label={`Play pronunciation of ${summary.primaryWord}`}
                title="Play pronunciation"
                onClick={() => {
                    new Audio(summary.audioUrl!).play().catch(console.warn)
                }}
            >
                {"▶"}
            </button>
        )}
    </div>
)

/** The experimental teanglann-style dictionary page: one word, every source —
 * per-dictionary sections, structured plurals, the root chain, pronunciation,
 * and near-spelling suggestions on a miss. */
export const Dictionary = () => {
    const { word } = useParams()
    const navigate = useNavigate()
    const [query, setQuery] = useState(word ?? "")
    const [page, setPage] = useState<DictionaryPageResponse | null>(null)
    const [failed, setFailed] = useState(false)

    useEffect(() => {
        setQuery(word ?? "")
        setPage(null)
        setFailed(false)
        if (!word) return
        const abort = new AbortController()
        dictionaryPage(word, abort.signal)
            .then(setPage)
            .catch((e) => {
                if (!abort.signal.aborted) {
                    console.warn(e)
                    setFailed(true)
                }
            })
        return () => abort.abort()
    }, [word])

    const onSubmit = (event: FormEvent) => {
        event.preventDefault()
        const trimmed = query.trim()
        if (trimmed) {
            void navigate(`/dictionary/${encodeURIComponent(trimmed)}`)
        }
    }

    const multidictWord = word ? getMultidictLookupWord(word) : null

    return (
        <div className="dict-page">
            <form className="dict-page-search" onSubmit={onSubmit}>
                <input
                    value={query}
                    onChange={(e) => setQuery(e.target.value)}
                    placeholder="Look up a Manx word…"
                    aria-label="Look up a Manx word"
                />
                <button type="submit">Look up</button>
            </form>

            {word && (
                <div className="dict-page-header">
                    <h1 className="dict-page-word">{word}</h1>
                    {page?.audio && (
                        <div className="dict-page-audio-corner">
                            <button
                                className="dict-page-audio-main"
                                aria-label={`Play pronunciation of ${word}`}
                                title="Play pronunciation"
                                onClick={() => {
                                    new Audio(page.audio!.url)
                                        .play()
                                        .catch(console.warn)
                                }}
                            >
                                {"▶"}
                            </button>
                            {page.audio.sourceUrl && (
                                <a
                                    className="dict-page-audio-credit"
                                    href={page.audio.sourceUrl}
                                    target="_blank"
                                    rel="noreferrer"
                                >
                                    <span>{page.audio.credit}</span>
                                    <span>{hostOf(page.audio.sourceUrl)}</span>
                                </a>
                            )}
                        </div>
                    )}
                </div>
            )}

            {word && page == null && !failed && (
                <div className="dict-page-loading">
                    <CircularProgress />
                </div>
            )}
            {failed && <p>Something went wrong — try again.</p>}

            {(() => {
                const target = page?.groups
                    .flatMap((g) => g.entries)
                    .find((x) => x.phillipsSpellingOf)?.phillipsSpellingOf
                return target ? (
                    <p className="dict-page-bridge">
                        <strong>{page!.word}</strong>
                        {" is a c. 1610 spelling (Phillips) of "}
                        <Link to={`/dictionary/${encodeURIComponent(target)}`}>
                            {target}
                        </Link>
                        {" — the entries below are for "}
                        <strong>{target}</strong>
                        {":"}
                    </p>
                ) : null
            })()}

            {page?.isSuggestionTier && (
                <p className="dict-page-suggestions-note">
                    Nothing found for “{page.word}” — near spellings:
                </p>
            )}

            {page?.groups.map((group) => (
                <section
                    className={
                        page.isSuggestionTier
                            ? "dict-page-group dict-page-suggestions"
                            : "dict-page-group"
                    }
                    key={group.dictionary}
                >
                    <h3 className="dict-page-dictionary">
                        {group.sourceUrl ? (
                            <a
                                href={group.sourceUrl}
                                target="_blank"
                                rel="noreferrer"
                            >
                                {group.dictionary}
                            </a>
                        ) : (
                            group.dictionary
                        )}
                    </h3>
                    {group.entries.map((summary, index) => (
                        <Entry word={page.word} summary={summary} key={index} />
                    ))}
                </section>
            ))}

            {word && page != null && !page.isSuggestionTier && (
                <WordHistory word={word} />
            )}

            {page != null && page.groups.length === 0 && (
                <p>
                    Could not find a definition
                    {multidictWord != null && (
                        <>
                            {". Try searching "}
                            <MultidictLink
                                word={multidictWord}
                                language="Manx"
                            />
                        </>
                    )}
                </p>
            )}

            {word && page != null && (
                <p className="dict-page-corpus-link">
                    <Link to={`/?q=${encodeURIComponent(word)}`}>
                        Search the corpus for “{word}”
                    </Link>
                </p>
            )}
        </div>
    )
}
