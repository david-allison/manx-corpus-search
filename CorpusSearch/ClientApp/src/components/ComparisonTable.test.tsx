import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import {
    act,
    cleanup,
    fireEvent,
    render,
    screen,
    waitFor,
} from "@testing-library/react"
import { MemoryRouter } from "react-router-dom"
import { ComparisonTable } from "./ComparisonTable"
import { segmentChunks } from "./LineText"
import { SearchWorkResponse, SearchWorkResult } from "../api/SearchWorkApi"
import { Ref } from "react"
import { Player } from "./YouTuber"

const mockPlayer = vi.hoisted(() => ({
    seek: vi.fn(),
    getCurrentTime: vi.fn((): number | null => null),
}))

/** what the table asked of the player, startSeconds included */
const mockYouTuberProps = vi.hoisted(
    (): { videoId?: string; startSeconds?: number } => ({}),
)

vi.mock("./YouTuber", async () => {
    const { useImperativeHandle } = await import("react")
    const MockYouTuber = ({
        videoId,
        startSeconds,
        ref,
    }: {
        videoId: string
        startSeconds?: number
        ref?: Ref<Player>
    }) => {
        useImperativeHandle(ref, () => mockPlayer)
        mockYouTuberProps.videoId = videoId
        mockYouTuberProps.startSeconds = startSeconds
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

const mockVerseAlignment = vi.hoisted(() => vi.fn())

vi.mock("../api/VerseAlignmentApi", () => ({
    fetchVerseAlignment: mockVerseAlignment,
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

    it("renders the rewritten word as a correction with a reveal chip", () => {
        const { container } = renderTable([correctedLine])
        expect(container.querySelector(".doc-correction")?.textContent).toBe(
            "çhengey",
        )
        const chip = container.querySelector("button.doc-correction-marker")
        fireEvent.click(chip!)
        expect(container.querySelector(".part-removed")?.textContent).toBe(
            "chengey",
        )
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
        expect(container.querySelector(".doc-correction")).not.toBeNull()
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

describe("a ?line= deep link into a recording", () => {
    const lines = [
        line({ manx: "moghrey mie", csvLineNumber: 1, subStart: 0, subEnd: 5 }),
        line({
            manx: "breck sailjey",
            csvLineNumber: 3,
            subStart: 9.5,
            subEnd: 11.4,
        }),
    ]
    const renderWithTarget = (targetLine?: number) =>
        renderTable(lines, {
            response: {
                ...response(lines),
                source: "https://www.youtube.com/watch?v=abc123",
            },
            targetLine,
        })

    beforeEach(() => {
        mockYouTuberProps.videoId = undefined
        mockYouTuberProps.startSeconds = undefined
    })

    it("cues the video at the target line's moment", () => {
        renderWithTarget(3)
        expect(mockYouTuberProps.startSeconds).toBe(9.5)
    })

    it("cues nothing without a target: playback starts at the top", () => {
        renderWithTarget(undefined)
        expect(mockYouTuberProps.startSeconds).toBeUndefined()
    })

    it("cues nothing when the target names no line of the document", () => {
        renderWithTarget(99)
        expect(mockYouTuberProps.startSeconds).toBeUndefined()
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
        // neither mocked headword heads the clicked word, so both render as
        // derived entries beneath a bare anchor line for the selection: 2 + 1
        expect(document.querySelectorAll(".dict-popup-entry")).toHaveLength(3)
    })

    it("reports when no dictionary defines the word", async () => {
        mockDictionaryLookup.mockResolvedValue([])
        openPopup()

        await screen.findByText(/Could not find definition/)
    })

    it("does not open on a non-Manx row", () => {
        // the Manx column of a row with a language marker is not Manx
        // (an untranslated English passage): there is nothing to look up
        mockGetWordAtPoint.mockReturnValue("cat")
        const { getByText } = renderTable([
            line({ manx: "the cat sat", language: "en" }),
        ])

        fireEvent.click(getByText("the cat sat"))

        expect(mockDictionaryLookup).not.toHaveBeenCalled()
    })

    it("does not open from the English column", () => {
        mockGetWordAtPoint.mockReturnValue("tongue")
        const { getByText } = renderTable([
            line({ manx: "Ta çhengey aym", english: "I have a tongue" }),
        ])

        fireEvent.click(getByText("I have a tongue"))

        expect(mockDictionaryLookup).not.toHaveBeenCalled()
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

describe("reference column (verse references)", () => {
    beforeEach(() => {
        mockVerseAlignment.mockReset()
    })

    it("renders an unresolved reference as plain text", () => {
        const { container, getByText } = renderTable([
            line({ manx: "Ta fys aym", reference: "[12]" }),
        ])
        getByText("Ref")
        getByText("[12]")
        expect(container.querySelector(".doc-ref-link")).toBeNull()
    })

    it("renders a reference-only row as a section heading", () => {
        const { container } = renderTable([
            line({
                reference: "PSALM 23",
                canonicalReference: "psalms.23",
                csvLineNumber: 2,
            }),
            line({ manx: "Ta'n Chiarn my vochilley", csvLineNumber: 3 }),
        ])
        const band = container.querySelector(
            ".doc-row-heading .doc-heading-band",
        )
        expect(band?.textContent).toBe("PSALM 23")
    })

    it("opens the other-versions popup from a resolved reference", async () => {
        mockVerseAlignment.mockResolvedValue({
            key: "psalms.23.1",
            display: "Psalms 23:1",
            documents: [
                {
                    ident: "doc",
                    name: "This Psalter",
                    csvLineNumber: 2,
                },
                {
                    ident: "bible",
                    name: "Yn Vible Casherick",
                    year: 1819,
                    csvLineNumber: 15234,
                    manx: "Ta'n Chiarn my vochilley",
                },
            ],
        })
        const verse = line({
            manx: "Yn Chiarn hene my vochilley mie",
            reference: "1",
            canonicalReference: "psalms.23.1",
        })
        const { getByTitle } = render(
            <MemoryRouter>
                <ComparisonTable
                    response={response([verse])}
                    value=""
                    highlightManx={false}
                    highlightEnglish={false}
                    manxVisible={true}
                    englishVisible={true}
                    docIdent="doc"
                />
            </MemoryRouter>,
        )

        fireEvent.click(getByTitle("This verse in other versions"))

        expect(mockVerseAlignment).toHaveBeenCalledWith(
            "psalms.23.1",
            expect.anything(),
        )
        await screen.findByText("Psalms 23:1")
        // the other version links into its document at the verse...
        const link = screen.getByRole("link", {
            name: /Yn Vible Casherick/,
        })
        expect(link.getAttribute("href")).toBe("/docs/bible?ref=psalms.23.1")
        // ...while the document the reader is in is listed without a link
        screen.getByText("this document")
        expect(screen.queryByRole("link", { name: /This Psalter/ })).toBeNull()
    })

    it("flashes the deep link's target row", () => {
        const { container } = renderTable(
            [
                line({
                    manx: "Ta'n Chiarn my vochilley",
                    reference: "1",
                    canonicalReference: "psalms.23.1",
                    csvLineNumber: 7,
                }),
            ],
            { targetLine: 7 },
        )
        const row = container.querySelector("#line-7")
        expect(row?.classList.contains("doc-row-target")).toBe(true)
    })
})

describe("editorial asides", () => {
    it("styles a recording event as apparatus, not body prose", () => {
        const { container } = renderTable([
            line({ manx: "va shen mie [laughs] as eisht" }),
        ])
        const aside = container.querySelector(".doc-editorial")
        expect(aside?.textContent).toBe("[laughs]")
    })

    it("keeps highlight offsets correct around an aside", () => {
        // offsets are into the full cell, including the bracketed aside
        const { container } = renderTable([
            line({
                manx: "abc [laughs] cre def",
                manxHighlights: [{ start: 13, end: 16 }],
            }),
        ])
        const marks = container.querySelectorAll("mark.textHighlight")
        expect(Array.from(marks).map((x) => x.textContent)).toEqual(["cre"])
    })

    it("does not restyle numeric note markers", () => {
        const noted = line({
            manx: "Geaylin yn Cholloo. [1]",
            notes: "[1] a note",
        })
        const { container } = renderTable([noted])
        expect(container.querySelector(".doc-editorial")).toBeNull()
        expect(container.querySelector(".doc-note-marker")).not.toBeNull()
    })
})

describe("speaker column", () => {
    it("shows the speakers of a non-video document", () => {
        // e.g. an interview transcription: speakers are not a video-only concept
        const { getByText } = renderTable([
            line({ manx: "Ta fys aym", speaker: "NM" }),
        ])

        getByText("Speaker")
        getByText("NM")
    })

    it("has no Speaker column when no line names a speaker", () => {
        const { queryByText } = renderTable([line({ manx: "Ta fys aym" })])

        expect(queryByText("Speaker")).toBeNull()
    })
})
