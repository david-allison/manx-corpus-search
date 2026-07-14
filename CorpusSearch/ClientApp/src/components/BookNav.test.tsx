import { afterEach, expect, it, vi } from "vitest"
import { cleanup, fireEvent, render, screen } from "@testing-library/react"
import { BookNav } from "./BookNav"
import { BookSegment } from "../utils/BookSegments"

afterEach(cleanup)

const segments: BookSegment[] = [
    { book: "genesis", label: "Genesis", start: 0 },
    { book: "exodus", label: "Exodus", start: 100 },
    { book: "leviticus", label: "Leviticus", start: 200 },
]

it("offers the neighbouring books around the picker", () => {
    const onSelect = vi.fn()
    render(
        <BookNav segments={segments} activeBook="exodus" onSelect={onSelect} />,
    )

    fireEvent.click(screen.getByRole("button", { name: /Genesis/ }))
    expect(onSelect).toHaveBeenCalledWith("genesis")

    fireEvent.click(screen.getByRole("button", { name: /Leviticus/ }))
    expect(onSelect).toHaveBeenCalledWith("leviticus")
})

it("selects a book from the list", () => {
    const onSelect = vi.fn()
    render(
        <BookNav
            segments={segments}
            activeBook="genesis"
            onSelect={onSelect}
        />,
    )

    fireEvent.change(screen.getByRole("combobox", { name: "Book" }), {
        target: { value: "leviticus" },
    })
    expect(onSelect).toHaveBeenCalledWith("leviticus")
})

it("hides the arrow past either end", () => {
    render(
        <BookNav segments={segments} activeBook="genesis" onSelect={vi.fn()} />,
    )
    const [previous, next] = screen.getAllByRole<HTMLButtonElement>("button")
    expect(previous.disabled).toBe(true)
    expect(next.disabled).toBe(false)
})
