import Typography from "@mui/material/Typography"
import Slider from "@mui/material/Slider"
import React, {useState} from "react"
import {Home} from "./Home"

export type DateRange = {
    start:number,
    end: number
}
const AdvancedOptions = (props: { onDateRangeChange: (range: DateRange) => void }) => {
    const [dateRange, setDateRange] = useState([1500, Home.currentYear])
    
    return <details className="advanced-options">
        <summary>Advanced Options</summary>


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
    </details>
}

export default AdvancedOptions