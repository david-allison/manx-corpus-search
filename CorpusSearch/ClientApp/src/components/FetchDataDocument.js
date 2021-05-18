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
            docIdent: this.props.match.params.docId,
            value: q,
            searchManx: props.location.state ? props.location.state.searchManx : true,
            searchEnglish: props.location.state ? props.location.state.searchEnglish : false,
        };

        this.onQueryChanged = this.onQueryChanged.bind(this);
        this.onSearchEnglishChanged = this.onSearchEnglishChanged.bind(this);
        this.onSearchManxChanged = this.onSearchManxChanged.bind(this);
    }

    componentDidMount() {
        this.populateData();
    }

    static renderForecastsTable(response, value, manxhi, englishhi) {
        return (
            <div>
                { response.totalMatches} results ({ response.numberOfResults} lines) [{response.timeTaken}]
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
                        const eng = [...englishhi, " " + value + " "];
                        const manx = [...manxhi, " " + value + " "];
                        const englishHighlight = eng
                        const manxHighlight = manx

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

                            return <><tr key={line.date}>
                                <td>
                                    {manxText}
                                </td>
                                <td>
                                    { englishText }
                                </td>
                                <td>
                                    {line.page != null && response.pdfLink &&
                                        <a href={response.pdfLink + "#page=" + line.page} target="_blank" rel="noreferrer">p{line.page}</a> }
                                    {response.gitHubLink && <a href={response.gitHubLink + "#L" + line.csvLineNumber}>
                                        edit
                                    </a>}
                                </td>
                            </tr>
                            {line.notes ? <tr><td colspan="3" className="noteRow">{line.notes}</td></tr> : null}
                            </>;
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
            : FetchDataDocument.renderForecastsTable(this.state.forecasts, this.state.value, [], []); // TODO: Add highlights back in based on data.translations

        return (
            <div>
                <h1 id="tabelLabel" ><Link to={`/?q=${this.state.value}`} style={{ textDecoration: 'none' }}>⇦</Link>  { this.state.title }</h1>

                <input type="text" id="corpus-search-box" value={this.state.value} onChange={this.onQueryChanged} />
                <label for="manxSearch">Manx</label> <input id="manxSearch" type="checkbox" checked={this.state.searchManx} onChange={this.onSearchManxChanged} /><br/>
                <label for="englishSearch">English</label> <input id="englishSearch" type="checkbox" checked={this.state.searchEnglish}  onChange={this.onSearchEnglishChanged} /><br/>
                {contents}
            </div>
        );
    }

    async populateData() {
        const response = await fetch(`search/searchWork/${this.state.docIdent}/${encodeURIComponent(this.state.value)}?english=${this.state.searchEnglish}&manx=${this.state.searchManx}`);
        const data = await response.json();
        this.setState({ forecasts: data, title: data.title, loading: false, translations: data.translations });
    }

    onQueryChanged(event) {
        this.setState({ value: event.target.value }, () => this.populateData());
    }

    onSearchEnglishChanged(event) {
        this.setState({ searchEnglish: event.target.checked, searchManx: !event.target.checked }, () => this.populateData());
    }

    onSearchManxChanged(event) {
        this.setState({ searchManx: event.target.checked, searchEnglish: !event.target.checked }, () => this.populateData());
    }
}
