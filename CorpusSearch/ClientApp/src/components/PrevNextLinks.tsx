import { ReactNode } from "react"
import { Link } from "react-router-dom"
import "./PrevNextLinks.css"

export type PrevNextTarget = {
    to: string
    label: string
    /** the control's tooltip, where the label alone is too terse */
    title?: string
}

/** Previous/next arrows around a label, as links rather than buttons: each step
 * is a place you can bookmark, share and go back to.
 *
 * Deliberately not BookNav (which is buttons, a callback and a <select>): the
 * two share a look, not an API. An edge renders a hidden placeholder rather than
 * nothing, so the row does not jump as you walk. */
export const PrevNextLinks = ({
    previous,
    next,
    children,
    ariaLabel,
}: {
    previous: PrevNextTarget | null
    next: PrevNextTarget | null
    children: ReactNode
    ariaLabel: string
}) => (
    <nav className="prev-next" aria-label={ariaLabel}>
        {previous ? (
            <Link
                className="prev-next-link"
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
                className="prev-next-link"
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
    </nav>
)
