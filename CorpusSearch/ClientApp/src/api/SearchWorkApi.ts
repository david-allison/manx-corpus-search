import {Translations} from "./SearchApi"


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
}

export type SearchWorkResponse = {
    results: SearchWorkResult[]
    title: string
    translations : Translations
    totalMatches: number
    timeTaken: string
    numberOfResults: number
    notes: string
    source: string
    sourceLinks: SourceLink[] | null
    pdfLink: string
    gitHubLink: string
}

type WorkSearch = {
    docIdent: string,
    value: string
    searchEnglish: boolean
    searchManx: boolean
}

export const searchWork = async (params: WorkSearch): Promise<SearchWorkResponse> => {
    const response = await fetch(`search/searchWork/${params.docIdent}/${encodeURIComponent(params.value)}?english=${params.searchEnglish.toString()}&manx=${params.searchManx.toString()}`)
    return await response.json() as SearchWorkResponse
}