import {
    MouseEvent as ReactMouseEvent,
    useEffect,
    useRef,
    useState,
} from "react"
import { Box, CircularProgress, Modal } from "@mui/material"
import Typography from "@mui/material/Typography"
import { DictionaryResponse, manxDictionaryLookup } from "../api/DictionaryApi"
import { DefinitionText, GrammarLabel } from "./GrammarAbbr"
import { VerseVersionsModal } from "./VerseVersionsModal"
import { getMultidictLookupWord, MultidictLink } from "./MultidictLink"
import { getSelectedWordOrPhrase, getWordAtPoint } from "../utils/Selection"
import {
    classifyEntries,
    groupByDictionary,
    headingFor,
    trimPunctuation,
} from "../utils/DictionaryEntries"
import { UnverifiedMark } from "./UnverifiedMark"
import "./DictionaryLookupModal.css"

export type DictionaryLookupState = {
    open: boolean
    /** the looked-up word; kept while closed so reopening it skips the fetch */
    word: string
    /** the text surrounding the word: lets the server match phrases/idioms (#135) */
    context: string
    onClose: (event: unknown, reason?: string) => void
}

/** The word-under-the-click lookup driving [DictionaryLookupModal]: spread the
 * returned `modal` onto it, and wire `openFromClick` to the text's onClick */
export const useDictionaryLookup = (): {
    openFromClick: (event: ReactMouseEvent, context: string) => void
    modal: DictionaryLookupState
} => {
    const [open, setOpen] = useState(false)
    const openedAt = useRef(0)
    const [word, setWord] = useState("")
    const [context, setContext] = useState("")

    const openFromClick = (event: ReactMouseEvent, lineContext: string) => {
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

        // remove notes/citations '[1]' at the end of the string, and the
        // line's punctuation around the tap ('meenid,' -> 'meenid')
        const stringToSearch = trimPunctuation(
            wordOrPhrase.replace(/\[\d+]/g, " "),
        )
        if (stringToSearch == "") {
            return
        }

        setOpen(true)
        openedAt.current = performance.now()
        setWord(stringToSearch)
        setContext(lineContext)
    }

    const onClose = (_event: unknown, reason?: string) => {
        // a double-click's second click lands on the backdrop just after the
        // first click opened the popup: it must not immediately close it
        if (
            reason == "backdropClick" &&
            performance.now() - openedAt.current < 500
        ) {
            return
        }
        setOpen(false)
    }

    return { openFromClick, modal: { open, word, context, onClose } }
}

/** "learnmanx.com" from the source URL, for the corner credit's second line */
const hostOf = (url: string): string => {
    try {
        return new URL(url).hostname.replace(/^www\./, "")
    } catch {
        return ""
    }
}

/** The dictionary group heading; linked when the source publishes a home
 * page (the Culture Vannin citation) */
const DictionaryHeading = ({
    dictionaryName,
    entries,
}: {
    dictionaryName: string
    entries: DictionaryResponse
}) => (
    <h3 className="dict-popup-dictionary">
        {entries[0]?.sourceUrl ? (
            <a href={entries[0].sourceUrl} target="_blank" rel="noreferrer">
                {dictionaryName}
            </a>
        ) : (
            dictionaryName
        )}
    </h3>
)

/** Tapping a Phillips 1610 spelling (dwyne) reaches its entries through the
 * spelling link: say so up front, so the groups below read as the classical
 * word's entries rather than dictionaries knowing the 1610 form */
const PhillipsBridge = ({
    word,
    summaries,
}: {
    word: string
    summaries: DictionaryResponse
}) => {
    const target = summaries.find(
        (x) => x.phillipsSpellingOf,
    )?.phillipsSpellingOf
    return target ? (
        <p className="dict-popup-bridge">
            <strong>{trimPunctuation(word)}</strong>
            {" is a c. 1610 spelling (Phillips) of "}
            <strong>{target}</strong>
            {":"}
        </p>
    ) : null
}

/** The entry's declared plural, split out of the definition text */
const PluralNote = ({ summary }: { summary: DictionaryResponse[number] }) =>
    summary.plurals?.length ? (
        <span className="dict-popup-plural">
            {", "}
            <abbr className="dict-abbr" title="plural">
                pl.
            </abbr>{" "}
            {summary.plurals.join(", ")}
        </span>
    ) : null

/** The printed sense this line's occurrence was read as: a claim about the
 * clicked word in its line, not about the entry, so it trails the definition
 * as a note rather than joining it */
const SenseNote = ({ summary }: { summary: DictionaryResponse[number] }) =>
    summary.senseNote ? (
        <span className="dict-popup-sense">
            {" "}
            <abbr
                className="dict-abbr"
                title="the sense this line uses, as read from its translation"
            >
                here:
            </abbr>{" "}
            {summary.senseNote}
        </span>
    ) : null

/** Plays the entry's pronunciation recording (streamed from the source) */
const AudioButton = ({
    summary,
    className = "dict-popup-audio",
}: {
    summary: DictionaryResponse[number]
    className?: string
}) => {
    if (!summary.audioUrl) return null
    const url = summary.audioUrl
    return (
        <button
            className={className}
            aria-label={`Play pronunciation of ${summary.primaryWord}`}
            title="Play pronunciation"
            onClick={() => {
                new Audio(url).play().catch(console.warn)
            }}
        >
            {"\u25B6"}
        </button>
    )
}

export const DictionaryLookupModal = (props: DictionaryLookupState) => {
    const { open, word, context, onClose } = props

    const [summaries, setSummaries] = useState<DictionaryResponse | null>(null)

    // a tapped scripture citation ("Jud. xii. 6"): the verse's other-versions
    // popup opens over the dictionary popup; following a version link closes both
    const [citationKey, setCitationKey] = useState<string | null>(null)

    useEffect(() => {
        setSummaries(null)
        if (!word) return
        manxDictionaryLookup(word, context)
            .then(setSummaries)
            .catch((e) => {
                console.warn(e)
            })
    }, [word, context])

    const multidictWord = getMultidictLookupWord(word)

    // the tapped word's own recording anchors the modal's corner as a single
    // control, not an entry row; other words' recordings stay inline
    const wordKey = trimPunctuation(word).toLowerCase()
    const cornerAudio = summaries?.find(
        (x) =>
            x.audioUrl &&
            !x.nearMatchOf &&
            trimPunctuation(x.primaryWord).toLowerCase() == wordKey,
    )

    // the suggestion tier only fires on a total miss, so a response is either
    // all near-matches or none
    const nearMatchOnly =
        summaries != null &&
        summaries.length > 0 &&
        summaries.every((x) => x.nearMatchOf)

    return (
        <Modal
            open={open}
            onClose={onClose}
            aria-labelledby="modal-modal-title"
            aria-describedby="modal-modal-description"
        >
            <Box sx={style}>
                {cornerAudio && (
                    <div className="dict-popup-audio-corner">
                        <AudioButton
                            summary={cornerAudio}
                            className="dict-popup-audio-main"
                        />
                        {cornerAudio.sourceUrl && (
                            // the recording's attribution stays with the control
                            <a
                                className="dict-popup-audio-credit"
                                href={cornerAudio.sourceUrl}
                                target="_blank"
                                rel="noreferrer"
                            >
                                <span>
                                    {cornerAudio.sourceCredit ||
                                        cornerAudio.dictionaryName}
                                </span>
                                <span>{hostOf(cornerAudio.sourceUrl)}</span>
                            </a>
                        )}
                    </div>
                )}
                <Typography
                    id="modal-modal-title"
                    variant="h6"
                    component="h2"
                    sx={{
                        fontFamily: "Georgia, serif",
                        color: "#33454D",
                        pr: "96px",
                    }}
                >
                    {word}
                </Typography>
                <Typography
                    id="modal-modal-description"
                    component="div"
                    sx={{ mt: 2, color: "#2E3F46", overflowY: "auto" }}
                >
                    {summaries == null && (
                        <div
                            style={{
                                marginTop: 40,
                                display: "flex",
                                alignItems: "center",
                                justifyContent: "center",
                            }}
                        >
                            <CircularProgress style={{ alignSelf: "center" }} />
                        </div>
                    )}

                    {nearMatchOnly && summaries != null && (
                        // "did you mean" fallback: a suggestion box, styled so
                        // it can never read as an entry for the tapped word
                        <div className="dict-popup-suggestions">
                            <p className="dict-popup-suggestions-note">
                                Nothing found for “{word}”. Near spellings:
                            </p>
                            {groupByDictionary(summaries).map(
                                ([dictionaryName, entries]) => (
                                    <div
                                        className="dict-popup-group"
                                        key={dictionaryName}
                                    >
                                        <DictionaryHeading
                                            dictionaryName={dictionaryName}
                                            entries={entries}
                                        />
                                        {entries.map((summary, index) => (
                                            // flat, un-nested: a suggestion is
                                            // not the selection's root chain
                                            <div
                                                className="dict-popup-entry"
                                                key={index}
                                            >
                                                <strong>
                                                    {summary.primaryWord}
                                                </strong>
                                                <GrammarLabel
                                                    label={summary.grammarLabel}
                                                />
                                                {": "}
                                                <DefinitionText
                                                    text={summary.summary}
                                                    citations={
                                                        summary.citations
                                                    }
                                                    onCitationClick={
                                                        setCitationKey
                                                    }
                                                />
                                                {summary != cornerAudio && (
                                                    <AudioButton
                                                        summary={summary}
                                                    />
                                                )}
                                            </div>
                                        ))}
                                    </div>
                                ),
                            )}
                        </div>
                    )}

                    {summaries != null && !nearMatchOnly && (
                        <PhillipsBridge word={word} summaries={summaries} />
                    )}
                    {summaries != null &&
                        !nearMatchOnly &&
                        groupByDictionary(summaries).map(
                            ([dictionaryName, entries]) => {
                                const { own, derived } = classifyEntries(
                                    word,
                                    context,
                                    entries,
                                )
                                return (
                                    <div
                                        className="dict-popup-group"
                                        key={dictionaryName}
                                    >
                                        <DictionaryHeading
                                            dictionaryName={dictionaryName}
                                            entries={entries}
                                        />
                                        {own.map((summary, index) => (
                                            // primaryWord differentiates fuzzy matches:
                                            // 'dy hroggal' and 'cha greck' both resolve
                                            // via 'hroggal'/'greck'
                                            <div
                                                className="dict-popup-entry"
                                                key={index}
                                            >
                                                <strong>
                                                    {headingFor(word, summary)}
                                                </strong>
                                                <GrammarLabel
                                                    label={summary.grammarLabel}
                                                />
                                                {": "}
                                                <DefinitionText
                                                    text={summary.summary}
                                                    citations={
                                                        summary.citations
                                                    }
                                                    onCitationClick={
                                                        setCitationKey
                                                    }
                                                />
                                                <PluralNote summary={summary} />
                                                <SenseNote summary={summary} />
                                                {summary != cornerAudio && (
                                                    <AudioButton
                                                        summary={summary}
                                                    />
                                                )}
                                            </div>
                                        ))}
                                        {own.length == 0 &&
                                            derived.length > 0 && (
                                                // the selection has no entry of
                                                // its own: anchor the chain
                                                <div className="dict-popup-entry">
                                                    <strong>{word}</strong>
                                                </div>
                                            )}
                                        {derived.map((summary, index) => (
                                            // the selection's root-lemma chain:
                                            // each hop indents one level further
                                            <div
                                                className="dict-popup-entry dict-popup-root-entry"
                                                style={{
                                                    marginLeft:
                                                        20 *
                                                        Math.max(
                                                            1,
                                                            summary.rootDepth ||
                                                                0,
                                                        ),
                                                }}
                                                key={`derived-${index}`}
                                            >
                                                <span
                                                    className="dict-popup-root-connector"
                                                    aria-label="root form"
                                                >
                                                    {"↳ "}
                                                </span>
                                                <strong>
                                                    {summary.primaryWord}
                                                </strong>
                                                <UnverifiedMark
                                                    unverified={
                                                        summary.unverifiedLink
                                                    }
                                                />
                                                <GrammarLabel
                                                    label={summary.grammarLabel}
                                                />
                                                {": "}
                                                <DefinitionText
                                                    text={summary.summary}
                                                    citations={
                                                        summary.citations
                                                    }
                                                    onCitationClick={
                                                        setCitationKey
                                                    }
                                                />
                                                <PluralNote summary={summary} />
                                                <SenseNote summary={summary} />
                                                {summary != cornerAudio && (
                                                    <AudioButton
                                                        summary={summary}
                                                    />
                                                )}
                                            </div>
                                        ))}
                                    </div>
                                )
                            },
                        )}
                    {summaries?.length == 0 && (
                        <span>
                            Could not find definition
                            {multidictWord != null && (
                                <>
                                    {". Try searching "}
                                    <MultidictLink
                                        word={multidictWord}
                                        language="Manx"
                                    />
                                </>
                            )}
                        </span>
                    )}
                </Typography>
                <VerseVersionsModal
                    refKey={citationKey}
                    onClose={() => setCitationKey(null)}
                    onNavigate={() => onClose(undefined, "citation-navigate")}
                />
            </Box>
        </Modal>
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
