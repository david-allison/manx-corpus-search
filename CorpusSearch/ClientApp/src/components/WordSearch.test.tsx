import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { cleanup, fireEvent, render, screen } from "@testing-library/react"
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom"
import { WordSearch } from "./WordSearch"

const fetchMock = vi.fn<typeof fetch>()
vi.stubGlobal("fetch", fetchMock)

const respond = (suggestions: unknown) =>
    fetchMock.mockResolvedValue({
        ok: true,
        json: () => Promise.resolve(suggestions),
    } as Response)

beforeEach(() => {
    fetchMock.mockReset()
    respond({ words: [], fuzzy: false })
})
afterEach(cleanup)

const Where = () => <span data-testid="at">{useLocation().pathname}</span>

const renderSearch = (props: Parameters<typeof WordSearch>[0]) =>
    render(
        <MemoryRouter initialEntries={["/dictionary"]}>
            <WordSearch {...props} />
            <Routes>
                <Route path="*" element={<Where />} />
            </Routes>
        </MemoryRouter>,
    )

const lookUp = (word: string) => {
    fireEvent.change(screen.getByLabelText("Look up a Manx word"), {
        target: { value: word },
    })
    fireEvent.click(screen.getByRole("button", { name: "Look up" }))
}

const at = () => screen.getByTestId("at").textContent

describe("WordSearch", () => {
    it("opens the word that was typed", () => {
        renderSearch({})

        lookUp("billey")

        expect(at()).toBe("/dictionary/billey")
    })

    /** A word looked up from inside Cregeen should stay in Cregeen: widening to
     * every book is a thing the reader would have asked for */
    it("keeps the book being read", () => {
        renderSearch({ dict: "cregeen" })

        lookUp("billey")

        expect(at()).toBe("/dictionary/in/cregeen/billey")
    })

    it("goes nowhere on an empty look-up", () => {
        renderSearch({ word: "caag" })

        lookUp("   ")

        expect(at()).toBe("/dictionary")
    })

    it("starts on the page's own word, so it can be edited rather than retyped", () => {
        renderSearch({ word: "caag" })

        expect(
            screen.getByLabelText<HTMLInputElement>("Look up a Manx word")
                .value,
        ).toBe("caag")
    })

    /** The walk changes the word under the box without remounting it */
    it("follows the word when the page moves under it", () => {
        const { rerender } = renderSearch({ word: "caag" })

        rerender(
            <MemoryRouter initialEntries={["/dictionary"]}>
                <WordSearch word="caag airh" />
            </MemoryRouter>,
        )

        expect(
            screen.getByLabelText<HTMLInputElement>("Look up a Manx word")
                .value,
        ).toBe("caag airh")
    })

    it("offers the way back to the index only where there is one", () => {
        renderSearch({ word: "caag", indexUrl: "/dictionary/browse/cregeen/c" })
        expect(
            screen.getByLabelText("Back to the index").getAttribute("href"),
        ).toBe("/dictionary/browse/cregeen/c")

        cleanup()
        // the index itself is already there
        renderSearch({})
        expect(screen.queryByLabelText("Back to the index")).toBeNull()
    })
})

describe("WordSearch suggestions", () => {
    const twoWords = {
        words: [
            { word: "dy", attested: true },
            { word: "dooinney", attested: true },
        ],
        fuzzy: false,
    }

    const type = (value: string) =>
        fireEvent.change(screen.getByLabelText("Look up a Manx word"), {
            target: { value },
        })

    it("offers a few entries while typing, tappable into their pages", async () => {
        respond(twoWords)
        renderSearch({})

        type("d")

        expect(await screen.findByRole("option", { name: "dy" })).toBeTruthy()
        fireEvent.click(screen.getByRole("option", { name: "dooinney" }))
        expect(at()).toBe("/dictionary/dooinney")
    })

    it("walks the offers with the arrows and takes the marked one", async () => {
        respond(twoWords)
        renderSearch({})
        const input = screen.getByLabelText("Look up a Manx word")
        type("d")
        await screen.findByRole("option", { name: "dy" })

        fireEvent.keyDown(input, { key: "ArrowDown" })
        fireEvent.keyDown(input, { key: "ArrowDown" })
        fireEvent.click(screen.getByRole("button", { name: "Look up" }))

        expect(at()).toBe("/dictionary/dooinney")
    })

    it("greys an offer no text says", async () => {
        respond({
            words: [{ word: "ynrican", attested: false }],
            fuzzy: false,
        })
        renderSearch({})

        type("ynr")

        const offer = await screen.findByRole("option", { name: "ynrican" })
        expect(offer.className).toContain("dict-unattested")
    })

    it("says when the offers are near spellings, not matches", async () => {
        respond({ words: [{ word: "dooinney", attested: true }], fuzzy: true })
        renderSearch({})

        type("dooiney")

        expect(await screen.findByText(/Near spellings/)).toBeTruthy()
    })

    it("puts the offers away on Escape", async () => {
        respond(twoWords)
        renderSearch({})
        const input = screen.getByLabelText("Look up a Manx word")
        type("d")
        await screen.findByRole("option", { name: "dy" })

        fireEvent.keyDown(input, { key: "Escape" })

        expect(screen.queryByRole("option")).toBeNull()
    })

    it("offers nothing for the page's own word: it needs no completing", async () => {
        renderSearch({ word: "caag" })

        // past the debounce: nothing was even asked
        await new Promise((resolve) => setTimeout(resolve, 250))
        expect(fetchMock).not.toHaveBeenCalled()
    })
})
