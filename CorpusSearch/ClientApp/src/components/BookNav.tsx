import { BookSegment } from "../utils/BookSegments"
import "./BookNav.css"

/** The long-document book picker: previous/next arrows around a book list.
 * Rendered above and below the lines, so the reader never scrolls a whole
 * book to reach it. */
export const BookNav = (props: {
    segments: BookSegment[]
    activeBook: string
    onSelect: (book: string) => void
}) => {
    const { segments, activeBook, onSelect } = props
    const index = segments.findIndex((x) => x.book == activeBook)
    const previous = index > 0 ? segments[index - 1] : null
    const next =
        index >= 0 && index + 1 < segments.length ? segments[index + 1] : null
    return (
        <nav className="doc-book-nav" aria-label="Books">
            <button
                type="button"
                disabled={previous == null}
                title={previous?.label}
                onClick={() => previous && onSelect(previous.book)}
            >
                ‹ {previous?.label ?? " "}
            </button>
            <select
                aria-label="Book"
                value={activeBook}
                onChange={(e) => onSelect(e.target.value)}
            >
                {segments.map((segment) => (
                    <option key={segment.book} value={segment.book}>
                        {segment.label}
                    </option>
                ))}
            </select>
            <button
                type="button"
                disabled={next == null}
                title={next?.label}
                onClick={() => next && onSelect(next.book)}
            >
                {next?.label ?? " "} ›
            </button>
        </nav>
    )
}
