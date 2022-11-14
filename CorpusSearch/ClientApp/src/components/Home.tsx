import './Home.css';

import React, { Component } from 'react';
import qs from "qs";
import MainSearchResults from './MainSearchResults'
import { DictionaryLink } from './DictionaryLink'
import { TranslationList } from './TranslationList'
import AdvancedOptions from "./AdvancedOptions";

type State = { 
    forecasts: [] | any 
    loading: boolean,
    value: string,
    searchLanguage: SearchLanguage
    dateRange: number[]
    matchPhrase: boolean
};


type SearchLanguage = "English" | "Manx"

export class Home extends Component<{}, State> {
    static displayName = Home.name;
    static currentYear = new Date().getFullYear();

    constructor(props: {}) {
        super(props);
        const { q } = qs.parse((this.props as any).location.search, { ignoreQueryPrefix: true });
        this.state = {
            forecasts: [],
            loading: true,
            value: q?.toString() ?? '',
            searchLanguage: "Manx",
            dateRange: [1500, Home.currentYear],
            matchPhrase: false,
        };

        this.handleChange = this.handleChange.bind(this);
        
        this.handleManxChange = this.handleManxChange.bind(this);
        this.handleEnglishChange = this.handleEnglishChange.bind(this);
        this.handleMatchPhraseChange = this.handleMatchPhraseChange.bind(this);

        this.getQuery = this.getQuery.bind(this);

    }

    componentDidMount() {
        this.populateData();
    }
    //<MainSearchResults products={response.results} />
    static renderGeneralTable(response: any, searchLanguage: SearchLanguage) {
        let query = response.query ? response.query : '';
        return (
            <div>
                <hr />
                Returned { response.numberOfResults} matches in { response.numberOfDocuments} texts [{response.timeTaken }] for query '{ query  }'
                <br />
                {/* @ts-expect-error TS(2769): No overload matches this call. */}
                { response.definedInDictionaries && <><DictionaryLink query={ query } dictionaries={ response.definedInDictionaries }/><br/></> }
                {/* @ts-expect-error TS(2769): No overload matches this call. */}
                { response.translations && <><TranslationList translations={response.translations} /></ >}
                <br /><br />
                <MainSearchResults query={query} results={response.results} manx={ searchLanguage == "Manx" } english={ searchLanguage == "English" }/>

            </div>
        );
    }



    handleManxChange(event: any) {
        this.setState({ searchLanguage: "Manx" }, () => this.populateData());
    }

    handleEnglishChange(event: any) {
        this.setState({ searchLanguage: "English" }, () => this.populateData());
    }

    handleMatchPhraseChange(event: any) {
        this.setState({ matchPhrase: event.target.checked }, () => this.populateData());
    }

    render() {
        let searchResults = this.state.loading
            ? <p></p>
            : Home.renderGeneralTable(this.state.forecasts, this.state.searchLanguage);

        return (
            <div>
                <div className="search-options">
                    <a style={{"float":"right"}} href="https://github.com/david-allison/manx-corpus-search/blob/master/CorpusSearch/Docs/searching.md#searching" target="_blank" rel="noreferrer">Search Help ℹ</a>
                    <input id="corpus-search-box" placeholder="Enter search term" type="text" value={this.state.value} onChange={this.handleChange} /> 


                    <div className="search-language">
                        Language: 
                        <label htmlFor="manxSearch" id="manxSearchLabel">Manx</label> <input id="manxSearch" type="checkbox" checked={this.state.searchLanguage == "Manx"} defaultChecked={this.state.searchLanguage == "Manx"} onChange={this.handleManxChange} />
                        <label htmlFor="englishSearch">English</label> <input id="englishSearch" type="checkbox" checked={this.state.searchLanguage == "English"} defaultChecked={this.state.searchLanguage == "English"} onChange={this.handleEnglishChange} /> 
                        <label style={{ "paddingLeft": "5px" }} htmlFor="matchPhrase">Match Phrase</label> <input id="matchPhrase" type="checkbox" checked={this.state.matchPhrase} onChange={this.handleMatchPhraseChange} /><br />
                    </div>

                    <AdvancedOptions onDateRangeChange={(v) => {
                        this.setState({ dateRange: [v.start, v.end] }, () => this.populateData());
                    }} />

                </div>
                {searchResults}
            </div>
        );
    }

    getQuery() {
        return this.state.matchPhrase ? "*" + this.state.value + "*" : this.state.value;
    }

    async populateData() {
        const response = await fetch(`search/search/${encodeURIComponent(this.getQuery())}?minDate=${this.state.dateRange[0]}&maxDate=${this.state.dateRange[1]}&manx=${this.state.searchLanguage == "Manx"}&english=${this.state.searchLanguage == "English"}`);
        const data = await response.json();

        // Handle C# casting an empty list to null
        if (data.results === null) {
            data.results = [];
        }

        if (data.query === this.getQuery()) {
            this.setState({ forecasts: data, loading: false });
        }
    }

    handleChange(event: any) {
        (this.props as any).navigation(`/?q=${event.target.value}`, { replace: true });
        this.setState({ value: event.target.value }, () => this.populateData());
    }
}
