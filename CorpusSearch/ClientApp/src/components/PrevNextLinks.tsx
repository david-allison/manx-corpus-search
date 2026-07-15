import { ReactNode } from "react"
import { Link } from "react-router-dom"
import "./PrevNextLinks.css"

export type PrevNextTarget = {
    to: string
    label: string
    /** the control's tooltip, where the label alone is too terse */
    title?: string
    /** greys the step out: its target is there, but is of lesser standing than
     * the walk's ordinary steps. What the caller means by that is the caller's */
    muted?: boolean
}

/** Previous/next arrows around a label, as links rather than buttons: each step
 * is a place you can bookmark, share and go back to.
 *
 * `farPrevious`/`farNext` are the outer skip steps, past whatever the caller
 * wants skipped. They carry no label — only an arrow and a tooltip — because the
 * row is already as wide as two names and a title.
 *
 * Deliberately not BookNav (which is buttons, a callback and a <select>): the
 * two share a look, not an API. An edge renders a hidden placeholder rather than
 * nothing, so the row does not jump as you walk. */
export const PrevNextLinks = ({
    previous,
    next,
    farPrevious,
    farNext,
    children,
    ariaLabel,
}: {
    previous: PrevNextTarget | null
    next: PrevNextTarget | null
    /** omit entirely for a walk without skip steps: passing null keeps the slot
     * (and the row's width) while there is nowhere to skip to */
    farPrevious?: PrevNextTarget | null
    farNext?: PrevNextTarget | null
    children: ReactNode
    ariaLabel: string
}) => (
    <nav className="prev-next" aria-label={ariaLabel}>
        {farPrevious !== undefined && <Skip target={farPrevious} arrow="«" />}
        {previous ? (
            <Link
                className={linkClass(previous)}
                to={previous.to}
                title={previous.title ?? previous.label}
                rel="prev"
            >
                {"‹ "}
                {previous.label}
            </Link>
        ) : (
            <span
                className="prev-next-link prev-next-edge"
                aria-hidden="true"
            />
        )}
        <span className="prev-next-current">{children}</span>
        {next ? (
            <Link
                className={linkClass(next)}
                to={next.to}
                title={next.title ?? next.label}
                rel="next"
            >
                {next.label}
                {" ›"}
            </Link>
        ) : (
            <span
                className="prev-next-link prev-next-edge"
                aria-hidden="true"
            />
        )}
        {farNext !== undefined && <Skip target={farNext} arrow="»" />}
    </nav>
)

const linkClass = (target: PrevNextTarget) =>
    target.muted ? "prev-next-link dict-unattested" : "prev-next-link"

/** An arrow and a tooltip: the label would be a third name in a row that has no
 * room for one. Holds its space at the edges, as the ordinary steps do. */
const Skip = ({
    target,
    arrow,
}: {
    target: PrevNextTarget | null
    arrow: string
}) =>
    target ? (
        <Link
            className="prev-next-link prev-next-skip"
            to={target.to}
            title={target.title ?? target.label}
        >
            {arrow}
        </Link>
    ) : (
        <span
            className="prev-next-link prev-next-skip prev-next-edge"
            aria-hidden="true"
        />
    )
