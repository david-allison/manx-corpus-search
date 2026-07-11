import { MouseEvent as ReactMouseEvent, ReactNode } from "react"
import Highlighter from "react-highlight-words"
import { diffChars } from "diff"
import { HighlightRange } from "../api/SearchApi"
import { SearchWorkResult } from "../api/SearchWorkApi"

function escapeRegex(s: string) {
    return s.replace(/[/\-\\^$*+?.()|[\]{}]/g, "\\$&")
}

/** A note/citation marker such as "[1]", linking a line to its note row (#132) */
const NOTE_MARKER = /\[\d+\]/
// split() with the marker captured, so the markers survive as parts
const NOTE_MARKER_SPLIT = /(\[\d+\])/
const isNoteMarker = (part: string) => /^\[\d+\]$/.test(part)

/**
 * Shifts server highlight ranges (offsets into the full line) into offsets local to one
 * newline-separated segment of it, clipping ranges which cross the boundary.
 *
 * @example
 * // the cell "abc\ncre def" is rendered as two segments:
 * //   "abc"     (segmentStart 0, length 3)
 * //   "cre def" (segmentStart 4, length 7)
 * // a server range of {start: 4, end: 7} ("cre") only affects the second segment:
 * segmentChunks([{ start: 4, end: 7 }], 0, 3) // => []
 * segmentChunks([{ start: 4, end: 7 }], 4, 7) // => [{ start: 0, end: 3 }]
 */
export function segmentChunks(
    highlights: HighlightRange[],
    segmentStart: number,
    segmentLength: number,
): { start: number; end: number }[] {
    const segmentEnd = segmentStart + segmentLength
    return highlights
        .filter((x) => x.start < segmentEnd && x.end > segmentStart)
        .map((x) => ({
            start: Math.max(x.start - segmentStart, 0),
            end: Math.min(x.end - segmentStart, segmentLength),
        }))
}

/**
 * Wraps the chunks (segment-local offsets) of `text` in the match highlight `<mark>`
 *
 * @example
 * // {start: 3, end: 10} covers "çhengey":
 * markChunks("Ta çhengey aym", [{ start: 3, end: 10 }])
 * // => ["Ta ", <mark className="textHighlight">çhengey</mark>, " aym"]
 */
function markChunks(
    text: string,
    chunks: { start: number; end: number }[],
): ReactNode {
    if (chunks.length == 0) {
        return text
    }
    const nodes: ReactNode[] = []
    let pos = 0
    for (const chunk of chunks) {
        if (chunk.start > pos) {
            nodes.push(text.slice(pos, chunk.start))
        }
        nodes.push(
            <mark className="textHighlight" key={chunk.start}>
                {text.slice(chunk.start, chunk.end)}
            </mark>,
        )
        pos = chunk.end
    }
    if (pos < text.length) {
        nodes.push(text.slice(pos))
    }
    return nodes
}

export type NoteToggle = {
    /** the linked note row is currently displayed */
    noteVisible: boolean
    toggle: () => void
}

/** A clickable "[1]" marker: shows/hides the note row it references (#132) */
const NoteMarkerButton = (props: {
    marker: string
    noteToggle: NoteToggle
}) => {
    const { marker, noteToggle } = props
    return (
        <button
            type="button"
            className="doc-note-marker"
            aria-expanded={noteToggle.noteVisible}
            title={noteToggle.noteVisible ? "Hide note" : "Show note"}
            onClick={(e) => {
                e.stopPropagation() // not a dictionary lookup
                noteToggle.toggle()
            }}
        >
            {/* the chip already delimits the number: drop the brackets of "[1]" */}
            {marker.slice(1, -1)}
        </button>
    )
}

/** One newline-separated segment of a cell: its highlighted text, with any
 * "[1]" markers rendered as toggles for the note row (#132) */
const SegmentText = (props: {
    text: string
    /** the segment's offset into the full cell: highlight ranges are cell-wide */
    segmentStart: number
    /** the server-matched ranges; null fuzzy-matches `searchWords` instead */
    highlights: HighlightRange[] | null
    searchWords: string[]
    noteToggle?: NoteToggle
}) => {
    const { text, segmentStart, highlights, searchWords, noteToggle } = props
    // note markers split the segment and become the note row's toggle
    const parts = noteToggle ? text.split(NOTE_MARKER_SPLIT) : [text]
    let partStart = segmentStart
    return (
        <>
            {parts.map((part, key) => {
                const start = partStart
                partStart += part.length
                if (noteToggle && isNoteMarker(part)) {
                    return (
                        <NoteMarkerButton
                            key={key}
                            marker={part}
                            noteToggle={noteToggle}
                        />
                    )
                }
                if (part == "") {
                    return null
                }
                const chunks = highlights
                    ? segmentChunks(highlights, start, part.length)
                    : null
                return (
                    <Highlighter
                        key={key}
                        highlightClassName={
                            highlights
                                ? "textHighlight"
                                : "textHighlightAlternate"
                        }
                        searchWords={searchWords}
                        autoEscape={false}
                        findChunks={chunks ? () => chunks : undefined}
                        textToHighlight={part}
                    />
                )
            })}
        </>
    )
}

/** A "[1]" marker in a visible, undiffed cell links the line to its note row
 * (#132). An unlinked note has no toggle, so it is always shown as-is. */
export const isNoteLinked = (
    line: SearchWorkResult,
    manxVisible: boolean,
    englishVisible: boolean,
): boolean =>
    Boolean(line.notes) &&
    ((manxVisible && !line.manxOriginal && NOTE_MARKER.test(line.manx)) ||
        (englishVisible &&
            !line.englishOriginal &&
            NOTE_MARKER.test(line.english)))

const HighlightedLine = (props: {
    /** highlight the server-matched ranges (the searched language) */
    shouldHighlight: boolean
    languageCode: "gv" | "en"
    text: string
    highlights?: HighlightRange[]
    /** dictionary translations of the query, fuzzy-matched on the unsearched column */
    translations: string[]
    /** the raw query: no fuzzy highlighting without one */
    query: string
    noteToggle?: NoteToggle
    onWordClick: (event: ReactMouseEvent, context: string) => void
}) => {
    const {
        shouldHighlight,
        languageCode,
        text,
        highlights,
        translations,
        query,
        noteToggle,
        onWordClick,
    } = props
    // The searched language: highlight the ranges the server matched (#40).
    // The other language: fuzzy-match the dictionary translations of the query.
    const translationWords = shouldHighlight ? [] : translations
    const translationPattern = translationWords
        .map((x) => `(${escapeRegex(x)})`)
        .join("|")
    // no highlighting if we don't have a value
    const searchWords =
        translationWords.length > 0 && query
            ? [` [,\\.!]?(${translationPattern})[,\\.!]?[ (—)]`]
            : []
    let segmentStart = 0
    return (
        <>
            {text.split("\n").map((item, key) => {
                const start = segmentStart
                segmentStart += item.length + 1 // + the removed "\n"
                return (
                    <div
                        onClick={(event) => {
                            if (languageCode == "gv") {
                                onWordClick(event, text)
                            }
                        }}
                        className="doc-line"
                        key={key}
                    >
                        <SegmentText
                            text={item}
                            segmentStart={start}
                            highlights={
                                shouldHighlight ? (highlights ?? []) : null
                            }
                            searchWords={searchWords}
                            noteToggle={noteToggle}
                        />
                        <br />
                    </div>
                )
            })}
        </>
    )
}

/**
 * A character diff of the original text against the correction we made to it.
 *
 * The server's highlight ranges are offsets into `text`, so they mark the
 * unchanged/added parts; removed parts only exist in `original` and cannot
 * hold a match.
 */
const DiffedLine = (props: {
    original: string
    text: string
    highlights?: HighlightRange[]
    onWordClick: (event: ReactMouseEvent, context: string) => void
}) => {
    const { original, text, highlights, onWordClick } = props
    const result = diffChars(original, text)

    let partStart = 0
    // TODO: This only handles the correction, not the original
    // TODO: Also apply justify to 'browse' screen
    return (
        <div
            onClick={(event) => {
                onWordClick(event, text)
            }}
            className="doc-line"
        >
            {result.map((part, index) => {
                const chunks = part.removed
                    ? []
                    : segmentChunks(
                          highlights ?? [],
                          partStart,
                          part.value.length,
                      )
                if (!part.removed) {
                    partStart += part.value.length
                }
                const color = part.added
                    ? "rgba(0, 128, 0, 0.3)"
                    : part.removed
                      ? "rgba(255, 0, 0, 0.3)"
                      : ""
                const className = part.added
                    ? "part-added"
                    : part.removed
                      ? "part-removed"
                      : ""
                return (
                    <span
                        key={index}
                        className={className}
                        style={{ backgroundColor: color }}
                    >
                        {markChunks(part.value, chunks)}
                    </span>
                )
            })}
        </div>
    )
}

/** One language cell of a document line: a diff against the pre-correction
 * text when one exists, otherwise the highlighted text */
export const LineText = (props: {
    text: string
    /** when set, the pre-correction text: the cell renders as a character diff */
    original?: string
    highlights?: HighlightRange[]
    shouldHighlight: boolean
    languageCode: "gv" | "en"
    translations: string[]
    query: string
    noteToggle?: NoteToggle
    onWordClick: (event: ReactMouseEvent, context: string) => void
}) => {
    const { original, ...rest } = props
    if (original) {
        return (
            <DiffedLine
                original={original}
                text={rest.text}
                highlights={rest.shouldHighlight ? rest.highlights : undefined}
                onWordClick={rest.onWordClick}
            />
        )
    }
    return <HighlightedLine {...rest} />
}
