type Summary = { summary: string; primaryWord: string }
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
