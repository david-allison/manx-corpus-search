import React, {useState} from "react"
import {Grid, Switch} from "@mui/material"
import {SearchLanguage} from "./Home"


export const ManxEnglishSelector = (props: { onLanguageChange: (lang: SearchLanguage) => void }) => {
    const [language, setLanguage] = useState<SearchLanguage>("Manx")

    const onSetLanguage = (checked: boolean) => {
        const newLanguage: SearchLanguage = checked ? "English" : "Manx"
        setLanguage(newLanguage)
        props.onLanguageChange(newLanguage)
    }

    return <div style={{display: "inline-block", minWidth: 160}}><Grid component="label" container alignItems="center" spacing={1}>
        <Grid item>Manx</Grid>
        <Grid item>
            <Switch
                checked={language == "English"} 
                onChange={(x) => onSetLanguage(x.target.checked)} 
                value="checked"
                sx={{
                    "& .MuiSwitch-switchBase": {
                        color: "#1976d2"
                    },
                    "&	.MuiSwitch-track": {
                        backgroundColor: "#1976d2"
                    }
                }}
            />
        </Grid>
        <Grid item>English</Grid>
    </Grid>
    </div>
}