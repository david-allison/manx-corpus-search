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
import { usePersistedState } from "../hooks/usePersistedState"
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
}: {
    manx?: string | null
    english?: string | null
    highlights?: { start: number; end: number }[] | null
}) => {
    const [translated, setTranslated] = useState(false)
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

/** The uses a text makes of one reading: the lexeme in a left column, its lines
 * tabbed across from it. An ambiguous word ('vee' is bee or mee) meets the
 * reader as one row per lexeme rather than as one interleaved list. */
const LemmaGroup = ({
    group,
    ident,
}: {
    group: AttestationLemmaGroup
    ident: string
}) => (
    <div className="attest-group">
        <p className="attest-group-head">
            <em className="attest-group-lemma">{group.lemma}</em>
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
    const [open, setOpen] = usePersistedState<boolean>(
        OPEN_KEY,
        // open unless shut before: the evidence is the point of the section
        (stored) => stored !== "false",
        (value) => String(value),
    )
    const [walk, setWalk] = useState<DictionaryAttestationsResponse | null>(
        null,
    )
    const [lines, setLines] = useState<AttestationLinesResponse | null>(null)
    // The walk reserves the tallest step it has shown. Steps differ in height
    // (a text may use one reading or three), and a reader scrolled to the foot
    // of the page is held at the bottom by the browser: shortening the page
    // under them slides everything, arrows included, mid-click. Growing is
    // harmless, so only the floor is held.
    const body = useRef<HTMLDivElement>(null)
    const [reserved, setReserved] = useState(0)

    useLayoutEffect(() => {
        const height = body.current?.getBoundingClientRect().height ?? 0
        if (height > reserved) {
            setReserved(height)
        }
    }, [lines, open, reserved])

    // another word is another walk: it should not inherit this one's floor
    useEffect(() => setReserved(0), [word])

    useEffect(() => {
        setWalk(null)
        const abort = new AbortController()
        dictionaryAttestations(word, abort.signal)
            .then(setWalk)
            .catch((e) => {
                if (!abort.signal.aborted) console.warn(e)
            })
        return () => abort.abort()
    }, [word])

    // no step named: the walk starts at the word's first attestation
    const index = Math.max(
        0,
        walk?.documents.findIndex((d) => d.ident === at) ?? 0,
    )
    const current = walk?.documents[index]
    const currentIdent = current?.ident

    useEffect(() => {
        // shut: the uses are not worth fetching until they would be shown
        if (currentIdent == null || !open) {
            setLines(null)
            return
        }
        // the previous step's uses stay on screen until the next arrive. Blanking
        // them would collapse the section to a one-line "Loading…" and grow it
        // back, moving the arrows out from under the cursor: the walk is meant to
        // be clicked through.
        const abort = new AbortController()
        dictionaryAttestationLines(word, currentIdent, abort.signal)
            .then(setLines)
            .catch((e) => {
                if (!abort.signal.aborted) console.warn(e)
            })
        return () => abort.abort()
    }, [word, currentIdent, open])

    const hasWalk = walk != null && walk.documents.length > 0 && current != null
    if (!hasWalk && !hasAttestation(history)) {
        return null
    }

    // the uses on screen belong to the step we are on only once they arrive
    const fresh = lines != null && lines.ident === currentIdent

    const stepTo = (ident: string) =>
        `${pathname}?at=${encodeURIComponent(ident)}`
    const previous = walk?.documents[index - 1]
    const next = walk?.documents[index + 1]

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

            {!hasWalk ? null : (
                <>
                    <p className="attest-summary">
                        {`${plural(walk.documents.length, "text", "texts")}, `}
                        {walk.documents[0].year}
                        {"–"}
                        {walk.documents[walk.documents.length - 1].year}
                    </p>

                    <div
                        id="attest-body"
                        ref={body}
                        hidden={!open}
                        style={
                            open && reserved
                                ? { minHeight: reserved }
                                : undefined
                        }
                    >
                        <PrevNextLinks
                            ariaLabel="Texts using this word, in date order"
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
                            {fresh && (
                                <span className="attest-count">
                                    {` · ${plural(lines.useCount, "use", "uses")}`}
                                </span>
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
                                        key={group.lemmaId}
                                        group={group}
                                        ident={lines.ident}
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
        </section>
    )
}
