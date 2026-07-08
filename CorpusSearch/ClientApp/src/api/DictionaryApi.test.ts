import { describe, expect, it } from "vitest"
import { trimContext } from "./DictionaryApi"

describe("trimContext", () => {
    it("returns short contexts unchanged", () => {
        const context = "v'eh goll mygeayrt y valley"
        expect(trimContext(context, "goll")).toBe(context)
    })

    it("trims long contexts to a window around the selection", () => {
        const context = `${"a".repeat(500)} goll mygeayrt ${"b".repeat(500)}`
        const trimmed = trimContext(context, "goll")
        expect(trimmed).toContain("goll mygeayrt")
        expect(trimmed.length).toBeLessThanOrEqual(500 + "goll".length)
    })

    it("keeps the selection when it appears late in the context", () => {
        const context = `${"a".repeat(1000)} goll-mygeayrt`
        expect(trimContext(context, "goll-mygeayrt")).toContain("goll-mygeayrt")
    })

    it("finds the selection case-insensitively", () => {
        const context = `Goll mygeayrt ${"b".repeat(1000)}`
        expect(trimContext(context, "goll")).toContain("Goll mygeayrt")
    })

    it("falls back to the head of the line when the selection is not found", () => {
        const context = "c".repeat(1000)
        const trimmed = trimContext(context, "goll")
        expect(trimmed).toBe("c".repeat(300))
    })
})
