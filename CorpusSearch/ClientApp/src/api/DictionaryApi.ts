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
    /** whether the corpus says the word: false where only a dictionary knows it.
     * Null while not yet known — a phrase is answered from a read of the whole
     * corpus, which runs behind the server for a few seconds after it starts */
    attested: boolean | null
    /** the slug of every dictionary with something to say about the word,
     * whatever scope the page was asked for: the picker greys the rest */
    answering: string[]
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
/** Which dictionaries the site has is fixed when it deploys, so the answer is
 * kept once it arrives. App.tsx keys its tree on the path, so every step from
 * one headword to the next is a fresh mount: without this the picker re-asks,
 * and blinks out of the page and back while it waits. */
let known: DictionaryInfo[] | null = null

/** The dictionaries if they have already been fetched, for a first paint that
 * does not have to wait for them */
export const dictionariesAlreadyKnown = (): DictionaryInfo[] | null => known

export const dictionaryList = async (
    signal?: AbortSignal,
): Promise<DictionaryInfo[]> => {
    if (known != null) {
        return known
    }
    const response = await fetch("/api/Dictionary/dictionaries?lang=gv", {
        signal,
    })
    if (!response.ok) {
        throw new Error(`dictionary list failed: ${response.status}`)
    }
    known = (await response.json()) as DictionaryInfo[]
    return known
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
    /** "beg.a": distinguishes homographs the display lemma cannot. More than one
     * where the readings share that lemma and claim the very same words; empty
     * where the row is a spelling the lemma table knows no lexeme for */
    lemmaIds: string[]
    /** the headword a reader would look up ("beg"), or the spelling scanned
     * where `lemmaIds` is empty */
    lemma: string
    /** the word classes of `lemmaIds` ("n", "v"), for naming a reading the
     * headword alone does not; empty where an id names no class */
    classes: string[]
    /** uses this row claims across the document, not only in `lines` */
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

/** One page of a dictionary's index: the letters, and one letter's headwords in
 * the chapters they file under */
export type DictionaryBrowseResponse = {
    dictionary: string
    slug: string
    /** in capitals, as a printed index has them */
    letters: string[]
    /** null when the dictionary is empty (its JSON is downloaded on deployment) */
    letter?: string | null
    chapters: BrowseChapter[]
}

/** One prefix and the headwords filed under it */
export type BrowseChapter = {
    /** the prefix in capitals: "AAL", or "AD" where the word is shorter than the
     * chapter is deep */
    key: string
    /** in the book's order. A key can repeat where the book doubles back, and so
     * can a word — Kelly prints five headwords 'A' */
    words: BrowseWord[]
}

/** A headword in the index, and whether the corpus ever says it */
export type BrowseWord = {
    /** as the dictionary prints it: Kelly capitalises, Cregeen does not */
    word: string
    /** false where no text we hold uses the word */
    attested: boolean
}

/** @param at a letter ("a"), or a prefix ("aal") from a link made when a prefix
 * was a page of its own; the dictionary's first letter when it names neither */
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

/** The lemma index, in the browse page's shape: the same letters and chapters,
 * over every lemma the tables link a form to instead of one book's headwords */
export const lemmaIndex = async (
    at?: string | null,
    signal?: AbortSignal,
): Promise<DictionaryBrowseResponse> => {
    const params = new URLSearchParams()
    if (at) {
        params.set("at", at)
    }
    const response = await fetch(
        `/api/Dictionary/lemmas?${params.toString()}`,
        { signal },
    )
    if (!response.ok) {
        throw new Error(`lemma index failed: ${response.status}`)
    }
    return (await response.json()) as DictionaryBrowseResponse
}

/** One lemma's form tree: the forms the lemma tables link to it, grouped by
 * how each hangs off it */
export type LemmaTreeResponse = {
    /** as the tables spell it ("aa-aase", "Aachummey") */
    lemma: string
    /** whether the corpus says the lemma by its own spelling: the forms below
     * answer for the rest of the paradigm */
    attested: boolean
    /** the lemma's own row is hand-asserted (the vocab supplement): the root
     * itself is a guess, like the popup's unverifiedLink */
    unverified: boolean
    groups: LemmaTreeGroup[]
}

export type LemmaTreeGroup = {
    /** the tables' own name for the link ("inflected", "mutation", "variant",
     * "undecided", ...): the page puts the reader's words on it */
    linkType: string
    forms: LemmaTreeForm[]
}

export type LemmaTreeForm = {
    form: string
    /** whether any text says the form by this spelling — no lemma hop, which
     * would answer for the whole paradigm at once */
    attested: boolean
    /** no row attests the link: made by rule (a generated mutation) or asserted
     * by hand (the vocab supplement), and possibly wrong */
    unverified: boolean
}

export const lemmaTree = async (
    lemma: string,
    signal?: AbortSignal,
): Promise<LemmaTreeResponse> => {
    const params = new URLSearchParams({ lemma })
    const response = await fetch(`/api/Dictionary/lemma?${params.toString()}`, {
        signal,
    })
    if (!response.ok) {
        throw new Error(`lemma tree failed: ${response.status}`)
    }
    return (await response.json()) as LemmaTreeResponse
}

/** The headwords either side of a word, for stepping through a dictionary */
export type DictionaryNeighboursResponse = {
    word: string
    previous?: string | null
    next?: string | null
    /** whether the corpus uses the word itself */
    attested: boolean
    previousAttested: boolean
    nextAttested: boolean
    /** the nearest headword either side the corpus actually uses, which is rarely
     * the one next door: half of Phil Kelly is unattested */
    previousUsed?: string | null
    nextUsed?: string | null
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
