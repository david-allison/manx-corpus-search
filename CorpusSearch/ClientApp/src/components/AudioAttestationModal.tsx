import { useEffect, useRef, useState } from "react"
import { Box, CircularProgress, Modal } from "@mui/material"
import { Link } from "react-router-dom"
import Highlighter from "react-highlight-words"
import {
    AttestationLemmaGroup,
    AttestationLinesResponse,
    dictionaryAttestationLines,
} from "../api/DictionaryApi"
import { metadataLookup } from "../api/MetadataApi"
import { formatTime, getVideoId } from "../hooks/useVideoSync"
import { resolveSpeaker } from "../utils/Speakers"
import YouTuber, { Player } from "./YouTuber"
import { segmentChunks } from "./LineText"
import "./AudioAttestationModal.css"

const style = {
    position: "absolute",
    top: "50%",
    left: "50%",
    transform: "translate(-50%, -50%)",
    // wider than the verse popup's 560: the arrow gutters take a column each
    maxWidth: 640,
    width: "calc(100% - 32px)",
    maxHeight: "85vh",
    overflowY: "auto",
    bgcolor: "var(--c-surface, #fff)",
    color: "var(--c-ink, inherit)",
    borderRadius: 2,
    boxShadow: 24,
    p: 3,
}

type AttestationLine = AttestationLemmaGroup["lines"][number]

/** The recording's uses of the word, one row per line however many readings
 * claim it, in the order they are spoken. Untimed lines ride along at the
 * end: some transcripts carry no timings at all (Skeealyn Vannin Track 12),
 * and the word is no less said for the clock not being written down. */
const spokenLines = (response: AttestationLinesResponse): AttestationLine[] => {
    const seen = new Map<number, AttestationLine>()
    for (const group of response.groups) {
        for (const line of group.lines) {
            if (!seen.has(line.csvLineNumber)) {
                seen.set(line.csvLineNumber, line)
            }
        }
    }
    return [...seen.values()].sort(
        (a, b) =>
            (a.subStart ?? Infinity) - (b.subStart ?? Infinity) ||
            a.csvLineNumber - b.csvLineNumber,
    )
}

/** A recording the popup can show: what the attestation walk knows of it */
export type AudioAttestationDoc = {
    ident: string
    title: string
    year?: number | null
}

/** The word said out loud: the recording using it, opened from the title's
 * "audio" link or from a use in the corpus walk, playing from the moment the
 * word is spoken. Arrows at the popup's edges step through every recording
 * using the word, oldest first, without leaving it.
 *
 * The player autoplays — the one place on the site that dares to, because here
 * the reader's click asked for exactly this: to hear the word. Each line of
 * dialog is a control seeking its own moment, and the full document stays a
 * link away for anyone who wants the whole conversation. */
export const AudioAttestationModal = ({
    word,
    docs,
    openAt,
    onClose,
}: {
    word: string
    /** every recording using the word, in the walk's date order: what the
     * arrows step through */
    docs: AudioAttestationDoc[]
    /** where to open: a recording, and optionally the csvLineNumber of the
     * tapped use in it — the moment to start from. Null while closed. */
    openAt: { ident: string; at?: number | null } | null
    onClose: () => void
}) => {
    const [lines, setLines] = useState<AttestationLine[] | null>(null)
    const [videoId, setVideoId] = useState<string | null>(null)
    // the manifest's author list: what the speaker codes resolve against
    const [author, setAuthor] = useState<string | null>(null)
    // the recording on screen: begins where the opener asked, then follows
    // the arrows
    const [ident, setIdent] = useState<string | null>(null)
    useEffect(() => setIdent(openAt?.ident ?? null), [openAt])
    const player = useRef<Player>(null)

    useEffect(() => {
        setLines(null)
        setVideoId(null)
        setAuthor(null)
        if (ident == null) {
            return
        }
        const abort = new AbortController()
        dictionaryAttestationLines(word, ident, undefined, abort.signal)
            .then((response) => setLines(spokenLines(response)))
            .catch((e) => {
                if (!abort.signal.aborted) console.warn(e)
            })
        metadataLookup(ident, abort.signal)
            .then((metadata: Record<string, unknown>) => {
                const source = metadata["source"]
                setVideoId(
                    typeof source === "string" ? getVideoId(source) : null,
                )
                const by = metadata["author"]
                setAuthor(typeof by === "string" ? by : null)
            })
            .catch((e) => {
                if (!abort.signal.aborted) console.warn(e)
            })
        return () => abort.abort()
    }, [word, ident])

    const index = docs.findIndex((d) => d.ident === ident)
    const doc = index >= 0 ? docs[index] : null
    const previous = index > 0 ? docs[index - 1] : null
    const next = index >= 0 && index < docs.length - 1 ? docs[index + 1] : null
    // the tapped use leads — on the recording it was tapped in; a recording
    // arrived at by arrow starts at its own first use
    const at = openAt != null && openAt.ident === ident ? openAt.at : undefined
    const cued =
        (at != null
            ? lines?.find((line) => line.csvLineNumber === at)
            : undefined) ?? lines?.[0]
    return (
        <Modal
            open={openAt != null && doc != null}
            onClose={onClose}
            aria-labelledby="audio-attestation-title"
        >
            <Box sx={style}>
                {/* three columns, a table's discipline: an arrow gutter each
                    side with nothing above or below it, and everything the
                    popup says in the middle */}
                <div className="audio-attest-layout">
                    <div className="audio-attest-gutter">
                        {/* at the first recording the arrow stays, disabled:
                            a control that vanishes reads as never having
                            existed, one that greys reads as an edge */}
                        {docs.length > 1 && (
                            <button
                                type="button"
                                className="audio-attest-nav"
                                aria-label={
                                    previous
                                        ? `Previous recording: ${previous.title}`
                                        : "Previous recording"
                                }
                                title={
                                    previous
                                        ? `${previous.title}${previous.year != null ? ` (${previous.year.toString()})` : ""}`
                                        : undefined
                                }
                                disabled={previous == null}
                                onClick={() =>
                                    previous && setIdent(previous.ident)
                                }
                            >
                                ‹
                            </button>
                        )}
                    </div>
                    <div className="audio-attest-content">
                        <h2
                            id="audio-attestation-title"
                            className="audio-attest-title"
                            lang="gv"
                        >
                            {word}
                        </h2>
                        {doc != null && (
                            <p className="audio-attest-source">
                                {doc.title}
                                {doc.year != null && (
                                    <span className="audio-attest-year">
                                        {doc.year}
                                    </span>
                                )}
                                {docs.length > 1 && (
                                    <span className="audio-attest-position">
                                        {`${(index + 1).toString()} of ${docs.length.toString()}`}
                                    </span>
                                )}
                            </p>
                        )}
                        {lines == null && (
                            <div className="audio-attest-loading">
                                <CircularProgress size={28} />
                            </div>
                        )}
                        {/* the player waits for the lines: startSeconds is read once,
                    and a player mounted before the moment is known would only
                    ever start from the top */}
                        {videoId != null && lines != null && (
                            <div className="audio-attest-video">
                                <div className="youtube-container">
                                    <YouTuber
                                        ref={player}
                                        videoId={videoId}
                                        startSeconds={
                                            cued?.subStart ?? undefined
                                        }
                                        autoplay
                                    />
                                </div>
                            </div>
                        )}
                        {lines != null && (
                            <ul className="audio-attest-lines">
                                {lines.map((line) => {
                                    const said = (
                                        <>
                                            {line.speaker && (
                                                <span className="audio-attest-speaker">
                                                    {resolveSpeaker(
                                                        line.speaker,
                                                        author,
                                                    )}
                                                </span>
                                            )}
                                            <span
                                                className="audio-attest-manx"
                                                lang="gv"
                                            >
                                                <Highlighter
                                                    highlightClassName="textHighlight"
                                                    searchWords={[]}
                                                    autoEscape={false}
                                                    findChunks={() =>
                                                        segmentChunks(
                                                            line.manxHighlights ??
                                                                [],
                                                            0,
                                                            line.manx?.length ??
                                                                0,
                                                        )
                                                    }
                                                    textToHighlight={
                                                        line.manx ?? ""
                                                    }
                                                />
                                            </span>
                                        </>
                                    )
                                    return (
                                        <li key={line.csvLineNumber}>
                                            {/* only a timed line can be a seek
                                        control: an untimed one is still the
                                        evidence, but has no moment to jump
                                        to — ??:?? in the time slot says the
                                        transcript wrote no clock down
                                        (Skeealyn Vannin Track 12) */}
                                            {line.subStart != null ? (
                                                <button
                                                    type="button"
                                                    className="audio-attest-line"
                                                    title="Play from this line"
                                                    onClick={() =>
                                                        player.current?.seek(
                                                            line.subStart!,
                                                        )
                                                    }
                                                >
                                                    <span className="audio-attest-time">
                                                        {`▶ ${formatTime(line.subStart)}`}
                                                    </span>
                                                    {said}
                                                </button>
                                            ) : (
                                                <div
                                                    className="audio-attest-line"
                                                    title="The transcript does not say when this line is spoken"
                                                >
                                                    <span className="audio-attest-time audio-attest-time-unknown">
                                                        ??:??
                                                    </span>
                                                    {said}
                                                </div>
                                            )}
                                            {line.english && (
                                                <div className="audio-attest-english">
                                                    {line.english}
                                                </div>
                                            )}
                                        </li>
                                    )
                                })}
                                {lines.length == 0 && (
                                    <li className="audio-attest-none">
                                        No use of the word could be found in
                                        this transcript.
                                    </li>
                                )}
                            </ul>
                        )}
                        {doc != null && (
                            <p className="audio-attest-more">
                                <Link
                                    to={`/docs/${doc.ident}${cued != null ? `?line=${cued.csvLineNumber.toString()}` : ""}`}
                                    onClick={onClose}
                                >
                                    Full document ›
                                </Link>
                            </p>
                        )}
                    </div>
                    <div className="audio-attest-gutter">
                        {docs.length > 1 && (
                            <button
                                type="button"
                                className="audio-attest-nav"
                                aria-label={
                                    next
                                        ? `Next recording: ${next.title}`
                                        : "Next recording"
                                }
                                title={
                                    next
                                        ? `${next.title}${next.year != null ? ` (${next.year.toString()})` : ""}`
                                        : undefined
                                }
                                disabled={next == null}
                                onClick={() => next && setIdent(next.ident)}
                            >
                                ›
                            </button>
                        )}
                    </div>
                </div>
            </Box>
        </Modal>
    )
}
