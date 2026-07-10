import { SearchWorkResponse, SearchWorkResult } from "../api/SearchWorkApi"
import { HighlightRange, Translations } from "../api/SearchApi"
import { getSelectedWordOrPhrase, getWordAtPoint } from "../utils/Selection"
import {
    Fragment,
    MouseEvent as ReactMouseEvent,
    useEffect,
    useRef,
    useState,
    ReactNode,
} from "react"
import { DictionaryResponse, manxDictionaryLookup } from "../api/DictionaryApi"
import { getMultidictLookupWord, MultidictLink } from "./MultidictLink"
import Highlighter from "react-highlight-words"
import {
    Box,
    CircularProgress,
    IconButton,
    Menu,
    MenuItem,
    Modal,
    useMediaQuery,
} from "@mui/material"
import Typography from "@mui/material/Typography"
import { diffChars } from "diff"
import YouTuber, { Player } from "./YouTuber"
import useInterval from "../vendor/use-interval/UseInterval"
import {
    CONTEXT_CHUNK,
    ContextGap,
    ExpandDirection,
    SMALL_GAP,
    useContextExpansion,
} from "../hooks/useContextExpansion"
import "./ComparisonTable.css"

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

type NoteToggle = {
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
const isNoteLinked = (
    line: SearchWorkResult,
    manxVisible: boolean,
    englishVisible: boolean,
): boolean =>
    Boolean(line.notes) &&
    ((manxVisible && !line.manxOriginal && NOTE_MARKER.test(line.manx)) ||
        (englishVisible &&
            !line.englishOriginal &&
            NOTE_MARKER.test(line.english)))

const formatTime = (seconds: number): string => {
    const m = Math.floor(seconds / 60)
    const s = Math.floor(seconds % 60)
    return `${m}:${String(s).padStart(2, "0")}`
}

/** "line" / "5 lines": the count is omitted for a single line */
const countedLines = (n: number) => (n == 1 ? "line" : `${n} lines`)

const Chevron = ({ direction }: { direction: "up" | "down" }) => (
    <svg
        className="doc-expand-chevron"
        width="12"
        height="12"
        viewBox="0 0 16 16"
        aria-hidden="true"
    >
        <path
            d={direction == "down" ? "M3 6l5 5 5-5" : "M3 10l5-5 5 5"}
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
        />
    </svg>
)

/** A GitHub-style 'expand hunk' divider for the hidden lines between search results (#286) */
const ExpanderRow = (props: {
    gap: ContextGap
    /** empty cells before the band (the video/speaker columns) */
    leadingCells: number
    /** how many language columns the band covers */
    languageColSpan: number
    /** the Link column is present: keep the band off it */
    hasLinkCell: boolean
    onExpand: (gap: ContextGap, direction: ExpandDirection) => void
}) => {
    const { gap, leadingCells, languageColSpan, hasLinkCell, onExpand } = props
    const span = gap.end - gap.start + 1
    const chunk = Math.min(CONTEXT_CHUNK, span)
    // the edges of the document only reveal towards the nearest result
    const showDown = gap.position != "leading"
    const showUp = gap.position != "trailing"
    const showAll = gap.position == "middle" && span <= SMALL_GAP

    // an extremity fills the whole band: balance it with a chevron on each side
    const isEdge = gap.position != "middle"
    const button = (direction: ExpandDirection, label: string) => (
        <button
            type="button"
            className={`doc-expand-btn doc-expand-btn-${direction}`}
            disabled={gap.loading}
            onClick={() => onExpand(gap, direction)}
        >
            {direction != "all" && <Chevron direction={direction} />}
            {label}
            {direction != "all" && isEdge && <Chevron direction={direction} />}
        </button>
    )

    return (
        <tr className="doc-expand-row">
            {Array.from({ length: leadingCells }, (_, i) => (
                <td key={i} />
            ))}
            <td colSpan={languageColSpan}>
                <div className="doc-expand-buttons">
                    {showAll ? (
                        button("all", "Show context")
                    ) : (
                        <>
                            {showDown &&
                                button(
                                    "down",
                                    `Show next ${countedLines(chunk)}`,
                                )}
                            {showUp &&
                                button(
                                    "up",
                                    `Show previous ${countedLines(chunk)}`,
                                )}
                        </>
                    )}
                </div>
            </td>
            {hasLinkCell && <td />}
        </tr>
    )
}

/** Groups the popup's entries under the dictionary defining them (#51) */
export const groupByDictionary = (
    summaries: DictionaryResponse,
): [string, DictionaryResponse][] => {
    const groups = new Map<string, DictionaryResponse>()
    for (const summary of summaries) {
        const group = groups.get(summary.dictionaryName)
        if (group == null) {
            groups.set(summary.dictionaryName, [summary])
        } else {
            group.push(summary)
        }
    }
    return [...groups.entries()]
}

export const ComparisonTable = (props: {
    response: SearchWorkResponse
    value: string
    highlightManx: boolean
    highlightEnglish: boolean
    manxVisible: boolean
    englishVisible: boolean
    translations?: Translations
    /** enables 'expand context' between the results (#286) */
    docIdent?: string
    /** the "Show context" option: hides the expanders without dropping the docIdent */
    expandContext?: boolean
    /** the "Show notes" option. When off, a note row linked to a "[1]"
     * marker collapses behind it, and the marker reveals it */
    showNotes?: boolean
}) => {
    const {
        response,
        value,
        highlightManx,
        highlightEnglish,
        manxVisible,
        englishVisible,
        translations,
        docIdent,
        expandContext,
        showNotes,
    } = props

    const onClickWordForDictionaryLookup = (
        event: ReactMouseEvent,
        context: string,
    ) => {
        // a mouse click places a caret to expand into the word under it (and a
        // double-click selects the word itself), but a touch tap places
        // nothing: locate the word from the tap position instead
        const isTouch =
            "pointerType" in event.nativeEvent &&
            (event.nativeEvent as PointerEvent).pointerType == "touch"
        const selection = window.getSelection()
        const wordOrPhrase =
            isTouch || selection == null || selection.rangeCount == 0
                ? getWordAtPoint(event.clientX, event.clientY)
                : getSelectedWordOrPhrase(selection)

        if (wordOrPhrase == null || wordOrPhrase.split(" ").length > 4) {
            return
        }

        // remove notes/citations '[1]' at the end of the string
        const stringToSearch = wordOrPhrase.replace(/\[\d+]/g, " ")

        setModalOpen(true)
        modalOpenedAt.current = performance.now()
        setModalText(stringToSearch.trim())
        setModalContext(context)
    }

    const [modalOpen, setModalOpen] = useState(false)
    const modalOpenedAt = useRef(0)
    const [modalText, setModalText] = useState("")
    // the text surrounding modalText: lets the server match phrases/idioms (#135)
    const [modalContext, setModalContext] = useState("")
    const handleModalClose = (_event: unknown, reason?: string) => {
        // a double-click's second click lands on the backdrop just after the
        // first click opened the popup: it must not immediately close it
        if (
            reason == "backdropClick" &&
            performance.now() - modalOpenedAt.current < 500
        ) {
            return
        }
        setModalOpen(false)
    }
    const modalMultidictWord = getMultidictLookupWord(modalText)

    const [modalSummaries, setModalSummaries] =
        useState<DictionaryResponse | null>(null)

    useEffect(() => {
        setModalSummaries(null)
        if (!modalText) return
        manxDictionaryLookup(modalText, modalContext)
            .then(setModalSummaries)
            .catch((e) => {
                console.warn(e)
            })
    }, [modalText, modalContext])

    // TODO: We use original for two concepts:
    // The Original text (compared to a corrected text)
    // The original text (whether Manx -> English or English -> Manx)
    const originalManx = response.original != "English" // anything other than English is Manx

    const notesShown = showNotes !== false
    // notes the reader toggled via a "[1]" marker: these rows flip against the
    // "Show notes" baseline, keyed by csvLineNumber (#132)
    const [toggledNotes, setToggledNotes] = useState<Set<number>>(new Set())
    useEffect(() => {
        setToggledNotes((previous) =>
            previous.size == 0 ? previous : new Set(),
        )
    }, [docIdent, notesShown])
    const toggleNote = (lineNumber: number) => {
        setToggledNotes((previous) => {
            const next = new Set(previous)
            if (!next.delete(lineNumber)) {
                next.add(lineNumber)
            }
            return next
        })
    }

    const highlightText = (
        shouldHighlight: boolean,
        languageCode: "gv" | "en",
        lineValue: string,
        highlights?: HighlightRange[],
        noteToggle?: NoteToggle,
    ) => {
        // The searched language: highlight the ranges the server matched (#40).
        // The other language: fuzzy-match the dictionary translations of the query.
        const translationWords = shouldHighlight
            ? []
            : getTranslations(languageCode)
        const translationPattern = translationWords
            .map((x) => `(${escapeRegex(x)})`)
            .join("|")
        // no highlighting if we don't have a value
        const searchWords =
            translationWords.length > 0 && value
                ? [` [,\\.!]?(${translationPattern})[,\\.!]?[ (—)]`]
                : []
        let segmentStart = 0
        return lineValue.split("\n").map((item, key) => {
            const start = segmentStart
            segmentStart += item.length + 1 // + the removed "\n"
            return (
                <div
                    onClick={(event) => {
                        if (languageCode == "gv") {
                            onClickWordForDictionaryLookup(event, lineValue)
                        }
                    }}
                    className="doc-line"
                    key={key}
                >
                    <SegmentText
                        text={item}
                        segmentStart={start}
                        highlights={shouldHighlight ? (highlights ?? []) : null}
                        searchWords={searchWords}
                        noteToggle={noteToggle}
                    />
                    <br />
                </div>
            )
        })
    }

    /**
     * If originalText exists, perform a diff and display this to the user
     * This displays changes we made to the document
     *
     * The server's highlight ranges are offsets into currentText, so they mark
     * the unchanged/added parts; removed parts only exist in originalText and
     * cannot hold a match.
     */
    const diffCorrectedText = (
        originalText: string | undefined,
        currentText: string,
        highlights?: HighlightRange[],
    ): ReactNode | null => {
        if (!originalText) {
            return null
        }
        const result = diffChars(originalText, currentText)

        let partStart = 0
        // TODO: This only handles the correction, not the original
        // TODO: Also apply justify to 'browse' screen
        return (
            <div
                onClick={(event) => {
                    onClickWordForDictionaryLookup(event, currentText)
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

    const getTranslations = (key: string) => {
        if (translations == null) return []
        return translations[key] ?? []
    }

    // null until the video loads: lines with subStart 0 must not highlight before then
    const [videoTime, setVideoTime] = useState<number | null>(null)

    // while paused the time doesn't change, so setState skips the re-render
    useInterval(
        () => setVideoTime(player.current?.getCurrentTime() ?? null),
        250,
    )

    /** The video id, when the source is a YouTube watch URL */
    const getVideoId = (source: string): string | null => {
        let url: URL
        try {
            url = new URL(source)
        } catch {
            return null // most sources are not URLs at all
        }
        // [security] block 'www.youtube.evil.com'
        if (
            url.protocol != "https:" ||
            (url.hostname != "www.youtube.com" && url.hostname != "youtube.com")
        ) {
            return null
        }
        return url.searchParams.get("v")
    }

    const videoId = response?.source ? getVideoId(response.source) : null
    const isVideo = Boolean(videoId)
    const player = useRef<Player>(null)

    const isPlaying = (line: SearchWorkResult): boolean => {
        if (!isVideo || videoTime == null) return false
        if (line.subStart == null || line.subEnd == null) return false
        return videoTime >= line.subStart && videoTime <= line.subEnd
    }

    const playingIndex = response.results.findIndex(isPlaying)
    const videoDock = useRef<HTMLDivElement>(null)
    const rowElements = useRef(new Map<number, HTMLTableRowElement>())
    const lastPlayingIndex = useRef(-1)

    // Follow the playback through the transcript, but only while the user is
    // reading around the playhead: once they scroll elsewhere, leave them be.
    useEffect(() => {
        if (playingIndex === -1 || playingIndex === lastPlayingIndex.current) {
            return
        }
        const previousRow = rowElements.current.get(lastPlayingIndex.current)
        lastPlayingIndex.current = playingIndex
        const row = rowElements.current.get(playingIndex)
        if (row == null) {
            return
        }
        const onScreen = (element: HTMLElement | undefined) => {
            if (element == null) return false
            const dockBottom =
                videoDock.current?.getBoundingClientRect().bottom ?? 0
            const rect = element.getBoundingClientRect()
            return rect.bottom > dockBottom && rect.top < window.innerHeight
        }
        if (!onScreen(previousRow) && !onScreen(row)) {
            return
        }
        row.scrollIntoView({
            block: "center",
            behavior: window.matchMedia("(prefers-reduced-motion: reduce)")
                .matches
                ? "auto"
                : "smooth",
        })
    }, [playingIndex])

    const getRowClassName = (
        line: SearchWorkResult,
        index: number,
        isContext: boolean,
    ): string | undefined => {
        const classes: string[] = []
        if (isPlaying(line)) {
            classes.push("doc-row-playing")
        } else if (index % 2 === 1) {
            classes.push("doc-row-striped")
        }
        if (isContext) {
            classes.push("doc-row-context")
        }
        return classes.length > 0 ? classes.join(" ") : undefined
    }

    const { entries, expand } = useContextExpansion(
        response,
        docIdent,
        expandContext,
    )
    const displayedLines = entries.flatMap((x) =>
        x.type == "line" ? [x.line] : [],
    )

    const hasSpeakerColumn =
        isVideo &&
        displayedLines.filter((x) => x.speaker != null && x.speaker != "")
            .length > 0

    const leftVisible =
        (manxVisible && originalManx) || (englishVisible && !originalManx)
    const rightVisible =
        (englishVisible && originalManx) || (manxVisible && !originalManx)
    // on a phone the per-line links collapse into a per-row menu; for video
    // transcripts the column is dropped entirely ("Edit on GitHub" above the
    // transcript covers it). Rendered conditionally (not hidden in CSS) so the
    // note rows' colSpan stays in step with the column count.
    const isMobile = useMediaQuery("(max-width: 600px)")
    // TODO: optimise this - no need to iterate each render
    const linkVisible =
        !(isVideo && isMobile) &&
        (response.gitHubLink ||
            displayedLines.filter(
                (x) =>
                    x.page != null &&
                    (response.pdfLink || response.googleBooksId),
            ).length > 0)
    // the one menu is shared by every row's hamburger: anchored to whichever
    // button opened it
    const [lineMenu, setLineMenu] = useState<{
        anchor: HTMLElement
        line: SearchWorkResult
    } | null>(null)
    const leftLang = originalManx ? "gv" : "en"
    const rightLang = originalManx ? "en" : "gv"
    // the expander and note bands cover the language columns only, leaving the
    // video/speaker/Link cells uncoloured
    const leadingCells = (isVideo ? 1 : 0) + (hasSpeakerColumn ? 1 : 0)
    const languageColSpan = Math.max(
        1,
        (leftVisible ? 1 : 0) + (rightVisible ? 1 : 0),
    )
    return (
        <>
            <div>
                {/*TODO: Lazy Load Youtube player*/}
                {isVideo && videoId != null && (
                    <div className="video-dock" ref={videoDock}>
                        <div className={"youtube-container center"}>
                            <YouTuber ref={player} videoId={videoId} />
                        </div>
                    </div>
                )}
                <div>
                    <table
                        className={
                            "doc-table" + (isVideo ? " doc-table-video" : "")
                        }
                        style={{ tableLayout: "fixed" }}
                        aria-labelledby="tabelLabel"
                    >
                        <thead>
                            <tr>
                                {isVideo && <th className="doc-th-play" />}
                                {hasSpeakerColumn && (
                                    <th className="doc-th-speaker">Speaker</th>
                                )}
                                {leftVisible && (
                                    <th className="doc-lang-head">
                                        {originalManx ? "Gaelg" : "English"}
                                    </th>
                                )}
                                {rightVisible && (
                                    <th className="doc-lang-head">
                                        {originalManx ? "English" : "Gaelg"}
                                    </th>
                                )}
                                {linkVisible && (
                                    <th className="doc-th-link">
                                        {/* on a phone the column is just the
                                            hamburgers: a label adds nothing */}
                                        {!isMobile && "Link"}
                                    </th>
                                )}
                            </tr>
                        </thead>
                        <tbody>
                            {entries.map((entry) => {
                                if (entry.type == "gap") {
                                    return (
                                        <ExpanderRow
                                            key={`gap-${entry.gap.start}`}
                                            gap={entry.gap}
                                            leadingCells={leadingCells}
                                            languageColSpan={languageColSpan}
                                            hasLinkCell={Boolean(linkVisible)}
                                            onExpand={expand}
                                        />
                                    )
                                }
                                const { line, index, isContext } = entry
                                const noteLinked = isNoteLinked(
                                    line,
                                    manxVisible,
                                    englishVisible,
                                )
                                const noteVisible =
                                    Boolean(line.notes) &&
                                    (!noteLinked ||
                                        notesShown !==
                                            toggledNotes.has(
                                                line.csvLineNumber,
                                            ))
                                const noteToggle = noteLinked
                                    ? {
                                          noteVisible,
                                          toggle: () =>
                                              toggleNote(line.csvLineNumber),
                                      }
                                    : undefined
                                const manxText =
                                    diffCorrectedText(
                                        line.manxOriginal,
                                        line.manx,
                                        highlightManx
                                            ? line.manxHighlights
                                            : undefined,
                                    ) ??
                                    highlightText(
                                        highlightManx,
                                        "gv",
                                        line.manx,
                                        line.manxHighlights,
                                        noteToggle,
                                    )
                                const englishText =
                                    diffCorrectedText(
                                        line.englishOriginal,
                                        line.english,
                                        highlightEnglish
                                            ? line.englishHighlights
                                            : undefined,
                                    ) ??
                                    highlightText(
                                        highlightEnglish,
                                        "en",
                                        line.english,
                                        line.englishHighlights,
                                        noteToggle,
                                    )

                                return (
                                    <Fragment
                                        key={
                                            response.title +
                                            line.csvLineNumber.toString()
                                        }
                                    >
                                        <tr
                                            key={line.date}
                                            ref={(element) => {
                                                if (element == null) {
                                                    rowElements.current.delete(
                                                        index,
                                                    )
                                                } else {
                                                    rowElements.current.set(
                                                        index,
                                                        element,
                                                    )
                                                }
                                            }}
                                            className={getRowClassName(
                                                line,
                                                index,
                                                isContext,
                                            )}
                                        >
                                            {isVideo && (
                                                <td>
                                                    <button
                                                        type="button"
                                                        className="doc-play-btn"
                                                        title={
                                                            line.subStart !=
                                                            null
                                                                ? `Play from ${formatTime(line.subStart)}`
                                                                : undefined
                                                        }
                                                        onClick={() => {
                                                            if (
                                                                line.subStart !=
                                                                    null &&
                                                                player.current
                                                            ) {
                                                                player.current.seek(
                                                                    line.subStart,
                                                                )
                                                            }
                                                        }}
                                                    >
                                                        ▶
                                                    </button>
                                                </td>
                                            )}
                                            {hasSpeakerColumn && (
                                                <td>
                                                    {line.speaker}
                                                    {line.subStart != null && (
                                                        <>
                                                            {" "}
                                                            <span className="doc-speaker-time">
                                                                {formatTime(
                                                                    line.subStart,
                                                                )}
                                                            </span>
                                                        </>
                                                    )}
                                                </td>
                                            )}
                                            {leftVisible && (
                                                <td lang={leftLang}>
                                                    {originalManx
                                                        ? manxText
                                                        : englishText}
                                                </td>
                                            )}
                                            {rightVisible && (
                                                <td lang={rightLang}>
                                                    {originalManx
                                                        ? englishText
                                                        : manxText}
                                                </td>
                                            )}
                                            {linkVisible && (
                                                <td className="doc-link-cell">
                                                    {isMobile ? (
                                                        (response.gitHubLink ||
                                                            (line.page !=
                                                                null &&
                                                                (response.pdfLink ||
                                                                    response.googleBooksId))) && (
                                                            <IconButton
                                                                className="doc-link-menu-btn"
                                                                size="small"
                                                                aria-label="Links for this line"
                                                                aria-haspopup="true"
                                                                onClick={(e) =>
                                                                    setLineMenu(
                                                                        {
                                                                            anchor: e.currentTarget,
                                                                            line,
                                                                        },
                                                                    )
                                                                }
                                                            >
                                                                {/* Material "menu" glyph, inlined rather than
                                                                    pulling in the whole @mui/icons-material
                                                                    package */}
                                                                <svg
                                                                    viewBox="0 0 24 24"
                                                                    width="18"
                                                                    height="18"
                                                                    fill="currentColor"
                                                                    aria-hidden="true"
                                                                >
                                                                    <path d="M3 18h18v-2H3v2Zm0-5h18v-2H3v2Zm0-7v2h18V6H3Z" />
                                                                </svg>
                                                            </IconButton>
                                                        )
                                                    ) : (
                                                        <>
                                                            {line.page !=
                                                                null &&
                                                                response.pdfLink && (
                                                                    <>
                                                                        <a
                                                                            href={
                                                                                response.pdfLink +
                                                                                "#page=" +
                                                                                line.page
                                                                            }
                                                                            target="_blank"
                                                                            rel="noreferrer"
                                                                        >
                                                                            p.
                                                                            {
                                                                                line.page
                                                                            }
                                                                        </a>{" "}
                                                                    </>
                                                                )}
                                                            {line.page !=
                                                                null &&
                                                                response.googleBooksId && (
                                                                    <>
                                                                        <a
                                                                            href={`https://books.google.im/books?id=${response.googleBooksId}&pg=PA${line.page}`}
                                                                            target="_blank"
                                                                            rel="noreferrer"
                                                                        >
                                                                            p.
                                                                            {
                                                                                line.page
                                                                            }
                                                                        </a>{" "}
                                                                    </>
                                                                )}
                                                            {response.gitHubLink && (
                                                                <a
                                                                    href={`${response.gitHubLink}#L${line.csvLineNumber}`}
                                                                >
                                                                    edit
                                                                </a>
                                                            )}
                                                        </>
                                                    )}
                                                </td>
                                            )}
                                        </tr>
                                        {noteVisible ? (
                                            <tr className="noteRow">
                                                {Array.from(
                                                    { length: leadingCells },
                                                    (_, i) => (
                                                        <td key={i} />
                                                    ),
                                                )}
                                                <td
                                                    className="doc-note-band"
                                                    colSpan={languageColSpan}
                                                >
                                                    {line.notes}
                                                </td>
                                                {linkVisible && <td />}
                                            </tr>
                                        ) : null}
                                    </Fragment>
                                )
                            })}
                        </tbody>
                    </table>
                </div>
            </div>

            <Menu
                anchorEl={lineMenu?.anchor}
                open={lineMenu != null}
                onClose={() => setLineMenu(null)}
            >
                {lineMenu != null && [
                    lineMenu.line.page != null && response.pdfLink && (
                        <MenuItem
                            key="pdf"
                            dense
                            component="a"
                            href={`${response.pdfLink}#page=${lineMenu.line.page}`}
                            target="_blank"
                            rel="noreferrer"
                            onClick={() => setLineMenu(null)}
                        >
                            Page {lineMenu.line.page} (PDF)
                        </MenuItem>
                    ),
                    lineMenu.line.page != null && response.googleBooksId && (
                        <MenuItem
                            key="books"
                            dense
                            component="a"
                            href={`https://books.google.im/books?id=${response.googleBooksId}&pg=PA${lineMenu.line.page}`}
                            target="_blank"
                            rel="noreferrer"
                            onClick={() => setLineMenu(null)}
                        >
                            Page {lineMenu.line.page} (Google Books)
                        </MenuItem>
                    ),
                    response.gitHubLink && (
                        <MenuItem
                            key="edit"
                            dense
                            component="a"
                            href={`${response.gitHubLink}#L${lineMenu.line.csvLineNumber}`}
                            onClick={() => setLineMenu(null)}
                        >
                            Edit on GitHub
                        </MenuItem>
                    ),
                ]}
            </Menu>

            <Modal
                open={modalOpen}
                onClose={handleModalClose}
                aria-labelledby="modal-modal-title"
                aria-describedby="modal-modal-description"
            >
                <Box sx={style}>
                    <Typography
                        id="modal-modal-title"
                        variant="h6"
                        component="h2"
                        sx={{ fontFamily: "Georgia, serif", color: "#33454D" }}
                    >
                        {modalText}
                    </Typography>
                    <Typography
                        id="modal-modal-description"
                        component="div"
                        sx={{ mt: 2, color: "#2E3F46", overflowY: "auto" }}
                    >
                        {modalSummaries == null && (
                            <div
                                style={{
                                    marginTop: 40,
                                    display: "flex",
                                    alignItems: "center",
                                    justifyContent: "center",
                                }}
                            >
                                <CircularProgress
                                    style={{ alignSelf: "center" }}
                                />
                            </div>
                        )}

                        {modalSummaries != null &&
                            groupByDictionary(modalSummaries).map(
                                ([dictionaryName, summaries]) => (
                                    <div
                                        className="dict-popup-group"
                                        key={dictionaryName}
                                    >
                                        <h3 className="dict-popup-dictionary">
                                            {dictionaryName}
                                        </h3>
                                        {summaries.map((summary, index) => (
                                            // primaryWord differentiates fuzzy matches:
                                            // 'dy hroggal' and 'cha greck' both resolve
                                            // via 'hroggal'/'greck'
                                            <div
                                                className="dict-popup-entry"
                                                key={index}
                                            >
                                                <strong>
                                                    {summary.primaryWord}
                                                </strong>
                                                {": "}
                                                {summary.summary}
                                            </div>
                                        ))}
                                    </div>
                                ),
                            )}
                        {modalSummaries?.length == 0 && (
                            <span>
                                Could not find definition
                                {modalMultidictWord != null && (
                                    <>
                                        {". Try searching "}
                                        <MultidictLink
                                            word={modalMultidictWord}
                                            language="Manx"
                                        />
                                    </>
                                )}
                            </span>
                        )}
                    </Typography>
                </Box>
            </Modal>
        </>
    )
}

const style = {
    position: "absolute" as const,
    top: "50%",
    left: "50%",
    transform: "translate(-50%, -50%)",
    width: 520,
    maxWidth: "90vw",
    maxHeight: "85vh",
    display: "flex",
    flexDirection: "column" as const,
    bgcolor: "#FFFEF9",
    border: "1px solid #E8DDC4",
    borderRadius: "4px",
    boxShadow: "0 2px 12px rgba(62,80,88,0.2)",
    p: 4,
}
