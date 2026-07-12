import { describe, expect, it } from "vitest"
import { classifyEntries } from "./DictionaryLookupModal"

const entry = (primaryWord: string, rootDepth: number = 0) => ({
    primaryWord,
    summary: "…",
    dictionaryName: "Cregeen",
    rootDepth,
})

describe("classifyEntries", () => {
    it("keeps the phrase the tapped line contains, demotes the one it doesn't", () => {
        // tapping 'gheddyn' in 'Padjeryn dy gheddyn aarloo ny chour': the line
        // says 'dy gheddyn'; 'ry gheddyn' merely shares the word
        const { own, derived } = classifyEntries(
            "gheddyn",
            "Padjeryn dy gheddyn aarloo ny chour",
            [entry("dy gheddyn"), entry("ry gheddyn"), entry("geddyn", 1)],
        )

        expect(own.map((x) => x.primaryWord)).toEqual(["dy gheddyn"])
        expect(derived.map((x) => x.primaryWord)).toEqual([
            "ry gheddyn",
            "geddyn",
        ])
    })

    it("keeps an out-of-context phrase when nothing else heads the selection", () => {
        // tapping 'yinnagh' where the line doesn't say 'my yinnagh': the
        // phrase entry is all Cregeen has, so it stays own, roots beneath it
        const { own, derived } = classifyEntries(
            "yinnagh",
            "cha yinnagh eh shen",
            [entry("my yinnagh"), entry("jean", 1)],
        )

        expect(own.map((x) => x.primaryWord)).toEqual(["my yinnagh"])
        expect(derived.map((x) => x.primaryWord)).toEqual(["jean"])
    })

    it("keeps phrase entries when no context is known", () => {
        const { own } = classifyEntries("gheddyn", "", [entry("ry gheddyn")])

        expect(own.map((x) => x.primaryWord)).toEqual(["ry gheddyn"])
    })

    it("demotes a variant-spelling headword ('muir' -> mooir)", () => {
        const { own, derived } = classifyEntries("muir", "er yn muir", [
            entry("mooir"),
        ])

        expect(own).toEqual([])
        expect(derived.map((x) => x.primaryWord)).toEqual(["mooir"])
    })

    it("keeps the parts of a compound selection", () => {
        const { own } = classifyEntries(
            "goll-mygeayrt",
            "va mee goll-mygeayrt",
            [entry("goll"), entry("mygeayrt")],
        )

        expect(own.map((x) => x.primaryWord)).toEqual(["goll", "mygeayrt"])
    })

    it("a tapped word keeps its own entry despite the line's punctuation", () => {
        // 'meenid,' mid-sentence: the comma must not demote the exact entry
        const { own, derived } = classifyEntries("meenid,", "lesh meenid, as", [
            entry("meenid"),
        ])

        expect(own.map((x) => x.primaryWord)).toEqual(["meenid"])
        expect(derived).toEqual([])
    })

    it("quotes and brackets around the tap are ignored too", () => {
        const { own } = classifyEntries("“meenid”", "as “meenid” v'eh", [
            entry("meenid"),
        ])

        expect(own.map((x) => x.primaryWord)).toEqual(["meenid"])
    })

    it("internal apostrophes and hyphens survive punctuation trimming", () => {
        const { own } = classifyEntries("aa-aase,", "yn aa-aase, as", [
            entry("aa-aase"),
        ])

        expect(own.map((x) => x.primaryWord)).toEqual(["aa-aase"])
    })

    it("root-lemma entries are always derived", () => {
        const { own, derived } = classifyEntries("daase", "daase eh", [
            entry("daase"),
            entry("aase", 1),
        ])

        expect(own.map((x) => x.primaryWord)).toEqual(["daase"])
        expect(derived.map((x) => x.primaryWord)).toEqual(["aase"])
    })
})
