import { afterEach, describe, expect, it } from "vitest"
import { cleanup, render, screen } from "@testing-library/react"
import { MemoryRouter } from "react-router-dom"
import { WordListCitations } from "./WordListCitations"
import type { WordListCitation } from "../api/DictionaryApi"

afterEach(cleanup)

const source = {
    listId: "morrison-plants",
    name: "Manx Plant Names",
    credit: "Sophia Morrison",
    date: "1908",
    documentIdent: "Manx-Plant-Names-by-Miss-S-Morrison",
    url: "https://corpus.gaelg.im/docs/Manx-Plant-Names-by-Miss-S-Morrison",
    citation: "Manx Wild Flowers, Peel, 1908",
}

const citation = (over: Partial<WordListCitation> = {}): WordListCitation => ({
    source,
    headword: "Keirn",
    gloss: "Ash (mountain)",
    binomial: "Pyrus aucuparia",
    ...over,
})

const show = (citations: WordListCitation[], word?: string) =>
    render(
        <MemoryRouter>
            <WordListCitations citations={citations} word={word} />
        </MemoryRouter>,
    )

describe("WordListCitations", () => {
    it("names what the list calls the plant, the Latin name and the list", () => {
        show([citation()], "keirn")
        expect(screen.getByText("Ash (mountain)")).toBeTruthy()
        expect(screen.getByText("Pyrus aucuparia")).toBeTruthy()
        expect(screen.getByText(/Manx Plant Names/)).toBeTruthy()
    })

    /** the naming and its source are one statement: it reads as a sentence
     * rather than as fields the reader has to reassemble */
    it("reads as one line, not as separate fields", () => {
        const { container } = show([citation()], "keirn")
        expect(
            container.querySelector(".dict-wordlist-line")?.textContent,
        ).toBe(
            "Ash (mountain), Pyrus aucuparia — Manx Plant Names, Sophia Morrison (1908)",
        )
    })

    /** on the page for Ollyssyn the reader can already see the word: saying it
     * back adds nothing */
    it("does not repeat the word whose page this is", () => {
        show(
            [citation({ headword: "Ollyssyn", gloss: "Alexanders" })],
            "Ollyssyn",
        )
        expect(screen.queryByText("Ollyssyn")).toBeNull()
        expect(screen.getByText(/Alexanders/)).toBeTruthy()
    })

    /** a hyphenated head reached by its folded spelling is the same head */
    it("treats a hyphenated head as the word it was reached by", () => {
        show([citation({ headword: "Lus-ny-Geayee" })], "lus ny geayee")
        expect(screen.queryByText(/^as /)).toBeNull()
    })

    /** the citation has to be checkable: the reader gets to the page it was read
     * off rather than taking our word for it */
    it("links the corpus document it was transcribed from", () => {
        show([citation()])
        expect(screen.getByRole("link").getAttribute("href")).toBe(
            "/docs/Manx-Plant-Names-by-Miss-S-Morrison",
        )
    })

    /** a word no list names gets no heading at all, rather than an empty one */
    it("says nothing when no list names the word", () => {
        const { container } = show([])
        expect(container.textContent).toBe("")
    })

    /** the page's own typo stands in the quotation; its note reads it back */
    it("shows a print correction as a note beside the printed wording", () => {
        show([
            citation({
                gloss: "Anemome (wood)",
                note: "the page prints 'Anemome (wood)'; read 'Anemone (wood)'",
            }),
        ])
        expect(screen.getByText("Anemome (wood)")).toBeTruthy()
        expect(screen.getByText(/read 'Anemone \(wood\)'/)).toBeTruthy()
    })

    /** one name, two plants: the page named both, so both are shown */
    it("keeps every plant a name was printed against", () => {
        show(
            [
                citation({ headword: "Aghaue", gloss: "Hemlock" }),
                citation({ headword: "Aghaue", gloss: "Water hemlock" }),
            ],
            "aghaue",
        )
        expect(screen.getByText("Hemlock")).toBeTruthy()
        expect(screen.getByText("Water hemlock")).toBeTruthy()
    })

    /** reached by the bare word, the page's own head is a different spelling —
     * saying so keeps the citation from quietly answering another word */
    it("says the printed head where it differs from the word looked up", () => {
        show([citation({ headword: "Yn luss", gloss: "Vervain" })], "luss")
        expect(screen.getByText("Yn luss")).toBeTruthy()
        expect(screen.getByText(/^as /)).toBeTruthy()
    })

    /** not every line names a species */
    it("omits the Latin name where the list gives none", () => {
        show([
            citation({
                headword: "Smeyr",
                gloss: "Blackberry",
                binomial: null,
            }),
        ])
        expect(screen.queryByText("Pyrus aucuparia")).toBeNull()
        expect(screen.getByText("Blackberry")).toBeTruthy()
    })
})
