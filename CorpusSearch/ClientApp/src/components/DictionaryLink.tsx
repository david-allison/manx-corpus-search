import { DefinedInDictionaries, DictionaryDefinition } from "../api/SearchApi"

export function hasDictionaryDefinitions(dictionaries?: DefinedInDictionaries) {
    if (!dictionaries) {
        return false
    }
    return (
        Object.keys(dictionaries).filter(
            (dictionaryName) => dictionaries[dictionaryName].entries.length > 0,
        ).length > 0
    )
}

export const DictionaryLink = (props: {
    query: string
    dictionaries: DefinedInDictionaries
}) => {
    return (
        <>
            {Object.keys(props.dictionaries)
                .filter(
                    (dictionaryName) =>
                        props.dictionaries[dictionaryName].entries.length > 0,
                )
                .map((dictionaryName) => {
                    const element = props.dictionaries[dictionaryName]
                    return (
                        <div className="dict-strip-row" key={dictionaryName}>
                            <DictionaryNameHeader
                                dictionary={element}
                                dictionaryName={dictionaryName}
                                query={props.query}
                            />
                            <span className="dict-strip-text">
                                {element.entries
                                    .map((e, i) => `${i + 1}) ${e}`)
                                    .join(" ")}
                            </span>
                        </div>
                    )
                })}
        </>
    )
}

const DictionaryNameHeader = (props: {
    dictionary: DictionaryDefinition
    dictionaryName: string
    query: string
}) => {
    const { dictionaryName, query, dictionary } = props

    if (!dictionary.allowLookup) {
        return <span className="dict-strip-label">{dictionaryName}:</span>
    }

    return (
        <a
            className="dict-strip-label"
            href={`/Dictionary/${dictionaryName}/${query}`}
            target="_blank"
            rel="noreferrer"
        >
            {dictionaryName}:
        </a>
    )
}
