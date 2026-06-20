/* eslint @typescript-eslint/no-misused-promises: 0 */  

import "./Home.css"

import {Suspense, use, useEffect, useRef, useState, useTransition, ChangeEvent, useMemo} from "react"
import MainSearchResults from "../components/MainSearchResults"
import {DictionaryLink, hasDictionaryDefinitions} from "../components/DictionaryLink"
import {hasTranslations, TranslationList} from "../components/TranslationList"
import AdvancedOptions, {DateRange} from "../components/AdvancedOptions"
import {useSearchParams} from "react-router-dom"
import {search, SearchResponse} from "../api/SearchApi"
import {CircularProgress} from "@mui/material"
import {ManxEnglishSelector} from "../components/ManxEnglishSelector"
import {getCorpusStatistics, Statistics} from "../api/CorpusStatistics"
import {SearchBar} from "../components/SearchBar"
import {NewDocList} from "../components/NewDocList"

export type SearchLanguage = "English" | "Manx"

type SearchResult = { status: "success"; data: SearchResponse } 
    | { status: "error" }

export class HomeData {
    static displayName = HomeData.name
    static currentYear = new Date().getFullYear()
}

const parseLanguage = (language?: string | null): SearchLanguage | null => {
    if (!language) {
        return null
    }
    switch (language) {
        case "en": return "English"
        case "gv": return "Manx"
        default: return null
    }
}

const toLangParam = (param: SearchLanguage): string => {
    switch (param) {
        case "Manx": return "gv"
        case "English": return "en"
    }
}

export const Home = () => {
    const [searchParams, setSearchParams] = useSearchParams()
    const query = searchParams.get("q") ?? ""
    const searchLanguage = parseLanguage(searchParams.get("lang")) ?? "Manx"

    const updateSearch = (q: string, lang: SearchLanguage) => {
        const nextParams: Record<string, string> = (!q && lang === "Manx") ? {} : { q, lang: toLangParam(lang) }
        setSearchParams(nextParams, { replace: true })
    }
    const setQuery = (next: string) => updateSearch(next, searchLanguage)
    const setSearchLanguage = (next: SearchLanguage) => updateSearch(query, next)

    const [isPending, startTransition] = useTransition()
    const [result, setResult] = useState<SearchResult | null>(null) // null until a search runs

    const [dateRange, setDateRange] = useState<DateRange>( { start: 1500, end: HomeData.currentYear })
    const [matchPhrase, setMatchPhrase] = useState(false)

    const hasNoSearch = query.trim() == "" 
    
    const currentQuery = useRef(query)
    currentQuery.current = query
    
    // load the data
    useEffect(() => {
        const getData = async () => {
            
            const parsedQuery = matchPhrase ? `*${query}*` : query
            
            const data = await search({
                query: parsedQuery,
                minDate: dateRange.start,
                maxDate: dateRange.end,
                manx: searchLanguage == "Manx",
                english: searchLanguage == "English"
            })

            // ensure the return value is valid
            if (data.query != parsedQuery) {
                return null
            }
            
            return data
        }

        if (hasNoSearch) {
            return
        }

        startTransition(async () => {
            try {
                const maybeData = await getData()
                if (maybeData == null || maybeData.query != currentQuery.current) {
                    return
                }
                setResult({ status: "success", data: maybeData })
            } catch (e) {
                setResult({ status: "error" })
                console.error(e)
            }
        })
    }, [dateRange, query, searchLanguage, matchPhrase, hasNoSearch])

    const handleChange = (event: ChangeEvent<HTMLInputElement>) => {
        setQuery(event.target.value)
    }

    const statsPromise = useMemo(() => getCorpusStatistics().catch(() => "error" as const), [])

    return (
        <div>
            <div className="search-options">

                <div id={"corpus-search-box-container"} style={{display: "flex", flex: 1}}>
                    <ManxEnglishSelector initialLanguage={searchLanguage} onLanguageChange={setSearchLanguage}/>
                    <SearchBar query={query} onChange={handleChange}/>
                </div>

                <div style={{clear: "both"}} />

                <AdvancedOptions onDateRangeChange={setDateRange} onMatchChange={setMatchPhrase} />

            </div>

            {hasNoSearch && <Suspense fallback={<ProgressBar/>}>
                <HomeIntro statsPromise={statsPromise}/>
            </Suspense>}

            {!hasNoSearch && result?.status === "error" && <span className={"homeText"}>
                Something went wrong, please try again
            </span>}

            {!hasNoSearch && result === null && isPending && <ProgressBar/>}

            {!hasNoSearch && result?.status === "success" &&
                // dim stale results when there is a pending update
                <div style={{opacity: isPending ? 0.5 : 1, transition: "opacity 150ms ease"}}>
                    <SearchResultHeader
                        response={result.data} />
                    <MainSearchResults
                        query={result.data.query}
                        results={result.data.results}
                        manx={ searchLanguage == "Manx" }
                        english={ searchLanguage == "English" }/>
                </div>}

        </div>
    )
}

const HomeIntro = ({ statsPromise }: { statsPromise: Promise<Statistics | "error"> }) => {
    const stats = use(statsPromise)
    return (
        <span className={"homeText"}>
            {stats != "error" ?
                <span className={"homeText"} style={{textAlign: "center"}}>
                    <span style={{display: "inline"}}>Search our growing collection of over <b title={`${stats.uniqueManxWordCount.toLocaleString()} unique words`}>{stats.manxWordCount.toLocaleString()} Manx words</b> or&nbsp;<a href={"/Browse"}>browse&nbsp;{stats.documentCount.toLocaleString()} documents</a></span>
                </span>
            :
                <>
                    <span className={"homeText"}>
                        <span style={{display: "inline"}}>Enter a search term, or&nbsp;<a href={"/Browse"}>Browse</a>&nbsp;all content</span>
                    </span>
                </>
            }
            <div><Suspense fallback={null}><NewDocList/></Suspense></div>
            <span style={{display: "inline", marginTop: "1em"}}>Support our revitalisation efforts by <a href={"/MailingList"}>signing up for our mailing list</a>. We'll email once in a while with updates to the corpus & other projects.</span>
            <br/>
            <span>If we're missing anything, please let us know at</span>
            <span><a href="mailto:corpus-submissions@gaelg.im">corpus-submissions@gaelg.im</a>.</span>
        </span>
    )
}

const SearchResultHeader = (props: { response: SearchResponse })  => {
    const { response } = props
    const query = response.query ?? ""

    const isDict = hasDictionaryDefinitions(response.definedInDictionaries)
    const isTranslation = hasTranslations(response.translations)
    
    return (
        <div>
            <hr />
            Found { response.numberOfResults} matches in { response.numberOfDocuments} texts
            <br/><br/>
            { isDict && <><DictionaryLink query={ query } dictionaries={ response.definedInDictionaries }/></> }
            { isTranslation && <><TranslationList translations={response.translations} /></ >}
            { (isDict || isTranslation) && <br/>}
        </div>
    )
}

const ProgressBar = () => {
    return <div style={{
        marginTop: 40,
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
    }}>
        <CircularProgress style={{alignSelf: "center"}} />
    </div>
}
