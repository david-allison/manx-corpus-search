import { SearchLanguage } from "../routes/Home"

/**
 * Extracts a single word suitable for a Multidict lookup, or null.
 *
 * Multidict looks up single words, so phrases and wildcard queries are
 * rejected. "Match phrase" searches wrap the query in asterisks; these are
 * stripped.
 *
 * TODO: get this metadata (whether a raw word was provided) from the server.
 */
export const getMultidictLookupWord = (query: string): string | null => {
    const word = query.replace(/^\*+|\*+$/g, "").trim()
    if (word == "" || /[\s*?"()[\]]/.test(word)) {
        return null
    }
    return word
}

export const multidictUrl = (word: string, language: SearchLanguage) => {
    const sourceLanguage = language === "Manx" ? "gv" : "en"
    const targetLanguage = language === "Manx" ? "en" : "gv"
    return `https://multidict.net/multidict/?word=${encodeURIComponent(word)}&sl=${sourceLanguage}&tl=${targetLanguage}`
}

/** A link to https://multidict.net/, searching many dictionaries at once */
export const MultidictLink = (props: {
    word: string
    language: SearchLanguage
}) => (
    <a
        href={multidictUrl(props.word, props.language)}
        target="_blank"
        rel="noreferrer"
    >
        Multidict
    </a>
)

/** Shown in the dictionary strip when none of our dictionaries know the word */
export const MultidictNotFoundRow = (props: {
    word: string
    language: SearchLanguage
}) => (
    <div className="dict-strip-row">
        <div className="dict-strip-entry">
            <span className="dict-strip-label">Dictionaries:</span>{" "}
            <span className="dict-strip-text">
                No results found. Try searching{" "}
                <MultidictLink word={props.word} language={props.language} />
            </span>
        </div>
    </div>
)
