import './FetchDataDocument.css';

import React, { Component } from 'react';
import qs from "qs";
import Highlighter from "react-highlight-words";
import { Link } from 'react-router-dom';

export class FetchDataDocument extends Component {
    static displayName = FetchDataDocument.name;

    constructor(props) {
        super(props);
        const { q } = qs.parse(this.props.location.search, { ignoreQueryPrefix: true });
        this.state = {
            forecasts: [],
            loading: true,
            title: "Work Search",
            docId: this.props.match.params.docId,
            value: q,
            searchManx: true,
            searchEnglish: false,
            fullTextSearch: false,
        };

        this.handleChange = this.handleChange.bind(this);
        this.handleChange1 = this.handleChange1.bind(this);
        this.handleChange2 = this.handleChange2.bind(this);
        this.handleChange3 = this.handleChange3.bind(this);
    }

    componentDidMount() {
        this.populateData();
    }

    static renderForecastsTable(response, value, fullTextSearch, manxhi, englishhi) {
        return (
            <div>
                Returned { response.numberOfResults} matches [{response.timeTaken}]
            <table className='table table-striped' aria-labelledby="tabelLabel">
                <thead>
                    <tr>
                        <th>Manx</th>
                        <th>English</th>
                        <th>Link</th>
                    </tr>
                </thead>
                <tbody>
                    {response.results.map(line =>
                    {
                        const link = process.env.PUBLIC_URL + "Coyrle%20Sodjey%20G%20as%20B.pdf#page=" + line.page
                        const eng = [...englishhi, " " + value + " "];
                        const manx = [...manxhi, " " + value + " "];
                        const englishHighlight = fullTextSearch ? [value] : eng
                        const manxHighlight = fullTextSearch ? [value] : manx

                        // TODO: replace \n with <br/>: https://kevinsimper.medium.com/react-newline-to-break-nl2br-a1c240ba746
                        let englishText = line.english.split('\n').map((item, key) => {
                            return <span key={key}><Highlighter
                                highlightClassName="textHighlight"
                                searchWords={englishHighlight}
                                autoEscape={true}
                                textToHighlight={item} /><br /></span>
                        })
                        let manxText = line.manx.split('\n').map((item, key) => {
                            return <span key={key}><Highlighter
                                highlightClassName="textHighlight"
                                searchWords={manxHighlight}
                                autoEscape={true}
                                textToHighlight={item} /><br /></span>
                        })

                            return <tr key={line.date}>
                                <td>
                                    {manxText}
                                </td>
                                <td>
                                    { englishText }
                                </td>
                                <td>
                                    {line.page != null &&
                                        <a href={link} target="_blank">p{line.page}</a> }

                                </td>
                            </tr>;
                        }
                    )}
                </tbody>
                </table>
             </div>
        );
    }

    render() {
        let contents = this.state.loading
            ? <p></p>
            : FetchDataDocument.renderForecastsTable(this.state.forecasts, this.state.value, this.state.fullTextSearch, this.state.manxHighlights, this.state.englishHighlights);

        return (
            <div>
                <h1 id="tabelLabel" ><Link to={`/?q=${this.state.value}`} style={{ textDecoration: 'none' }}>⇦</Link>  { this.state.title }</h1>

                <input type="text" id="corpus-search-box" value={this.state.value} onChange={this.handleChange} />
                <label for="manxSearch">Manx</label> <input id="manxSearch" type="checkbox" defaultChecked={this.state.searchManx} onChange={this.handleChange2} /><br/>
                <label for="englishSearch">English</label> <input id="englishSearch" type="checkbox" defaultChecked={this.state.searchEnglish} onChange={this.handleChange1} /><br/>
                <label for="fullTextSearch">Full Text Search</label> <input id="fullTextSearch" type="checkbox" defaultChecked={this.state.fullTextSearch} onChange={this.handleChange3} />
                {contents}
            </div>
        );
    }

    async populateData() {
        const response = await fetch(`search/searchWork/${this.state.docId}/${encodeURIComponent(this.state.value)}?english=${this.state.searchEnglish}&manx=${this.state.searchManx}&fullTextSearch=${this.state.fullTextSearch}`);
        const data = await response.json();
        this.setState({ forecasts: data, title: data.title, loading: false, manxHighlights: data.manxTranslations, englishHighlights: data.englishTranslations });
    }

    handleChange(event) {
        this.setState({ value: event.target.value }, () => this.populateData());
    }

    handleChange1(event) {
        this.setState({ searchEnglish: event.target.checked }, () => this.populateData());
    }

    handleChange2(event) {
        this.setState({ searchManx: event.target.checked }, () => this.populateData());
    }

    handleChange3(event) {
        this.setState({ fullTextSearch: event.target.checked }, () => this.populateData());
    }
}
