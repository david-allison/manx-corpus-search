import { describe, expect, it, vi } from "vitest"
import { cleanup, fireEvent, render, screen } from "@testing-library/react"
import { afterEach } from "vitest"
import { DefinitionText, expandGrammarLabel, GrammarLabel } from "./GrammarAbbr"

afterEach(cleanup)

describe("expandGrammarLabel", () => {
    it("expands Cregeen's gendered noun labels, naming the substantive", () => {
        expect(expandGrammarLabel("s. m.")).toBe(
            "noun (substantive), masculine",
        )
        expect(expandGrammarLabel("s. f.")).toBe("noun (substantive), feminine")
        expect(expandGrammarLabel("v.")).toBe("verb")
        expect(expandGrammarLabel("in.")).toBe("interjection")
    })

    it("expands a compound label token by token", () => {
        expect(expandGrammarLabel("pro. adv.")).toBe("pronoun, adverb")
    })

    it("expands the page's own gendered sense labels", () => {
        expect(expandGrammarLabel("n. m.")).toBe("noun, masculine")
        expect(expandGrammarLabel("n. f.")).toBe("noun, feminine")
        expect(expandGrammarLabel("n. m. f.")).toBe(
            "noun, masculine or feminine",
        )
    })

    it("returns undefined when any part is unknown", () => {
        expect(expandGrammarLabel("a. d.")).toBeUndefined()
    })
})

describe("GrammarLabel", () => {
    it("renders the printed label with the expansion on hover", () => {
        render(<GrammarLabel label="s. f." />)

        const abbr = screen.getByText("s. f.")
        expect(abbr.getAttribute("title")).toBe("noun (substantive), feminine")
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
            "noun (substantive), masculine",
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

describe("DefinitionText citations", () => {
    const text = "cow's milk; 1 Sam. vi. 7. See also Jud. v. 17."
    const citations = [
        { text: "1 Sam. vi. 7", key: "1-samuel.6.7" },
        { text: "Jud. v. 17", key: "judges.5.17" },
    ]

    it("turns each citation into a link and keeps the rest as text", () => {
        const onCitationClick = vi.fn()
        const { container } = render(
            <DefinitionText
                text={text}
                citations={citations}
                onCitationClick={onCitationClick}
            />,
        )
        const links = container.querySelectorAll(".dict-citation-link")
        expect(Array.from(links).map((x) => x.textContent)).toEqual([
            "1 Sam. vi. 7",
            "Jud. v. 17",
        ])

        fireEvent.click(links[1])
        expect(onCitationClick).toHaveBeenCalledWith("judges.5.17")
    })

    it("renders plain text when there are no citations", () => {
        const { container } = render(
            <DefinitionText text={text} onCitationClick={vi.fn()} />,
        )
        expect(container.querySelector(".dict-citation-link")).toBeNull()
        expect(container.textContent).toContain("1 Sam. vi. 7")
    })

    it("still explains abbreviations around a citation", () => {
        // "lit." carries a hover expansion; the citation must not break it
        const { container } = render(
            <DefinitionText
                text="lit. the same; Jud. v. 17"
                citations={[{ text: "Jud. v. 17", key: "judges.5.17" }]}
                onCitationClick={vi.fn()}
            />,
        )
        const abbr = container.querySelector("abbr.dict-abbr")
        expect(abbr?.textContent).toBe("lit.")
        expect(
            container.querySelector(".dict-citation-link")?.textContent,
        ).toBe("Jud. v. 17")
    })
})
