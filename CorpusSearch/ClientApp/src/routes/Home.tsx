/* eslint @typescript-eslint/no-misused-promises: 0 */

import "./Home.css"

import {
    Suspense,
    use,
    useEffect,
    useState,
    useTransition,
    ChangeEvent,
    useMemo,
} from "react"
import MainSearchResults, {
    ResultsDensity,
    ResultsSortKey,
} from "../components/MainSearchResults"
import {
    DictionaryLink,
    hasDictionaryDefinitions,
} from "../components/DictionaryLink"
import { hasTranslations, TranslationList } from "../components/TranslationList"
import AdvancedOptions, { DateRange } from "../components/AdvancedOptions"
import { useSearchParams } from "react-router-dom"
import {
    search,
    SearchResponse,
    SearchParams,
    MAX_QUERY_LENGTH,
} from "../api/SearchApi"
import { CircularProgress } from "@mui/material"
import { ManxEnglishSelector } from "../components/ManxEnglishSelector"
import { getCorpusStatistics, Statistics } from "../api/CorpusStatistics"
import { SearchBar } from "../components/SearchBar"
import { NewDocList } from "../components/NewDocList"

export type SearchLanguage = "English" | "Manx"

type SearchResult =
    { status: "success"; data: SearchResponse } | { status: "error" }

export class HomeData {
    static displayName = HomeData.name
    static currentYear = new Date().getFullYear()
}

const parseLanguage = (language?: string | null): SearchLanguage | null => {
    if (!language) {
        return null
    }
    switch (language) {
        case "en":
            return "English"
        case "gv":
            return "Manx"
        default:
            return null
    }
}

const toLangParam = (param: SearchLanguage): string => {
    switch (param) {
        case "Manx":
            return "gv"
        case "English":
            return "en"
    }
}

const loadDensity = (): ResultsDensity => {
    try {
        return localStorage.getItem("resultsDensity") === "compact"
            ? "compact"
            : "comfortable"
    } catch {
        return "comfortable"
    }
}

export const Home = () => {
    const [searchParams, setSearchParams] = useSearchParams()
    const query = searchParams.get("q") ?? ""
    const searchLanguage = parseLanguage(searchParams.get("lang")) ?? "Manx"

    const updateSearch = (q: string, lang: SearchLanguage) => {
        const nextParams: Record<string, string> =
            !q && lang === "Manx" ? {} : { q, lang: toLangParam(lang) }
        setSearchParams(nextParams, { replace: true })
    }
    const setQuery = (next: string) => updateSearch(next, searchLanguage)
    const setSearchLanguage = (next: SearchLanguage) =>
        updateSearch(query, next)

    const [isPending, startTransition] = useTransition()
    const [result, setResult] = useState<SearchResult | null>(null) // null until a search runs

    const [dateRange, setDateRange] = useState<DateRange>({
        start: 1500,
        end: HomeData.currentYear,
    })
    const [matchPhrase, setMatchPhrase] = useState(false)

    const [sortKey, setSortKey] = useState<ResultsSortKey>("year")
    const [density, setDensityState] = useState<ResultsDensity>(loadDensity)
    const setDensity = (next: ResultsDensity) => {
        setDensityState(next)
        try {
            localStorage.setItem("resultsDensity", next)
        } catch {
            /* preference only - ignore storage failures */
        }
    }

    const request = useMemo<SearchParams>(
        () => ({
            query: matchPhrase ? `*${query}*` : query,
            minDate: dateRange.start,
            maxDate: dateRange.end,
            manx: searchLanguage === "Manx",
            english: searchLanguage === "English",
        }),
        [query, searchLanguage, dateRange, matchPhrase],
    )

    const hasNoSearch = query.trim() == ""
    // the server rejects long queries; "match phrase" adds 2 characters
    const queryTooLong = request.query.length > MAX_QUERY_LENGTH

    // load the data
    useEffect(() => {
        if (hasNoSearch || queryTooLong) {
            return
        }

        const controller = new AbortController()
        startTransition(async () => {
            try {
                const data = await search(request, controller.signal)
                if (controller.signal.aborted) return
                setResult({ status: "success", data })
            } catch (e) {
                if (controller.signal.aborted) return
                setResult({ status: "error" })
                console.error(e)
            }
        })
        return () => controller.abort()
    }, [request, hasNoSearch, queryTooLong])

    const handleChange = (event: ChangeEvent<HTMLInputElement>) => {
        setQuery(event.target.value)
    }

    const statsPromise = useMemo(
        () => getCorpusStatistics().catch(() => "error" as const),
        [],
    )

    const renderContent = () => {
        if (hasNoSearch) {
            return (
                <Suspense fallback={<ProgressBar />}>
                    <HomeIntro statsPromise={statsPromise} />
                </Suspense>
            )
        }
        if (queryTooLong) {
            return (
                <div className={"home-error"}>
                    Search text is too long. The maximum is {MAX_QUERY_LENGTH}{" "}
                    characters.
                </div>
            )
        }
        if (result === null) {
            return isPending ? <ProgressBar /> : null
        }
        if (result.status === "error") {
            return (
                <div className={"home-error"}>
                    Something went wrong, please try again
                </div>
            )
        }
        // dim stale results when there is a pending update
        return (
            <div
                style={{
                    opacity: isPending ? 0.5 : 1,
                    transition: "opacity 150ms ease",
                }}
            >
                <SearchResultHeader
                    response={result.data}
                    sortKey={sortKey}
                    onSortKeyChange={setSortKey}
                    density={density}
                    onDensityChange={setDensity}
                />
                {result.data.results.length === 0 ? (
                    <div className="no-results">
                        No matches for “{result.data.query || query}”. Try
                        another spelling, or widen the date range.
                    </div>
                ) : (
                    <MainSearchResults
                        query={result.data.query}
                        results={result.data.results}
                        manx={searchLanguage == "Manx"}
                        english={searchLanguage == "English"}
                        sortKey={sortKey}
                        density={density}
                    />
                )}
            </div>
        )
    }

    return (
        <div>
            <div
                className="search-row search-row-hero"
                id={"corpus-search-box-container"}
            >
                <SearchBar
                    query={query}
                    onChange={handleChange}
                    language={searchLanguage}
                />
                <ManxEnglishSelector
                    initialLanguage={searchLanguage}
                    onLanguageChange={setSearchLanguage}
                />
            </div>

            <AdvancedOptions
                onDateRangeChange={setDateRange}
                onMatchChange={setMatchPhrase}
            >
                {/*on mobile the View control lives here rather than in the results header*/}
                {!hasNoSearch && (
                    <ViewSelect
                        density={density}
                        onDensityChange={setDensity}
                        className="view-control-mobile"
                    />
                )}
            </AdvancedOptions>

            {renderContent()}
        </div>
    )
}

const HomeIntro = ({
    statsPromise,
}: {
    statsPromise: Promise<Statistics | "error">
}) => {
    const stats = use(statsPromise)
    return (
        <>
            <div className="home-intro">
                {stats != "error" ? (
                    <>
                        Search our growing collection of over{" "}
                        <b
                            title={`${stats.uniqueManxWordCount.toLocaleString()} unique words`}
                        >
                            {stats.manxWordCount.toLocaleString()} Manx words
                        </b>
                        <br />
                        or{" "}
                        <a href={"/Browse"}>
                            browse {stats.documentCount.toLocaleString()}{" "}
                            documents
                        </a>
                        .
                    </>
                ) : (
                    <>
                        Enter a search term,
                        <br />
                        or <a href={"/Browse"}>browse all documents</a>.
                    </>
                )}
            </div>
            <Suspense fallback={null}>
                <NewDocList />
            </Suspense>
            <div className="home-support">
                Support our revitalisation efforts by{" "}
                <a href={"/MailingList"}>signing up for our mailing list</a>.
                <br />
                If you know about texts we're missing or want to get in touch,
                please email us at{" "}
                <a href="mailto:corpus-submissions@gaelg.im">
                    corpus-submissions@gaelg.im
                </a>
                .
            </div>
        </>
    )
}

const ViewSelect = (props: {
    density: ResultsDensity
    onDensityChange: (density: ResultsDensity) => void
    className: string
}) => (
    <label className={props.className}>
        View
        <select
            className="corpus-select"
            value={props.density}
            onChange={(e) =>
                props.onDensityChange(e.target.value as ResultsDensity)
            }
        >
            <option value="comfortable">Comfortable</option>
            <option value="compact">Compact</option>
        </select>
    </label>
)

const SearchResultHeader = (props: {
    response: SearchResponse
    sortKey: ResultsSortKey
    onSortKeyChange: (key: ResultsSortKey) => void
    density: ResultsDensity
    onDensityChange: (density: ResultsDensity) => void
}) => {
    const { response } = props
    const query = response.query ?? ""

    const isDict = hasDictionaryDefinitions(response.definedInDictionaries)
    const isTranslation = hasTranslations(response.translations)

    return (
        <div>
            <div className="results-header">
                <div className="results-count">
                    Found{" "}
                    <b className="results-count-matches">
                        {response.numberOfResults.toLocaleString()}
                    </b>{" "}
                    matches in{" "}
                    <b>{response.numberOfDocuments.toLocaleString()}</b> texts
                </div>
                <div className="results-controls">
                    <ViewSelect
                        density={props.density}
                        onDensityChange={props.onDensityChange}
                        className="view-control-desktop"
                    />
                    <label>
                        Sort by
                        <select
                            className="corpus-select"
                            value={props.sortKey}
                            onChange={(e) =>
                                props.onSortKeyChange(
                                    e.target.value as ResultsSortKey,
                                )
                            }
                        >
                            <option value="year">Date</option>
                            <option value="title">Title</option>
                            <option value="count">Matches</option>
                        </select>
                    </label>
                </div>
            </div>
            {(isDict || isTranslation) && (
                <div className="dict-strip">
                    {isDict && (
                        <DictionaryLink
                            query={query}
                            dictionaries={response.definedInDictionaries}
                        />
                    )}
                    {isTranslation && (
                        <TranslationList translations={response.translations} />
                    )}
                </div>
            )}
        </div>
    )
}

const ProgressBar = () => {
    return (
        <div
            style={{
                marginTop: 40,
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
            }}
        >
            <CircularProgress style={{ alignSelf: "center" }} />
        </div>
    )
}
