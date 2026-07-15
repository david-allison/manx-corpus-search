import { describe, expect, it } from "vitest"
import { buildKwic } from "./Kwic"

describe("buildKwic", () => {
    it("splits the sample around the highlighted range", () => {
        expect(buildKwic("Ta çhengey aym", [{ start: 3, end: 10 }])).toEqual({
            pre: "Ta ",
            match: "çhengey",
            post: " aym",
        })
    })

    it("handles a match at the start of the sample", () => {
        expect(buildKwic("cre ta shen", [{ start: 0, end: 3 }])).toEqual({
            pre: "",
            match: "cre",
            post: " ta shen",
        })
    })

    it("handles a match at the end of the sample", () => {
        expect(buildKwic("cre ta cre", [{ start: 7, end: 10 }])).toEqual({
            pre: "cre ta ",
            match: "cre",
            post: "",
        })
    })

    it("highlights the first match of the line", () => {
        const highlights = [
            { start: 0, end: 3 },
            { start: 7, end: 10 },
        ]
        expect(buildKwic("cre ta cre", highlights)?.pre).toBe("")
    })

    it("returns null without highlights, so the caller shows the plain sample", () => {
        expect(buildKwic("cre ta", [])).toBeNull()
    })

    it("limits the context to the same number of words on each side", () => {
        const sample = "a b c d e f MATCH u v w x y z"
        const result = buildKwic(sample, [{ start: 12, end: 17 }])
        expect(result).toEqual({
            pre: " b c d e f ",
            match: "MATCH",
            post: " u v w x y",
        })
    })

    it("takes a narrower window when the caller has less room", () => {
        const sample = "a b c d e f MATCH u v w x y z"
        const result = buildKwic(sample, [{ start: 12, end: 17 }], 3)
        expect(result).toEqual({
            pre: " d e f ",
            match: "MATCH",
            post: " u v w",
        })
    })
})
