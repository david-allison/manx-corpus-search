import { describe, expect, it } from "vitest"
import { getMultidictLookupWord, multidictUrl } from "./MultidictLink"

describe("getMultidictLookupWord", () => {
    it("accepts a single word", () => {
        expect(getMultidictLookupWord("moghrey")).toBe("moghrey")
    })

    it("strips the wildcards added by 'match phrase'", () => {
        expect(getMultidictLookupWord("*moghrey*")).toBe("moghrey")
    })

    it("accepts words with apostrophes and hyphens", () => {
        expect(getMultidictLookupWord("'sy")).toBe("'sy")
        expect(getMultidictLookupWord("lhiann-oo")).toBe("lhiann-oo")
    })

    it("rejects phrases", () => {
        expect(getMultidictLookupWord("moghrey mie")).toBeNull()
    })

    it("rejects wildcard queries", () => {
        expect(getMultidictLookupWord("mogh*ey")).toBeNull()
        expect(getMultidictLookupWord("mogh?ey")).toBeNull()
    })

    it("rejects empty queries", () => {
        expect(getMultidictLookupWord("")).toBeNull()
        expect(getMultidictLookupWord("*")).toBeNull()
        expect(getMultidictLookupWord("  ")).toBeNull()
    })
})

describe("multidictUrl", () => {
    it("looks up Manx to English", () => {
        expect(multidictUrl("moghrey", "Manx")).toBe(
            "https://multidict.net/multidict/?word=moghrey&sl=gv&tl=en",
        )
    })

    it("looks up English to Manx", () => {
        expect(multidictUrl("morning", "English")).toBe(
            "https://multidict.net/multidict/?word=morning&sl=en&tl=gv",
        )
    })

    it("escapes the word", () => {
        expect(multidictUrl("çhengey", "Manx")).toBe(
            "https://multidict.net/multidict/?word=%C3%A7hengey&sl=gv&tl=en",
        )
    })
})
