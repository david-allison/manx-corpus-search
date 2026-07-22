import { Fragment, useEffect, useMemo, useReducer, useState } from "react"
import { Link, useParams, useSearchParams } from "react-router-dom"
import { CircularProgress } from "@mui/material"
import {
    AttestationDocument,
    dictionaryAttestations,
    dictionaryPage,
    DictionaryPageResponse,
    Summary,
} from "../api/DictionaryApi"
import {
    corpusSearchUrl,
    declaredClassesIn,
    dictionaryIndexUrl,
    dictionaryWordUrl,
    headingFor,
    rootsBySense,
    senseGroupsIn,
} from "../utils/DictionaryEntries"
import {
    DefinitionText,
    expandGrammarLabel,
    GrammarLabel,
} from "../components/GrammarAbbr"
import { UnverifiedMark } from "../components/UnverifiedMark"
import { DictionaryScope } from "../components/DictionaryScope"
import { DictionaryLetters } from "../components/DictionaryLetters"
import { DictionaryCoverage } from "../components/DictionaryCoverage"
import { DictionaryFeedback } from "../components/DictionaryFeedback"
import { isDictionaryHost } from "../utils/Host"
import { WordSearch } from "../components/WordSearch"
import { HeadwordNav } from "../components/HeadwordNav"
import {
    getMultidictLookupWord,
    MultidictLink,
} from "../components/MultidictLink"
import { VerseVersionsModal } from "../components/VerseVersionsModal"
import { AudioAttestationModal } from "../components/AudioAttestationModal"
import { useWordHistory } from "../hooks/useWordHistory"
import { useDictionaryHead } from "../hooks/useDictionaryHead"
import { AttestationWalker } from "../components/AttestationWalker"
import { WordFamily } from "../components/LemmaTree"
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
        <UnverifiedMark unverified={summary.unverifiedLink} />
        <GrammarLabel
            label={summary.grammarLabel}
            warning={summary.genderNote}
        />
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

/** A sense's word classes beside the headword: "n.", or "adv., prep." where one
 * sense gathers more than one label for the one job.
 *
 * Each expands on hover, as every printed abbreviation on the page does — the
 * entries below wear the same dotted underline for the labels their books
 * printed, and a reader has no reason to care that this one was worked out from
 * them rather than read off a page. */
const SenseLabel = ({ labels }: { labels: string[] }) => (
    <span className="dict-page-sense-label">
        {labels.map((label, index) => {
            const expansion = expandGrammarLabel(label)
            return (
                <Fragment key={label}>
                    {index > 0 && ", "}
                    {expansion ? (
                        <abbr className="dict-abbr" title={expansion}>
                            {label}
                        </abbr>
                    ) : (
                        // no tooltip is better than a wrong one, and better than
                        // a control that offers nothing when you reach for it
                        label
                    )}
                </Fragment>
            )
        })}
    </span>
)

/** The experimental teanglann-style dictionary page: one word, every source —
 * per-dictionary sections, structured plurals, the root chain, pronunciation,
 * and near-spelling suggestions on a miss. */
export const Dictionary = () => {
    // `dict` is set only by /dictionary/in/:dict/:word: the scoped page
    const { word, dict } = useParams()
    // arrived from the spoken index: the headword walk steps the heard words
    // instead, and each step carries the walk along
    const [searchParams] = useSearchParams()
    const spokenNav = searchParams.get("nav") === "spoken"
    // the page together with what was asked for, so the entries on screen can be
    // told from the URL, which moves the moment you click. Same reason
    // DocumentView keeps `displayed` beside its query.
    const [shown, setShown] = useState<{
        page: DictionaryPageResponse
        word: string
        dict?: string
    } | null>(null)
    const [failed, setFailed] = useState(false)
    // bumped to ask the page again: a phrase's attestation is worked out behind
    // the server, so the answer this page could not get may be there by now
    const [asked, askAgain] = useReducer((x: number) => x + 1, 0)
    // a tapped scripture citation: the verse's other-versions popup
    const [citationKey, setCitationKey] = useState<string | null>(null)
    // the recordings using the word (the 🎥 name marks audio documents), with
    // the word they answer for: the title's "audio" link and the popup behind
    // it. Asked beside the page rather than lifted from the walk — the walk
    // is one reading's at a time, and the title speaks for the word.
    const [heard, setHeard] = useState<{
        word: string
        docs: AttestationDocument[]
        /** the recording the link opens: the earliest whose uses of the word
         * can be jumped into, or — only when there is no such thing — an
         * untimed one */
        lead: AttestationDocument
    } | null>(null)
    const [heardOpen, setHeardOpen] = useState(false)
    // keyed on the word alone, so it runs beside the lookup rather than after
    // it: the first-attestation band heads the page. A total miss discards it.
    // The corpus does not know which dictionary you came in through, so the
    // history is the same in every scope.
    const history = useWordHistory(word)

    useEffect(() => {
        setFailed(false)
        if (!word) {
            setShown(null)
            return
        }
        // the word before's entries are not blanked first. Dropping them takes
        // the page down to a spinner and grows it back: the scrollbar goes, the
        // footer jumps up into the gap, and the arrows you are walking with move
        // — for the tens of milliseconds it takes to answer. They fade instead.
        const abort = new AbortController()
        dictionaryPage(word, dict, abort.signal)
            .then((page) => setShown({ page, word, dict }))
            .catch((e) => {
                if (!abort.signal.aborted) {
                    console.warn(e)
                    setFailed(true)
                }
            })
        return () => abort.abort()
    }, [word, dict, asked])

    useEffect(() => {
        setHeardOpen(false)
        if (!word) {
            setHeard(null)
            return
        }
        const abort = new AbortController()
        dictionaryAttestations(word, undefined, abort.signal)
            .then((walk) => {
                const docs = walk.documents.filter((d) =>
                    d.title.startsWith("🎥"),
                )
                // the link leads with a recording it can jump into: one whose
                // uses of the word carry no timestamp (an untimed transcript,
                // or an untimed corner of a timed one) plays only from the
                // top, so it leads only when there is nothing better
                const lead = docs.find((d) => d.timed !== false) ?? docs[0]
                // an answer either way: the last word's recordings must not
                // linger on a word nothing says (the render checks the word too,
                // so the stale moment shows no link rather than a wrong one)
                setHeard(lead ? { word, docs, lead } : null)
            })
            .catch((e) => {
                // no link is a quieter title, never a broken one
                if (!abort.signal.aborted) console.warn(e)
            })
        return () => abort.abort()
    }, [word])

    // a new word opens at its top: the link that brought you here may sit at
    // the foot of a long page — the word family, a walk step — and the next
    // word's heading must not arrive off-screen
    useEffect(() => {
        window.scrollTo(0, 0)
    }, [word, dict])

    // the tab names the word being read; the landing names the feature
    useDictionaryHead(word ?? "Dictionary")

    const page = shown?.page ?? null
    /** Whether what is on screen is this word's yet: the URL changes on the click,
     * the entries a moment later, and until they agree the page is the last
     * word's and says so by fading */
    const stale = shown != null && (shown.word !== word || shown.dict !== dict)

    const multidictWord = word ? getMultidictLookupWord(word) : null
    const rootEntries =
        page == null || page.isSuggestionTier
            ? []
            : page.groups.flatMap((g) => g.entries).filter((e) => e.rootDepth)

    const senses =
        page != null && !page.isSuggestionTier ? senseGroupsIn(page) : []

    /** the roots split among the senses they belong to, the unthreadable left
     * in a page-level basket */
    const sensedRoots = rootsBySense(senses, rootEntries)

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

    /** What can honestly be said about this word's attestation. Nothing, while the
     * page on screen is the last word's: the title is the word you clicked, and it
     * must not be greyed on another word's evidence. */
    const attested = stale ? null : page?.attested

    /** The recording saying this word — only once the answer is this word's:
     * the title must not offer another word's audio for the moment between
     * the click and the fetch */
    const heardDocs = heard != null && heard.word === word ? heard : null

    /** The word's readings, each the root of a family tree: what the "Word
     * family" section at the end of the page draws. Deduped — the history
     * lists a reading once per source it knows it from — and memoized, so the
     * section's fetch keys on the readings rather than on every render. */
    const familyLemmas = useMemo(
        () => [...new Set(history?.lemmas ?? [])],
        [history],
    )

    const header = (
        <div className="dict-page-header">
            <h1
                className={
                    attested === false
                        ? "dict-page-word dict-unattested"
                        : "dict-page-word"
                }
            >
                {word}
                {/* the class is the entries', so it waits for them: the title is
                    already this word, and "CREEAGH n." on cree's authority is a
                    claim about a word that has not arrived */}
                {!stale && singleSense && (
                    <SenseLabel labels={senses[0].labels} />
                )}
            </h1>
            {/* across the row from the word, not part of it: the corpus's
                recording, beside the spoken dictionary's corner when both
                have something to play, with the way to make the page better
                beneath whatever is there */}
            <div className="dict-page-corner">
                {(heardDocs != null || page?.audio != null) && (
                    <div className="dict-page-corner-audio">
                        {heardDocs != null && (
                            <button
                                type="button"
                                className="dict-page-heard"
                                title={`Hear it spoken: ${heardDocs.lead.title} (${heardDocs.lead.year.toString()})`}
                                onClick={() => setHeardOpen(true)}
                            >
                                🔊 audio
                            </button>
                        )}
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
                                        <span>
                                            {hostOf(page.audio.sourceUrl)}
                                        </span>
                                    </a>
                                )}
                            </div>
                        )}
                    </div>
                )}
                {word != null && <DictionaryFeedback word={word} dict={dict} />}
            </div>
        </div>
    )

    return (
        <div className="dict-page">
            <WordSearch
                word={word}
                dict={dict}
                indexUrl={word ? dictionaryIndexUrl(word, dict) : undefined}
            />

            {!word && <DictionaryLetters />}
            {/*the dictionary host's landing doubles as the front door: the
               coverage numbers close it, as the context they are*/}
            {!word && isDictionaryHost() && <DictionaryCoverage />}

            {word && (
                <DictionaryScope
                    word={word}
                    dict={dict}
                    // which books answer is the last word's answer until this
                    // word's lands: greyed on it, the picker would be greying
                    // the wrong tabs for a moment
                    answering={stale ? undefined : page?.answering}
                />
            )}

            {word && (
                <HeadwordNav word={word} dict={dict} spoken={spokenNav}>
                    <span
                        className={
                            attested === false
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

            {/* everything below is the entries and their evidence, and all of it
                belongs to `shown` rather than to the URL: dimmed together while
                the word moves out from under it, the way the corpus search dims
                a stale result */}
            <div
                className={
                    stale
                        ? "dict-page-entries dict-page-stale"
                        : "dict-page-entries"
                }
            >
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
                            {!singleSense && sense.labels.length > 0 && (
                                <h3 className="dict-page-sense">
                                    <span className="dict-page-sense-word">
                                        {page.word}
                                    </span>
                                    <SenseLabel labels={sense.labels} />
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
                            {/* each reading owns the roots it is built from: the
                            dog sense says moddey, "not long" says foddey, and
                            neither answers for the other's ancestry */}
                            {(sensedRoots.bySense.get(sense.key) ?? []).length >
                                0 && (
                                <>
                                    <h4 className="dict-page-dictionary">
                                        Built from
                                    </h4>
                                    {sensedRoots.bySense
                                        .get(sense.key)!
                                        .map((summary, index) => (
                                            <Entry
                                                word={page.word}
                                                summary={summary}
                                                credit
                                                onCitationClick={setCitationKey}
                                                key={index}
                                            />
                                        ))}
                                </>
                            )}
                        </section>
                    ))}

                {/* a root the thread cannot place under one sense: the mixed
                basket is an admission, never a guess */}
                {sensedRoots.unclaimed.length > 0 && (
                    <section className="dict-page-group">
                        <h3 className="dict-page-dictionary">Built from</h3>
                        {sensedRoots.unclaimed.map((summary, index) => (
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

                {/* the word's whole family ends the page, above the way out
                    to the corpus: every form the corpus search groups with
                    it, one tree per reading — the same trees the lemma pages
                    draw, brought to where the reader already is */}
                {word && !stale && page != null && !page.isSuggestionTier && (
                    <WordFamily lemmas={familyLemmas} />
                )}

                {/* A word no text says has nothing to find: the offer would promise
                evidence the corpus does not hold. Asked of the history, which
                really scanned this word, rather than of page.attested, which
                answers for a browse page of ten thousand headwords at once. */}
                {word && usedInCorpus && (
                    <p className="dict-page-corpus-link">
                        <Link to={corpusSearchUrl(word)}>
                            Search the corpus for “{word}”
                        </Link>
                    </p>
                )}

                {/* A phrase is answered from a read of the whole corpus, which runs
                behind the server: for the few seconds before it lands there is no
                answer to give, and the page says so rather than guessing at one.
                The way to the corpus stays open — a reader who came to look
                should not be kept waiting on a claim about the looking. */}
                {word && page?.attested == null && page != null && (
                    <p className="dict-page-pending" role="status">
                        {"Still reading the corpus for phrases like this one. "}
                        <button type="button" onClick={() => askAgain()}>
                            Check again
                        </button>
                        {!usedInCorpus && (
                            <>
                                {" or "}
                                <Link to={corpusSearchUrl(word)}>
                                    search it yourself
                                </Link>
                            </>
                        )}
                    </p>
                )}
            </div>

            {/* a dialog is not part of the page being read: it belongs to
                whatever was clicked, and dimming it would dim the thing the
                reader just opened */}
            <VerseVersionsModal
                refKey={citationKey}
                onClose={() => setCitationKey(null)}
            />
            <AudioAttestationModal
                word={word ?? ""}
                docs={heardDocs?.docs ?? []}
                openAt={
                    heardOpen && heardDocs != null
                        ? { ident: heardDocs.lead.ident }
                        : null
                }
                onClose={() => setHeardOpen(false)}
            />
        </div>
    )
}
