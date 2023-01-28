export type DictionaryDefinition = {
    entries: string[],
    allowLookup: boolean
}

export type DefinedInDictionaries = Record<string, DictionaryDefinition> // Dictionary<string, string[]>
export type Translations = Record<string, string[]> // Dictionary<string, IList<string>>

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
}


type SearchParams = {
    query: string
    minDate: number
    maxDate: number
    manx: boolean
    english: boolean
}

export const search = async (params: SearchParams): Promise<SearchResponse> => {
    const query = encodeURIComponent(params.query)
    const response = await fetch(`search/search/${query}?minDate=${params.minDate}&maxDate=${params.maxDate}&manx=${params.manx.toString()}&english=${params.english.toString()}`)
    // TODO: Validation
    const ret = await response.json() as SearchResponse

    // Handle C# casting an empty list to null
    if (ret.results === null) {
        ret.results = []
    }
    
    return ret
}