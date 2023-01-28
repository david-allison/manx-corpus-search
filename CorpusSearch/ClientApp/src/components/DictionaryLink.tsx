import React from "react"
import {DefinedInDictionaries} from "../api/SearchApi"

export const DictionaryLink = (props: { query: string, dictionaries: DefinedInDictionaries}) => {

        return <>
            {Object.keys(props.dictionaries).map(dictionaryName => <>
                    {props.dictionaries[dictionaryName].allowLookup ? 
                        <a href={`/Dictionary/${dictionaryName}/${props.query}`} target="_blank" rel="noreferrer" style={{ "fontWeight": "bold" }}>{dictionaryName}</a> :
                        <span style={{ "fontWeight": "bold" }}>{dictionaryName}</span> }
                    : {props.dictionaries[dictionaryName].entries.map((e, i) => `${i+1}) ${e}`).join("; ")}<br />
                </>)}
        </>
    
}
