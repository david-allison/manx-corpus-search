import { SearchWorkResult } from "../api/SearchWorkApi"

/** A book-sized slice of a scripture document's lines */
export type BookSegment = {
    /** canonical book key ("psalms", "1-corinthians") */
    book: string
    /** display name ("Psalms", "1 Corinthians") */
    label: string
    /** index (into the results) of the segment's first line */
    start: number
}

/** Only a document this long is broken into books: chunking a short
 * two-book document (1 & 2 Thessalonians) would just add friction */
export const BOOK_SEGMENT_MIN_LINES = 2000

/** words kept lowercase inside a book name ("Song of Solomon") */
const minorWords = new Set(["of", "the"])

/** "1-corinthians" -> "1 Corinthians", "song-of-solomon" -> "Song of Solomon" */
const bookLabel = (book: string): string =>
    book
        .split("-")
        .map((word, index) =>
            index > 0 && minorWords.has(word)
                ? word
                : word.charAt(0).toUpperCase() + word.slice(1),
        )
        .join(" ")

/**
 * The books of a scripture document, in reading order, from the canonical
 * "book.chapter.verse" keys of its lines. Segment k spans from its `start`
 * to the next segment's `start` (the last runs to the end). Lines before the
 * first referenced book (front matter) belong to the first segment.
 */
export const bookSegments = (results: SearchWorkResult[]): BookSegment[] => {
    const segments: BookSegment[] = []
    const seen = new Set<string>()
    for (let i = 0; i < results.length; i++) {
        const book = results[i].canonicalReference?.split(".")[0]
        if (!book || seen.has(book)) {
            continue
        }
        seen.add(book)
        segments.push({
            book,
            label: bookLabel(book),
            // front matter belongs to the first book
            start: segments.length == 0 ? 0 : i,
        })
    }
    return segments
}

/** [start, end) of {@link book}'s lines within the results */
export const segmentRange = (
    segments: BookSegment[],
    book: string,
    totalLines: number,
): [number, number] => {
    const index = segments.findIndex((x) => x.book == book)
    if (index < 0) {
        return [0, totalLines]
    }
    const end =
        index + 1 < segments.length ? segments[index + 1].start : totalLines
    return [segments[index].start, end]
}
