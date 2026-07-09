import { HighlightRange, Translations } from "./SearchApi"
import { SearchOptions, searchOptionsQuery } from "./SearchOptions"

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
    /** The document's first CSV line number: lets us offer 'expand context' above the first
     * result (#286). Absent when searching for '*' or when there are no results */
    firstLineNumber?: number
    /** The document's last CSV line number (see `firstLineNumber`) */
    lastLineNumber?: number
}

export type WorkLinesResponse = {
    /** The requested lines, in document order */
    lines: SearchWorkResult[]
    /** Lines in [start, end] before the limit was applied: if this is no more than the
     * limit, the range is exhausted */
    totalInRange: number
}

type WorkSearch = {
    docIdent: string
    value: string
    searchEnglish: boolean
    searchManx: boolean
} & SearchOptions

/**
 * @throws Error if the search fails (e.g. query is too long)
 */
export const searchWork = async (
    params: WorkSearch,
    signal?: AbortSignal,
): Promise<SearchWorkResponse> => {
    const searchValue = !params.value ? "*" : params.value
    const response = await fetch(
        `api/search/searchWork/${params.docIdent}/${encodeURIComponent(searchValue)}?english=${params.searchEnglish.toString()}&manx=${params.searchManx.toString()}${searchOptionsQuery(params)}`,
        { signal },
    )
    if (!response.ok) {
        throw new Error(`work search failed: ${response.status}`)
    }
    return (await response.json()) as SearchWorkResponse
}

/**
 * Lines of the document with a csvLineNumber in [start, end]: the first `limit` of them, or
 * the last if `fromEnd`. Expands the context around a search result (#286).
 *
 * @throws Error if the fetch fails
 */
export const fetchLines = async (params: {
    docIdent: string
    start: number
    end: number
    limit: number
    fromEnd: boolean
}): Promise<WorkLinesResponse> => {
    const response = await fetch(
        `api/search/lines/${params.docIdent}?start=${params.start.toString()}&end=${params.end.toString()}&limit=${params.limit.toString()}&fromEnd=${params.fromEnd.toString()}`,
    )
    if (!response.ok) {
        throw new Error(`line fetch failed: ${response.status}`)
    }
    return (await response.json()) as WorkLinesResponse
}
