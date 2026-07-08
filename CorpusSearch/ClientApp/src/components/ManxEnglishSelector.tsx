import {useState} from "react"
import {SearchLanguage} from "../routes/Home"

export const ManxEnglishSelector = (props: { onLanguageChange: (lang: SearchLanguage) => void, initialLanguage?: SearchLanguage }) => {
    const [language, setLanguage] = useState<SearchLanguage>(props.initialLanguage ?? "Manx")

    const setLang = (newLanguage: SearchLanguage) => {
        setLanguage(newLanguage)
        props.onLanguageChange(newLanguage)
    }

    return <div className="seg-control seg-search">
        <button
            type="button"
            title="Search in Manx"
            className={language == "Manx" ? "active" : undefined}
            onClick={() => setLang("Manx")}>Gaelg</button>
        <button
            type="button"
            title="Search in English"
            className={language == "English" ? "active" : undefined}
            onClick={() => setLang("English")}>English</button>
    </div>
}
