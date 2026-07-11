import { Fragment, useEffect, useState } from "react"
import { useMediaQuery } from "@mui/material"
import { SearchWorkResponse, SearchWorkResult } from "../api/SearchWorkApi"
import { Translations } from "../api/SearchApi"
import YouTuber from "./YouTuber"
import { useContextExpansion } from "../hooks/useContextExpansion"
import { formatTime, useVideoSync } from "../hooks/useVideoSync"
import {
    DictionaryLookupModal,
    useDictionaryLookup,
} from "./DictionaryLookupModal"
import { isNoteLinked, LineText, NoteToggle } from "./LineText"
import { ExpanderRow } from "./ExpanderRow"
import { LineLinkCell } from "./LineLinks"
import "./ComparisonTable.css"

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

    const dictionary = useDictionaryLookup()

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

    const getTranslations = (key: string) => {
        if (translations == null) return []
        return translations[key] ?? []
    }

    const { videoId, isVideo, player, videoDock, rowElements, isPlaying } =
        useVideoSync(response?.source, response.results)

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
    // on a phone the Link column is dropped entirely: the "Edit on GitHub"
    // link above the document covers it, and the text needs the width.
    // Rendered conditionally (not hidden in CSS) so the note rows' colSpan
    // stays in step with the column count.
    const isMobile = useMediaQuery("(max-width: 600px)")
    // TODO: optimise this - no need to iterate each render
    const linkVisible =
        !isMobile &&
        (response.gitHubLink ||
            displayedLines.filter(
                (x) =>
                    x.page != null &&
                    (response.pdfLink || response.googleBooksId),
            ).length > 0)
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
                                const noteToggle: NoteToggle | undefined =
                                    noteLinked
                                        ? {
                                              noteVisible,
                                              toggle: () =>
                                                  toggleNote(
                                                      line.csvLineNumber,
                                                  ),
                                          }
                                        : undefined
                                const manxText = (
                                    <LineText
                                        text={line.manx}
                                        original={line.manxOriginal}
                                        highlights={line.manxHighlights}
                                        shouldHighlight={highlightManx}
                                        languageCode="gv"
                                        translations={getTranslations("gv")}
                                        query={value}
                                        noteToggle={noteToggle}
                                        onWordClick={dictionary.openFromClick}
                                    />
                                )
                                const englishText = (
                                    <LineText
                                        text={line.english}
                                        original={line.englishOriginal}
                                        highlights={line.englishHighlights}
                                        shouldHighlight={highlightEnglish}
                                        languageCode="en"
                                        translations={getTranslations("en")}
                                        query={value}
                                        noteToggle={noteToggle}
                                        onWordClick={dictionary.openFromClick}
                                    />
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
                                                <LineLinkCell
                                                    line={line}
                                                    response={response}
                                                />
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

            <DictionaryLookupModal {...dictionary.modal} />
        </>
    )
}
