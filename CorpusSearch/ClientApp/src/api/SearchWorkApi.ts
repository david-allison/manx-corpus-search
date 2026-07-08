import { HighlightRange, Translations } from "./SearchApi"

export type SourceLink = {
    url: string
    text: string
}

export type SearchWorkResult = {
    english: string
    manx: string
    page: string // number?
    csvLineNumber: number
    date: string // TODO: Why on the detail result?
    notes: string
    manxOriginal?: string
    englishOriginal?: string
    subStart?: number
    subEnd?: number
    speaker?: string
    /** Ranges of `manx` which matched. Absent unless Manx was searched */
    manxHighlights?: HighlightRange[]
    /** Ranges of `english` which matched. Absent unless English was searched */
    englishHighlights?: HighlightRange[]
}

export type SearchWorkResponse = {
    results: SearchWorkResult[]
    title: string
    translations: Translations
    totalMatches: number | null
    timeTaken: string
    numberOfResults: number
    notes: string
    source: string
    sourceLinks: SourceLink[] | null
    pdfLink: string | undefined
    googleBooksId: string | undefined
    gitHubLink: string
    original?: string
}

type WorkSearch = {
    docIdent: string
    value: string
    searchEnglish: boolean
    searchManx: boolean
}

/**
 * @throws Error if the search fails (e.g. query is too long)
 */
export const searchWork = async (
    params: WorkSearch,
    signal?: AbortSignal,
): Promise<SearchWorkResponse> => {
    const searchValue = !params.value ? "*" : params.value
    const response = await fetch(
        `search/searchWork/${params.docIdent}/${encodeURIComponent(searchValue)}?english=${params.searchEnglish.toString()}&manx=${params.searchManx.toString()}`,
        { signal },
    )
    if (!response.ok) {
        throw new Error(`work search failed: ${response.status}`)
    }
    return (await response.json()) as SearchWorkResponse
}
