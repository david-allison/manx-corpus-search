/**
 * The boolean search options shared by every search endpoint and both search pages.
 *
 * To add an option (#316): add a field + default here, then follow the compile errors
 * (a `searchOptionConfig` entry in AdvancedOptions.tsx). The query params, page state
 * and result-link round-tripping all pick it up automatically.
 */
export type SearchOptions = {
    /** Hyphens, spaces and joined words are interchangeable: "lhiam-lhiat" matches "lhiam lhiat" and "lhiamlhiat" */
    ignoreHyphens: boolean
    /** Case must match: "Moir" does not match "moir" */
    caseSensitive: boolean
    /** Accents must match: "chengey" does not match "çhengey" */
    accentSensitive: boolean
}

/** Declaration order is canonical: it is the query-param order and the checkbox order */
export const defaultSearchOptions: SearchOptions = {
    ignoreHyphens: false,
    caseSensitive: false,
    accentSensitive: false,
}

export const searchOptionKeys = Object.keys(
    defaultSearchOptions,
) as (keyof SearchOptions)[]

/**
 * API form: every option explicitly `=true|false`, as an `&`-prefixed suffix for a
 * hand-built URL. Accepts any superset of SearchOptions (e.g. a whole SearchParams).
 */
export const searchOptionsQuery = (options: SearchOptions): string =>
    searchOptionKeys.map((key) => `&${key}=${options[key].toString()}`).join("")

/**
 * Internal-link form (search result → document page): only enabled options, so links
 * stay short; an absent param is false. Parsed back by `parseSearchOptions`.
 */
export const searchOptionsLinkQuery = (options: SearchOptions): string =>
    searchOptionKeys
        .filter((key) => options[key])
        .map((key) => `&${key}=true`)
        .join("")

/** Reads options from a query string (`location.search`); an absent param is false */
export const parseSearchOptions = (search: string): SearchOptions => {
    const params = new URLSearchParams(search)
    const options = { ...defaultSearchOptions }
    for (const key of searchOptionKeys) {
        options[key] = params.get(key) === "true"
    }
    return options
}
