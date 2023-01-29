import React, {useState} from "react"
import {SearchLanguage} from "./Home"
import "./ManxEnglishSelector.css"

export const ManxEnglishSelector = (props: { onLanguageChange: (lang: SearchLanguage) => void, initialLanguage?: SearchLanguage }) => {
    const [language, setLanguage] = useState<SearchLanguage>(props.initialLanguage ?? "Manx")

    const toggleLanguage = () => {
        const newLanguage: SearchLanguage = language == "Manx" ? "English" : "Manx"
        setLanguage(newLanguage)
        props.onLanguageChange(newLanguage)
    }
    
    return <div className={"languageSelectorButtonContainer"}>
        <button className={"languageSelectorButton"} onClick={() => toggleLanguage()}>
            {language == "Manx" ? "🇮🇲 Gaelg" : "🇬🇧 English"}
        </button>
    </div>
}