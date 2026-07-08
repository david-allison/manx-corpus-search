// Keep in sync with CorpusSearchQuery.MAX_LENGTH
export const MAX_QUERY_LENGTH = 100

export type DictionaryDefinition = {
    entries: string[]
    allowLookup: boolean
}

export type DefinedInDictionaries = Record<string, DictionaryDefinition> // Dictionary<string, string[]>
export type Translations = Record<string, string[]> // Dictionary<string, IList<string>>

/**
 * A range of a returned (raw) text line which matched the query: character offsets, end
 * exclusive. Computed server-side, as matching is done on normalized text (case, diacritics,
 * punctuation): the query may not occur verbatim in the text.
 */
export type HighlightRange = {
    start: number
    end: number
}

export type SearchResponse = {
    results: SearchResultEntry[]
    query: string
    numberOfResults: number
    numberOfDocuments: number
    timeTaken: string
    definedInDictionaries: DefinedInDictionaries
    translations: Translations
}

type date = string

export type SearchResultEntry = {
    startDate: date
    documentName: string
    count: number
    endDate: date
    ident: string
    sample: string
    /** Ranges of `sample` which matched. Absent when unavailable (e.g. English searches) */
    sampleHighlights?: HighlightRange[]
}

export type SearchParams = {
    query: string
    minDate: number
    maxDate: number
    manx: boolean
    english: boolean
    /** Hyphens, spaces and joined words are interchangeable: "lhiam-lhiat" matches "lhiam lhiat" and "lhiamlhiat" */
    ignoreHyphens: boolean
}

export const search = async (
    params: SearchParams,
    signal?: AbortSignal,
): Promise<SearchResponse> => {
    const query = encodeURIComponent(params.query)
    const response = await fetch(
        `search/search/${query}?minDate=${params.minDate}&maxDate=${params.maxDate}&manx=${params.manx.toString()}&english=${params.english.toString()}&ignoreHyphens=${params.ignoreHyphens.toString()}`,
        { signal },
    )
    if (!response.ok) {
        throw new Error(`search failed: ${response.status}`)
    }
    // TODO: Validation
    const ret = (await response.json()) as SearchResponse

    // Handle C# casting an empty list to null
    if (ret.results === null) {
        ret.results = []
    }

    return ret
}
