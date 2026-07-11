import { Fragment } from "react"
import { Translations } from "../api/SearchApi"

export function hasTranslations(translations?: Translations) {
    if (!translations) {
        return false
    }
    return (
        Object.keys(translations).filter(
            (code) => translations[code].length > 0,
        ).length > 0
    )
}

export const TranslationList = (props: { translations: Translations }) => {
    const { translations } = props
    return (
        <>
            {Object.keys(translations).map((langCode) => {
                const langCodeTranslations = translations[langCode]
                if (langCodeTranslations.length == 0) {
                    return <Fragment key={langCode}></Fragment>
                }
                return (
                    <div className="dict-strip-row" key={langCode}>
                        <div className="dict-strip-entry">
                            <span className="dict-strip-label">
                                {langCode}:
                            </span>{" "}
                            <span className="dict-strip-text">
                                {langCodeTranslations.join(", ")}
                            </span>
                        </div>
                    </div>
                )
            })}
        </>
    )
}
