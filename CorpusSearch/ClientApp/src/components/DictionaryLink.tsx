import { useLayoutEffect, useRef, useState } from "react"
import { DefinedInDictionaries, DictionaryDefinition } from "../api/SearchApi"

/* entries shorter than this render in full: collapsing 3-4 lines to 2 plus a
   toggle row saves no space */
const CLAMP_THRESHOLD_LINES = 5

export function hasDictionaryDefinitions(dictionaries?: DefinedInDictionaries) {
    if (!dictionaries) {
        return false
    }
    return (
        Object.keys(dictionaries).filter(
            (dictionaryName) => dictionaries[dictionaryName].entries.length > 0,
        ).length > 0
    )
}

export const DictionaryLink = (props: {
    query: string
    dictionaries: DefinedInDictionaries
}) => {
    return (
        <>
            {Object.keys(props.dictionaries)
                .filter(
                    (dictionaryName) =>
                        props.dictionaries[dictionaryName].entries.length > 0,
                )
                .map((dictionaryName) => (
                    <DictionaryRow
                        key={dictionaryName}
                        dictionary={props.dictionaries[dictionaryName]}
                        dictionaryName={dictionaryName}
                        query={props.query}
                    />
                ))}
        </>
    )
}

/** One dictionary's entries; long entries clamp to two lines with a toggle */
const DictionaryRow = (props: {
    dictionary: DictionaryDefinition
    dictionaryName: string
    query: string
}) => {
    const text = props.dictionary.entries
        .map((e, i) => `${i + 1}) ${e}`)
        .join(" ")

    const entryRef = useRef<HTMLDivElement>(null)
    const [expanded, setExpanded] = useState(false)
    const [clampable, setClampable] = useState(true)

    // measured before paint, from the clamped render: scrollHeight is the
    // full content height even while overflow is hidden
    useLayoutEffect(() => {
        setExpanded(false)
        const el = entryRef.current
        if (!el) {
            return
        }
        const lineHeight = parseFloat(getComputedStyle(el).lineHeight)
        const lines = el.scrollHeight / lineHeight
        setClampable(lines >= CLAMP_THRESHOLD_LINES)
    }, [text])

    const clamped = clampable && !expanded
    return (
        <div className="dict-strip-row">
            {/* the heading runs into the text: one wrapping paragraph */}
            <div
                ref={entryRef}
                className={
                    "dict-strip-entry" +
                    (clamped ? " dict-strip-entry-clamped" : "")
                }
            >
                <DictionaryNameHeader
                    dictionary={props.dictionary}
                    dictionaryName={props.dictionaryName}
                    query={props.query}
                />{" "}
                <span className="dict-strip-text">{text}</span>
            </div>
            {clampable && (
                <button
                    className="dict-strip-toggle"
                    aria-expanded={expanded}
                    onClick={() => setExpanded(!expanded)}
                >
                    {expanded ? "show less " : "show full entry "}
                    <span aria-hidden="true">{expanded ? "▴" : "▾"}</span>
                </button>
            )}
        </div>
    )
}

const DictionaryNameHeader = (props: {
    dictionary: DictionaryDefinition
    dictionaryName: string
    query: string
}) => {
    const { dictionaryName, query, dictionary } = props

    if (!dictionary.allowLookup) {
        return <span className="dict-strip-label">{dictionaryName}:</span>
    }

    return (
        <a
            className="dict-strip-label"
            href={`/Dictionary/${dictionaryName}/${query}`}
            target="_blank"
            rel="noreferrer"
        >
            {dictionaryName}:
        </a>
    )
}
