import { describe, expect, it } from "vitest"
import { render } from "@testing-library/react"
import { CoverageText, CoverageLegend } from "./CoverageText"
import { TokenCoverage } from "../api/DictionaryApi"

describe("CoverageText", () => {
    it("wraps each token in its status class, keeping the punctuation", () => {
        const tokens: TokenCoverage[] = [
            { start: 0, length: 6, status: "entry" },
            { start: 8, length: 5, status: "none" },
        ]
        const { container } = render(
            <CoverageText text="Moddey, xyzzy!" tokens={tokens} />,
        )

        expect(container.textContent).toBe("Moddey, xyzzy!")
        expect(
            container.querySelector(".dict-coverage-entry")?.textContent,
        ).toBe("Moddey")
        expect(
            container.querySelector(".dict-coverage-none")?.textContent,
        ).toBe("xyzzy")
    })

    /** a word only a printed list names is marked as such, not as a miss: it is
     * the one thing that documents such a word */
    it("marks a word-list-only token with its own status", () => {
        const { container } = render(
            <CoverageText
                text="Blughtyn"
                tokens={[{ start: 0, length: 8, status: "list" }]}
            />,
        )

        expect(
            container.querySelector(".dict-coverage-list")?.textContent,
        ).toBe("Blughtyn")
        expect(container.querySelector(".dict-coverage-none")).toBeNull()
    })
})

describe("CoverageLegend", () => {
    it("counts tokens across all lines", () => {
        const coverage = new Map<string, TokenCoverage[]>([
            ["a", [{ start: 0, length: 1, status: "entry" }]],
            [
                "b",
                [
                    { start: 0, length: 1, status: "entry" },
                    { start: 2, length: 1, status: "none" },
                ],
            ],
        ])
        const { container } = render(
            <CoverageLegend coverage={coverage} onClose={() => {}} />,
        )

        expect(container.textContent).toContain("2 (66.7%)")
        expect(container.textContent).toContain("1 (33.3%)")
    })

    /** the word-list row is counted like any other, not dropped into a key the
     * legend never declared */
    it("counts word-list-only tokens in their own row", () => {
        const coverage = new Map<string, TokenCoverage[]>([
            [
                "a",
                [
                    { start: 0, length: 1, status: "list" },
                    { start: 2, length: 1, status: "entry" },
                ],
            ],
        ])
        const { container } = render(
            <CoverageLegend coverage={coverage} onClose={() => {}} />,
        )

        expect(container.textContent).toContain("word list only")
        expect(container.textContent).not.toContain("NaN")
        expect(container.textContent).toContain("1 (50.0%)")
    })
})
