import { FormEvent, useEffect, useState } from "react"
import { Link, useNavigate, useParams } from "react-router-dom"
import { CircularProgress } from "@mui/material"
import {
    dictionaryPage,
    DictionaryPageResponse,
    Summary,
} from "../api/DictionaryApi"
import {
    declaredClassesIn,
    dictionaryIndexUrl,
    dictionaryWordUrl,
    headingFor,
    senseGroupsIn,
} from "../utils/DictionaryEntries"
import { DefinitionText, GrammarLabel } from "../components/GrammarAbbr"
import { UnverifiedMark } from "../components/UnverifiedMark"
import { DictionaryScope } from "../components/DictionaryScope"
import { DictionaryLetters } from "../components/DictionaryLetters"
import { HeadwordNav } from "../components/HeadwordNav"
import {
    getMultidictLookupWord,
    MultidictLink,
} from "../components/MultidictLink"
import { VerseVersionsModal } from "../components/VerseVersionsModal"
import { useWordHistory } from "../hooks/useWordHistory"
import { AttestationWalker } from "../components/AttestationWalker"
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
    credit,
    unplaced,
    onCitationClick,
}: {
    word: string
    summary: Summary
    /** name the dictionary on the entry: under a sense heading the entries come
     * from several at once, so the group can no longer say who said what */
    credit?: boolean
    /** this entry's source records no word class, so it is repeated under every
     * sense: it may belong to another one */
    unplaced?: boolean
    onCitationClick: (key: string) => void
}) => (
    <div
        className={[
            "dict-page-entry",
            summary.rootDepth ? "dict-page-root-entry" : "",
            unplaced ? "dict-page-entry-unplaced" : "",
        ]
            .filter(Boolean)
            .join(" ")}
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
                {", "}
                <abbr className="dict-abbr" title="plural">
                    pl.
                </abbr>{" "}
                {summary.plurals.join(", ")}
            </span>
        ) : null}
        {credit && (
            <span
                className="dict-page-credit"
                title={
                    unplaced
                        ? "Word classes were not extracted from this dictionary. This entry may belong to another sense"
                        : undefined
                }
            >
                {summary.dictionaryName}
            </span>
        )}
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
    // keyed on the word alone, so it runs beside the lookup rather than after
    // it: the first-attestation band heads the page. A total miss discards it.
    // The corpus does not know which dictionary you came in through, so the
    // history is the same in every scope.
    const history = useWordHistory(word)

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
    const rootEntries =
        page == null || page.isSuggestionTier
            ? []
            : page.groups.flatMap((g) => g.entries).filter((e) => e.rootDepth)

    const senses =
        page != null && !page.isSuggestionTier ? senseGroupsIn(page) : []

    /* The only sense of a word has nothing to tell apart, so it needs no heading
       of its own: its label rides on the title, and the word is not said twice
       over. Several senses each earn the word again, under a label that says
       which of them you are reading. */
    const singleSense = senses.length === 1

    /** Whether any text actually uses the word, as the history's own scan found
     * it: the evidence, rather than the browse page's guess at it */
    const usedInCorpus =
        history != null &&
        history.traditionalCount + history.revivedCount + history.undatedCount >
            0

    const header = (
        <div className="dict-page-header">
            <h1
                className={
                    page != null && !page.attested
                        ? "dict-page-word dict-unattested"
                        : "dict-page-word"
                }
            >
                {word}
                {singleSense && senses[0].label && (
                    <span className="dict-page-sense-label">
                        {senses[0].label}
                    </span>
                )}
            </h1>
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
    )

    return (
        <div className="dict-page">
            <form className="dict-page-search" onSubmit={onSubmit}>
                {/* the way out of the word and back to the index it is filed
                    in. A page-level control, so it keeps the page's own top
                    row rather than the headword walk's: that row is the walk,
                    and stepping out of it is not a step in it. */}
                {word && (
                    <Link
                        className="dict-page-index"
                        to={dictionaryIndexUrl(word, dict)}
                        title="Back to the index"
                        aria-label="Back to the index"
                    >
                        {"⌃"}
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

            {!word && <DictionaryLetters />}

            {word && (
                <DictionaryScope
                    word={word}
                    dict={dict}
                    answering={page?.answering}
                />
            )}

            {word && (
                <HeadwordNav word={word} dict={dict}>
                    <span
                        className={
                            page != null && !page.attested
                                ? "dict-page-headword-nav-word dict-unattested"
                                : "dict-page-headword-nav-word"
                        }
                    >
                        {word}
                    </span>
                </HeadwordNav>
            )}

            {word && header}

            {word && page == null && !failed && (
                <div className="dict-page-loading">
                    <CircularProgress />
                </div>
            )}
            {failed && <p>Something went wrong. Try again.</p>}

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
                        {". The entries below are for "}
                        <strong>{target}</strong>
                        {":"}
                    </p>
                ) : null
            })()}

            {page?.isSuggestionTier && (
                <p className="dict-page-suggestions-note">
                    Nothing found for “{page.word}”. Near spellings:
                </p>
            )}

            {/* near spellings are not senses of the word: they stay grouped
                under the dictionary that suggested them */}
            {page?.isSuggestionTier &&
                page.groups.map((group) => (
                    <section
                        className="dict-page-group dict-page-suggestions"
                        key={group.dictionary}
                    >
                        <h3 className="dict-page-dictionary">
                            {group.dictionary}
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

            {page != null &&
                !page.isSuggestionTier &&
                senses.map((sense) => (
                    <section className="dict-page-group" key={sense.key}>
                        {/* the heading is what tells one sense from another, so
                            it earns the word again. The only sense of a word has
                            nothing to tell apart: its label rides on the title
                            instead, and the heading would say the title over */}
                        {!singleSense && sense.label && (
                            <h3 className="dict-page-sense">
                                <span className="dict-page-sense-word">
                                    {page.word}
                                </span>
                                <span className="dict-page-sense-label">
                                    {sense.label}
                                </span>
                            </h3>
                        )}
                        {sense.entries.map((summary, index) => (
                            <Entry
                                word={page.word}
                                summary={summary}
                                credit
                                unplaced={
                                    senses.length > 1 &&
                                    !summary.partsOfSpeech?.length
                                }
                                onCitationClick={setCitationKey}
                                key={index}
                            />
                        ))}
                    </section>
                ))}

            {/* the roots are other words this one is built from, not senses of
                it: they follow the senses rather than sitting among them */}
            {rootEntries.length > 0 && (
                <section className="dict-page-group">
                    <h3 className="dict-page-dictionary">Built from</h3>
                    {rootEntries.map((summary, index) => (
                        <Entry
                            word={page!.word}
                            summary={summary}
                            credit
                            onCitationClick={setCitationKey}
                            key={index}
                        />
                    ))}
                </section>
            )}

            {word && page != null && !page.isSuggestionTier && (
                <AttestationWalker
                    word={word}
                    history={history}
                    classes={declaredClassesIn(page)}
                />
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

            {/* A word no text says has nothing to find: the offer would promise
                evidence the corpus does not hold.

                Asked of the history, which really scanned, rather than of
                page.attested, which is a guess cheap enough for a browse page of
                ten thousand words: that guess meets a phrase one word at a time,
                so it calls 'geinnagh vane' attested on the strength of 'geinnagh'
                and 'vane' turning up separately, and the corpus holds no such
                phrase. Here there is one word to answer for, and the real answer
                is already on the page. */}
            {word && usedInCorpus && (
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
