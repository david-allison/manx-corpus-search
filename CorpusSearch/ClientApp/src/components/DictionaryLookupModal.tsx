import {
    MouseEvent as ReactMouseEvent,
    useEffect,
    useRef,
    useState,
} from "react"
import { Box, CircularProgress, Modal } from "@mui/material"
import Typography from "@mui/material/Typography"
import { DictionaryResponse, manxDictionaryLookup } from "../api/DictionaryApi"
import { getMultidictLookupWord, MultidictLink } from "./MultidictLink"
import { getSelectedWordOrPhrase, getWordAtPoint } from "../utils/Selection"
import "./DictionaryLookupModal.css"

/** Splits a dictionary's entries into those that are entries *for* the
 * selection (`own`) and those merely reached through it (`derived`: root
 * lemmas and variant spellings like 'muir' -> mooir), nesting the derived
 * ones under the selection. Phrase entries the tapped line doesn't actually
 * contain ('ry gheddyn' when the line says 'dy gheddyn', 'thie veg' when it
 * says 'feer veg') are dropped from both tiers, unless nothing else would
 * remain. */
/** Trims punctuation (but never letters/digits, so internal apostrophes and
 * hyphens survive) from the edges of a tapped word: 'meenid,' -> 'meenid' */
export const trimPunctuation = (s: string): string =>
    s.trim().replace(/^[^\p{L}\p{N}]+|[^\p{L}\p{N}]+$/gu, "")

export const classifyEntries = (
    word: string,
    context: string,
    entries: DictionaryResponse,
): { own: DictionaryResponse; derived: DictionaryResponse } => {
    const wordLc = trimPunctuation(word.toLowerCase())
    // an entry heads the selection in either direction: 'dy yannoo' heads the
    // selection 'yannoo'; the part 'goll' heads the compound 'goll-mygeayrt'
    const headsSelection = (p: string) =>
        p.toLowerCase() == wordLc ||
        p
            .toLowerCase()
            .split(/[\s'’-]+/)
            .includes(wordLc) ||
        wordLc.split(/[\s'’-]+/).includes(p.toLowerCase())
    // a phrase or compound entry must also occur in the tapped line (when
    // one is known): 'ry gheddyn' matches the word 'gheddyn' but is not what
    // the line says, and 'sheeyney-magh' is not what a line saying just
    // 'magh' says. Texts and dictionaries spell compounds with hyphen or
    // space interchangeably, so containment ignores the difference
    const foldHyphens = (s: string) => s.toLowerCase().replace(/[-‑]/g, " ")
    const supportedByContext = (p: string) =>
        !/[\s\-‑]/.test(p) ||
        p.toLowerCase() == wordLc ||
        context == "" ||
        foldHyphens(context).includes(foldHyphens(p))
    const isOwn = (x: DictionaryResponse[number]) =>
        !x.rootDepth &&
        headsSelection(x.primaryWord) &&
        supportedByContext(x.primaryWord)
    let own = entries.filter(isOwn)
    if (own.length == 0) {
        // nothing heads the selection in-context: an out-of-context phrase
        // entry ('my yinnagh' for yinnagh) is better shown plainly than as
        // an orphaned arrow under a definition-less anchor
        own = entries.filter(
            (x) => !x.rootDepth && headsSelection(x.primaryWord),
        )
    }
    // the context rule applies down the chain too: a phrase entry is noise
    // unless the line says it, whether it rode in on a root's word list
    // ('cur mow' via vow ↳ mow, 'dy olk' via smessey ↳ olk) or on the
    // selection's own ('ro veg'/'thie veg' under veg)
    let derived = entries.filter((x) => !own.includes(x))
    const inContext = derived.filter((x) => supportedByContext(x.primaryWord))
    if (own.length > 0 || inContext.length > 0) {
        derived = inContext
    }
    return { own, derived }
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

export const DictionaryLookupModal = (props: DictionaryLookupState) => {
    const { open, word, context, onClose } = props

    const [summaries, setSummaries] = useState<DictionaryResponse | null>(null)

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
                <Typography
                    id="modal-modal-title"
                    variant="h6"
                    component="h2"
                    sx={{ fontFamily: "Georgia, serif", color: "#33454D" }}
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
                                Nothing found for “{word}” — near spellings:
                            </p>
                            {groupByDictionary(summaries).map(
                                ([dictionaryName, entries]) => (
                                    <div
                                        className="dict-popup-group"
                                        key={dictionaryName}
                                    >
                                        <h3 className="dict-popup-dictionary">
                                            {dictionaryName}
                                        </h3>
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
                                                {": "}
                                                {summary.summary}
                                            </div>
                                        ))}
                                    </div>
                                ),
                            )}
                        </div>
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
                                        <h3 className="dict-popup-dictionary">
                                            {dictionaryName}
                                        </h3>
                                        {own.map((summary, index) => (
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
                                                {": "}
                                                {summary.summary}
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
