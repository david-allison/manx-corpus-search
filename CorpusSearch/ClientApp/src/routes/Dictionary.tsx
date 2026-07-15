import { FormEvent, useEffect, useState } from "react"
import { Link, useNavigate, useParams } from "react-router-dom"
import { CircularProgress } from "@mui/material"
import {
    dictionaryPage,
    DictionaryPageResponse,
    Summary,
} from "../api/DictionaryApi"
import { dictionaryWordUrl, headingFor } from "../utils/DictionaryEntries"
import { DefinitionText, GrammarLabel } from "../components/GrammarAbbr"
import { UnverifiedMark } from "../components/UnverifiedMark"
import { DictionaryScope } from "../components/DictionaryScope"
import {
    getMultidictLookupWord,
    MultidictLink,
} from "../components/MultidictLink"
import { VerseVersionsModal } from "../components/VerseVersionsModal"
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

const Entry = ({
    word,
    summary,
    onCitationClick,
}: {
    word: string
    summary: Summary
    onCitationClick: (key: string) => void
}) => (
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
        <UnverifiedMark summary={summary} />
        <GrammarLabel label={summary.grammarLabel} />
        {": "}
        <DefinitionText
            text={summary.summary}
            citations={summary.citations}
            onCitationClick={onCitationClick}
        />
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
    // `dict` is set only by /dictionary/in/:dict/:word: the scoped page
    const { word, dict } = useParams()
    const navigate = useNavigate()
    const [query, setQuery] = useState(word ?? "")
    const [page, setPage] = useState<DictionaryPageResponse | null>(null)
    const [failed, setFailed] = useState(false)
    // a tapped scripture citation: the verse's other-versions popup
    const [citationKey, setCitationKey] = useState<string | null>(null)

    useEffect(() => {
        setQuery(word ?? "")
        setPage(null)
        setFailed(false)
        if (!word) return
        const abort = new AbortController()
        dictionaryPage(word, dict, abort.signal)
            .then(setPage)
            .catch((e) => {
                if (!abort.signal.aborted) {
                    console.warn(e)
                    setFailed(true)
                }
            })
        return () => abort.abort()
    }, [word, dict])

    const onSubmit = (event: FormEvent) => {
        event.preventDefault()
        const trimmed = query.trim()
        if (trimmed) {
            void navigate(dictionaryWordUrl(trimmed, dict))
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

            {word && <DictionaryScope word={word} dict={dict} />}

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
                        <strong>{page.word}</strong>
                        {" is a c. 1610 spelling (Phillips) of "}
                        <Link to={dictionaryWordUrl(target, dict)}>
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
                        <Entry
                            word={page.word}
                            summary={summary}
                            onCitationClick={setCitationKey}
                            key={index}
                        />
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

            <VerseVersionsModal
                refKey={citationKey}
                onClose={() => setCitationKey(null)}
            />
        </div>
    )
}
