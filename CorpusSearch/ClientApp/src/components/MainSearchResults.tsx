import { useMemo, ReactNode } from "react"
import { Link } from "react-router-dom"
import "./MainSearchResults.css"
import { HighlightRange, SearchResultEntry } from "../api/SearchApi"
import { SearchOptions, searchOptionsLinkQuery } from "../api/SearchOptions"
import { useLazyLoader } from "../hooks/useLazyLoader"

export type ResultsSortKey = "year" | "title" | "count"
export type ResultsDensity = "comfortable" | "compact"

// sort on the first letter: ignore leading whitespace, quotes and emoji (e.g. "🎥 Captan…" under C)
const titleSortKey = (title: string) =>
    title.replace(/^[^\p{L}\p{N}]+/u, "").toLowerCase()

const sortResults = (
    items: SearchResultEntry[],
    sortKey: ResultsSortKey,
): SearchResultEntry[] => {
    const sorted = [...items]
    switch (sortKey) {
        case "title":
            sorted.sort((a, b) =>
                titleSortKey(a.documentName).localeCompare(
                    titleSortKey(b.documentName),
                ),
            )
            break
        case "count":
            sorted.sort((a, b) => b.count - a.count)
            break
        case "year":
            // ISO dates sort lexicographically
            sorted.sort((a, b) =>
                a.startDate < b.startDate
                    ? -1
                    : a.startDate > b.startDate
                      ? 1
                      : 0,
            )
            break
    }
    return sorted
}

function getFullYear(date: string, edate: string) {
    if (!edate || edate === date) {
        return new Date(date).getFullYear()
    }

    const first = new Date(date)
    const second = new Date(edate)

    if (first.getFullYear() == second.getFullYear()) {
        return "c. " + first.getFullYear().toString()
    }

    return `${new Date(date).getFullYear()}-${new Date(edate).getFullYear()}`
}

/**
 * Splits a sample line around the first highlighted match (server-computed offsets, see #40),
 * keeping up to 5 words of context on each side.
 *
 * @returns null when the server provided no highlights (e.g. English searches):
 * the caller shows the sample unhighlighted
 */
export function buildKwic(
    sample: string,
    highlights: HighlightRange[],
): { pre: string; match: string; post: string } | null {
    if (highlights.length === 0) {
        return null
    }
    const match = highlights[0]

    let startIndex = match.start
    let count = 0
    let lastSpace = false
    while (startIndex > 0 && count < 5) {
        startIndex--
        if (sample[startIndex] === " ") {
            if (!lastSpace) {
                count++
            }
            lastSpace = true
        } else {
            lastSpace = false
        }
    }

    let endIndex = match.end
    count = 0
    lastSpace = false
    while (endIndex < sample.length && count < 5) {
        endIndex++
        if (sample[endIndex] === " ") {
            if (!lastSpace) {
                count++
            }
            lastSpace = true
        } else {
            lastSpace = false
        }
    }

    return {
        pre: sample.substring(startIndex, match.start),
        match: sample.substring(match.start, match.end),
        post: sample.substring(match.end, endIndex),
    }
}

export default function MainSearchResults(props: {
    query: string
    results: SearchResultEntry[]
    english: boolean
    manx: boolean
    options: SearchOptions
    sortKey: ResultsSortKey
    density: ResultsDensity
}) {
    const { results, query, sortKey, density } = props
    const items = useMemo(
        () => sortResults(results, sortKey),
        [results, sortKey],
    )

    if (density === "compact") {
        return (
            <div className="results-compact">
                <div className="results-compact-head">
                    <div>Date</div>
                    <div>Title</div>
                    <div className="results-compact-matches">Matches</div>
                </div>
                {items.map((result, i) => (
                    <ResultRow
                        key={result.ident + result.count.toString()}
                        result={result}
                        query={query}
                        manx={props.manx}
                        options={props.options}
                        striped={i % 2 === 1}
                    />
                ))}
            </div>
        )
    }

    return (
        <div className="results-cards">
            {items.map((result) => (
                <ResultCard
                    key={result.ident + result.count.toString()}
                    result={result}
                    query={query}
                    manx={props.manx}
                    options={props.options}
                />
            ))}
        </div>
    )
}

/** The first matched line of the document, with the match highlighted (KWIC) */
const KwicLine = (props: { result: SearchResultEntry; small?: boolean }) => {
    const { result, small } = props

    const kwicSample = useLazyLoader(
        () => buildKwic(result.sample, result.sampleHighlights ?? []),
        [result],
    )

    let text: ReactNode = null
    if (kwicSample) {
        text = (
            <>
                {kwicSample.pre}
                <strong className="kwic-match">{kwicSample.match}</strong>
                {kwicSample.post}
            </>
        )
    } else if (kwicSample == null) {
        text = result.sample // could not locate the match within the line - show it unhighlighted
    }

    return (
        <div className={"result-kwic" + (small ? " result-kwic-small" : "")}>
            {text}
        </div>
    )
}

const documentLink = (
    result: SearchResultEntry,
    query: string,
    manx: boolean,
    options: SearchOptions,
) => ({
    to: {
        pathname: `/docs/${result.ident}`,
        // `q` is deliberately unencoded (pre-existing behavior)
        search: `?q=${query}` + searchOptionsLinkQuery(options),
    },
    state: { searchLanguage: manx ? "Manx" : "English", previousPage: "/" },
})

/**
 * The document's match count, tinted like the KWIC match highlight.
 * Links to the document, same as the title.
 */
const MatchCountPill = (props: {
    result: SearchResultEntry
    link: ReturnType<typeof documentLink>
    numberOnly?: boolean
}) => {
    const { result, link, numberOnly } = props
    const count = result.count.toLocaleString()
    const noun = result.count === 1 ? "match" : "matches"
    return (
        <Link
            className="match-count-pill"
            to={link.to}
            state={link.state}
            aria-label={`${count} ${noun} in ${result.documentName}`}
        >
            {numberOnly ? count : `${count} ${noun}`}
            {/* disclosure cue: the pill (like the title) opens the document */}
            <span className="match-count-pill-chevron" aria-hidden="true">
                ›
            </span>
        </Link>
    )
}

/** Comfortable density: one card per document */
const ResultCard = (props: {
    result: SearchResultEntry
    query: string
    manx: boolean
    options: SearchOptions
}) => {
    const { result, query, manx, options } = props
    const link = documentLink(result, query, manx, options)

    return (
        <div className="result-card">
            <div className="result-card-top">
                <span className="year-badge">
                    {getFullYear(result.startDate, result.endDate)}
                </span>
                <Link
                    className="result-card-title"
                    to={link.to}
                    state={link.state}
                >
                    {result.documentName}
                </Link>
                <MatchCountPill result={result} link={link} />
            </div>
            <KwicLine result={result} />
        </div>
    )
}

/** Compact density: original-style table - a data row + a smaller KWIC row */
const ResultRow = (props: {
    result: SearchResultEntry
    query: string
    manx: boolean
    options: SearchOptions
    striped: boolean
}) => {
    const { result, query, manx, options, striped } = props
    const link = documentLink(result, query, manx, options)

    return (
        <div className={"results-compact-row" + (striped ? " striped" : "")}>
            <div className="results-compact-grid">
                <div className="results-compact-year">
                    {getFullYear(result.startDate, result.endDate)}
                </div>
                <div className="results-compact-title">
                    <Link to={link.to} state={link.state}>
                        {result.documentName}
                    </Link>
                </div>
                <div className="results-compact-matches">
                    <MatchCountPill result={result} link={link} numberOnly />
                </div>
            </div>
            <KwicLine result={result} small />
        </div>
    )
}
