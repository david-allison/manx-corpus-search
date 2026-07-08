import { describe, expect, it } from "vitest"
import { buildKwic } from "./MainSearchResults"

describe("buildKwic", () => {
    it("splits the sample around the highlighted range", () => {
        expect(buildKwic("Ta çhengey aym", [{ start: 3, end: 10 }], 0)).toEqual(
            {
                pre: "Ta ",
                match: "çhengey",
                post: " aym",
            },
        )
    })

    it("handles a match at the start of the sample", () => {
        expect(buildKwic("cre ta shen", [{ start: 0, end: 3 }], 0)).toEqual({
            pre: "",
            match: "cre",
            post: " ta shen",
        })
    })

    it("handles a match at the end of the sample", () => {
        expect(buildKwic("cre ta cre", [{ start: 7, end: 10 }], 0)).toEqual({
            pre: "cre ta ",
            match: "cre",
            post: "",
        })
    })

    it("selects the requested match of the line", () => {
        const highlights = [
            { start: 0, end: 3 },
            { start: 7, end: 10 },
        ]
        expect(buildKwic("cre ta cre", highlights, 1)?.pre).toBe("cre ta ")
        expect(buildKwic("cre ta cre", highlights, 0)?.pre).toBe("")
    })

    it("clamps an out-of-range match index to the last match", () => {
        expect(buildKwic("cre ta", [{ start: 0, end: 3 }], 7)?.match).toBe(
            "cre",
        )
    })

    it("returns null without highlights, so the caller shows the plain sample", () => {
        expect(buildKwic("cre ta", [], 0)).toBeNull()
    })

    it("limits the context on each side", () => {
        const sample = "a b c d e f MATCH u v w x y z"
        const result = buildKwic(sample, [{ start: 12, end: 17 }], 0)
        expect(result).toEqual({
            pre: " c d e f ",
            match: "MATCH",
            post: " u v w x y",
        })
    })
})
