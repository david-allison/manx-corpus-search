/* eslint @typescript-eslint/no-misused-promises: 0 */  

import "./Home.css"

import React, {ChangeEvent, Component, useState} from "react"
import qs from "qs"
import MainSearchResults from "./MainSearchResults"
import { DictionaryLink } from "./DictionaryLink"
import { TranslationList } from "./TranslationList"
import AdvancedOptions from "./AdvancedOptions"
import {Location, NavigateFunction } from "react-router-dom"

export type DefinedInDictionaries = Record<string, string[]> // Dictionary<string, string[]>
export type Translations = Record<string, string[]> // Dictionary<string, IList<string>>

export type SearchResponse = {
    results: SearchResultEntry[]
    query: string
    numberOfResults: number
    numberOfDocuments: number
    timeTaken: string
    definedInDictionaries: DefinedInDictionaries
    translations: Translations
}

type date = string

export type SearchResultEntry = {
    startDate: date
    documentName: string
    count: number
    endDate: date
    ident: string
    sample: string
}


type State = { 
    forecasts: [] | SearchResponse 
    loading: boolean,
    value: string,
    searchLanguage: SearchLanguage
    dateRange: number[]
    matchPhrase: boolean
};


type SearchLanguage = "English" | "Manx"

export class Home extends Component<{ location: Location, navigation: NavigateFunction }, State> {
    static displayName = Home.name
    static currentYear = new Date().getFullYear()

    constructor(props: { location: Location, navigation: NavigateFunction  }) {
        super(props)
        const { q } = qs.parse(props.location.search, { ignoreQueryPrefix: true })
        this.state = {
            forecasts: [],
            loading: true,
            value: q?.toString() ?? "",
            searchLanguage: "Manx",
            dateRange: [1500, Home.currentYear],
            matchPhrase: false,
        }

        this.handleChange = this.handleChange.bind(this)

        this.getQuery = this.getQuery.bind(this)

    }

    async componentDidMount() {
        await this.populateData()
    }
    //<MainSearchResults products={response.results} />
    static renderGeneralTable(response: SearchResponse, searchLanguage: SearchLanguage) {
        const query = response.query ? response.query : ""
        return (
            <div>
                <hr />
                Returned { response.numberOfResults} matches in { response.numberOfDocuments} texts [{response.timeTaken }] for query '{ query  }'
                <br />
                { response.definedInDictionaries && <><DictionaryLink query={ query } dictionaries={ response.definedInDictionaries }/><br/></> }
                { response.translations && <><TranslationList translations={response.translations} /></ >}
                <br /><br />
                <MainSearchResults query={query} results={response.results} manx={ searchLanguage == "Manx" } english={ searchLanguage == "English" }/>

            </div>
        )
    }

    render() {
        const searchResults = this.state.loading
            ? <p></p>
            : Home.renderGeneralTable(this.state.forecasts as SearchResponse, this.state.searchLanguage)

        return (
            <div>
                <div className="search-options">
                    <a style={{"float":"right"}} href="https://github.com/david-allison/manx-corpus-search/blob/master/CorpusSearch/Docs/searching.md#searching" target="_blank" rel="noreferrer">Search Help ℹ</a>
                    <input id="corpus-search-box" placeholder="Enter search term" type="text" value={this.state.value} onChange={(x) => this.handleChange(x)} /> 

                    <SearchLanguageBox 
                        
                        onLanguageChange={lang => this.setState({ searchLanguage: lang }, () =>  this.populateData())}
                        onMatchChange={isMatch => this.setState({ matchPhrase: isMatch }, () =>  this.populateData())}
                    />

                    <AdvancedOptions onDateRangeChange={(v) => {
                        this.setState({ dateRange: [v.start, v.end] }, async () => await this.populateData())
                    }} />

                </div>
                {searchResults}
            </div>
        )
    }

    getQuery() {
        return this.state.matchPhrase ? "*" + this.state.value + "*" : this.state.value
    }

    async populateData() {
        const response = await fetch(`search/search/${encodeURIComponent(this.getQuery())}?minDate=${this.state.dateRange[0]}&maxDate=${this.state.dateRange[1]}&manx=${(this.state.searchLanguage == "Manx").toString()}&english=${(this.state.searchLanguage == "English").toString()}`)
        // TODO: Validation
        const data: SearchResponse = await response.json() as SearchResponse

        // Handle C# casting an empty list to null
        if (data.results === null) {
            data.results = []
        }

        if (data.query === this.getQuery()) {
            this.setState({ forecasts: data, loading: false })
        }
    }

    handleChange(event: React.ChangeEvent<HTMLInputElement>) {
        this.props.navigation(`/?q=${event.target.value}`, { replace: true })
        // eslint-disable-next-line 
        this.setState({ value: event.target.value }, async () => await this.populateData())
    }
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