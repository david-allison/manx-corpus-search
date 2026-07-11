import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { cleanup, fireEvent, render, screen } from "@testing-library/react"
import { DictionaryLink } from "./DictionaryLink"

// jsdom has no layout: fake a 20px line-height and let each test set the
// measured content height
let scrollHeight = 0
beforeEach(() => {
    Object.defineProperty(HTMLElement.prototype, "scrollHeight", {
        configurable: true,
        get: () => scrollHeight,
    })
    vi.spyOn(window, "getComputedStyle").mockReturnValue({
        lineHeight: "20px",
    } as CSSStyleDeclaration)
})
afterEach(() => {
    // @ts-expect-error restores the Element.prototype getter
    delete HTMLElement.prototype.scrollHeight
    vi.restoreAllMocks()
    cleanup()
})

const renderStrip = (entries: string[]) =>
    render(
        <DictionaryLink
            query="as"
            dictionaries={{ Cregeen: { entries, allowLookup: false } }}
        />,
    )

describe("DictionaryLink clamping", () => {
    /** the clampable paragraph (heading + text) containing the entry text */
    const entryOf = (text: HTMLElement) => text.closest(".dict-strip-entry")!

    it("shows short entries in full, without a toggle", () => {
        scrollHeight = 40 // 2 lines
        renderStrip(["conj. and."])
        expect(screen.queryByRole("button")).toBeNull()
        expect(
            entryOf(screen.getByText("1) conj. and.")).classList,
        ).not.toContain("dict-strip-entry-clamped")
    })

    it("clamps long entries behind a toggle", () => {
        scrollHeight = 200 // 10 lines
        renderStrip(["adv. as, when equality is signified…"])
        expect(screen.getByRole("button").textContent).toBe("show full entry ▾")
        expect(
            entryOf(screen.getByText(/when equality is signified/)).classList,
        ).toContain("dict-strip-entry-clamped")
    })

    it("expands and collapses on toggle", () => {
        scrollHeight = 200
        renderStrip(["adv. as, when equality is signified…"])
        const button = screen.getByRole("button")

        fireEvent.click(button)
        expect(button.textContent).toBe("show less ▴")
        expect(button.getAttribute("aria-expanded")).toBe("true")
        expect(
            entryOf(screen.getByText(/when equality is signified/)).classList,
        ).not.toContain("dict-strip-entry-clamped")

        fireEvent.click(button)
        expect(button.textContent).toBe("show full entry ▾")
        expect(
            entryOf(screen.getByText(/when equality is signified/)).classList,
        ).toContain("dict-strip-entry-clamped")
    })
})
