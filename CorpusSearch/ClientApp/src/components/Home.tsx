/* eslint @typescript-eslint/no-misused-promises: 0 */  

import "./Home.css"

import React, {ChangeEvent, Component, useState} from "react"
import qs from "qs"
import MainSearchResults from "./MainSearchResults"
import { DictionaryLink } from "./DictionaryLink"
import { TranslationList } from "./TranslationList"
import AdvancedOptions from "./AdvancedOptions"
import {Location, NavigateFunction } from "react-router-dom"
import {search, SearchResponse} from "../api/SearchApi"


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


    render() {
        
        const { searchLanguage, forecasts } = this.state
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
                {this.state.loading || <>
                    <SearchResultHeader 
                        response={forecasts as SearchResponse} />
                    <MainSearchResults 
                        query={(forecasts as SearchResponse).query} 
                        results={(forecasts as SearchResponse).results} 
                        manx={ searchLanguage == "Manx" } 
                        english={ searchLanguage == "English" }/>

                </>}

            </div>
        )
    }

    getQuery() {
        return this.state.matchPhrase ? "*" + this.state.value + "*" : this.state.value
    }

    async populateData() {
        const { dateRange, searchLanguage } = this.state
        
        const data = await search({ 
            query: this.getQuery(),
            minDate: dateRange[0],
            maxDate: dateRange[1],
            manx: searchLanguage == "Manx",
            english: searchLanguage == "English"
        })
        
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