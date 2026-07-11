import { afterEach, describe, expect, it } from "vitest"
import { cleanup, fireEvent, render, screen } from "@testing-library/react"
import { SeeMore } from "./SeeMore"

afterEach(cleanup)

// jsdom performs no layout: fake the heights driving the overflow detection
const setHeights = (scrollHeight: number, clientHeight: number) => {
    Object.defineProperty(HTMLElement.prototype, "scrollHeight", {
        configurable: true,
        get: () => scrollHeight,
    })
    Object.defineProperty(HTMLElement.prototype, "clientHeight", {
        configurable: true,
        get: () => clientHeight,
    })
}

afterEach(() => {
    Reflect.deleteProperty(HTMLElement.prototype, "scrollHeight")
    Reflect.deleteProperty(HTMLElement.prototype, "clientHeight")
})

describe("SeeMore", () => {
    it("collapses overflowing content behind a 'See more' toggle", () => {
        setHeights(200, 80)
        const { container } = render(<SeeMore>a long description</SeeMore>)

        expect(container.querySelector(".see-more-clamped")).not.toBeNull()
        screen.getByRole("button", { name: /See more/ })
    })

    it("expands on click, and collapses again", () => {
        setHeights(200, 80)
        const { container } = render(<SeeMore>a long description</SeeMore>)

        fireEvent.click(screen.getByRole("button", { name: /See more/ }))
        expect(container.querySelector(".see-more-clamped")).toBeNull()
        const less = screen.getByRole("button", { name: /See less/ })
        expect(less.getAttribute("aria-expanded")).toBe("true")

        fireEvent.click(less)
        expect(container.querySelector(".see-more-clamped")).not.toBeNull()
        screen.getByRole("button", { name: /See more/ })
    })

    it("renders content that fits without a toggle", () => {
        setHeights(80, 80)
        render(<SeeMore>a short description</SeeMore>)

        expect(screen.queryByRole("button")).toBeNull()
    })
})
