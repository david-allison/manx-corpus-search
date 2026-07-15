import { DictionaryResponse } from "../api/DictionaryApi"

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
