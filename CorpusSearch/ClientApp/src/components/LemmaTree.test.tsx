import { describe, expect, it } from "vitest"
import { senseKeyOfPos } from "./LemmaTree"

/* The sense-section key a tree's printed class files under: what seats a
 * lexeme's family tree beneath the section that reads about it. The split
 * must agree with SENSE_OF in DictionaryEntries, or a section and its own
 * tree end up on different parts of the page. */
describe("senseKeyOfPos", () => {
    it("files the open classes under their own sections", () => {
        expect(senseKeyOfPos("s.")).toBe("noun")
        expect(senseKeyOfPos("s. m.")).toBe("noun")
        expect(senseKeyOfPos("n.")).toBe("noun")
        expect(senseKeyOfPos("v.")).toBe("verb")
        expect(senseKeyOfPos("v. i.")).toBe("verb")
        expect(senseKeyOfPos("a.")).toBe("adjective")
        expect(senseKeyOfPos("a. d.")).toBe("adjective")
    })

    it("files the function-word cluster together, as SENSE_OF does", () => {
        expect(senseKeyOfPos("adv.")).toBe("particle")
        expect(senseKeyOfPos("pro.")).toBe("particle")
        expect(senseKeyOfPos("pre.")).toBe("particle")
        expect(senseKeyOfPos("conj.")).toBe("particle")
    })

    it("keeps the interjection apart from the cluster", () => {
        expect(senseKeyOfPos("in.")).toBe("interjection")
    })

    /* Cregeen's doubled p is his prepositional pronoun (mooin "about us",
     * orrym "on me"): without it the p. p. section stood empty while its
     * family tree sat at the page's foot. The comma spelling is the book's
     * own ("p, p." is printed twice). */
    it("reads the doubled p as the prepositional pronoun's", () => {
        expect(senseKeyOfPos("p. p.")).toBe("particle")
        expect(senseKeyOfPos("p, p.")).toBe("particle")
    })

    it("reads the lone p as pronominal", () => {
        expect(senseKeyOfPos("p.")).toBe("particle")
    })

    it("lets no other p-label ride the one-letter match", () => {
        expect(senseKeyOfPos("pt.")).toBeNull()
        expect(senseKeyOfPos("part.")).toBeNull()
        expect(senseKeyOfPos("pl.")).toBeNull()
    })

    it("claims nothing for a label it cannot read", () => {
        expect(senseKeyOfPos("")).toBeNull()
        expect(senseKeyOfPos("usage.")).toBeNull()
    })
})
