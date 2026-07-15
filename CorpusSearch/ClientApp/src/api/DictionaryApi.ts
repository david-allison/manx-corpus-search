export type Summary = {
    summary: string
    primaryWord: string
    /** the dictionary defining the entry, e.g. 'Cregeen' (#51) */
    dictionaryName: string
    /** root-lemma hops from the selection: 0 = the selection's own entries,
     * 1 = its root ('gheiney' -> 'deiney'), 2 = the root's root ('dooinney');
     * each level nests under the previous one */
    rootDepth: number
    /** set on a root the lemma table only reaches by rule — a generated
     * mutation, an unvalidated demutation — with no dictionary page attesting
     * the link. Marked in the popup: a derivation is not documentation */
    unverifiedLink?: boolean | null
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
    /** the printed grammar label ("s. m.", "s. f."): word class and gender
     * as the dictionary abbreviates them, expanded on hover */
    grammarLabel?: string | null
    /** the entry's word classes ("Noun", "Verb", …) where the dictionary
     * declares them. Absent for Phil Kelly, which merges senses, and for the
     * entries whose printed definition names no class */
    partsOfSpeech?: string[] | null
    /** the classical spelling a Phillips 1610 form stands for ("dooinney"
     * when tapping dwyne): shown as a bridge line so the entries never
     * imply a dictionary lists the 1610 spelling */
    phillipsSpellingOf?: string | null
    /** scripture citations quoted in `summary` ("Jud. xii. 6") with their
     * canonical verse keys ("judges.12.6"): each occurrence renders as a
     * link to the verse in the corpus */
    citations?: { text: string; key: string }[] | null
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
        /** the dictionary's URL slug ('cregeen'): what /dictionary/in/ scopes on */
        slug?: string | null
        sourceUrl?: string | null
        entries: Summary[]
    }[]
}

/** A dictionary the page can be scoped to */
export type DictionaryInfo = {
    slug: string
    name: string
}

/** @param dict optional dictionary slug: scopes the page to one dictionary */
export const dictionaryPage = async (
    word: string,
    dict?: string,
    signal?: AbortSignal,
): Promise<DictionaryPageResponse> => {
    const params = new URLSearchParams({ lang: "gv", word })
    if (dict) {
        params.set("dict", dict)
    }
    const response = await fetch(`/api/Dictionary/page?${params.toString()}`, {
        signal,
    })
    if (!response.ok) {
        throw new Error(`dictionary page failed: ${response.status}`)
    }
    return (await response.json()) as DictionaryPageResponse
}

/** The dictionaries answering Manx, for the page's scope picker */
export const dictionaryList = async (
    signal?: AbortSignal,
): Promise<DictionaryInfo[]> => {
    const response = await fetch("/api/Dictionary/dictionaries?lang=gv", {
        signal,
    })
    if (!response.ok) {
        throw new Error(`dictionary list failed: ${response.status}`)
    }
    return (await response.json()) as DictionaryInfo[]
}

/** The lexeme's corpus history (experimental): earliest attestation, the
 * spelling cluster, use by decade, the traditional/revived split, cognates */
export type DictionaryHistoryResponse = {
    word: string
    lemmas: string[]
    revivalBoundaryYear: number
    forms: HistoryForm[]
    truncatedForms: number
    earliest?: HistoryForm | null
    decades: { decade: number; count: number }[]
    traditionalCount: number
    revivedCount: number
    undatedCount: number
    dictionaries: { name: string; era?: string | null }[]
    cognates: string[]
}

export type HistoryForm = {
    form: string
    total: number
    documents: number
    /** the spelling also belongs to another lexeme (mutation ambiguity) */
    sharedWithOtherLemmas: boolean
    earliestYear?: number | null
    earliestIdent?: string | null
    earliestTitle?: string | null
    sample?: string | null
    /** where the form sits in `sample`: lets the page quote a few words around
     * the word rather than the head of a long verse */
    sampleHighlights?: { start: number; end: number }[] | null
}

export const dictionaryHistory = async (
    word: string,
    signal?: AbortSignal,
): Promise<DictionaryHistoryResponse> => {
    const params = new URLSearchParams({ lang: "gv", word })
    const response = await fetch(
        `/api/Dictionary/history?${params.toString()}`,
        { signal },
    )
    if (!response.ok) {
        throw new Error(`dictionary history failed: ${response.status}`)
    }
    return (await response.json()) as DictionaryHistoryResponse
}

/** The corpus documents attesting a word's lexeme, oldest first (experimental) */
export type DictionaryAttestationsResponse = {
    word: string
    /** the display lemmas being walked; empty when the table doesn't know the word */
    lemmas: string[]
    documents: AttestationDocument[]
    /** attesting documents with no date: they cannot be placed in the walk */
    undatedDocuments: number
    undatedUses: number
}

/** A step in the walk */
export type AttestationDocument = {
    ident: string
    title: string
    year: number
    /** uses of the lexeme, where the scan can be trusted to count them (see
     * AttestationDocument on the server). Absent for an ambiguous word, whose
     * uses are counted a document at a time as `useCount` */
    uses?: number | null
}

/** The lexeme's uses inside one document, split by the reading each line
 * resolved to */
export type AttestationLinesResponse = {
    ident: string
    title: string
    year?: number | null
    /** uses of the lexeme: surface words, not lines, counted once each however
     * many readings claim them */
    useCount: number
    groups: AttestationLemmaGroup[]
}

export type AttestationLemmaGroup = {
    /** "beg.a": distinguishes homographs the display lemma cannot */
    lemmaId: string
    /** the headword a reader would look up ("beg") */
    lemma: string
    /** uses this reading claims across the document, not only in `lines` */
    count: number
    lines: {
        manx?: string | null
        english?: string | null
        manxHighlights?: { start: number; end: number }[] | null
        csvLineNumber: number
    }[]
}

export const dictionaryAttestations = async (
    word: string,
    signal?: AbortSignal,
): Promise<DictionaryAttestationsResponse> => {
    const params = new URLSearchParams({ word })
    const response = await fetch(
        `/api/Dictionary/attestations?${params.toString()}`,
        { signal },
    )
    if (!response.ok) {
        throw new Error(`attestations failed: ${response.status}`)
    }
    return (await response.json()) as DictionaryAttestationsResponse
}

export const dictionaryAttestationLines = async (
    word: string,
    ident: string,
    signal?: AbortSignal,
): Promise<AttestationLinesResponse> => {
    const params = new URLSearchParams({ word })
    const response = await fetch(
        `/api/Dictionary/attestations/${encodeURIComponent(ident)}?${params.toString()}`,
        { signal },
    )
    if (!response.ok) {
        throw new Error(`attestation lines failed: ${response.status}`)
    }
    return (await response.json()) as AttestationLinesResponse
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

/** One page of a dictionary's index: the letters, one letter's prefix bar, and
 * the headwords under a prefix */
export type DictionaryBrowseResponse = {
    dictionary: string
    slug: string
    letters: string[]
    /** null when the dictionary is empty (its JSON is downloaded on deployment) */
    letter?: string | null
    prefixes: string[]
    prefix?: string | null
    headwords: { word: string; gloss?: string | null }[]
}

/** @param at a letter ("a") or a prefix ("aal"); the dictionary's first letter
 * when it names neither */
export const dictionaryBrowse = async (
    dict: string,
    at?: string,
    signal?: AbortSignal,
): Promise<DictionaryBrowseResponse> => {
    const params = new URLSearchParams({ dict })
    if (at) {
        params.set("at", at)
    }
    const response = await fetch(
        `/api/Dictionary/browse?${params.toString()}`,
        {
            signal,
        },
    )
    if (!response.ok) {
        throw new Error(`dictionary browse failed: ${response.status}`)
    }
    return (await response.json()) as DictionaryBrowseResponse
}

/** The headwords either side of a word, for stepping through a dictionary */
export type DictionaryNeighboursResponse = {
    word: string
    previous?: string | null
    next?: string | null
}

/** @param dict optional slug: one book's own order. Without it, the union
 * across every dictionary in collation order */
export const dictionaryNeighbours = async (
    word: string,
    dict?: string,
    signal?: AbortSignal,
): Promise<DictionaryNeighboursResponse> => {
    const params = new URLSearchParams({ word })
    if (dict) {
        params.set("dict", dict)
    }
    const response = await fetch(
        `/api/Dictionary/neighbours?${params.toString()}`,
        { signal },
    )
    if (!response.ok) {
        throw new Error(`dictionary neighbours failed: ${response.status}`)
    }
    return (await response.json()) as DictionaryNeighboursResponse
}
