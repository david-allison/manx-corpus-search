import { describe, expect, it } from "vitest"
import { classifyEntries, headingFor } from "./DictionaryLookupModal"

const entry = (
    primaryWord: string,
    rootDepth: number = 0,
    words?: string[],
) => ({
    primaryWord,
    summary: "…",
    dictionaryName: "Cregeen",
    rootDepth,
    words,
})

describe("classifyEntries", () => {
    it("keeps the phrase the tapped line contains, drops the one it doesn't", () => {
        // tapping 'gheddyn' in 'Padjeryn dy gheddyn aarloo ny chour': the line
        // says 'dy gheddyn'; 'ry gheddyn' merely shares the word
        const { own, derived } = classifyEntries(
            "gheddyn",
            "Padjeryn dy gheddyn aarloo ny chour",
            [entry("dy gheddyn"), entry("ry gheddyn"), entry("geddyn", 1)],
        )

        expect(own.map((x) => x.primaryWord)).toEqual(["dy gheddyn"])
        expect(derived.map((x) => x.primaryWord)).toEqual(["geddyn"])
    })

    it("an out-of-context phrase gives way to the root chain", () => {
        // tapping 'yinnagh' where the line doesn't say 'my yinnagh': the
        // chain answers the tap, so the phrase is noise - the popup anchors
        // on the selection with the root beneath
        const { own, derived } = classifyEntries(
            "yinnagh",
            "cha yinnagh eh shen",
            [entry("my yinnagh"), entry("jean", 1)],
        )

        expect(own).toEqual([])
        expect(derived.map((x) => x.primaryWord)).toEqual(["jean"])
    })

    it("keeps an out-of-context phrase when there is no chain beneath", () => {
        // with no roots at all, the phrase entry is all Cregeen has: shown
        // plainly rather than nothing
        const { own, derived } = classifyEntries(
            "yinnagh",
            "cha yinnagh eh shen",
            [entry("my yinnagh")],
        )

        expect(own.map((x) => x.primaryWord)).toEqual(["my yinnagh"])
        expect(derived).toEqual([])
    })

    it("the Chreest shape: phrases sharing the word never outrank the name", () => {
        // tapping 'Chreest' in a line saying neither phrase: only the bridge's
        // root entry answers the tap
        const { own, derived } = classifyEntries(
            "Chreest",
            "ayns ennym Chreest y Chiarn",
            [
                entry("fuill Chreest"),
                entry("moylley Chreest"),
                entry("Creest", 1),
            ],
        )

        expect(own).toEqual([])
        expect(derived.map((x) => x.primaryWord)).toEqual(["Creest"])
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

    it("a homograph listing the tapped word is own, not a root (BILL, BILLEY)", () => {
        // Kelly's 'BILL, BILLEY: a bill' entry answers a tap on 'billey'
        // directly: it must render as a peer of the tree entry, not '↳ BILL'
        const { own, derived } = classifyEntries("billey", "yn billey mooar", [
            entry("BILLEY"),
            entry("BILL", 0, ["BILL", "BILLEY"]),
        ])

        expect(own.map((x) => x.primaryWord)).toEqual(["BILLEY", "BILL"])
        expect(derived).toEqual([])
    })

    it("a word list does not rescue an entry for a word it doesn't carry", () => {
        const { own, derived } = classifyEntries("muir", "er yn muir", [
            entry("mooir", 0, ["mooir", "vooir"]),
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

    it("drops a root's phrase entry the tapped line doesn't contain", () => {
        // tapping 'vow' surfaces the root chain (fow, and the demutation
        // candidate mow); neither 'cha vow' (a phrase on the selection) nor
        // 'cur mow' (riding in on mow's word list) is what the line says
        const { own, derived } = classifyEntries(
            "vow",
            "T’ad shirrey mayrneys raad nagh vow ad eh,",
            [
                entry("vow"),
                entry("cha vow"),
                entry("fow", 1),
                entry("mow", 1),
                entry("cur mow", 1),
            ],
        )

        expect(own.map((x) => x.primaryWord)).toEqual(["vow"])
        expect(derived.map((x) => x.primaryWord)).toEqual(["fow", "mow"])
    })

    it("drops the selection's own phrase entries the line doesn't contain", () => {
        // tapping 'veg' in a 'feer veg' line: 'ro veg' and 'thie veg' head
        // the selection but are not what the line says; the root beg and the
        // demutation candidate meg stay
        const { own, derived } = classifyEntries(
            "veg",
            "Feer veg ta’d cur da’n labbree boght,",
            [
                entry("feer veg"),
                entry("veg"),
                entry("ro veg"),
                entry("thie veg"),
                entry("beg", 1),
                entry("meg", 1),
            ],
        )

        expect(own.map((x) => x.primaryWord)).toEqual(["feer veg", "veg"])
        expect(derived.map((x) => x.primaryWord)).toEqual(["beg", "meg"])
    })

    it("drops a root's phrase entry at any depth ('dy olk' under smessey)", () => {
        const { own, derived } = classifyEntries(
            "smessey",
            "Agh va whisteragh ny smessey na ooilley.",
            [entry("smessey"), entry("olk", 1), entry("dy olk", 1)],
        )

        expect(own.map((x) => x.primaryWord)).toEqual(["smessey"])
        expect(derived.map((x) => x.primaryWord)).toEqual(["olk"])
    })

    it("drops a hyphenated compound entry the line doesn't contain", () => {
        // tapping 'magh': 'sheeyney-magh' is a compound sharing the word,
        // not what the line says - hyphens don't exempt it from the rule
        const { own, derived } = classifyEntries(
            "magh",
            "O Yee, ta liauyragh magh my vea",
            [entry("magh"), entry("sheeyney-magh")],
        )

        expect(own.map((x) => x.primaryWord)).toEqual(["magh"])
        expect(derived).toEqual([])
    })

    it("a compound entry matches the line across hyphen/space spelling", () => {
        // the line writes 'sheeyney magh'; Cregeen's headword is hyphenated
        const { own } = classifyEntries(
            "magh",
            "lesh keoied, sheeyney magh e vair dys yn droyn",
            [entry("magh"), entry("sheeyney-magh")],
        )

        expect(own.map((x) => x.primaryWord)).toEqual(["magh", "sheeyney-magh"])
    })

    it("keeps the root's phrase entry when the line does contain it", () => {
        const { derived } = classifyEntries(
            "smessey",
            "ta shen dy olk as ny smessey foast",
            [entry("smessey"), entry("olk", 1), entry("dy olk", 1)],
        )

        expect(derived.map((x) => x.primaryWord)).toEqual(["olk", "dy olk"])
    })
})

describe("headingFor", () => {
    it("appends the tapped spelling to a homograph's canonical head", () => {
        expect(headingFor("billey", entry("BILL", 0, ["BILL", "BILLEY"]))).toBe(
            "BILL, BILLEY",
        )
    })

    it("shows just the head when it is what was tapped", () => {
        expect(headingFor("billey", entry("BILLEY"))).toBe("BILLEY")
    })

    it("a ç/c respelling is the same word, not another spelling", () => {
        // the c-variant word list rides in for matching; the heading must not
        // read 'ÇHENGEY, CHENGEY'
        expect(
            headingFor(
                "chengey",
                entry("ÇHENGEY", 0, ["ÇHENGEY", "TEANGEY", "CHENGEY"]),
            ),
        ).toBe("ÇHENGEY")
    })

    it("a genuinely different listed spelling is appended (TEANGEY)", () => {
        expect(
            headingFor(
                "teangey",
                entry("ÇHENGEY", 0, ["ÇHENGEY", "TEANGEY", "CHENGEY"]),
            ),
        ).toBe("ÇHENGEY, TEANGEY")
    })
})
