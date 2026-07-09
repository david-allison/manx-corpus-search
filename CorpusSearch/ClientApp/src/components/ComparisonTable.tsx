import { SearchWorkResponse, SearchWorkResult } from "../api/SearchWorkApi"
import { HighlightRange, Translations } from "../api/SearchApi"
import { getSelectedWordOrPhrase } from "../utils/Selection"
import { Fragment, useEffect, useRef, useState, ReactNode } from "react"
import { DictionaryResponse, manxDictionaryLookup } from "../api/DictionaryApi"
import { getMultidictLookupWord, MultidictLink } from "./MultidictLink"
import Highlighter from "react-highlight-words"
import { Box, CircularProgress, Modal, useMediaQuery } from "@mui/material"
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
    } = props

    const onClickWordForDictionaryLookup = (context: string) => {
        const selection = window.getSelection()
        if (selection == null) {
            return
        }

        const range = getSelectedWordOrPhrase(selection)

        if (range == null || range.split(" ").length > 4) {
            return
        }

        // remove notes/citations '[1]' at the end of the string
        const stringToSearch = range.replace(/\[\d+]/g, " ")

        setModalOpen(true)
        setModalText(stringToSearch.trim())
        setModalContext(context)
    }

    const [modalOpen, setModalOpen] = useState(false)
    const [modalText, setModalText] = useState("")
    // the text surrounding modalText: lets the server match phrases/idioms (#135)
    const [modalContext, setModalContext] = useState("")
    const handleModalClose = () => setModalOpen(false)
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

    const highlightText = (
        shouldHighlight: boolean,
        languageCode: "gv" | "en",
        lineValue: string,
        highlights?: HighlightRange[],
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
            const chunks = shouldHighlight
                ? segmentChunks(highlights ?? [], segmentStart, item.length)
                : null
            segmentStart += item.length + 1 // + the removed "\n"
            return (
                <div
                    onClick={() => {
                        if (languageCode == "gv") {
                            onClickWordForDictionaryLookup(lineValue)
                        }
                    }}
                    className="doc-line"
                    key={key}
                >
                    <Highlighter
                        highlightClassName={
                            shouldHighlight
                                ? "textHighlight"
                                : "textHighlightAlternate"
                        }
                        searchWords={searchWords}
                        autoEscape={false}
                        findChunks={chunks ? () => chunks : undefined}
                        textToHighlight={item}
                    />
                    <br />
                </div>
            )
        })
    }

    /**
     * If originalText exists, perform a diff and display this to the user
     * This displays changes we made to the document
     */
    const diffCorrectedText = (
        originalText: string | undefined,
        currentText: string,
    ): ReactNode | null => {
        if (!originalText) {
            return null
        }
        const result = diffChars(originalText, currentText)

        // TODO: This only handles the correction, not the original
        // TODO: Also apply justify to 'browse' screen
        return (
            <div
                onClick={() => {
                    onClickWordForDictionaryLookup(currentText)
                }}
                className="doc-line"
            >
                {/* TODO: improve highlighting: search matches aren't highlighted in the diff view */}
                {result.map((part) => {
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
                            className={className}
                            style={{ backgroundColor: color }}
                        >
                            {part.value}
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

    const getVideoId = (source: string) => {
        try {
            return new URL(source).searchParams.get("v")
        } catch (e) {
            console.warn(e)
            return ""
        }
    }

    let isVideo =
        response?.source?.startsWith("https://www.youtube") ||
        response?.source?.startsWith("https://youtube.com")
    const videoId = !isVideo ? "" : getVideoId(response.source)
    if (!videoId) {
        isVideo = false
    }
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
    // per-line edit links don't earn a whole column on a phone; "Edit on GitHub"
    // above the transcript still covers it. Rendered conditionally (not hidden in
    // CSS) so the note rows' colSpan stays in step with the column count.
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
    const leftLang = originalManx ? "gv" : "en"
    const rightLang = originalManx ? "en" : "gv"
    const visibleColumnCount = [
        isVideo,
        hasSpeakerColumn,
        leftVisible,
        rightVisible,
        linkVisible,
    ].filter(Boolean).length
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
                                    <th className="doc-th-link">Link</th>
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
                                            leadingCells={
                                                (isVideo ? 1 : 0) +
                                                (hasSpeakerColumn ? 1 : 0)
                                            }
                                            languageColSpan={Math.max(
                                                1,
                                                (leftVisible ? 1 : 0) +
                                                    (rightVisible ? 1 : 0),
                                            )}
                                            hasLinkCell={Boolean(linkVisible)}
                                            onExpand={expand}
                                        />
                                    )
                                }
                                const { line, index, isContext } = entry
                                // TODO: Only due to technical reasons, we can't mix highlights and diffs.
                                // This should be fixed via vendoring react-highlight-words's `Highlighter` class
                                const manxText =
                                    diffCorrectedText(
                                        line.manxOriginal,
                                        line.manx,
                                    ) ??
                                    highlightText(
                                        highlightManx,
                                        "gv",
                                        line.manx,
                                        line.manxHighlights,
                                    )
                                const englishText =
                                    diffCorrectedText(
                                        line.englishOriginal,
                                        line.english,
                                    ) ??
                                    highlightText(
                                        highlightEnglish,
                                        "en",
                                        line.english,
                                        line.englishHighlights,
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
                                                    {line.page != null &&
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
                                                                    {line.page}
                                                                </a>{" "}
                                                            </>
                                                        )}
                                                    {line.page != null &&
                                                        response.googleBooksId && (
                                                            <>
                                                                <a
                                                                    href={`https://books.google.im/books?id=${response.googleBooksId}&pg=PA${line.page}`}
                                                                    target="_blank"
                                                                    rel="noreferrer"
                                                                >
                                                                    p.
                                                                    {line.page}
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
                                                </td>
                                            )}
                                        </tr>
                                        {line.notes ? (
                                            <tr className="noteRow">
                                                <td
                                                    colSpan={visibleColumnCount}
                                                >
                                                    {line.notes}
                                                </td>
                                            </tr>
                                        ) : null}
                                    </Fragment>
                                )
                            })}
                        </tbody>
                    </table>
                </div>
            </div>

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
