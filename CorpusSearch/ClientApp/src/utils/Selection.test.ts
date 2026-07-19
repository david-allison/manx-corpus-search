import { afterEach, describe, expect, it } from "vitest"
import { getSelectedWordOrPhrase } from "./Selection"

afterEach(() => {
    document.body.innerHTML = ""
})

/** A collapsed caret in `node` at `offset`, as a click places one */
const caretAt = (node: Node, offset: number): Selection =>
    ({
        rangeCount: 1,
        toString: () => "",
        anchorNode: node,
        anchorOffset: offset,
    }) as unknown as Selection

/** A revealed correction, as DiffedLine renders it:
 * "cloan ny ~moadea~ moddee[±]!" */
const revealedLine = () => {
    document.body.innerHTML =
        '<div><span>cloan ny </span><span class="part-removed">moadea</span> ' +
        '<span class="doc-correction part-added">moddee</span>' +
        "<button>±</button><span>!</span></div>"
    return document.body
}

describe("a click in a diffed line", () => {
    it("reads the corrected text around unchanged words", () => {
        const line = revealedLine()
        const cloan = line.querySelector("span")!.firstChild!
        expect(getSelectedWordOrPhrase(caretAt(cloan, 2))).toBe("cloan")
    })

    it("reads the correction when the click lands on it", () => {
        const line = revealedLine()
        const moddee = line.querySelector(".doc-correction")!.firstChild!
        // the struck "moadea" beside it is not part of the corrected reading
        expect(getSelectedWordOrPhrase(caretAt(moddee, 3))).toBe("moddee!")
    })

    it("reads the original when the click lands on struck text", () => {
        const line = revealedLine()
        const moadea = line.querySelector(".part-removed")!.firstChild!
        // the corrected "moddee" beside it is not part of the original reading
        expect(getSelectedWordOrPhrase(caretAt(moadea, 3))).toBe("moadea")
    })
})
