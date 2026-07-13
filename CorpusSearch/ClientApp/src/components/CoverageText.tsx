import { ReactNode } from "react"
import { TokenCoverage } from "../api/DictionaryApi"
import "./CoverageText.css"

/** The dictionary debug mode's line renderer: each token colour-coded by how
 * a dictionary tap on it would resolve (see TokenCoverage). Replaces the
 * normal highlighted renderer while the mode is active. */
export const CoverageText = (props: {
    text: string
    tokens: TokenCoverage[]
}) => {
    const { text, tokens } = props
    const nodes: ReactNode[] = []
    let pos = 0
    for (const token of tokens) {
        if (token.start > pos) {
            nodes.push(text.slice(pos, token.start))
        }
        const end = token.start + token.length
        nodes.push(
            <span
                key={token.start}
                className={`dict-coverage dict-coverage-${token.status}`}
                title={coverageTitle(token.status)}
            >
                {text.slice(token.start, end)}
            </span>,
        )
        pos = end
    }
    if (pos < text.length) {
        nodes.push(text.slice(pos))
    }
    return <>{nodes}</>
}

const coverageTitle = (status: TokenCoverage["status"]): string => {
    switch (status) {
        case "entry":
            return "In the dictionary"
        case "root":
            return "Found via its root form"
        case "lemma":
            return "Known to the lemma table, but no dictionary entry"
        case "none":
            return "Not found in any dictionary"
    }
}

/** The debug mode's floating legend: colour key, corpus-level counts, and the
 * off switch. Fixed to the corner while the mode is active. */
export const CoverageLegend = (props: {
    coverage: Map<string, TokenCoverage[]> | null
    onClose: () => void
}) => {
    const { coverage, onClose } = props
    const counts = { entry: 0, root: 0, lemma: 0, none: 0 }
    let total = 0
    if (coverage) {
        for (const tokens of coverage.values()) {
            for (const token of tokens) {
                counts[token.status]++
                total++
            }
        }
    }
    const row = (status: TokenCoverage["status"], label: string) => (
        <div className="dict-coverage-legend-row">
            <span>
                <span className={`dict-coverage dict-coverage-${status}`}>
                    {label}
                </span>
            </span>
            {total > 0 && (
                <span>
                    {counts[status].toLocaleString()} (
                    {((100 * counts[status]) / total).toFixed(1)}%)
                </span>
            )}
        </div>
    )
    return (
        <div className="dict-coverage-legend" role="status">
            <button
                type="button"
                className="dict-coverage-legend-close"
                onClick={onClose}
                title="Close (Ctrl+Alt+D)"
            >
                ✕
            </button>
            <strong>Dictionary coverage</strong>
            {coverage == null ? (
                <div>Loading…</div>
            ) : (
                <>
                    {row("entry", "in the dictionary")}
                    {row("root", "found via root form")}
                    {row("lemma", "lemma table only")}
                    {row("none", "not found")}
                </>
            )}
        </div>
    )
}
