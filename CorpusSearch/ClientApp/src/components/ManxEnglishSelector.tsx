import React, {useMemo, useState} from "react"
import {SearchLanguage} from "./Home"
import "./ManxEnglishSelector.css"
import ifEmoji from "if-emoji"

export const ManxEnglishSelector = (props: { onLanguageChange: (lang: SearchLanguage) => void, initialLanguage?: SearchLanguage }) => {
    const [language, setLanguage] = useState<SearchLanguage>(props.initialLanguage ?? "Manx")

    const toggleLanguage = () => {
        const newLanguage: SearchLanguage = language == "Manx" ? "English" : "Manx"
        setLanguage(newLanguage)
        props.onLanguageChange(newLanguage)
    }
    
    const canUseEmoji = useMemo(() => {
        return ifEmoji("🇮🇲")
    }, [])
    
    const manxText = canUseEmoji ? "🇮🇲 Gaelg": "Manx"
    const englishText = canUseEmoji ? "🇬🇧 English": "English"

    return <div className={"languageSelectorButtonContainer"}>
        <button className={"languageSelectorButton"} onClick={() => toggleLanguage()}>
            {language == "Manx" ? manxText : englishText}
        </button>
    </div>
}