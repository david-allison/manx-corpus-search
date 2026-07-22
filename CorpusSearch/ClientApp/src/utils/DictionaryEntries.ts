import {
    DictionaryPageResponse,
    DictionaryResponse,
    Summary,
} from "../api/DictionaryApi"

/** The word classes the page's own entries declare.
 *
 * More than one means a single spelling is carrying more than one word ('ass'
 * is both a weasel and 'out'), and nothing downstream can tell them apart: the
 * lemma table gives 'ass' one id, and the corpus indexes the spelling, not the
 * sense. Any claim made about the word as a whole — first attestation above
 * all — is then a claim about whichever of them came first.
 *
 * A lower bound, not a census: Phil Kelly declares no classes at all, ~20% of
 * Kelly's entries print none, and one class can still hold several senses (a
 * verb with three meanings looks like one word here). One class means "no
 * evidence of a split", never "only one sense".
 */
export const declaredClassesIn = (page: DictionaryPageResponse): string[] =>
    [
        ...new Set(
            page.groups
                .flatMap((g) => g.entries)
                .filter((e) => !e.rootDepth && !e.nearMatchOf)
                .flatMap((e) => e.partsOfSpeech ?? []),
        ),
    ].sort()

/** The printed abbreviation for a class, for a sense's title. The entries keep
 * whatever their own dictionary printed ('s.' for a noun, in both Cregeen and
 * Kelly): if a sense here is put together wrongly, the evidence for that is
 * still on the page underneath it. */
const ABBREVIATION: Record<string, string> = {
    Noun: "n.",
    Verb: "v.",
    Adjective: "a.",
    Adverb: "adv.",
    Preposition: "prep.",
    Conjunction: "conj.",
    Interjection: "interj.",
    Pronoun: "pron.",
}

/** Which classes are the same sense wearing different labels.
 *
 * Cregeen calls 'ass' an adverb and Kelly calls it a preposition, but it is one
 * word doing one job in both: the split a reader wants is 'out' against the
 * weasel, not adverb against preposition. Noun, verb and adjective stay apart —
 * those distinctions are real.
 *
 * This is a rule of thumb, not something the data says, and it will be wrong
 * for some words. That is why the sense's title names every class it merged and
 * the entries keep their printed labels.
 */
const SENSE_OF: Record<string, string> = {
    Noun: "noun",
    Verb: "verb",
    Adjective: "adjective",
    Adverb: "particle",
    Preposition: "particle",
    Conjunction: "particle",
    Interjection: "particle",
    Pronoun: "particle",
}

/** dictionary order, so a word's senses do not shuffle between pages */
const SENSE_ORDER = ["noun", "verb", "adjective", "particle"]

/** The gender a printed label declares: "s. m." says masculine, "s. f. pl."
 * says feminine, "s. m. f." says both. Null when the label is silent — most
 * are, and silence is not disagreement. */
const genderOf = (label: string | null | undefined): string | null => {
    const tokens = label?.split(/\s+/) ?? []
    const m = tokens.includes("m.")
    const f = tokens.includes("f.")
    return m && f ? "m. f." : m ? "m." : f ? "f." : null
}

export type SenseGroup = {
    /** the sense's key ("noun"); "" when nothing declared a class */
    key: string
    /** the classes it gathered, as the dictionaries abbreviate them
     * (["adv.", "prep."]); empty when nothing declared a class.
     *
     * Kept apart rather than joined into "adv., prep.": each is an abbreviation
     * the reader is owed the expansion of, and a tooltip cannot be hung on half
     * a string. */
    labels: string[]
    entries: DictionaryResponse
    /** the readings this sense's placed entries derive through ("moddey" for
     * the dog sense): what claims a root for the sense's own Built-from.
     * Empty when no entry could be threaded. */
    lemmas: string[]
}

/** The word's own entries, split into the senses they declare.
 *
 * An entry whose dictionary declares no class (all of Phil Kelly, ~20% of
 * Kelly) appears under **every** sense rather than in a bucket of its own: it
 * could belong to any of them, and putting it beside one would be a guess where
 * showing it beside each is only an admission.
 *
 * Roots and near-spellings are left out: they are other words, not senses of
 * this one. A word nothing declares a class for comes back as a single
 * unlabelled group — the page it has today.
 */
export const senseGroupsIn = (page: DictionaryPageResponse): SenseGroup[] => {
    const own = page.groups
        .flatMap((g) => g.entries)
        .filter((e) => !e.rootDepth && !e.nearMatchOf)
    const unplaceable = own.filter((e) => !e.partsOfSpeech?.length)
    const placed = own.filter((e) => e.partsOfSpeech?.length)
    const lemmasOf = (entries: Summary[]) => [
        ...new Set(
            entries.flatMap((e) => (e.throughLemma ? [e.throughLemma] : [])),
        ),
    ]
    if (placed.length === 0) {
        return [{ key: "", labels: [], entries: own, lemmas: lemmasOf(own) }]
    }

    const byKey = new Map<
        string,
        { classes: Set<string>; entries: Summary[] }
    >()
    for (const entry of placed) {
        // an entry declaring two classes belongs to both senses
        for (const declared of new Set(
            (entry.partsOfSpeech ?? []).map((c) => SENSE_OF[c] ?? "particle"),
        )) {
            const sense = byKey.get(declared) ?? {
                classes: new Set<string>(),
                entries: [],
            }
            for (const c of entry.partsOfSpeech ?? []) {
                if ((SENSE_OF[c] ?? "particle") === declared) {
                    sense.classes.add(c)
                }
            }
            sense.entries.push(entry)
            byKey.set(declared, sense)
        }
    }

    return SENSE_ORDER.filter((key) => byKey.has(key)).map((key) => {
        const sense = byKey.get(key)!
        const labels = [...sense.classes]
            .sort()
            .map((c) => ABBREVIATION[c] ?? c.toLowerCase())
        // a noun sense wears the gender its entries print ("n. m." over an
        // "s. m."), but only while they agree: a mixed or silent sense stays
        // "n.", and the printed labels underneath remain the evidence. An
        // entry whose printed gender the corpus disputes (genderNote) does
        // not get to elect the heading's gender.
        if (key === "noun") {
            const genders = new Set(
                sense.entries
                    .filter((e) => !e.genderNote)
                    .map((e) => genderOf(e.grammarLabel))
                    .filter((g) => g != null),
            )
            const at = labels.indexOf("n.")
            if (genders.size === 1 && at >= 0) {
                labels[at] = `n. ${[...genders][0]}`
            }
        }
        return {
            key,
            labels,
            // the unplaceable ride along with each: they may be any of them
            entries: [...sense.entries, ...unplaceable],
            // ...but only the placed entries speak for the sense's reading: an
            // entry that could be any sense must not claim a root for one
            lemmas: lemmasOf(sense.entries),
        }
    })
}

/** The roots each sense's entries derive through, threaded by `throughLemma`:
 * the moddey hop belongs under the dog sense, foddey's under "not long".
 * A root no sense claims stays page-level — where the thread is missing, the
 * old mixed basket is an admission rather than a guess. */
export const rootsBySense = (
    senses: SenseGroup[],
    roots: DictionaryResponse,
): {
    bySense: Map<string, DictionaryResponse>
    unclaimed: DictionaryResponse
} => {
    const bySense = new Map<string, DictionaryResponse>()
    const claimed = new Set<Summary>()
    for (const sense of senses) {
        const own = roots.filter(
            (root) =>
                root.throughLemma != null &&
                sense.lemmas.includes(root.throughLemma),
        )
        if (own.length > 0) {
            bySense.set(sense.key, own)
            own.forEach((root) => claimed.add(root))
        }
    }
    return { bySense, unclaimed: roots.filter((root) => !claimed.has(root)) }
}

/** A word's page, keeping the dictionary scope the reader is already in:
 * /dictionary/billey, or /dictionary/in/cregeen/billey under a scope.
 *
 * The scoped form is a nested path, so SpaRouteGuard must know it: an
 * unlisted sub-route 404s in production while working in development. */
export const dictionaryWordUrl = (word: string, dict?: string): string =>
    dict
        ? `/dictionary/in/${encodeURIComponent(dict)}/${encodeURIComponent(word)}`
        : `/dictionary/${encodeURIComponent(word)}`

/** The index a word is filed in, without the caller having to work out where:
 * browse takes a whole word for its `at` and opens the letter it starts,
 * folding ç to c the way the books do.
 *
 * A page of every dictionary at once has no one index behind it, and Cregeen is
 * the one that browses as a book: it is what /dictionary opens too. See
 * DictionaryLetters. */
export const dictionaryIndexUrl = (word: string, dict?: string): string =>
    `/dictionary/browse/${encodeURIComponent(dict ?? "cregeen")}/${encodeURIComponent(word)}`

const HYPHEN = /^[-‑]|[-‑]$/

/** The corpus search behind "Search the corpus for …".
 *
 * An affix is asked for as the words carrying it ('aa-' → 'aa-*'), the way the
 * evidence above the link is: it is attested by those and never on its own, and
 * asking for it as written finds nothing at all — the tokenizer keeps a hyphen
 * inside the token it joins, so no token is 'aa-'. The section would say 85
 * texts and the link would open on none.
 *
 * Mirrors Affix.cs, which is where the reasoning is written down. The server
 * decides what the corpus is asked; this only decides what to link to. */
export const corpusSearchUrl = (word: string): string => {
    const trimmed = word.trim()
    const query = HYPHEN.test(trimmed)
        ? trimmed.endsWith("-") || trimmed.endsWith("‑")
            ? `${trimmed.slice(0, -1).replace(/‑/g, "-")}-*`
            : `*-${trimmed.slice(1).replace(/‑/g, "-")}`
        : trimmed
    return `/?q=${encodeURIComponent(query)}`
}

/** Trims punctuation (but never letters/digits, so internal apostrophes and
 * hyphens survive) from the edges of a tapped word: 'meenid,' -> 'meenid' */
export const trimPunctuation = (s: string): string =>
    s.trim().replace(/^[^\p{L}\p{N}]+|[^\p{L}\p{N}]+$/gu, "")

/** Splits a dictionary's entries into those that are entries *for* the
 * selection (`own`) and those merely reached through it (`derived`: root
 * lemmas and variant spellings like 'muir' -> mooir), nesting the derived
 * ones under the selection. Phrase entries the tapped line doesn't actually
 * contain ('ry gheddyn' when the line says 'dy gheddyn', 'thie veg' when it
 * says 'feer veg') are dropped from both tiers, unless nothing else would
 * remain. */
export const classifyEntries = (
    word: string,
    context: string,
    entries: DictionaryResponse,
): { own: DictionaryResponse; derived: DictionaryResponse } => {
    const wordLc = trimPunctuation(word.toLowerCase())
    // an entry heads the selection in either direction: 'dy yannoo' heads the
    // selection 'yannoo'; the part 'goll' heads the compound 'goll-mygeayrt'
    const headsSelection = (p: string) =>
        p.toLowerCase() == wordLc ||
        p
            .toLowerCase()
            .split(/[\s'’-]+/)
            .includes(wordLc) ||
        wordLc.split(/[\s'’-]+/).includes(p.toLowerCase())
    // a phrase or compound entry must also occur in the tapped line (when
    // one is known): 'ry gheddyn' matches the word 'gheddyn' but is not what
    // the line says, and 'sheeyney-magh' is not what a line saying just
    // 'magh' says. Texts and dictionaries spell compounds with hyphen or
    // space interchangeably, so containment ignores the difference
    const foldHyphens = (s: string) => s.toLowerCase().replace(/[-‑]/g, " ")
    const supportedByContext = (p: string) =>
        !/[\s\-‑]/.test(p) ||
        p.toLowerCase() == wordLc ||
        context == "" ||
        foldHyphens(context).includes(foldHyphens(p))
    // a homograph headed by another spelling ('BILL, BILLEY') is still the
    // selection's own entry when its word list carries the tapped word - it
    // must not nest like a root
    const ownVia = (x: DictionaryResponse[number]) => {
        if (headsSelection(x.primaryWord)) {
            return supportedByContext(x.primaryWord)
        }
        const listed = (x.words ?? []).find(headsSelection)
        return listed != null && supportedByContext(listed)
    }
    const isOwn = (x: DictionaryResponse[number]) => !x.rootDepth && ownVia(x)
    let own = entries.filter(isOwn)
    // the context rule applies down the chain too: a phrase entry is noise
    // unless the line says it, whether it rode in on a root's word list
    // ('cur mow' via vow ↳ mow, 'dy olk' via smessey ↳ olk) or on the
    // selection's own ('ro veg'/'thie veg' under veg)
    const derived = entries.filter((x) => !own.includes(x))
    const inContext = derived.filter((x) => supportedByContext(x.primaryWord))
    if (own.length > 0 || inContext.length > 0) {
        return { own, derived: inContext }
    }
    // nothing heads the selection in-context and no chain survives the
    // context rule: an out-of-context phrase entry ('my yinnagh' with no
    // roots beneath) is better shown plainly than nothing at all. When a
    // chain did survive ('Chreest' ↳ Creest), it anchors the popup instead
    // and out-of-context phrases stay dropped.
    own = entries.filter(
        (x) =>
            !x.rootDepth &&
            (headsSelection(x.primaryWord) ||
                (x.words ?? []).some(headsSelection)),
    )
    return { own, derived: own.length > 0 ? [] : derived }
}

/** The heading for an own entry: "BILL, BILLEY" when the entry is headed by
 * another spelling but lists the tapped one - the reader sees both the
 * canonical head and the spelling they tapped. A ç/c respelling is the same
 * word, not another spelling. */
export const headingFor = (
    word: string,
    summary: { primaryWord: string; words?: string[] | null },
): string => {
    const fold = (s: string) =>
        trimPunctuation(s.toLowerCase()).replace(/ç/g, "c")
    const tapped = fold(word)
    if (fold(summary.primaryWord) == tapped) {
        return summary.primaryWord
    }
    const listed = (summary.words ?? []).find(
        (w) => fold(w) == tapped && fold(w) != fold(summary.primaryWord),
    )
    return listed ? `${summary.primaryWord}, ${listed}` : summary.primaryWord
}

/** Groups the popup's entries under the dictionary defining them (#51) */
export const groupByDictionary = (
    summaries: DictionaryResponse,
): [string, DictionaryResponse][] => {
    const groups = new Map<string, DictionaryResponse>()
    for (const summary of summaries) {
        const group = groups.get(summary.dictionaryName)
        if (group == null) {
            groups.set(summary.dictionaryName, [summary])
        } else {
            group.push(summary)
        }
    }
    return [...groups.entries()]
}
