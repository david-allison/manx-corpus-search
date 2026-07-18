import { afterEach, describe, expect, it } from "vitest"
import { cleanup, fireEvent, render, screen } from "@testing-library/react"
import { useTappableAbbrs } from "./useTappableAbbrs"

afterEach(cleanup)

const Page = () => {
    useTappableAbbrs()
    return (
        <div>
            <abbr title="plural">pl.</abbr>
            <abbr title="singular">s.</abbr>
            <p>plain text</p>
        </div>
    )
}

describe("useTappableAbbrs", () => {
    it("opens an abbreviation's title on tap, and closes it on the next", () => {
        render(<Page />)
        const abbr = screen.getByText("pl.")

        fireEvent.click(abbr)
        expect(abbr.classList.contains("abbr-open")).toBe(true)

        fireEvent.click(abbr)
        expect(abbr.classList.contains("abbr-open")).toBe(false)
    })

    it("one bubble at a time: tapping another closes the first", () => {
        render(<Page />)
        const first = screen.getByText("pl.")
        const second = screen.getByText("s.")

        fireEvent.click(first)
        fireEvent.click(second)

        expect(first.classList.contains("abbr-open")).toBe(false)
        expect(second.classList.contains("abbr-open")).toBe(true)
    })

    it("a tap anywhere else puts the bubble away", () => {
        render(<Page />)
        const abbr = screen.getByText("pl.")

        fireEvent.click(abbr)
        fireEvent.click(screen.getByText("plain text"))

        expect(abbr.classList.contains("abbr-open")).toBe(false)
    })
})
