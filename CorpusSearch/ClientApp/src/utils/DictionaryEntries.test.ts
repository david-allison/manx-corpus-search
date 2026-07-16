import { describe, expect, it } from "vitest"
import {
    corpusSearchUrl,
    declaredClassesIn,
    senseGroupsIn,
} from "./DictionaryEntries"
import { DictionaryPageResponse, Summary } from "../api/DictionaryApi"

const entry = (over: Partial<Summary>): Summary => ({
    summary: "…",
    primaryWord: "ass",
    dictionaryName: "Cregeen",
    rootDepth: 0,
    ...over,
})

const page = (entries: Summary[]): DictionaryPageResponse => ({
    word: "ass",
    isSuggestionTier: false,
    attested: true,
    answering: ["cregeen"],
    groups: [{ dictionary: "Cregeen", entries }],
})

describe("declaredClassesIn", () => {
    it("collects the classes the word's own entries declare", () => {
        expect(
            declaredClassesIn(
                page([
                    entry({ partsOfSpeech: ["Adverb"] }),
                    entry({ partsOfSpeech: ["Noun"] }),
                    entry({ partsOfSpeech: ["Adverb"] }),
                ]),
            ),
        ).toEqual(["Adverb", "Noun"])
    })

    it("ignores roots: they are other words, not senses of this one", () => {
        expect(
            declaredClassesIn(
                page([
                    entry({ partsOfSpeech: ["Noun"] }),
                    entry({ partsOfSpeech: ["Verb"], rootDepth: 1 }),
                ]),
            ),
        ).toEqual(["Noun"])
    })

    it("ignores near-spelling suggestions, which are not this word at all", () => {
        expect(
            declaredClassesIn(
                page([
                    entry({ partsOfSpeech: ["Noun"] }),
                    entry({ partsOfSpeech: ["Verb"], nearMatchOf: "asse" }),
                ]),
            ),
        ).toEqual(["Noun"])
    })

    it("is empty when nothing declares a class", () => {
        // Phil Kelly declares none at all, and ~20% of Kelly's print none
        expect(declaredClassesIn(page([entry({}), entry({})]))).toEqual([])
    })

    it("counts an entry that declares two classes as both", () => {
        expect(
            declaredClassesIn(
                page([entry({ partsOfSpeech: ["Noun", "Verb"] })]),
            ),
        ).toEqual(["Noun", "Verb"])
    })
})

describe("senseGroupsIn", () => {
    it("splits the weasel from 'out', merging adverb with preposition", () => {
        // the real 'ass': Cregeen calls it an adverb, Kelly a preposition, and
        // they mean the same word — the split a reader wants is against the weasel
        const groups = senseGroupsIn(
            page([
                entry({ primaryWord: "ass", partsOfSpeech: ["Adverb"] }),
                entry({ primaryWord: "ASS", partsOfSpeech: ["Noun"] }),
                entry({ primaryWord: "ASS", partsOfSpeech: ["Preposition"] }),
            ]),
        )

        expect(groups.map((g) => g.key)).toEqual(["noun", "particle"])
        expect(groups.map((g) => g.labels)).toEqual([["n."], ["adv.", "prep."]])
        expect(groups[1].entries).toHaveLength(2)
    })

    it("names every class it merged, so a wrong merge is visible", () => {
        const groups = senseGroupsIn(
            page([
                entry({ partsOfSpeech: ["Adverb"] }),
                entry({ partsOfSpeech: ["Conjunction"] }),
            ]),
        )

        expect(groups).toHaveLength(1)
        expect(groups[0].labels).toEqual(["adv.", "conj."])
    })

    it("keeps noun, verb and adjective apart: those distinctions are real", () => {
        const groups = senseGroupsIn(
            page([
                entry({ partsOfSpeech: ["Verb"] }),
                entry({ partsOfSpeech: ["Noun"] }),
                entry({ partsOfSpeech: ["Adjective"] }),
            ]),
        )

        expect(groups.map((g) => g.key)).toEqual(["noun", "verb", "adjective"])
    })

    it("shows an entry with no declared class under every sense", () => {
        // Phil Kelly declares none: it could be either, so it is beside both
        const groups = senseGroupsIn(
            page([
                entry({ primaryWord: "weasel", partsOfSpeech: ["Noun"] }),
                entry({ primaryWord: "out", partsOfSpeech: ["Adverb"] }),
                entry({
                    primaryWord: "philkelly",
                    dictionaryName: "Phil Kelly",
                }),
            ]),
        )

        expect(groups.map((g) => g.entries.map((e) => e.primaryWord))).toEqual([
            ["weasel", "philkelly"],
            ["out", "philkelly"],
        ])
    })

    it("puts an entry declaring two classes under both", () => {
        const groups = senseGroupsIn(
            page([entry({ partsOfSpeech: ["Noun", "Verb"] })]),
        )

        expect(groups.map((g) => g.key)).toEqual(["noun", "verb"])
        expect(groups.map((g) => g.labels)).toEqual([["n."], ["v."]])
    })

    it("returns one unlabelled group when nothing declares a class", () => {
        // the page it has today: no senses to show, so none are invented
        const groups = senseGroupsIn(page([entry({}), entry({})]))

        expect(groups).toHaveLength(1)
        expect(groups[0].labels).toEqual([])
        expect(groups[0].entries).toHaveLength(2)
    })

    it("leaves roots and near-spellings out: they are other words", () => {
        const groups = senseGroupsIn(
            page([
                entry({ primaryWord: "ass", partsOfSpeech: ["Noun"] }),
                entry({
                    primaryWord: "fass",
                    partsOfSpeech: ["Verb"],
                    rootDepth: 1,
                }),
                entry({
                    primaryWord: "asse",
                    partsOfSpeech: ["Verb"],
                    nearMatchOf: "asse",
                }),
            ]),
        )

        expect(groups.map((g) => g.key)).toEqual(["noun"])
        expect(groups[0].entries.map((e) => e.primaryWord)).toEqual(["ass"])
    })
})

/** An affix is attested by the words carrying it and never on its own, so the
 * link asks for those — as the evidence above it does. Asking for 'aa-' as
 * written finds nothing: no token is 'aa-'. See Affix.cs. */
describe("corpusSearchUrl", () => {
    it("asks for a word as it is written", () => {
        expect(corpusSearchUrl("billey")).toBe("/?q=billey")
    })

    it("asks for the words a prefix begins", () => {
        expect(corpusSearchUrl("aa-")).toBe("/?q=aa-*")
    })

    it("asks for the words a suffix ends", () => {
        expect(corpusSearchUrl("-agh")).toBe("/?q=*-agh")
        // the non-breaking hyphen is not what is indexed
        expect(corpusSearchUrl("‑ys")).toBe("/?q=*-ys")
    })

    it("leaves a hyphen inside a word alone: it joins, it does not cut loose", () => {
        expect(corpusSearchUrl("aa-aase")).toBe("/?q=aa-aase")
    })
})
