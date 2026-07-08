import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { act, fireEvent, render } from "@testing-library/react"
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
            await vi.advanceTimersByTimeAsync(50)
        })
        expect(container.querySelector(".doc-row-playing")).toBeNull()
    })

    it("highlights a line starting at 0 once the video plays at 0s", async () => {
        mockPlayer.getCurrentTime.mockReturnValue(0)
        const { container } = renderVideoTable()
        await act(async () => {
            await vi.advanceTimersByTimeAsync(50)
        })
        expect(container.querySelector(".doc-row-playing")).not.toBeNull()
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
