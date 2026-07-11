import {
    CONTEXT_CHUNK,
    ContextGap,
    ExpandDirection,
    SMALL_GAP,
} from "../hooks/useContextExpansion"

/** "line" / "5 lines": the count is omitted for a single line */
const countedLines = (n: number) => (n == 1 ? "line" : `${n} lines`)

const Chevron = ({ direction }: { direction: "up" | "down" }) => (
    <svg
        className="doc-expand-chevron"
        width="12"
        height="12"
        viewBox="0 0 16 16"
        aria-hidden="true"
    >
        <path
            d={direction == "down" ? "M3 6l5 5 5-5" : "M3 10l5-5 5 5"}
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
        />
    </svg>
)

/** A GitHub-style 'expand hunk' divider for the hidden lines between search results (#286) */
export const ExpanderRow = (props: {
    gap: ContextGap
    /** empty cells before the band (the video/speaker columns) */
    leadingCells: number
    /** how many language columns the band covers */
    languageColSpan: number
    /** the Link column is present: keep the band off it */
    hasLinkCell: boolean
    onExpand: (gap: ContextGap, direction: ExpandDirection) => void
}) => {
    const { gap, leadingCells, languageColSpan, hasLinkCell, onExpand } = props
    const span = gap.end - gap.start + 1
    const chunk = Math.min(CONTEXT_CHUNK, span)
    // the edges of the document only reveal towards the nearest result
    const showDown = gap.position != "leading"
    const showUp = gap.position != "trailing"
    const showAll = gap.position == "middle" && span <= SMALL_GAP

    // an extremity fills the whole band: balance it with a chevron on each side
    const isEdge = gap.position != "middle"
    const button = (direction: ExpandDirection, label: string) => (
        <button
            type="button"
            className={`doc-expand-btn doc-expand-btn-${direction}`}
            disabled={gap.loading}
            onClick={() => onExpand(gap, direction)}
        >
            {direction != "all" && <Chevron direction={direction} />}
            {label}
            {direction != "all" && isEdge && <Chevron direction={direction} />}
        </button>
    )

    return (
        <tr className="doc-expand-row">
            {Array.from({ length: leadingCells }, (_, i) => (
                <td key={i} />
            ))}
            <td colSpan={languageColSpan}>
                <div className="doc-expand-buttons">
                    {showAll ? (
                        button("all", "Show context")
                    ) : (
                        <>
                            {showDown &&
                                button(
                                    "down",
                                    `Show next ${countedLines(chunk)}`,
                                )}
                            {showUp &&
                                button(
                                    "up",
                                    `Show previous ${countedLines(chunk)}`,
                                )}
                        </>
                    )}
                </div>
            </td>
            {hasLinkCell && <td />}
        </tr>
    )
}
