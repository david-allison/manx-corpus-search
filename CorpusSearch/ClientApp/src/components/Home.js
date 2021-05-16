import './Home.css';

import React, { Component } from 'react';
import qs from "qs";
import Slider from '@material-ui/core/Slider';
import Typography from '@material-ui/core/Typography';
import MainSearchResults from './MainSearchResults'
import { DictionaryLink } from './DictionaryLink'

export class Home extends Component {
    static displayName = Home.name;
    static currentYear = new Date().getFullYear();

    constructor(props) {
        super(props);
        const { q } = qs.parse(this.props.location.search, { ignoreQueryPrefix: true });
        this.state = {
            forecasts: [],
            loading: true,
            value: q ?? '',
            searchManx: true,
            searchEnglish: false,
            dateRange: [1500, Home.currentYear]
        };

        this.handleChange = this.handleChange.bind(this);
        this.handleDateChange = this.handleDateChange.bind(this);
        this.handleDateChangeCommitted = this.handleDateChangeCommitted.bind(this);

        this.handleManxChange = this.handleManxChange.bind(this);
        this.handleEnglishChange = this.handleEnglishChange.bind(this);

    }

    componentDidMount() {
        this.populateData();
    }
    //<MainSearchResults products={response.results} />
    static renderGeneralTable(response, value, searchManx, searchEnglish) {
        let query = response.query ? response.query : '';
        return (
            <div>
                <hr />
                Returned { response.numberOfResults} matches in { response.numberOfDocuments} texts [{response.timeTaken }] for query '{ query  }'
                <br /><br />
                { response.definedInDictionaries && <><DictionaryLink query={ query } dictionaries={ response.definedInDictionaries }/><br/><br/></> }
                <MainSearchResults query={query} results={response.results} manx={ searchManx } english={ searchEnglish }/>

            </div>
        );
    }

    handleDateChange(event, value) {
        this.setState({ dateRange: value });
    }

    handleDateChangeCommitted(event, value) {
        this.setState({ dateRange: value }, () => this.populateData());
    }

    handleManxChange(event) {
        this.setState({ searchManx: event.target.checked, searchEnglish: !event.target.checked }, () => this.populateData());
    }

    handleEnglishChange(event) {
        this.setState({ searchEnglish: event.target.checked, searchManx: !event.target.checked }, () => this.populateData());
    }

    render() {
        let searchResults = this.state.loading
            ? <p></p>
            : Home.renderGeneralTable(this.state.forecasts, this.state.value, this.state.searchManx, this.state.searchEnglish);

        return (
            <div>
                <div className="search-options">
                    <input id="corpus-search-box" placeholder="Enter search term" type="text" value={this.state.value} onChange={this.handleChange} />


                    <div className="search-language">
                        Language: 
                        <label htmlFor="manxSearch" id="manxSearchLabel">Manx</label> <input id="manxSearch" type="checkbox" checked={this.state.searchManx} defaultChecked={this.state.searchManx} onChange={this.handleManxChange} />
                        <label htmlFor="englishSearch">English</label> <input id="englishSearch" type="checkbox" checked={this.state.searchEnglish}  defaultChecked={this.state.searchEnglish} onChange={this.handleEnglishChange} /><br />
                    </div>

                    <details className="advanced-options">
                        <summary>Advanced Options</summary>


                        <Typography id="range-output" gutterBottom>
                            Dates: {this.state.dateRange[0]}&ndash;{this.state.dateRange[1]}
                        </Typography>

                        <Slider
                            value={this.state.dateRange}
                            min={ 1500 }
                            max={ Home.currentYear }
                            valueLabelDisplay="auto"
                            onChange={this.handleDateChange}
                            onChangeCommitted={ this.handleDateChangeCommitted }
                            aria-labelledby="range-slider"
                            />
                    </details>

                </div>
                {searchResults}
            </div>
        );
    }

    async populateData() {
        const response = await fetch(`search/search/${encodeURIComponent(this.state.value)}?minDate=${this.state.dateRange[0]}&maxDate=${this.state.dateRange[1]}&manx=${this.state.searchManx}&english=${this.state.searchEnglish}`);
        const data = await response.json();

        // Handle C# casting an empty list to null
        if (data.results === null) {
            data.results = [];
        }

        if (data.query === this.state.value) {
            this.setState({ forecasts: data, loading: false });
        }
    }

    handleChange(event) {
        this.props.history.replace(`/?q=${event.target.value}`)
        this.setState({ value: event.target.value }, () => this.populateData());
    }
}
