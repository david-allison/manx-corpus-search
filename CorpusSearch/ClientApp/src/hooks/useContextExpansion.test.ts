import { describe, expect, it } from "vitest"
import { deriveGaps } from "./useContextExpansion"
import { SearchWorkResponse, SearchWorkResult } from "../api/SearchWorkApi"

const line = (csvLineNumber: number): SearchWorkResult => ({
    english: "",
    manx: "",
    page: "",
    csvLineNumber,
    date: "",
    notes: "",
})

const response = (
    lineNumbers: number[],
    overrides?: Partial<SearchWorkResponse>,
): SearchWorkResponse => ({
    results: lineNumbers.map(line),
    title: "Test Document",
    translations: {},
    totalMatches: lineNumbers.length,
    numberOfResults: lineNumbers.length,
    notes: "",
    source: "",
    sourceLinks: null,
    pdfLink: undefined,
    googleBooksId: undefined,
    gitHubLink: "",
    ...overrides,
})

describe("deriveGaps (#286)", () => {
    it("finds the gap between results more than one line apart", () => {
        expect(deriveGaps(response([2, 10]))).toEqual([
            { start: 3, end: 9, position: "middle", loading: false },
        ])
    })

    it("finds no gap between adjacent results", () => {
        expect(deriveGaps(response([2, 3, 4]))).toEqual([])
    })

    it("finds the gaps between the results and the document bounds", () => {
        const gaps = deriveGaps(
            response([10, 11], { firstLineNumber: 2, lastLineNumber: 20 }),
        )
        expect(gaps).toEqual([
            { start: 2, end: 9, position: "leading", loading: false },
            { start: 12, end: 20, position: "trailing", loading: false },
        ])
    })

    it("finds no boundary gaps when the results reach the document bounds", () => {
        const gaps = deriveGaps(
            response([2, 3], { firstLineNumber: 2, lastLineNumber: 3 }),
        )
        expect(gaps).toEqual([])
    })

    it("finds no boundary gaps without the document bounds", () => {
        // a '*' search or an empty result has no firstLineNumber/lastLineNumber
        expect(deriveGaps(response([10, 11]))).toEqual([])
    })

    it("finds no gaps for a '*' search", () => {
        // '*' returns every line: line-number jumps are multi-line records, not hidden lines
        expect(deriveGaps(response([2, 10], { totalMatches: null }))).toEqual(
            [],
        )
    })

    it("finds no gaps without results", () => {
        expect(
            deriveGaps(
                response([], { firstLineNumber: 2, lastLineNumber: 20 }),
            ),
        ).toEqual([])
    })
})
