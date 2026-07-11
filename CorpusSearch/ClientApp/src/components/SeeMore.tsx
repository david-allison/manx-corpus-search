import { ReactNode, useLayoutEffect, useRef, useState } from "react"
import "./SeeMore.css"

/** Clamps tall content to `lines` lines behind a "See more" toggle; content
 * short enough to fit renders as-is, with no toggle. */
export const SeeMore = (props: { lines?: number; children: ReactNode }) => {
    const { lines = 4, children } = props
    const [expanded, setExpanded] = useState(false)
    // the collapsed content overflows its clamp: a toggle is needed
    const [clamped, setClamped] = useState(false)
    const content = useRef<HTMLDivElement>(null)

    // new content (e.g. navigating to another document) starts collapsed
    useLayoutEffect(() => {
        setExpanded(false)
    }, [children])

    // measure while the clamp is applied; re-measure as resizes change the wrapping
    useLayoutEffect(() => {
        if (expanded) {
            return
        }
        const element = content.current
        if (element == null) {
            return
        }
        const measure = () =>
            setClamped(element.scrollHeight > element.clientHeight + 1)
        measure()
        if (typeof ResizeObserver == "undefined") {
            return // jsdom
        }
        const observer = new ResizeObserver(measure)
        observer.observe(element)
        return () => observer.disconnect()
    }, [children, expanded])

    return (
        <div>
            <div
                ref={content}
                className={expanded ? undefined : "see-more-clamped"}
                style={expanded ? undefined : { WebkitLineClamp: lines }}
            >
                {children}
            </div>
            {(clamped || expanded) && (
                <button
                    type="button"
                    className="see-more-toggle"
                    aria-expanded={expanded}
                    onClick={() => setExpanded((x) => !x)}
                >
                    {expanded ? "See less ▴" : "See more ▾"}
                </button>
            )}
        </div>
    )
}
