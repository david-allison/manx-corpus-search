import { describe, expect, it } from "vitest"
import { cleanup, render, screen } from "@testing-library/react"
import { afterEach } from "vitest"
import { DefinitionText, expandGrammarLabel, GrammarLabel } from "./GrammarAbbr"

afterEach(cleanup)

describe("expandGrammarLabel", () => {
    it("expands Cregeen's gendered noun labels", () => {
        expect(expandGrammarLabel("s. m.")).toBe("noun, masculine")
        expect(expandGrammarLabel("s. f.")).toBe("noun, feminine")
        expect(expandGrammarLabel("v.")).toBe("verb")
        expect(expandGrammarLabel("in.")).toBe("interjection")
    })

    it("expands a compound label token by token", () => {
        expect(expandGrammarLabel("pro. adv.")).toBe("pronoun, adverb")
    })

    it("returns undefined when any part is unknown", () => {
        expect(expandGrammarLabel("a. d.")).toBeUndefined()
    })
})

describe("GrammarLabel", () => {
    it("renders the printed label with the expansion on hover", () => {
        render(<GrammarLabel label="s. f." />)

        const abbr = screen.getByText("s. f.")
        expect(abbr.getAttribute("title")).toBe("noun, feminine")
    })

    it("renders an unknown label plainly, without a wrong tooltip", () => {
        render(<GrammarLabel label="a. d." />)

        const label = screen.getByText("a. d.")
        expect(label.getAttribute("title")).toBeNull()
    })
})

describe("DefinitionText", () => {
    it("explains the abbreviations inside a Kelly definition", () => {
        render(
            <DefinitionText text="s. a tree, a clump of trees. (Ir. bile.)" />,
        )

        expect(screen.getByText("s.").getAttribute("title")).toBe(
            "noun (substantive)",
        )
        expect(screen.getByText("Ir.").getAttribute("title")).toBe("Irish")
    })

    it("longest label wins: s. m. is one abbreviation, not two", () => {
        render(<DefinitionText text="s. m. a father" />)

        expect(screen.getByText("s. m.").getAttribute("title")).toBe(
            "noun, masculine",
        )
    })

    it("does not tag the s. inside Ps. (a Psalms citation)", () => {
        render(<DefinitionText text="a ruler. Ps. cx. 6." />)

        expect(screen.queryByText("s.")).toBeNull()
    })

    it("leaves prose words alone", () => {
        render(<DefinitionText text="the state he was in." />)

        expect(document.querySelectorAll("abbr")).toHaveLength(0)
    })
})
