/* eslint @typescript-eslint/no-misused-promises: 0 */  

import "./Home.css"

import React, {ChangeEvent, useEffect, useState} from "react"
import qs from "qs"
import MainSearchResults from "./MainSearchResults"
import { DictionaryLink } from "./DictionaryLink"
import { TranslationList } from "./TranslationList"
import AdvancedOptions, {DateRange} from "./AdvancedOptions"
import {useLocation, useNavigate} from "react-router-dom"
import {search, SearchResponse} from "../api/SearchApi"
import {CircularProgress} from "@mui/material"


export type SearchLanguage = "English" | "Manx"

export class Home {
    static displayName = Home.name
    static currentYear = new Date().getFullYear()
}

export const HomeFC = () => {
    const location = useLocation()
    const navigation = useNavigate()
    
    const [loading, setLoading] = useState(true)
    const [searchResponse, setSearchResponse] = useState<SearchResponse | null>(null)
    const [searchLanguage, setSearchLanguage] = useState<SearchLanguage>("Manx")
    const [query, setQuery] = useState(() => qs.parse(location.search, { ignoreQueryPrefix: true })?.q?.toString() ?? "")
    const [dateRange, setDateRange] = useState<DateRange>( { start: 1500, end: Home.currentYear })
    const [matchPhrase, setMatchPhrase] = useState(false)
    
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
        setLoading(true)
        
        getData()
            .then(maybeData => {
                setLoading(false)
                if (maybeData == null) {
                    return
                }
                setSearchResponse(maybeData)
            })
            .catch(e => {
                setLoading(false)
                console.error(e)
            })
        
    }, [dateRange, query, searchLanguage, matchPhrase])

    const handleChange = (event: React.ChangeEvent<HTMLInputElement>) => {
        navigation(`/?q=${event.target.value}`, { replace: true })
        setQuery(event.target.value)
    }

    return (
        <div>
            <div className="search-options">
                <a style={{"float":"right"}} href="https://github.com/david-allison/manx-corpus-search/blob/master/CorpusSearch/Docs/searching.md#searching" target="_blank" rel="noreferrer">Search Help ℹ</a>
                <input id="corpus-search-box" placeholder="Enter search term" type="text" value={query} onChange={handleChange} />

                <SearchLanguageBox
                    onLanguageChange={setSearchLanguage}
                    onMatchChange={setMatchPhrase}
                />

                <AdvancedOptions onDateRangeChange={setDateRange} />

            </div>

            {loading && <div style={{
                marginTop: 40,
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
            }}>
                <CircularProgress style={{alignSelf: "center"}} />
            </div>}
            
            {!loading && searchResponse != null && searchResponse.results.length > 0 && <>
                <SearchResultHeader
                    response={searchResponse} />
                <MainSearchResults
                    query={searchResponse.query}
                    results={searchResponse.results}
                    manx={ searchLanguage == "Manx" }
                    english={ searchLanguage == "English" }/>

            </>}

        </div>
    )
}

const SearchResultHeader = (props: { response: SearchResponse })  => {
    const { response } = props
    const query = response.query ?? ""

    return (
        <div>
            <hr />
            Returned { response.numberOfResults} matches in { response.numberOfDocuments} texts [{response.timeTaken }] for query '{ query  }'
            <br />
            { response.definedInDictionaries && <><DictionaryLink query={ query } dictionaries={ response.definedInDictionaries }/><br/></> }
            { response.translations && <><TranslationList translations={response.translations} /></ >}
            <br /><br />
        </div>
    )
}

const SearchLanguageBox = (props: {
    onLanguageChange: (lang: SearchLanguage) => void,
    onMatchChange: (match: boolean) => void
}) => {
    const [language, setLanguage] = useState<SearchLanguage>("Manx")
    const [matchPhrase, setMatchPhrase] = useState(false)
    
    const onMatchPhraseChanged = (event: ChangeEvent<HTMLInputElement>) => {
        setMatchPhrase(event.target.checked)
        props.onMatchChange(event.target.checked)
    }
    const onSetLanguage = (newLanguage: SearchLanguage) => {
        setLanguage(newLanguage)
        props.onLanguageChange(newLanguage)
    }
    
    return <div className="search-language">
        Language:
        <label htmlFor="manxSearch" id="manxSearchLabel">Manx</label> <input id="manxSearch" type="checkbox" checked={language == "Manx"} defaultChecked={language == "Manx"} onChange={() => onSetLanguage("Manx")} />
        <label htmlFor="englishSearch">English</label> <input id="englishSearch" type="checkbox" checked={language== "English"} defaultChecked={language == "English"} onChange={() =>onSetLanguage("English")} />
        <label style={{ "paddingLeft": "5px" }} htmlFor="matchPhrase">Match Phrase</label> <input id="matchPhrase" type="checkbox" checked={matchPhrase} onChange={onMatchPhraseChanged} /><br />
    </div>
}