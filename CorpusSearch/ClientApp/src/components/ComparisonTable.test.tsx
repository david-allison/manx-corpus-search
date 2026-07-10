import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import {
    act,
    cleanup,
    fireEvent,
    render,
    screen,
    waitFor,
} from "@testing-library/react"
import { ComparisonTable, segmentChunks } from "./ComparisonTable"
import { SearchWorkResponse, SearchWorkResult } from "../api/SearchWorkApi"
import { Ref } from "react"
import { Player } from "./YouTuber"

const mockPlayer = vi.hoisted(() => ({
    seek: vi.fn(),
    getCurrentTime: vi.fn((): number | null => null),
}))

vi.mock("./YouTuber", async () => {
    const { useImperativeHandle } = await import("react")
    const MockYouTuber = ({ ref }: { videoId: string; ref?: Ref<Player> }) => {
        useImperativeHandle(ref, () => mockPlayer)
        return null
    }
    return { default: MockYouTuber }
})

const mockFetchLines = vi.hoisted(() => vi.fn())

vi.mock("../api/SearchWorkApi", async (importOriginal) => ({
    ...(await importOriginal<typeof import("../api/SearchWorkApi")>()),
    fetchLines: mockFetchLines,
}))

const mockDictionaryLookup = vi.hoisted(() => vi.fn())

vi.mock("../api/DictionaryApi", () => ({
    manxDictionaryLookup: mockDictionaryLookup,
}))

const mockGetSelectedWordOrPhrase = vi.hoisted(() => vi.fn())
const mockGetWordAtPoint = vi.hoisted(() => vi.fn())

vi.mock("../utils/Selection", () => ({
    getSelectedWordOrPhrase: mockGetSelectedWordOrPhrase,
    getWordAtPoint: mockGetWordAtPoint,
}))

// vitest globals are off, so testing-library does not clean up by itself; without this,
// document-wide queries (getByText) see the tables of earlier tests
afterEach(cleanup)

const line = (overrides: Partial<SearchWorkResult>): SearchWorkResult => ({
    english: "",
    manx: "",
    page: "",
    csvLineNumber: 1,
    date: "",
    notes: "",
    ...overrides,
})

const response = (results: SearchWorkResult[]): SearchWorkResponse => ({
    results,
    title: "Test Document",
    translations: {},
    totalMatches: results.length,
    timeTaken: "0s",
    numberOfResults: results.length,
    notes: "",
    source: "",
    sourceLinks: null,
    pdfLink: undefined,
    googleBooksId: undefined,
    gitHubLink: "",
    original: "Manx",
})

const renderTable = (
    results: SearchWorkResult[],
    overrides?: Partial<Parameters<typeof ComparisonTable>[0]>,
) =>
    render(
        <ComparisonTable
            response={response(results)}
            value="chengey"
            highlightManx={true}
            highlightEnglish={false}
            manxVisible={true}
            englishVisible={true}
            {...overrides}
        />,
    )

describe("ComparisonTable highlighting", () => {
    it("highlights the server-provided ranges, not the literal query", () => {
        // the query 'chengey' does not occur in the raw text: the server matched the
        // diacritic-folded normalized text and returned offsets (#40)
        const { container } = renderTable([
            line({
                manx: "Ta çhengey aym",
                manxHighlights: [{ start: 3, end: 10 }],
            }),
        ])
        const marks = container.querySelectorAll("mark.textHighlight")
        expect(Array.from(marks).map((x) => x.textContent)).toEqual(["çhengey"])
    })

    it("renders no highlight when the server provided no ranges", () => {
        const { container } = renderTable([line({ manx: "Ta çhengey aym" })])
        expect(container.querySelector("mark.textHighlight")).toBeNull()
    })

    it("highlights the correct segment of a multi-line cell", () => {
        // offsets are into the full cell; rendering splits it on "\n"
        const { container } = renderTable([
            line({
                manx: "abc\ncre def",
                manxHighlights: [{ start: 4, end: 7 }],
            }),
        ])
        const marks = container.querySelectorAll("mark.textHighlight")
        expect(Array.from(marks).map((x) => x.textContent)).toEqual(["cre"])
    })

    it("highlights multiple matches in a line", () => {
        const { container } = renderTable([
            line({
                manx: "cre ta shen cre",
                manxHighlights: [
                    { start: 0, end: 3 },
                    { start: 12, end: 15 },
                ],
            }),
        ])
        const marks = container.querySelectorAll("mark.textHighlight")
        expect(Array.from(marks).map((x) => x.textContent)).toEqual([
            "cre",
            "cre",
        ])
    })

    it("does not use the manx ranges on the english column", () => {
        const { container } = renderTable([
            line({
                manx: "Ta çhengey aym",
                english: "I have a tongue",
                manxHighlights: [{ start: 3, end: 10 }],
            }),
        ])
        const marks = container.querySelectorAll("mark")
        expect(Array.from(marks).map((x) => x.textContent)).toEqual(["çhengey"])
    })
})

describe("diffed lines (#310)", () => {
    // "chengey" -> "çhengey": the diff removes "c" and adds "ç"
    const correctedLine = line({
        manx: "Ta çhengey aym",
        manxOriginal: "Ta chengey aym",
        manxHighlights: [{ start: 3, end: 10 }],
    })

    it("still renders the diff parts", () => {
        const { container } = renderTable([correctedLine])
        expect(container.querySelector(".part-added")?.textContent).toBe("ç")
        expect(container.querySelector(".part-removed")?.textContent).toBe("c")
    })

    it("highlights the server-provided ranges within the diff", () => {
        // the range spans the added "ç" and the unchanged "hengey"
        const { container } = renderTable([correctedLine])
        const marks = container.querySelectorAll("mark.textHighlight")
        expect(
            Array.from(marks)
                .map((x) => x.textContent)
                .join(""),
        ).toBe("çhengey")
    })

    it("does not highlight removed text", () => {
        // "cre va shen" -> "cre shen": the match "cre" stops before the removal
        const { container } = renderTable([
            line({
                manx: "cre shen",
                manxOriginal: "cre va shen",
                manxHighlights: [{ start: 0, end: 3 }],
            }),
        ])
        const marks = container.querySelectorAll("mark.textHighlight")
        expect(Array.from(marks).map((x) => x.textContent)).toEqual(["cre"])
        expect(container.querySelector(".part-removed mark")).toBeNull()
    })

    it("renders no highlight when highlighting is toggled off", () => {
        const { container } = renderTable([correctedLine], {
            highlightManx: false,
        })
        expect(container.querySelector("mark.textHighlight")).toBeNull()
        expect(container.querySelector(".part-added")).not.toBeNull()
    })
})

describe("ComparisonTable video (#200)", () => {
    // a 'subStart' of 0 is valid: the first subtitle of a video starts at 0s
    const videoLine = line({ manx: "moghrey mie", subStart: 0, subEnd: 5 })
    const renderVideoTable = () =>
        renderTable([videoLine], {
            response: {
                ...response([videoLine]),
                source: "https://www.youtube.com/watch?v=abc123",
            },
        })

    beforeEach(() => {
        vi.useFakeTimers()
        mockPlayer.seek.mockClear()
        mockPlayer.getCurrentTime.mockReturnValue(null)
    })

    afterEach(() => {
        vi.useRealTimers()
    })

    it("seeks to 0 when playing a line with a subStart of 0", () => {
        const { container } = renderVideoTable()
        fireEvent.click(container.querySelector(".doc-play-btn")!)
        expect(mockPlayer.seek).toHaveBeenCalledWith(0)
    })

    it("shows the play tooltip for a subStart of 0", () => {
        const { container } = renderVideoTable()
        const button = container.querySelector(".doc-play-btn")
        expect(button?.getAttribute("title")).toBe("Play from 0:00")
    })

    it("does not highlight a line starting at 0 while the video never loaded", async () => {
        const { container } = renderVideoTable()
        // let the playback-position polling fire while getCurrentTime() is null
        await act(async () => {
            await vi.advanceTimersByTimeAsync(300)
        })
        expect(container.querySelector(".doc-row-playing")).toBeNull()
    })

    it("highlights a line starting at 0 once the video plays at 0s", async () => {
        mockPlayer.getCurrentTime.mockReturnValue(0)
        const { container } = renderVideoTable()
        await act(async () => {
            await vi.advanceTimersByTimeAsync(300)
        })
        expect(container.querySelector(".doc-row-playing")).not.toBeNull()
    })
})

describe("YouTube source validation (code scanning #5, #6)", () => {
    const renderWithSource = (source: string) => {
        const videoLine = line({ manx: "moghrey mie", subStart: 0, subEnd: 5 })
        return renderTable([videoLine], {
            response: { ...response([videoLine]), source },
        })
    }

    it("embeds a youtube.com watch URL", () => {
        const { container } = renderWithSource(
            "https://youtube.com/watch?v=abc123",
        )
        expect(container.querySelector(".video-dock")).not.toBeNull()
    })

    it.each([
        "https://www.youtube.evil.com/watch?v=abc123",
        "https://youtube.com.evil.com/watch?v=abc123",
        "http://www.youtube.com/watch?v=abc123",
        "https://www.youtube.com/watch",
        "not a url",
    ])("does not embed %s", (source) => {
        const { container } = renderWithSource(source)
        expect(container.querySelector(".video-dock")).toBeNull()
        expect(container.querySelector(".doc-play-btn")).toBeNull()
    })
})

describe("context expansion (#286)", () => {
    const gapResults = () => [
        line({ manx: "top match", csvLineNumber: 10 }),
        line({ manx: "bottom match", csvLineNumber: 50 }),
    ]
    const renderWithGap = (
        overrides?: Partial<Parameters<typeof ComparisonTable>[0]>,
    ) =>
        renderTable(gapResults(), {
            docIdent: "doc",
            response: {
                ...response(gapResults()),
                firstLineNumber: 10,
                lastLineNumber: 50,
            },
            ...overrides,
        })

    beforeEach(() => {
        mockFetchLines.mockReset()
    })

    it("shows an expander between results with hidden lines", () => {
        const { container, getByText } = renderWithGap()
        expect(container.querySelectorAll(".doc-expand-row")).toHaveLength(1)
        expect(getByText("Show next 5 lines")).toBeTruthy()
        expect(getByText("Show previous 5 lines")).toBeTruthy()
    })

    it("shows no expander without a docIdent", () => {
        const { container } = renderTable(gapResults())
        expect(container.querySelector(".doc-expand-row")).toBeNull()
    })

    it("shows no expander when the 'Show context' option is off", () => {
        const { container } = renderWithGap({ expandContext: false })
        expect(container.querySelector(".doc-expand-row")).toBeNull()
    })

    it("shows no expander for a '*' search", () => {
        const { container } = renderTable(gapResults(), {
            docIdent: "doc",
            response: { ...response(gapResults()), totalMatches: null },
        })
        expect(container.querySelector(".doc-expand-row")).toBeNull()
    })

    it("shows no expander between adjacent lines", () => {
        const results = [
            line({ manx: "a", csvLineNumber: 2 }),
            line({ manx: "b", csvLineNumber: 3 }),
        ]
        const { container } = renderTable(results, {
            docIdent: "doc",
            response: {
                ...response(results),
                firstLineNumber: 2,
                lastLineNumber: 3,
            },
        })
        expect(container.querySelector(".doc-expand-row")).toBeNull()
    })

    it("expands downwards into the gap", async () => {
        mockFetchLines.mockResolvedValue({
            lines: [11, 12, 13, 14, 15].map((n) =>
                line({ manx: `context ${n}`, csvLineNumber: n }),
            ),
            totalInRange: 39,
        })
        const { container, getByText } = renderWithGap()

        fireEvent.click(getByText("Show next 5 lines"))

        expect(mockFetchLines).toHaveBeenCalledWith({
            docIdent: "doc",
            start: 11,
            end: 49,
            limit: 5,
            fromEnd: false,
        })
        await waitFor(() => expect(getByText("context 15")).toBeTruthy())
        // the revealed lines are marked as context, and more lines remain hidden
        expect(container.querySelectorAll(".doc-row-context")).toHaveLength(5)
        expect(container.querySelectorAll(".doc-expand-row")).toHaveLength(1)
    })

    it("expands upwards into the gap", async () => {
        mockFetchLines.mockResolvedValue({
            lines: [45, 46, 47, 48, 49].map((n) =>
                line({ manx: `context ${n}`, csvLineNumber: n }),
            ),
            totalInRange: 39,
        })
        const { getByText } = renderWithGap()

        fireEvent.click(getByText("Show previous 5 lines"))

        expect(mockFetchLines).toHaveBeenCalledWith({
            docIdent: "doc",
            start: 11,
            end: 49,
            limit: 5,
            fromEnd: true,
        })
        await waitFor(() => expect(getByText("context 45")).toBeTruthy())
    })

    it("removes the expander when the gap is exhausted", async () => {
        mockFetchLines.mockResolvedValue({
            lines: [11, 12].map((n) =>
                line({ manx: `context ${n}`, csvLineNumber: n }),
            ),
            totalInRange: 2,
        })
        const { container, getByText } = renderWithGap()

        fireEvent.click(getByText("Show next 5 lines"))

        await waitFor(() =>
            expect(container.querySelector(".doc-expand-row")).toBeNull(),
        )
        expect(getByText("context 12")).toBeTruthy()
    })

    it("expands a small gap with a single click", async () => {
        // between lines 10 and 18 only 7 lines can hide: one button reveals them all
        const results = [
            line({ manx: "top match", csvLineNumber: 10 }),
            line({ manx: "bottom match", csvLineNumber: 18 }),
        ]
        mockFetchLines.mockResolvedValue({
            lines: [line({ manx: "context 11", csvLineNumber: 11 })],
            totalInRange: 1,
        })
        const { container, getByText } = renderTable(results, {
            docIdent: "doc",
            response: response(results),
        })

        fireEvent.click(getByText("Show context"))

        expect(mockFetchLines).toHaveBeenCalledWith({
            docIdent: "doc",
            start: 11,
            end: 17,
            limit: 7,
            fromEnd: false,
        })
        await waitFor(() =>
            expect(container.querySelector(".doc-expand-row")).toBeNull(),
        )
        expect(getByText("context 11")).toBeTruthy()
    })

    it("omits the count when a single line can hide", () => {
        const results = [line({ manx: "match", csvLineNumber: 3 })]
        const { getByText } = renderTable(results, {
            docIdent: "doc",
            response: {
                ...response(results),
                firstLineNumber: 2,
                lastLineNumber: 3,
            },
        })

        expect(getByText("Show previous line")).toBeTruthy()
    })

    it("only expands towards the results at the document bounds", () => {
        const results = [line({ manx: "only match", csvLineNumber: 20 })]
        const { container, queryByText, getByText } = renderTable(results, {
            docIdent: "doc",
            response: {
                ...response(results),
                firstLineNumber: 2,
                lastLineNumber: 40,
            },
        })

        // above the match: only 'previous'; below it: only 'next'
        expect(container.querySelectorAll(".doc-expand-row")).toHaveLength(2)
        expect(getByText("Show previous 5 lines")).toBeTruthy()
        expect(getByText("Show next 5 lines")).toBeTruthy()
        expect(queryByText("Show context")).toBeNull()
    })
})

describe("dictionary popup (#51)", () => {
    beforeEach(() => {
        mockDictionaryLookup.mockReset()
        // a jsdom click places no caret, so the word comes from the click
        // position: bypass the browser plumbing
        mockGetWordAtPoint.mockReturnValue("lhiam")
    })

    afterEach(() => {
        mockGetWordAtPoint.mockReset()
    })

    const openPopup = () => {
        const { getByText } = renderTable([line({ manx: "she lhiam eh" })])
        fireEvent.click(getByText("she lhiam eh"))
    }

    it("labels each entry with the dictionary defining it", async () => {
        mockDictionaryLookup.mockResolvedValue([
            {
                primaryWord: "lhiam",
                summary: "with me",
                dictionaryName: "Cregeen",
            },
            {
                primaryWord: "lhiam",
                summary: "to me, with me",
                dictionaryName: "J Kelly Manx to English",
            },
        ])
        openPopup()

        await screen.findByText("Cregeen")
        screen.getByText("J Kelly Manx to English")
        expect(document.querySelectorAll(".dict-popup-group")).toHaveLength(2)
        expect(screen.getAllByText("lhiam")).not.toHaveLength(0)
    })

    it("groups entries of one dictionary under a single header", async () => {
        mockDictionaryLookup.mockResolvedValue([
            {
                primaryWord: "goll",
                summary: "to go",
                dictionaryName: "Cregeen",
            },
            {
                primaryWord: "mygeayrt",
                summary: "about",
                dictionaryName: "Cregeen",
            },
        ])
        openPopup()

        await screen.findByText(/to go/)
        expect(screen.getAllByText("Cregeen")).toHaveLength(1)
        expect(document.querySelectorAll(".dict-popup-entry")).toHaveLength(2)
    })

    it("reports when no dictionary defines the word", async () => {
        mockDictionaryLookup.mockResolvedValue([])
        openPopup()

        await screen.findByText(/Could not find definition/)
    })
})

describe("dictionary popup on touch", () => {
    beforeEach(() => {
        mockDictionaryLookup.mockReset()
        mockDictionaryLookup.mockResolvedValue([])
        mockGetSelectedWordOrPhrase.mockReset()
        mockGetWordAtPoint.mockReset()
    })

    // a touch-originated click: a PointerEvent tap, which places no caret
    const tap = (element: Element) => {
        const event = new MouseEvent("click", {
            bubbles: true,
            cancelable: true,
            clientX: 12,
            clientY: 34,
        })
        Object.defineProperty(event, "pointerType", { value: "touch" })
        fireEvent(element, event)
    }

    it("opens the popup for the word under the tap", async () => {
        mockGetWordAtPoint.mockReturnValue("lhiam")
        const { getByText } = renderTable([line({ manx: "she lhiam eh" })])

        tap(getByText("she lhiam eh"))

        expect(mockGetWordAtPoint).toHaveBeenCalledWith(12, 34)
        await screen.findByRole("heading", { name: "lhiam" })
    })

    it("ignores a stale selection when tapping", () => {
        // e.g. text long-press-selected earlier: the tapped word wins
        mockGetWordAtPoint.mockReturnValue("lhiam")
        const { getByText } = renderTable([line({ manx: "she lhiam eh" })])
        const range = document.createRange()
        range.selectNodeContents(getByText("she lhiam eh"))
        window.getSelection()?.addRange(range)

        tap(getByText("she lhiam eh"))

        expect(mockGetWordAtPoint).toHaveBeenCalledWith(12, 34)
        expect(mockGetSelectedWordOrPhrase).not.toHaveBeenCalled()
        window.getSelection()?.removeAllRanges()
    })

    it("does not open the popup when no word is under the tap", () => {
        mockGetWordAtPoint.mockReturnValue(null)
        const { getByText } = renderTable([line({ manx: "she lhiam eh" })])

        tap(getByText("she lhiam eh"))

        expect(mockDictionaryLookup).not.toHaveBeenCalled()
        expect(screen.queryByRole("heading", { name: "lhiam" })).toBeNull()
    })

    it("uses the selection, not the position, for a mouse click with a caret", () => {
        mockGetSelectedWordOrPhrase.mockReturnValue("lhiam")
        const { getByText } = renderTable([line({ manx: "she lhiam eh" })])
        const range = document.createRange()
        range.selectNodeContents(getByText("she lhiam eh"))
        window.getSelection()?.addRange(range)

        fireEvent.click(getByText("she lhiam eh"))

        expect(mockGetSelectedWordOrPhrase).toHaveBeenCalled()
        expect(mockGetWordAtPoint).not.toHaveBeenCalled()
        window.getSelection()?.removeAllRanges()
    })

    it("is not closed again by the second click of a double-click", async () => {
        // the first click opens the popup; the second click of a double-click
        // lands on the backdrop and must not immediately close it
        const nowSpy = vi.spyOn(performance, "now")
        nowSpy.mockReturnValue(1000)
        mockGetWordAtPoint.mockReturnValue("lhiam")
        const { getByText } = renderTable([line({ manx: "she lhiam eh" })])
        fireEvent.click(getByText("she lhiam eh"))

        nowSpy.mockReturnValue(1100) // within the double-click window
        fireEvent.click(document.querySelector(".MuiBackdrop-root")!)
        expect(screen.queryByRole("heading", { name: "lhiam" })).not.toBeNull()

        nowSpy.mockReturnValue(2000) // a deliberate dismissal later
        fireEvent.click(document.querySelector(".MuiBackdrop-root")!)
        await waitFor(() =>
            expect(screen.queryByRole("heading", { name: "lhiam" })).toBeNull(),
        )
        nowSpy.mockRestore()
    })
})

describe("note rows", () => {
    const notedLine = () =>
        line({
            manx: "Geaylin yn Cholloo. [1]",
            english: "The headland on the Calf.",
            notes: "[1] Geaylin yn Cholloo - ‘headland on the Calf of Man’",
        })

    beforeEach(() => {
        mockGetSelectedWordOrPhrase.mockClear()
        mockGetWordAtPoint.mockClear()
    })

    it("shows a linked note by default", () => {
        const { container } = renderTable([notedLine()])
        expect(container.querySelector(".noteRow")).not.toBeNull()
    })

    it("hides a linked note when 'Show notes' is off", () => {
        const { container } = renderTable([notedLine()], { showNotes: false })
        expect(container.querySelector(".noteRow")).toBeNull()
    })

    it("reveals a hidden note via its marker, and hides it again", () => {
        const { container, getByTitle } = renderTable([notedLine()], {
            showNotes: false,
        })
        fireEvent.click(getByTitle("Show note"))
        expect(container.querySelector(".noteRow")).not.toBeNull()
        fireEvent.click(getByTitle("Hide note"))
        expect(container.querySelector(".noteRow")).toBeNull()
    })

    it("hides a shown note via its marker", () => {
        const { container } = renderTable([notedLine()])
        fireEvent.click(container.querySelector(".doc-note-marker")!)
        expect(container.querySelector(".noteRow")).toBeNull()
    })

    it("always shows a note without a marker in the text", () => {
        const unlinked = line({
            manx: "gyn cowrey",
            notes: "an editorial note",
        })
        const { container } = renderTable([unlinked], { showNotes: false })
        expect(container.querySelector(".noteRow")).not.toBeNull()
        expect(container.querySelector(".doc-note-marker")).toBeNull()
    })

    it("shows a note as-is when its marker is in a hidden column", () => {
        // the marker sits in the Manx text, but only English is displayed
        const { container } = renderTable([notedLine()], {
            showNotes: false,
            manxVisible: false,
        })
        expect(container.querySelector(".noteRow")).not.toBeNull()
    })

    it("shows a corrected (diffed) line's note as-is", () => {
        // the diff view renders plain text: there is no marker to click
        const diffed = line({
            manx: "kiartit [1]",
            manxOriginal: "kiartit[1]",
            notes: "[1] a note",
        })
        const { container } = renderTable([diffed], { showNotes: false })
        expect(container.querySelector(".noteRow")).not.toBeNull()
    })

    it("leaves markers as plain text on lines without notes", () => {
        const { container } = renderTable([line({ manx: "cowrey [1] elley" })])
        expect(container.querySelector(".doc-note-marker")).toBeNull()
        expect(container.textContent).toContain("[1]")
    })

    it("does not open the dictionary popup when clicking a marker", () => {
        const { container } = renderTable([notedLine()])
        fireEvent.click(container.querySelector(".doc-note-marker")!)
        expect(mockGetSelectedWordOrPhrase).not.toHaveBeenCalled()
        expect(mockGetWordAtPoint).not.toHaveBeenCalled()
    })

    it("keeps the note row off the Link column", () => {
        const noted = notedLine()
        const { container } = renderTable([noted], {
            response: {
                ...response([noted]),
                gitHubLink: "https://github.com/x/y",
            },
        })
        const cells = container.querySelectorAll(".noteRow td")
        // the note band spans the two language columns; the Link cell stays empty
        expect(cells).toHaveLength(2)
        expect(cells[0].getAttribute("colspan")).toBe("2")
        expect(cells[0].classList.contains("doc-note-band")).toBe(true)
        expect(cells[1].textContent).toBe("")
    })

    it("keeps highlight offsets correct around a marker", () => {
        // server offsets are into the full cell, including the marker
        const { container } = renderTable([
            line({
                manx: "abc [1] cre def",
                notes: "[1] a note",
                manxHighlights: [{ start: 8, end: 11 }],
            }),
        ])
        const marks = container.querySelectorAll("mark.textHighlight")
        expect(Array.from(marks).map((x) => x.textContent)).toEqual(["cre"])
    })
})

describe("segmentChunks", () => {
    it("shifts ranges into segment-local offsets", () => {
        // "abc\ncre def": segment 2 starts at offset 4
        expect(segmentChunks([{ start: 4, end: 7 }], 4, 7)).toEqual([
            { start: 0, end: 3 },
        ])
    })

    it("excludes ranges outside the segment", () => {
        expect(segmentChunks([{ start: 4, end: 7 }], 0, 3)).toEqual([])
    })

    it("clips ranges crossing the segment boundary", () => {
        expect(segmentChunks([{ start: 2, end: 6 }], 0, 3)).toEqual([
            { start: 2, end: 3 },
        ])
        expect(segmentChunks([{ start: 2, end: 6 }], 4, 3)).toEqual([
            { start: 0, end: 2 },
        ])
    })
})
