import React from "react"
import {Translations} from "./Home"


export const TranslationList = (props: { translations: Translations }) => {
    return <>
        {Object.keys(props.translations).map(langCode => <><strong>{langCode}:</strong> {props.translations[langCode].map(x => <>{x}, </>)}
        </>)}
    </>
}