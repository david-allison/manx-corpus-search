import React, {Fragment} from "react"
import {Translations} from "../api/SearchApi"

export function hasTranslations(translations?: Translations) {
    if (!translations) { return false }
    return Object.keys(translations).filter(code => translations[code].length > 0).length > 0
}

export const TranslationList = (props: { translations: Translations }) => {
    const { translations } = props
    return <>
        {Object.keys(translations).map(langCode => {
            const langCodeTranslations = translations[langCode]
            if (langCodeTranslations.length == 0) { return <></> }
            return <Fragment key={langCode}>
                <strong>{langCode}:</strong> {langCodeTranslations.join(", ")}
            </Fragment>}
        )}
        {/*return a newline if we had results*/}
        <br/>
    </>
}