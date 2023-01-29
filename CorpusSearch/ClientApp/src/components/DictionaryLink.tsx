import React from "react"
import {DefinedInDictionaries, DictionaryDefinition} from "../api/SearchApi"

export const DictionaryLink = (props: { query: string, dictionaries: DefinedInDictionaries}) => {
        return <>
            {Object.keys(props.dictionaries).map(dictionaryName => {
                const element = props.dictionaries[dictionaryName]
                return (
                    <>
                        <DictionaryNameHeader dictionary={element} dictionaryName={dictionaryName} query={props.query} />
                        : {/*: A literal colon*/}
                        {
                            element.entries.map((e, i) => 
                            <>
                                <b>{`${i+1})`}</b>
                                {` ${e}`}
                                {i == element.entries.length -1 ? "" : "; "}
                            </>)
                        }
                        <br />
                    </>
                )})
            }
        </>
    
}

const DictionaryNameHeader = (props: { dictionary: DictionaryDefinition, dictionaryName: string, query: string}) => {
        const {dictionaryName, query, dictionary } = props
    
    if (!dictionary.allowLookup) {
        return <span style={{ "fontWeight": "bold" }}>{dictionaryName}</span>
    }
    
    return <a href={`/Dictionary/${dictionaryName}/${query}`} target="_blank" rel="noreferrer" style={{ "fontWeight": "bold" }}>
                {dictionaryName}
            </a>
            
}
