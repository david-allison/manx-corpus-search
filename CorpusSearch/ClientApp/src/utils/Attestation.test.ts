import { describe, expect, it } from "vitest"
import { DictionaryHistoryResponse, HistoryForm } from "../api/DictionaryApi"
import { earliestForLemma, earliestForWord } from "./Attestation"

const form = (
    f: string,
    earliestYear: number | null,
    sharedWithOtherLemmas = false,
): HistoryForm => ({
    form: f,
    total: 1,
    documents: 1,
    sharedWithOtherLemmas,
    earliestYear,
})

const history = (
    word: string,
    forms: HistoryForm[],
): DictionaryHistoryResponse => ({
    word,
    lemmas: [],
    revivalBoundaryYear: 1900,
    forms,
    truncatedForms: 0,
    decades: [],
    traditionalCount: 0,
    revivedCount: 0,
    undatedCount: 0,
    dictionaries: [],
    cognates: [],
})

describe("earliestForWord", () => {
    it("finds the looked-up spelling regardless of case", () => {
        const h = history("Bee", [form("bee", 1748), form("vee", 1610, true)])

        expect(earliestForWord(h)).toMatchObject({ year: 1748 })
    })

    it("marks the claim when the spelling belongs to another lexeme too", () => {
        const h = history("vee", [form("vee", 1610, true)])

        expect(earliestForWord(h)).toMatchObject({
            year: 1610,
            uncertain: true,
        })
    })

    it("is null when the corpus never attests the spelling", () => {
        const h = history("bee", [form("bee", null), form("veg", 1748)])

        expect(earliestForWord(h)).toBeNull()
    })
})

describe("earliestForLemma", () => {
    it("prefers an unambiguous spelling over an earlier shared one", () => {
        // the shipped rule: 'vee' may be another word, so it cannot headline
        const h = history("bee", [form("bee", 1748), form("vee", 1610, true)])

        const result = earliestForLemma(h)!
        expect(result.claim).toMatchObject({ year: 1748, uncertain: false })
        // ...but the earlier reading is still surfaced, marked
        expect(result.earlierShared).toMatchObject({
            year: 1610,
            uncertain: true,
        })
    })

    it("offers no earlier reading when the unambiguous form is already earliest", () => {
        const h = history("bee", [form("bee", 1610), form("vee", 1748, true)])

        const result = earliestForLemma(h)!
        expect(result.claim).toMatchObject({ year: 1610 })
        expect(result.earlierShared).toBeNull()
    })

    it("falls back to a shared spelling, marked, when nothing else is attested", () => {
        const h = history("vee", [form("vee", 1610, true), form("bee", null)])

        const result = earliestForLemma(h)!
        expect(result.claim).toMatchObject({ year: 1610, uncertain: true })
        // the claim is already the earliest: nothing further to offer
        expect(result.earlierShared).toBeNull()
    })

    it("takes the earliest unambiguous spelling, not the first listed", () => {
        const h = history("bee", [form("beeys", 1801), form("bee", 1748)])

        expect(earliestForLemma(h)!.claim).toMatchObject({
            form: { form: "bee" },
            year: 1748,
        })
    })

    it("is null when no form is dated", () => {
        const h = history("bee", [form("bee", null)])

        expect(earliestForLemma(h)).toBeNull()
    })
})
