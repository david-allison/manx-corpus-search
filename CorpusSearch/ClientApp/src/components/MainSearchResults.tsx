import { useState, useMemo, MouseEvent, ReactNode } from "react"
import { Link } from "react-router-dom"
import "./MainSearchResults.css"
import { SearchResultEntry } from "../api/SearchApi"
import { GetMatch } from "../api/Matches"
import { floatingPromiseReturn } from "../utils/Promise"
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

const nthIndexOf = (
    inputString: string,
    searchString: string,
    index: number,
) => {
    let i = -1

    while (index-- && i++ < inputString.length) {
        i = inputString.indexOf(searchString, i)
        if (i < 0) break
    }

    return i
}
function findNth(string: string, query: string, fromIndex: number) {
    // TODO: make this work
    const searchable =
        " " +
        string
            .toLowerCase()
            .replace(" ", " ")
            .replace(/[^\w\s]/gi, " ")
            .replace("\r", " ")
            .replace("\n", " ") +
        " "

    // assume per-word
    const stringStartIndex = nthIndexOf(
        searchable,
        " " + query.toLowerCase() + " ",
        fromIndex + 1,
    )
    const stringEndIndex = stringStartIndex + query.length

    if (stringStartIndex === -1) {
        return null
    }

    let startIndex = stringStartIndex
    let count = 0
    let lastSpace = false
    while (startIndex > 0 && count < 5) {
        startIndex--
        if (string[startIndex] === " ") {
            if (!lastSpace) {
                count++
            }
            lastSpace = true
        } else {
            lastSpace = false
        }
    }

    let endIndex = stringEndIndex
    count = 0
    lastSpace = false
    while (endIndex < string.length && count < 5) {
        endIndex++
        if (string[endIndex] === " ") {
            if (!lastSpace) {
                count++
            }
            lastSpace = true
        } else {
            lastSpace = false
        }
    }

    return {
        pre: string.substring(startIndex, stringStartIndex),
        match: string.substring(stringStartIndex, stringEndIndex),
        post: string.substring(stringEndIndex, endIndex),
    }
}

export default function MainSearchResults(props: {
    query: string
    results: SearchResultEntry[]
    english: boolean
    manx: boolean
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
                    <div>Matches</div>
                </div>
                {items.map((result, i) => (
                    <ResultRow
                        key={result.ident + result.count.toString()}
                        result={result}
                        query={query}
                        manx={props.manx}
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
                />
            ))}
        </div>
    )
}

/** Stepping through the matches of a document via the GetMatch API (KWIC) */
const useMatchStepper = (result: SearchResultEntry, query: string) => {
    const [matchNumber, setMatchNumber] = useState(1) // 1-based
    const [sample, setSample] = useState(result.sample)
    const [indexInLine, setIndexInLine] = useState(0) // 0-based

    const changeLine = async (line: number) => {
        const lineResult = await GetMatch({
            query: query,
            match: line,
            docIdent: result.ident,
        })
        setSample(lineResult.manx)
        setMatchNumber(lineResult.matchNumber)
        setIndexInLine(lineResult.matchIndexInLine)
    }

    const canNext = matchNumber < result.count
    const canPrev = matchNumber > 1

    const next = async (e: MouseEvent) => {
        e.preventDefault()
        if (!canNext) {
            return
        }
        await changeLine(matchNumber + 1)
    }
    const prev = async (e: MouseEvent) => {
        e.preventDefault()
        if (!canPrev) {
            return
        }
        await changeLine(matchNumber - 1)
    }

    const kwicSample = useLazyLoader(
        () => findNth(sample, query, indexInLine),
        [sample, query, indexInLine],
    )

    return { matchNumber, sample, canNext, canPrev, next, prev, kwicSample }
}

/** The `01/07 ‹ ›` counter/steppers + the matched line with the match highlighted */
const KwicLine = (props: {
    stepper: ReturnType<typeof useMatchStepper>
    count: number
    small?: boolean
}) => {
    const { stepper, count, small } = props
    const { matchNumber, sample, canNext, canPrev, next, prev, kwicSample } =
        stepper

    const padWidth = Math.max(2, String(count).length)
    const pad = (n: number) => String(n).padStart(padWidth, "0")

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
        text = sample // could not locate the match within the line - show it unhighlighted
    }

    return (
        <div className={"result-kwic" + (small ? " result-kwic-small" : "")}>
            <span className="kwic-counter">
                {pad(matchNumber)}/{pad(count)}
            </span>
            <button
                className="kwic-step"
                disabled={!canPrev}
                onClick={floatingPromiseReturn(prev)}
            >
                ‹
            </button>
            <button
                className="kwic-step"
                disabled={!canNext}
                onClick={floatingPromiseReturn(next)}
            >
                ›
            </button>
            <span className="result-kwic-text">{text}</span>
        </div>
    )
}

const documentLink = (
    result: SearchResultEntry,
    query: string,
    manx: boolean,
) => ({
    to: {
        pathname: `/docs/${result.ident}`,
        search: `?q=${query}`,
    },
    state: { searchLanguage: manx ? "Manx" : "English", previousPage: "/" },
})

/** Comfortable density: one card per document */
const ResultCard = (props: {
    result: SearchResultEntry
    query: string
    manx: boolean
}) => {
    const { result, query, manx } = props
    const stepper = useMatchStepper(result, query)
    const link = documentLink(result, query, manx)

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
                <Link
                    className="result-card-browse"
                    to={link.to}
                    state={link.state}
                >
                    Browse&nbsp;({result.count})&nbsp;→
                </Link>
            </div>
            <KwicLine stepper={stepper} count={result.count} />
        </div>
    )
}

/** Compact density: original-style table - a data row + a smaller KWIC row */
const ResultRow = (props: {
    result: SearchResultEntry
    query: string
    manx: boolean
    striped: boolean
}) => {
    const { result, query, manx, striped } = props
    const stepper = useMatchStepper(result, query)
    const link = documentLink(result, query, manx)

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
                <div className="results-compact-browse">
                    <Link to={link.to} state={link.state}>
                        Browse&nbsp;({result.count})
                    </Link>
                </div>
            </div>
            <KwicLine stepper={stepper} count={result.count} small />
        </div>
    )
}
