import { ChangeEvent, ReactNode, useState } from "react"
import { HomeData } from "../routes/Home"
import "./AdvancedOptions.css"

export type DateRange = {
    start: number
    end: number
}

const AdvancedOptions = (props: {
    onDateRangeChange: (range: DateRange) => void
    onMatchChange: (match: boolean) => void
    ignoreHyphens: boolean
    onIgnoreHyphensChange: (ignoreHyphens: boolean) => void
    caseSensitive: boolean
    onCaseSensitiveChange: (caseSensitive: boolean) => void
    children?: ReactNode
}) => {
    // keep the raw text so clearing a field while typing doesn't snap the value back
    const [startText, setStartText] = useState("1500")
    const [endText, setEndText] = useState(String(HomeData.currentYear))

    const commitRange = (start: string, end: string) => {
        props.onDateRangeChange({
            start: parseInt(start, 10) || 1500,
            end: parseInt(end, 10) || HomeData.currentYear,
        })
    }

    return (
        <details className="advanced-options">
            <summary>
                Advanced options
                <a
                    href="https://github.com/david-allison/manx-corpus-search/blob/master/CorpusSearch/Docs/searching.md#searching"
                    target="_blank"
                    rel="noreferrer"
                >
                    Search help&nbsp;<span className={"noUnderline"}>ⓘ</span>
                </a>
            </summary>

            <div className="advanced-options-content">
                <span className="advanced-options-dates">
                    <span className="advanced-options-dates-label">Dates</span>
                    <input
                        type="number"
                        className="corpus-num-input"
                        min={1500}
                        max={HomeData.currentYear}
                        value={startText}
                        onChange={(e) => {
                            setStartText(e.target.value)
                            commitRange(e.target.value, endText)
                        }}
                    />
                    <span className="advanced-options-dates-dash">–</span>
                    <input
                        type="number"
                        className="corpus-num-input"
                        min={1500}
                        max={HomeData.currentYear}
                        value={endText}
                        onChange={(e) => {
                            setEndText(e.target.value)
                            commitRange(startText, e.target.value)
                        }}
                    />
                </span>
                <MatchWithinWords onMatchChange={props.onMatchChange} />
                <IgnoreHyphens
                    ignoreHyphens={props.ignoreHyphens}
                    onIgnoreHyphensChange={props.onIgnoreHyphensChange}
                />
                <CaseSensitive
                    caseSensitive={props.caseSensitive}
                    onCaseSensitiveChange={props.onCaseSensitiveChange}
                />
                {props.children}
            </div>
        </details>
    )
}

// controlled from Home: the 'no results' suggestion can also enable the option (#158)
const IgnoreHyphens = (props: {
    ignoreHyphens: boolean
    onIgnoreHyphensChange: (ignoreHyphens: boolean) => void
}) => {
    return (
        <label
            className="advanced-options-match"
            title="“lhiam-lhiat” also matches “lhiam lhiat” and “lhiamlhiat” (and vice-versa)"
        >
            <input
                id="ignoreHyphens"
                type="checkbox"
                checked={props.ignoreHyphens}
                onChange={(e) => props.onIgnoreHyphensChange(e.target.checked)}
            />
            Ignore hyphens
        </label>
    )
}

// controlled from the page so the option can follow a result onto the document page (#19)
export const CaseSensitive = (props: {
    caseSensitive: boolean
    onCaseSensitiveChange: (caseSensitive: boolean) => void
}) => {
    return (
        <label
            className="advanced-options-match"
            title="“Moir” does not match “moir”"
        >
            <input
                id="caseSensitive"
                type="checkbox"
                checked={props.caseSensitive}
                onChange={(e) => props.onCaseSensitiveChange(e.target.checked)}
            />
            Match case
        </label>
    )
}

const MatchWithinWords = (props: {
    onMatchChange: (match: boolean) => void
}) => {
    const [matchPhrase, setMatchPhrase] = useState(false)

    const onMatchPhraseChanged = (event: ChangeEvent<HTMLInputElement>) => {
        setMatchPhrase(event.target.checked)
        props.onMatchChange(event.target.checked)
    }

    return (
        <label className="advanced-options-match">
            <input
                id="matchPhrase"
                type="checkbox"
                checked={matchPhrase}
                onChange={onMatchPhraseChanged}
            />
            Match within words
        </label>
    )
}

export default AdvancedOptions
