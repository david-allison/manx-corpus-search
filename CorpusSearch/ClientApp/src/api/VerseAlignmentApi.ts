export type VerseAlignmentDocument = {
    ident: string
    name: string
    /** the translation's year, when the document is dated */
    year?: number
    csvLineNumber: number
    /** the raw reference of the aligned line ("Psalmyn:23:1", "PSALM 23") */
    reference?: string
    /** the aligned line's own key: the chapter key when the version has no
     * verse rows (the Metrical Psalms) */
    canonicalReference?: string
    manx?: string
    english?: string
}

export type VerseAlignmentResponse = {
    /** the canonical "book.chapter[.verse]" key that was aligned */
    key: string
    /** "Psalms 23:1" */
    display: string
    /** every document with the verse (or its chapter), oldest translation first */
    documents: VerseAlignmentDocument[]
}

/**
 * The verse with the given canonical key in every document that has it.
 *
 * @throws Error if the fetch fails (e.g. the key isn't canonical)
 */
export const fetchVerseAlignment = async (
    key: string,
    signal?: AbortSignal,
): Promise<VerseAlignmentResponse> => {
    const response = await fetch(
        `api/search/verseAlignment/${encodeURIComponent(key)}`,
        { signal },
    )
    if (!response.ok) {
        throw new Error(`verse alignment failed: ${response.status}`)
    }
    return (await response.json()) as VerseAlignmentResponse
}
