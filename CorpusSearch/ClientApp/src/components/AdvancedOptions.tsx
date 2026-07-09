import { ChangeEvent, ReactNode, useState } from "react"
import { HomeData } from "../routes/Home"
import { SearchOptions, searchOptionKeys } from "../api/SearchOptions"
import "./AdvancedOptions.css"

export type DateRange = {
    start: number
    end: number
}

/** Checkbox label + tooltip per option: the Record forces an entry for every option */
const searchOptionConfig: Record<
    keyof SearchOptions,
    { label: string; title: string }
> = {
    ignoreHyphens: {
        label: "Ignore hyphens",
        title: "“lhiam-lhiat” also matches “lhiam lhiat” and “lhiamlhiat” (and vice-versa)",
    },
    caseSensitive: {
        label: "Match case",
        title: "“Moir” does not match “moir”",
    },
    accentSensitive: {
        label: "Match accents",
        title: "“chengey” does not match “çhengey”",
    },
}

const AdvancedOptions = (props: {
    onDateRangeChange: (range: DateRange) => void
    onMatchChange: (match: boolean) => void
    options: SearchOptions
    onOptionsChange: (options: SearchOptions) => void
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
                {searchOptionKeys.map((key) => (
                    <OptionCheckbox
                        key={key}
                        option={key}
                        options={props.options}
                        onOptionsChange={props.onOptionsChange}
                    />
                ))}
                {props.children}
            </div>
        </details>
    )
}

// controlled from the page so an option can follow a result onto the document page (#19, #20)
export const OptionCheckbox = (props: {
    option: keyof SearchOptions
    options: SearchOptions
    onOptionsChange: (options: SearchOptions) => void
}) => {
    const { label, title } = searchOptionConfig[props.option]
    return (
        <label className="advanced-options-match" title={title}>
            <input
                id={props.option}
                type="checkbox"
                checked={props.options[props.option]}
                onChange={(e) =>
                    props.onOptionsChange({
                        ...props.options,
                        [props.option]: e.target.checked,
                    })
                }
            />
            {label}
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
