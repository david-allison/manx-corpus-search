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

vi.mock("../utils/Selection", () => ({
    getSelectedWordOrPhrase: mockGetSelectedWordOrPhrase,
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

describe("context expansion (#286)", () => {
    const gapResults = () => [
        line({ manx: "top match", csvLineNumber: 10 }),
        line({ manx: "bottom match", csvLineNumber: 50 }),
    ]
    const renderWithGap = () =>
        renderTable(gapResults(), {
            docIdent: "doc",
            response: {
                ...response(gapResults()),
                firstLineNumber: 10,
                lastLineNumber: 50,
            },
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
        // clicking a word 'selects' it: bypass the browser selection plumbing
        mockGetSelectedWordOrPhrase.mockReturnValue("lhiam")
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
