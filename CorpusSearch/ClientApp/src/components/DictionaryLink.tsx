import React from "react"

export const DictionaryLink = (props: { query: string, dictionaries: Record<string, string[]>}) => {

        return <>
            {Object.keys(props.dictionaries).map(dictionaryName => <>
                    <a href={`/Dictionary/${dictionaryName}/${props.query}`} target="_blank" rel="noreferrer" style={{ "fontWeight": "bold" }}>{dictionaryName}</a>
                    : {props.dictionaries[dictionaryName]}<br />
                </>)}
        </>
    
}
