import { Fragment, memo, useEffect, useState } from "react"
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
import { CoverageText } from "./CoverageText"
import { TokenCoverage } from "../api/DictionaryApi"
import { VerseVersionsModal } from "./VerseVersionsModal"
import "./ComparisonTable.css"

/** memoized: a keystroke in the document search box re-renders the page
 * shell, but none of these props change until its results arrive — the
 * table (up to thousands of rows) must not re-render per letter */
export const ComparisonTable = memo(function ComparisonTableInner(props: {
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
    /** dictionary debug mode: per-token coverage keyed by the line's Manx
     * text; when set, Manx cells render colour-coded tokens instead */
    dictCoverage?: Map<string, TokenCoverage[]> | null
    /** csvLineNumber of a ?ref= deep link's verse: the row is flashed */
    targetLine?: number
}) {
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
        dictCoverage,
        targetLine,
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
        if (line.csvLineNumber == targetLine) {
            classes.push("doc-row-target")
        }
        return classes.length > 0 ? classes.join(" ") : undefined
    }

    // the Ref column's "other versions" popup: canonical key of the tapped verse
    const [versionsKey, setVersionsKey] = useState<string | null>(null)

    /** The Ref cell: a verse with a cross-version identity links to its other
     * versions; an unresolved reference is plain text */
    const referenceCell = (line: SearchWorkResult) =>
        line.canonicalReference ? (
            <button
                type="button"
                className="doc-ref-link"
                title="This verse in other versions"
                onClick={() => setVersionsKey(line.canonicalReference ?? null)}
            >
                {line.reference}
            </button>
        ) : (
            line.reference
        )

    /** A reference-only row (chapter heading): no text of its own, so it renders
     * as a section heading across the language columns */
    const isHeadingRow = (line: SearchWorkResult) =>
        Boolean(line.reference) && !line.manx && !line.english

    const { entries, expand } = useContextExpansion(
        response,
        docIdent,
        expandContext,
    )
    const displayedLines = entries.flatMap((x) =>
        x.type == "line" ? [x.line] : [],
    )

    // any document may carry speakers (interview transcriptions), not just videos
    const hasSpeakerColumn =
        displayedLines.filter((x) => x.speaker != null && x.speaker != "")
            .length > 0

    // verse/chapter references (scripture documents): metadata, not body text
    const hasReferenceColumn =
        displayedLines.filter((x) => x.reference != null && x.reference != "")
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
    const leadingCells =
        (isVideo ? 1 : 0) +
        (hasSpeakerColumn ? 1 : 0) +
        (hasReferenceColumn ? 1 : 0)
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
                                {hasReferenceColumn && (
                                    <th className="doc-th-reference">Ref</th>
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
                                if (isHeadingRow(line)) {
                                    return (
                                        <tr
                                            key={
                                                response.title +
                                                line.csvLineNumber.toString()
                                            }
                                            id={`line-${line.csvLineNumber.toString()}`}
                                            className={
                                                "doc-row-heading" +
                                                (line.csvLineNumber ==
                                                targetLine
                                                    ? " doc-row-target"
                                                    : "")
                                            }
                                        >
                                            {isVideo && <td />}
                                            {hasSpeakerColumn && <td />}
                                            {hasReferenceColumn && <td />}
                                            <td
                                                className="doc-heading-band"
                                                colSpan={languageColSpan}
                                            >
                                                {referenceCell(line)}
                                            </td>
                                            {linkVisible && <td />}
                                        </tr>
                                    )
                                }
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
                                const lineCoverage =
                                    line.language == null
                                        ? dictCoverage?.get(line.manx)
                                        : undefined
                                const manxText = lineCoverage ? (
                                    <CoverageText
                                        text={line.manx}
                                        tokens={lineCoverage}
                                    />
                                ) : (
                                    <LineText
                                        text={line.manx}
                                        original={line.manxOriginal}
                                        highlights={line.manxHighlights}
                                        shouldHighlight={highlightManx}
                                        translations={getTranslations("gv")}
                                        query={value}
                                        noteToggle={noteToggle}
                                        // a non-Manx row (line.language set, e.g. an untranslated
                                        // English passage) has no Manx to look up
                                        onWordClick={
                                            line.language == null
                                                ? dictionary.openFromClick
                                                : undefined
                                        }
                                    />
                                )
                                const englishText = (
                                    <LineText
                                        text={line.english}
                                        original={line.englishOriginal}
                                        highlights={line.englishHighlights}
                                        shouldHighlight={highlightEnglish}
                                        translations={getTranslations("en")}
                                        query={value}
                                        noteToggle={noteToggle}
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
                                            id={`line-${line.csvLineNumber.toString()}`}
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
                                            {hasReferenceColumn && (
                                                <td className="doc-td-reference">
                                                    {referenceCell(line)}
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
            <VerseVersionsModal
                refKey={versionsKey}
                docIdent={docIdent}
                onClose={() => setVersionsKey(null)}
            />
        </>
    )
})
