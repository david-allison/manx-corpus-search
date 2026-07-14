import { expect, it } from "vitest"
import { bookSegments, segmentRange } from "./BookSegments"
import { SearchWorkResult } from "../api/SearchWorkApi"

const line = (canonicalReference?: string): SearchWorkResult =>
    ({ canonicalReference }) as SearchWorkResult

it("segments a document at its book boundaries", () => {
    const results = [
        line("genesis.1"),
        line("genesis.1.1"),
        line("exodus.1"),
        line("exodus.1.1"),
    ]
    expect(bookSegments(results)).toEqual([
        { book: "genesis", label: "Genesis", start: 0 },
        { book: "exodus", label: "Exodus", start: 2 },
    ])
})

it("attaches front matter and unreferenced lines to the current book", () => {
    const results = [
        line(), // a title line before any reference
        line("genesis.1.1"),
        line(), // a blank/note line inside Genesis
        line("exodus.1.1"),
    ]
    const segments = bookSegments(results)
    expect(segments.map((x) => x.start)).toEqual([0, 3])
    expect(segmentRange(segments, "genesis", results.length)).toEqual([0, 3])
    expect(segmentRange(segments, "exodus", results.length)).toEqual([3, 4])
})

it("keeps a book's first position when a stray reference reappears", () => {
    const results = [
        line("genesis.1.1"),
        line("exodus.1.1"),
        line("genesis.50.1"), // out of order: stays inside exodus's span
        line("exodus.2.1"),
    ]
    expect(bookSegments(results).map((x) => x.book)).toEqual([
        "genesis",
        "exodus",
    ])
})

it("labels numbered and multi-word books", () => {
    const labels = bookSegments([
        line("1-corinthians.1.1"),
        line("song-of-solomon.1.1"),
        line("acts.1.1"),
    ]).map((x) => x.label)
    expect(labels).toEqual(["1 Corinthians", "Song of Solomon", "Acts"])
})

it("returns no segments without canonical references", () => {
    expect(bookSegments([line(), line()])).toEqual([])
})
