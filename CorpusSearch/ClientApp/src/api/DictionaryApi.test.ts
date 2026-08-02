import { describe, expect, it } from "vitest"
import { trimContext } from "./DictionaryApi"

describe("trimContext", () => {
    it("returns short contexts unchanged", () => {
        const context = "v'eh goll mygeayrt y valley"
        expect(trimContext(context, "goll")).toBe(context)
    })

    it("sends a whole long line untrimmed: the per-line lemma resolution hashes the full token stream", () => {
        const context = `${"a".repeat(500)} goll mygeayrt ${"b".repeat(500)}`
        expect(trimContext(context, "goll")).toBe(context)
    })

    it("trims a pathological line to a window around the selection", () => {
        const context = `${"a".repeat(5000)} goll mygeayrt ${"b".repeat(5000)}`
        const trimmed = trimContext(context, "goll")
        expect(trimmed).toContain("goll mygeayrt")
        expect(trimmed.length).toBeLessThanOrEqual(4000 + "goll".length)
    })

    it("keeps the selection when it appears late in a pathological line", () => {
        const context = `${"a".repeat(10000)} goll-mygeayrt`
        expect(trimContext(context, "goll-mygeayrt")).toContain("goll-mygeayrt")
    })

    it("finds the selection case-insensitively", () => {
        const context = `Goll mygeayrt ${"b".repeat(10000)}`
        expect(trimContext(context, "goll")).toContain("Goll mygeayrt")
    })

    it("falls back to the head of the line when the selection is not found", () => {
        const context = "c".repeat(10000)
        const trimmed = trimContext(context, "goll")
        expect(trimmed).toBe("c".repeat(4000))
    })
})
