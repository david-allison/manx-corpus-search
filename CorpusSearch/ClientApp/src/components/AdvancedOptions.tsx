import Typography from "@mui/material/Typography"
import Slider from "@mui/material/Slider"
import React, {ChangeEvent, useState} from "react"
import {Home} from "./Home"

export type DateRange = {
    start:number,
    end: number
}
const AdvancedOptions = (props: { onDateRangeChange: (range: DateRange) => void, onMatchChange: (match: boolean) => void }) => {
    const [dateRange, setDateRange] = useState([1500, Home.currentYear])
    
    return <details className="advanced-options">
        <summary>Advanced Options
            <a style={{"float":"right"}} href="https://github.com/david-allison/manx-corpus-search/blob/master/CorpusSearch/Docs/searching.md#searching" target="_blank" rel="noreferrer">Search Help ℹ</a>
        </summary>

        <Typography id="range-output" gutterBottom>
            Dates: {dateRange[0]}&ndash;{dateRange[1]}
        </Typography>

        <Slider
            value={dateRange}
            min={ 1500 }
            max={ Home.currentYear }
            valueLabelDisplay="auto"
            onChange={(_, value) => setDateRange(value as number[])}
            onChangeCommitted={(_, value) => {
                const v = value as number[]
                setDateRange(v)
                props.onDateRangeChange({ start: v[0], end: v[1]})
            }}
            aria-labelledby="range-slider"
        />
        <SearchLanguageBox onMatchChange={props.onMatchChange} />

    </details>
}

const SearchLanguageBox = (props: {onMatchChange: (match: boolean) => void}) => {
    const [matchPhrase, setMatchPhrase] = useState(false)

    const onMatchPhraseChanged = (event: ChangeEvent<HTMLInputElement>) => {
        setMatchPhrase(event.target.checked)
        props.onMatchChange(event.target.checked)
    }
    
    return <div className="search-language">
        <label style={{ paddingLeft: 10 }} htmlFor="matchPhrase">Match Phrase</label> <input id="matchPhrase" type="checkbox" checked={matchPhrase} onChange={onMatchPhraseChanged} /><br />
    </div>
}

export default AdvancedOptions