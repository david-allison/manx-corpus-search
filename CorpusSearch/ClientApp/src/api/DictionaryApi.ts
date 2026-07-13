export type Summary = {
    summary: string
    primaryWord: string
    /** the dictionary defining the entry, e.g. 'Cregeen' (#51) */
    dictionaryName: string
    /** root-lemma hops from the selection: 0 = the selection's own entries,
     * 1 = its root ('gheiney' -> 'deiney'), 2 = the root's root ('dooinney');
     * each level nests under the previous one */
    rootDepth: number
    /** set on "did you mean" fallback entries only: the near spelling the
     * entry was reached through, when nothing matched the selection itself */
    nearMatchOf?: string | null
    /** pronunciation recording, streamed from the defining source's site */
    audioUrl?: string | null
    /** the defining source's home page: the group heading links the citation */
    sourceUrl?: string | null
    /** compact display credit for the corner control ("Spoken Dictionary") */
    sourceCredit?: string | null
    /** the entry's full headword list where it goes beyond primaryWord
     * (Kelly's 'BILL, BILLEY'): a homograph headed by another spelling is
     * the selection's own entry, not a root */
    words?: string[] | null
    /** plural forms the dictionary declares ('BILJIN' under BILLEY),
     * shown as structured metadata after the definition */
    plurals?: string[] | null
}
export type DictionaryResponse = Summary[]

/**
 * The server matches phrases of up to a few words around the selection, so a
 * short window of context is enough: keep the URL bounded for long lines.
 */
export const trimContext = (
    context: string,
    selection: string,
    radius: number = 150,
): string => {
    if (context.length <= 2 * radius + selection.length) {
        return context
    }
    const index = context.toLowerCase().indexOf(selection.toLowerCase())
    if (index === -1) {
        // selection not found verbatim (e.g. it spans markup): send the head of the line
        return context.slice(0, 2 * radius)
    }
    const start = Math.max(0, index - radius)
    return context.slice(start, index + selection.length + radius)
}

export const manxDictionaryLookup = async (
    queryUnsafe: string,
    context?: string,
    signal?: AbortSignal,
): Promise<DictionaryResponse> => {
    const params = new URLSearchParams({ lang: "gv", word: queryUnsafe })
    if (context) {
        params.set("context", trimContext(context, queryUnsafe))
    }
    const response = await fetch(`api/Dictionary/?${params.toString()}`, {
        signal,
    })
    // TODO: Validation
    return (await response.json()) as DictionaryResponse
}

/** The teanglann-style full page for a word (experimental): per-dictionary
 * groups, the word's own recording, near-match suggestions as a tier */
export type DictionaryPageResponse = {
    word: string
    /** nothing matched the word itself: every group is a near spelling */
    isSuggestionTier: boolean
    audio?: {
        url: string
        credit?: string | null
        sourceUrl?: string | null
    } | null
    groups: {
        dictionary: string
        sourceUrl?: string | null
        entries: Summary[]
    }[]
}

export const dictionaryPage = async (
    word: string,
    signal?: AbortSignal,
): Promise<DictionaryPageResponse> => {
    const params = new URLSearchParams({ lang: "gv", word })
    const response = await fetch(`/api/Dictionary/page?${params.toString()}`, {
        signal,
    })
    if (!response.ok) {
        throw new Error(`dictionary page failed: ${response.status}`)
    }
    return (await response.json()) as DictionaryPageResponse
}

/** One token of a line in the dictionary-coverage debug view: how a tap on it
 * would resolve. "entry" = a dictionary lists the token; "root" = reached
 * through the lemma table's root chain; "lemma" = the table knows it but no
 * dictionary documents it; "none" = unknown everywhere. */
export type TokenCoverage = {
    start: number
    length: number
    status: "entry" | "root" | "lemma" | "none"
}

/** Per-token dictionary coverage for each line (the dictionary debug mode).
 * Requests are chunked: the endpoint bounds each call. */
export const dictionaryCoverage = async (
    lang: string,
    lines: string[],
    signal?: AbortSignal,
): Promise<TokenCoverage[][]> => {
    const result: TokenCoverage[][] = []
    const chunkSize = 300
    for (let i = 0; i < lines.length; i += chunkSize) {
        const response = await fetch("api/Dictionary/coverage", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                lang,
                lines: lines.slice(i, i + chunkSize),
            }),
            signal,
        })
        const data = (await response.json()) as { lines: TokenCoverage[][] }
        result.push(...data.lines)
    }
    return result
}
