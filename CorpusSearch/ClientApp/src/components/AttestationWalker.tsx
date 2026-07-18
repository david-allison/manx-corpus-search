import { useEffect, useLayoutEffect, useRef, useState } from "react"
import { Link, useLocation, useSearchParams } from "react-router-dom"
import Highlighter from "react-highlight-words"
import {
    AttestationLemmaGroup,
    AttestationLinesResponse,
    dictionaryAttestationLines,
    dictionaryAttestations,
    DictionaryAttestationsResponse,
    DictionaryHistoryResponse,
} from "../api/DictionaryApi"
import { hasAttestation } from "../utils/Attestation"
import { formatTime } from "../hooks/useVideoSync"
import { usePersistedState } from "../hooks/usePersistedState"
import { AudioAttestationModal } from "./AudioAttestationModal"
import { FirstAttestation } from "./FirstAttestation"
import { segmentChunks } from "./LineText"
import { PrevNextLinks } from "./PrevNextLinks"
import "./AttestationWalker.css"

/** The section is long even collapsed to a preview, so whether it is open is
 * remembered: a reader who does not want the evidence should not have to shut
 * it on every word. */
const OPEN_KEY = "dictionary.corpus.open"

const plural = (n: number, one: string, many: string) =>
    `${n.toLocaleString()} ${n === 1 ? one : many}`

/** One corpus line, the lexeme's surface word marked. Reuses the search
 * results' highlight machinery, so a use looks the same wherever you meet it.
 *
 * The Manx stands alone until asked: this is a page about Manx words, and a
 * translation under every line halves how many uses fit on the screen. */
const ManxText = ({
    manx,
    highlights,
}: {
    manx?: string | null
    highlights?: { start: number; end: number }[] | null
}) => (
    <Highlighter
        highlightClassName="textHighlight"
        searchWords={[]}
        autoEscape={false}
        findChunks={() => segmentChunks(highlights ?? [], 0, manx?.length ?? 0)}
        textToHighlight={manx ?? ""}
    />
)

const UseLine = ({
    manx,
    english,
    highlights,
    play,
}: {
    manx?: string | null
    english?: string | null
    highlights?: { start: number; end: number }[] | null
    /** where hearing the line starts, for a line of a recording — `at` is
     * null on an untimed transcript, which plays from the start */
    play?: { at: number | null; open: () => void }
}) => {
    const [translated, setTranslated] = useState(false)
    // a use in a recording: the whole line — Manx, timestamp and all — is the
    // way to hearing it, opening the listening popup at its moment while the
    // walk stays where the reader left it. Its translation waits in the popup,
    // under the video, so the line keeps only one job here.
    if (play) {
        return (
            <div className="attest-line">
                <button
                    type="button"
                    className="attest-line-manx attest-line-tappable"
                    title="Hear this line"
                    onClick={play.open}
                >
                    <ManxText manx={manx} highlights={highlights} />
                    <span className="attest-line-play">
                        {/* ??:?? where the transcript wrote no clock down:
                            the popup still plays, from the start */}
                        {play.at != null
                            ? `▶ ${formatTime(play.at)}`
                            : "▶ ??:??"}
                    </span>
                </button>
            </div>
        )
    }
    // an untranslated row has nothing to reveal, so it is not a control
    if (!english) {
        return (
            <div className="attest-line">
                <div className="attest-line-manx">
                    <ManxText manx={manx} highlights={highlights} />
                </div>
            </div>
        )
    }
    return (
        <div className="attest-line">
            <button
                type="button"
                className="attest-line-manx attest-line-tappable"
                aria-expanded={translated}
                title={translated ? "Hide the translation" : "Translate"}
                onClick={() => setTranslated(!translated)}
            >
                <ManxText manx={manx} highlights={highlights} />
            </button>
            {translated && <div className="attest-line-english">{english}</div>}
        </div>
    )
}

/** The abbreviation a lemma id's word class reads as, as the books print it.
 * Only the three the ids name: 'x' is every class that is not one of them —
 * adverbs, pronouns, particles, participles — a bucket, and no name to print. */
const CLASS_ABBREVIATION: Record<string, string> = {
    n: "n.",
    v: "v.",
    a: "a.",
}

/** How a row names the reading it stands for, where the headword alone does not:
 * 'n. or v.' on a row standing for both readings of a line that is genuinely
 * either, and 'n.' where another row is the same headword read differently.
 *
 * Null where the headword is the whole name — the usual case, and a class on a
 * row with nothing to be told from is noise — and null too where a class has no
 * abbreviation to print, since naming only the rest would misread the row. */
const classLabelFor = (
    group: AttestationLemmaGroup,
    groups: AttestationLemmaGroup[],
): string | null => {
    const sharesLemma = groups.some(
        (other) => other !== group && other.lemma === group.lemma,
    )
    // one reading, and nothing to be told apart from: the lemma is its whole name.
    // No readings at all is a row that is a spelling — nothing has said its class
    if (group.lemmaIds.length < 2 && !sharesLemma) {
        return null
    }
    const named = group.classes.map((c) => CLASS_ABBREVIATION[c])
    return named.length > 0 && named.every(Boolean) ? named.join(" or ") : null
}

/** The uses a text makes of one reading: the lexeme in a left column, its lines
 * tabbed across from it. An ambiguous word ('vee' is bee or mee) meets the
 * reader as one row per lexeme rather than as one interleaved list.
 *
 * Where the row stands for readings that cannot be told apart ('jaagh' is smoke
 * or the verb), `classes` names them: the row is one use of one word which is
 * genuinely either, and saying so is the difference between an ambiguity and
 * what would otherwise look like the same quote printed twice. */
const LemmaGroup = ({
    group,
    classes,
    ident,
    audio,
    onPlay,
}: {
    group: AttestationLemmaGroup
    classes: string | null
    ident: string
    /** the step is a recording: every line is a way to hearing it, even one
     * whose transcript wrote no timestamp down (Skeealyn Vannin Track 12) */
    audio: boolean
    /** opens the listening popup at a tapped use's line */
    onPlay: (csvLineNumber: number) => void
}) => (
    <div className="attest-group">
        <p className="attest-group-head">
            <em className="attest-group-lemma">{group.lemma}</em>
            {classes && (
                <span className="attest-group-class">{` ${classes}`}</span>
            )}
            <span className="attest-group-count">
                {` ×${group.count.toLocaleString()}`}
            </span>
        </p>
        <div className="attest-group-lines">
            {group.lines.map((line) => (
                <UseLine
                    key={line.csvLineNumber}
                    manx={line.manx}
                    english={line.english}
                    highlights={line.manxHighlights}
                    play={
                        audio || line.subStart != null
                            ? {
                                  at: line.subStart ?? null,
                                  open: () => onPlay(line.csvLineNumber),
                              }
                            : undefined
                    }
                />
            ))}
            {group.count > group.lines.length && (
                <p className="attest-more">
                    <Link
                        to={`/docs/${ident}?q=${encodeURIComponent(group.lemma)}`}
                    >
                        {`All ${plural(group.count, "use", "uses")} in this text ›`}
                    </Link>
                </p>
            )}
        </div>
    </div>
)

/** Everything the corpus has to say about a word: when it was first seen, then
 * a walk through the texts using it in date order, one at a time, showing a
 * taste of each reading's uses.
 *
 * The band leads because it is the section's headline, and it stays out of the
 * collapse: shutting the evidence should not take the finding with it. The two
 * are gated apart because they can fail apart — the band scans spellings, the
 * walk queries lemma ids, and a word the lemma table does not know has one and
 * not the other.
 *
 * The step is a URL parameter, not state: a use you found is a place you can
 * link someone to. It sits on the search string rather than the path, so the
 * walk needs no route of its own (and no SpaRouteGuard entry).
 */
export const AttestationWalker = ({
    word,
    history,
    classes,
}: {
    word: string
    history: DictionaryHistoryResponse | null
    classes: string[]
}) => {
    const { pathname } = useLocation()
    const [params] = useSearchParams()
    const at = params.get("at")
    // the open tab: which reading the walk below is of. A tab you chose is a
    // walk you can link someone to, so it lives on the URL like the step does.
    const reading = params.get("reading")
    const [open, setOpen] = usePersistedState<boolean>(
        OPEN_KEY,
        // open unless shut before: the evidence is the point of the section
        (stored) => stored !== "false",
        (value) => String(value),
    )
    // the walk together with what was asked for: a garbage ?reading= falls back
    // to the whole walk on the server, so the response alone cannot say whether
    // it answers this URL
    const [shown, setShown] = useState<{
        walk: DictionaryAttestationsResponse
        word: string
        reading: string | null
    } | null>(null)
    const [lines, setLines] = useState<AttestationLinesResponse | null>(null)
    // a tapped use's listening popup: the recording, and the line it opens at
    const [heard, setHeard] = useState<{
        doc: { ident: string; title: string; year?: number | null }
        at: number
    } | null>(null)
    // another word is another walk: its popup must not outlive the page it
    // was opened from
    useEffect(() => setHeard(null), [word])
    // The walk holds its height while an answer is on its way. Steps differ
    // in height (a text may use one reading or three), and a reader scrolled
    // to the foot of the page is held at the bottom by the browser:
    // shortening the page under them slides everything, arrows included,
    // mid-click. The floor lifts once the answer is on screen — the held
    // space is for the wait, not after it.
    const body = useRef<HTMLDivElement>(null)
    const [reserved, setReserved] = useState(0)

    useEffect(() => {
        // the word before's walk is not blanked first. This section is the tallest
        // thing on the page, and dropping it between one headword and the next
        // takes 200px out of the middle: the page shortens, the scrollbar goes and
        // the footer comes up to meet the gap, for as long as an answer takes. The
        // page fades what has not caught up (dict-page-entries).
        const abort = new AbortController()
        const load = async () => {
            let walk = await dictionaryAttestations(
                word,
                reading ?? undefined,
                abort.signal,
            )
            // the walk is one reading's at a time: with none asked and several
            // to choose from, the first tab is the one walked. One extra round
            // trip, and only for the ~4% of words with more than one reading.
            if (reading == null && walk.lemmas.length > 1) {
                walk = await dictionaryAttestations(
                    word,
                    walk.lemmas[0],
                    abort.signal,
                )
            }
            setShown({ walk, word, reading })
        }
        load().catch((e) => {
            if (!abort.signal.aborted) console.warn(e)
        })
        return () => abort.abort()
    }, [word, reading])

    const walk = shown?.walk ?? null
    // the walk on screen is this word's, on this tab's, only once it arrives
    const walked =
        shown != null && shown.word === word && shown.reading === reading

    // the recordings among the texts (the 🎥 name marks audio documents, as
    // the Audio tag reads it): most of the corpus only writes a word down, and
    // the texts that *say* it earn a tab of their own
    const audioDocs =
        walk?.documents.filter((d) => d.title.startsWith("🎥")) ?? []
    // the audio tab is open: the walk steps through the recordings alone. A
    // ?audio= on a word no recording says falls back to the whole walk, the
    // way a garbage ?reading= does.
    const audioTab = params.get("audio") != null && audioDocs.length > 0
    const documents = audioTab ? audioDocs : (walk?.documents ?? [])

    // no step named: the walk starts at the word's first attestation
    const index = Math.max(
        0,
        documents.findIndex((d) => d.ident === at),
    )
    const current: (typeof documents)[number] | undefined = documents[index]
    const currentIdent = current?.ident

    // what the step is asked to show: the walked reading's uses, so the lines
    // agree with the tab the document list came from
    const walkLemma = walked ? (walk?.lemma ?? null) : null

    useEffect(() => {
        // shut: the uses are not worth fetching until they would be shown
        if (currentIdent == null || !open) {
            setLines(null)
            return
        }
        // the step is still the last word's: its documents are not this word's,
        // and asking for one in the other's name answers about neither. The uses
        // on screen keep the section's shape until the walk catches up.
        if (!walked) {
            return
        }
        // the previous step's uses stay on screen until the next arrive. Blanking
        // them would collapse the section to a one-line "Loading…" and grow it
        // back, moving the arrows out from under the cursor: the walk is meant to
        // be clicked through.
        const abort = new AbortController()
        dictionaryAttestationLines(
            word,
            currentIdent,
            walkLemma ?? undefined,
            abort.signal,
        )
            .then(setLines)
            .catch((e) => {
                if (!abort.signal.aborted) console.warn(e)
            })
        return () => abort.abort()
    }, [word, currentIdent, open, walked, walkLemma])

    // the uses on screen belong to the step we are on, and to its tab's
    // reading, only once they arrive
    const fresh =
        walked &&
        lines != null &&
        lines.ident === currentIdent &&
        (lines.lemma ?? null) === (walk?.lemma ?? null)

    useLayoutEffect(() => {
        const height = body.current?.getBoundingClientRect().height ?? 0
        if (fresh) {
            // the answer owns the screen: the floor follows it down as well
            // as up, so a short entry contracts once it has loaded
            if (height !== reserved) {
                setReserved(height)
            }
        } else if (height > reserved) {
            setReserved(height)
        }
    }, [lines, open, reserved, fresh])

    const hasWalk = walk != null && walk.documents.length > 0 && current != null
    // the band is this word's or nothing; the walk may still be the last word's,
    // and holds its place while this one's is fetched
    if (!hasWalk && !hasAttestation(history)) {
        return null
    }

    /* Always a tab, even for the ~96% of words with one reading (and for the
       walk of a spelling, whose one tab is the spelling): the tab is the
       walk's caption — it names the lexeme being walked, which no other line
       does. Only a word with no walk at all earns no bar, except that several
       readings keep theirs: an empty tab must leave a way to the others. */
    const tabs =
        walk == null ? [] : walk.lemmas.length > 0 ? walk.lemmas : [walk.word]
    const activeTab =
        reading != null && tabs.includes(reading)
            ? reading
            : (walk?.lemma ?? tabs[0])
    const showTabs = tabs.length > 0 && (hasWalk || tabs.length > 1)

    const stepTo = (ident: string) => {
        const query = new URLSearchParams()
        if (reading != null) {
            query.set("reading", reading)
        }
        if (audioTab) {
            query.set("audio", "1")
        }
        query.set("at", ident)
        return `${pathname}?${query.toString()}`
    }
    /** the audio tab's own address: same reading, no step — the recordings are
     * walked from the earliest, like a walk newly opened */
    const audioTabTo = () => {
        const query = new URLSearchParams()
        if (reading != null) {
            query.set("reading", reading)
        }
        query.set("audio", "1")
        return `${pathname}?${query.toString()}`
    }
    const previous = documents[index - 1]
    const next = documents[index + 1]

    return (
        <section className="attest">
            <h3 className="dict-page-dictionary">
                {hasWalk ? (
                    <button
                        type="button"
                        className="attest-toggle"
                        aria-expanded={open}
                        aria-controls="attest-body"
                        onClick={() => setOpen(!open)}
                    >
                        {open ? "▾" : "▸"} In the corpus
                    </button>
                ) : (
                    // nothing to collapse: a control that does nothing is worse
                    // than none
                    "In the corpus"
                )}
                <span className="attest-experimental">
                    experimental &amp; incomplete
                </span>
            </h3>

            <FirstAttestation history={history} classes={classes} />

            {showTabs && (
                <nav
                    className="attest-tabs"
                    aria-label="Readings and recordings of this word"
                >
                    {tabs.map((lemma) =>
                        // with the audio tab open, the active reading is the
                        // way back to its whole walk rather than a caption
                        lemma === activeTab && !audioTab ? (
                            <span
                                key={lemma}
                                className="attest-tab attest-tab-active"
                                aria-current="true"
                            >
                                {lemma}
                            </span>
                        ) : (
                            // replace, as every same-page view switch here
                            // does: flipping tabs is not travelling
                            <Link
                                key={lemma}
                                className="attest-tab"
                                to={`${pathname}?reading=${encodeURIComponent(lemma)}`}
                                replace
                            >
                                {lemma}
                            </Link>
                        ),
                    )}
                    {/* the texts that *say* the word, walked alone: most of
                        the corpus only writes it down */}
                    {audioDocs.length > 0 &&
                        (audioTab ? (
                            <span
                                className="attest-tab attest-tab-active"
                                aria-current="true"
                            >
                                {`🔊 audio${audioDocs.length > 1 ? ` ×${audioDocs.length.toLocaleString()}` : ""}`}
                            </span>
                        ) : (
                            <Link
                                className="attest-tab"
                                to={audioTabTo()}
                                title="Only the recordings, oldest first"
                                replace
                            >
                                {`🔊 audio${audioDocs.length > 1 ? ` ×${audioDocs.length.toLocaleString()}` : ""}`}
                            </Link>
                        ))}
                    {/* the reading's whole family, drawn on the lemma page:
                        the tab names the lexeme, this is the way to its tree.
                        Not offered for a spelling walk — there is no lemma to
                        draw */}
                    {walk != null && walk.lemmas.includes(activeTab) && (
                        <Link
                            className="attest-tab-tree"
                            to={`/dictionary/lemma/${encodeURIComponent(activeTab)}`}
                            title={`Every form of “${activeTab}”, as a tree`}
                        >
                            All forms ›
                        </Link>
                    )}
                </nav>
            )}
            {showTabs && !hasWalk && (
                <p className="attest-empty">No dated text uses this reading.</p>
            )}

            {!hasWalk ? null : (
                <>
                    <p className="attest-summary">
                        {audioTab
                            ? `${plural(documents.length, "recording", "recordings")}, `
                            : `${plural(documents.length, "text", "texts")}, `}
                        {documents[0].year}
                        {/* one year said once: a lone document has no range */}
                        {documents[0].year !==
                            documents[documents.length - 1].year && (
                            <>
                                {"–"}
                                {documents[documents.length - 1].year}
                            </>
                        )}
                    </p>

                    <div
                        id="attest-body"
                        ref={body}
                        hidden={!open}
                        // held only while the step's answer is on its way:
                        // once it is on screen, the section takes its size
                        style={
                            open && reserved && !fresh
                                ? { minHeight: reserved }
                                : undefined
                        }
                    >
                        <PrevNextLinks
                            ariaLabel={
                                audioTab
                                    ? "Recordings using this word, in date order"
                                    : "Texts using this word, in date order"
                            }
                            previous={
                                previous
                                    ? {
                                          to: stepTo(previous.ident),
                                          label: `${previous.year}`,
                                          title: `${previous.title} (${previous.year})`,
                                      }
                                    : null
                            }
                            next={
                                next
                                    ? {
                                          to: stepTo(next.ident),
                                          label: `${next.year}`,
                                          title: `${next.title} (${next.year})`,
                                      }
                                    : null
                            }
                        >
                            <strong className="attest-year">
                                {current.year}
                            </strong>
                            {" · "}
                            <Link
                                to={`/docs/${current.ident}?q=${encodeURIComponent(word)}`}
                            >
                                {current.title}
                            </Link>
                            {/* the walk's own count where it has one, so it
                                is there as you arrive; otherwise the document's
                                own, once counted from the offsets */}
                            {current.uses != null ? (
                                <span className="attest-count">
                                    {` · ${plural(current.uses, "use", "uses")}`}
                                </span>
                            ) : (
                                fresh && (
                                    <span className="attest-count">
                                        {` · ${plural(lines.useCount, "use", "uses")}`}
                                    </span>
                                )
                            )}
                        </PrevNextLinks>

                        {lines == null ? (
                            <p className="attest-loading">Loading uses…</p>
                        ) : (
                            // dimmed while they are the step before's: the shape
                            // of the section holds, so the arrows stay put
                            <div className={fresh ? undefined : "attest-stale"}>
                                {lines.groups.map((group) => (
                                    <LemmaGroup
                                        // a row with no readings is a spelling,
                                        // and there is only ever one of those
                                        key={
                                            group.lemmaIds.join(" ") ||
                                            group.lemma
                                        }
                                        group={group}
                                        classes={classLabelFor(
                                            group,
                                            lines.groups,
                                        )}
                                        ident={lines.ident}
                                        audio={lines.title.startsWith("🎥")}
                                        onPlay={(csvLineNumber) =>
                                            setHeard({
                                                doc: {
                                                    ident: lines.ident,
                                                    title: lines.title,
                                                    year: lines.year,
                                                },
                                                at: csvLineNumber,
                                            })
                                        }
                                    />
                                ))}
                            </div>
                        )}

                        {walk.undatedDocuments > 0 && (
                            <p className="attest-undated">
                                {`Also used in ${plural(walk.undatedDocuments, "undated text", "undated texts")}, which the walk cannot place.`}
                            </p>
                        )}
                    </div>
                </>
            )}

            <AudioAttestationModal
                word={word}
                // the word's other recordings ride along for the popup's
                // arrows; a recording the walk does not list (a timed line in
                // something unnamed as audio) leafs through itself alone
                docs={
                    heard == null
                        ? []
                        : audioDocs.some((d) => d.ident === heard.doc.ident)
                          ? audioDocs
                          : [heard.doc]
                }
                openAt={
                    heard == null
                        ? null
                        : { ident: heard.doc.ident, at: heard.at }
                }
                onClose={() => setHeard(null)}
            />
        </section>
    )
}
