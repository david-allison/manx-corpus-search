import { afterEach, describe, expect, it, vi } from "vitest"
import { cleanup, fireEvent, render } from "@testing-library/react"
import { LineText } from "./LineText"

// vitest globals are off, so testing-library does not clean up by itself; without this,
// document-wide queries see the renders of earlier tests
afterEach(cleanup)

const renderDiff = (
    original: string,
    text: string,
    extra?: Partial<Parameters<typeof LineText>[0]>,
) =>
    render(
        <LineText
            text={text}
            original={original}
            shouldHighlight={true}
            translations={[]}
            query=""
            {...extra}
        />,
    )

describe("a corrected line", () => {
    it("diffs a word the correction only added characters to inline", () => {
        const { container } = renderDiff(
            "cha nel eh er yarrood me.",
            "cha nel eh er yarrood mee.",
        )
        expect(container.querySelector("button")).toBeNull()
        const added = [...container.querySelectorAll(".part-added")]
        expect(added.map((x) => x.textContent)).toEqual(["e"])
        expect(container.textContent).toBe("cha nel eh er yarrood mee.")
    })

    it("diffs a word the correction only removed characters from inline", () => {
        const { container } = renderDiff("Cummal seoise", "Cummal seose")
        expect(container.querySelector("button")).toBeNull()
        const removed = [...container.querySelectorAll(".part-removed")]
        expect(removed.map((x) => x.textContent)).toEqual(["i"])
        // the removed character is struck, not hidden: both spellings read
        expect(container.textContent).toBe("Cummal seoise")
    })

    it("shows only the correction of a rewritten word", () => {
        const { container, queryByText } = renderDiff(
            "cloan ny moadea!",
            "cloan ny moddee!",
        )
        // a character diff would render the unreadable jumble "moaddeae";
        // the "±" chip after the word reveals the original
        expect(container.textContent).toBe("cloan ny moddee±!")
        expect(queryByText("moadea")).toBeNull()
        expect(container.querySelector(".doc-correction")?.textContent).toBe(
            "moddee",
        )
        const chip = container.querySelector("button.doc-correction-marker")
        expect(chip?.getAttribute("aria-expanded")).toBe("false")
    })

    it("reveals and hides a rewritten word's original from the chip", () => {
        const { container, getByRole } = renderDiff(
            "cloan ny moadea!",
            "cloan ny moddee!",
        )
        const chip = getByRole("button", { name: "Show original text" })
        fireEvent.click(chip)
        expect(chip.getAttribute("aria-expanded")).toBe("true")
        const removed = container.querySelector(".part-removed")
        expect(removed?.textContent).toBe("moadea")
        expect(container.textContent).toBe("cloan ny moadea moddee±!")

        fireEvent.click(chip)
        expect(chip.getAttribute("aria-expanded")).toBe("false")
        expect(container.textContent).toBe("cloan ny moddee±!")
    })

    it("opens the dictionary from the word but not from the chip", () => {
        const onWordClick = vi.fn()
        const { getByRole, getByText } = renderDiff(
            "cloan ny moadea!",
            "cloan ny moddee!",
            { onWordClick },
        )
        fireEvent.click(getByRole("button"))
        expect(onWordClick).not.toHaveBeenCalled()

        // the rewritten word itself is plain text: tapping it looks up
        // the dictionary like any other word (a regression in #310's redesign)
        fireEvent.click(getByText("moddee"))
        expect(onWordClick).toHaveBeenCalledOnce()
    })

    it("marks server match ranges against the corrected text", () => {
        // "moddee" is chars 9-15 of the correction; "gys" follows at 16-19
        const { container } = renderDiff(
            "cloan ny moadea gys",
            "cloan ny moddee gys",
            { highlights: [{ start: 9, end: 15 }] },
        )
        const mark = container.querySelector("mark.textHighlight")
        expect(mark?.textContent).toBe("moddee")
        expect(mark?.closest(".doc-correction")).not.toBeNull()
    })

    it("marks a match after an inline diff at its corrected offset", () => {
        // the correction removes " " from "vaik ym": later offsets shift left
        const { container } = renderDiff(
            "cha vaik ym oo",
            "cha vaikym oo",
            // "oo" is chars 11-13 of the corrected text
            { highlights: [{ start: 11, end: 13 }] },
        )
        const mark = container.querySelector("mark.textHighlight")
        expect(mark?.textContent).toBe("oo")
    })
})
