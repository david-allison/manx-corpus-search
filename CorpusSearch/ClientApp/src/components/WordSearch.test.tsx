import { afterEach, describe, expect, it } from "vitest"
import { cleanup, fireEvent, render, screen } from "@testing-library/react"
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom"
import { WordSearch } from "./WordSearch"

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
