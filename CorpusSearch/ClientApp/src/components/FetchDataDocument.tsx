/* eslint @typescript-eslint/no-misused-promises: 0 */  
import "./FetchDataDocument.css"

import React, { Component } from "react"
import qs from "qs"
// @ts-expect-error TS(7016): Could not find a declaration file for module 'reac... Remove this comment to see the full error message
import Highlighter from "react-highlight-words"
import {Link, Location, PathMatch} from "react-router-dom"
import {searchWork, SearchWorkResponse} from "../api/SearchWorkApi"


    
type State = {
    forecasts: SearchWorkResponse | [],
    loading: boolean,
    title: string,
    docIdent: string | undefined
    value: string,
    searchManx: boolean
    searchEnglish: boolean
};

export class FetchDataDocument extends Component<{ location: Location, match: PathMatch<"docId"> | null}, State> {
    static displayName = FetchDataDocument.name

    constructor(props: {location: Location, match: PathMatch<"docId"> | null}) {
        super(props)
        const { q } = qs.parse(props.location.search, { ignoreQueryPrefix: true })
        this.state = {
            forecasts: [],
            loading: true,
            title: "Work Search",
            docIdent: props.match?.params.docId,
            value: q?.toString() ?? "",
            // eslint-disable-next-line 
            searchManx: props.location.state ? props.location.state.searchLanguage == "Manx" : true,
            // eslint-disable-next-line 
            searchEnglish: props.location.state ? props.location.state.searchLanguage == "English" : false,
        }

        this.onQueryChanged = this.onQueryChanged.bind(this)
        this.onSearchEnglishChanged = this.onSearchEnglishChanged.bind(this)
        this.onSearchManxChanged = this.onSearchManxChanged.bind(this)
    }

    async componentDidMount() {
        await this.populateData()
    }

    static renderForecastsTable(response: SearchWorkResponse, value: string, manxhi: string[], englishhi: string[]) {
        return (
            <div>
                { response.totalMatches} results ({ response.numberOfResults} lines) [{response.timeTaken}]

                { response.notes && <><br /><br />{response.notes}</>}

                { response.source && <><br /><br />{response.source}</>} { response.sourceLinks && <>{response.sourceLinks.map(x => <>| <a rel="noreferrer" href={x.url}>{x.text}</a> </>)}</>}
            <table className='table table-striped' aria-labelledby="tabelLabel">
                <thead>
                    <tr>
                        <th>Manx</th>
                        <th>English</th>
                        <th>Link</th>
                    </tr>
                </thead>
                <tbody>
                    {response.results.map(line => {
                        const eng = [...englishhi, " " + value + " "]
                        const manx = [...manxhi, " " + value + " "]
                        const englishHighlight = eng
                        const manxHighlight = manx

                        // TODO: replace \n with <br/>: https://kevinsimper.medium.com/react-newline-to-break-nl2br-a1c240ba746
                        const englishText = line.english.split("\n").map((item, key) => {
                            return <span key={key}><Highlighter
                                highlightClassName="textHighlight"
                                searchWords={englishHighlight}
                                autoEscape={true}
                                textToHighlight={item} /><br /></span>
                        })
                        const manxText = line.manx.split("\n").map((item, key) => {
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
                                    {response.gitHubLink && <a href={`${response.gitHubLink}#L${line.csvLineNumber}`}>
                                        edit
                                    </a>}
                                </td>
                            </tr>
                            {/* @ts-expect-error TS(2322): Type 'string' is not assignable to type 'number'. */}
                            {line.notes ? <tr><td colSpan="3" className="noteRow">{line.notes}</td></tr> : null}
                            </>
                        }
                    )}
                </tbody>
                </table>
             </div>
        )
    }

    render() {
        const contents = this.state.loading
            ? <p></p>
            : FetchDataDocument.renderForecastsTable(this.state.forecasts as SearchWorkResponse, this.state.value, [], []) // TODO: Add highlights back in based on data.translations

        return (
            <div>
                <h1 id="tabelLabel" ><Link to={`/?q=${this.state.value}`} style={{ textDecoration: "none" }}>⇦</Link>  { this.state.title }</h1>

                <input type="text" id="corpus-search-box" value={this.state.value} onChange={(x) => this.onQueryChanged(x)} />
                {/* @ts-expect-error TS(2322): Type '{ children: string; for: string; }' is not a... Remove this comment to see the full error message */}
                <label for="manxSearch">Manx</label> <input id="manxSearch" type="checkbox" checked={this.state.searchManx} onChange={(x) => this.onSearchManxChanged(x)} /><br/>
                {/* @ts-expect-error TS(2322): Type '{ children: string; for: string; }' is not a... Remove this comment to see the full error message */}
                <label for="englishSearch">English</label> <input id="englishSearch" type="checkbox" checked={this.state.searchEnglish}  onChange={(x) => this.onSearchEnglishChanged(x)} /><br/>
                {contents}
            </div>
        )
    }

    async populateData() {
        if (!this.state.docIdent) {
            throw new Error("no identifier provided")
        }
        const { docIdent, value, searchManx, searchEnglish } = this.state 
        const data = await searchWork({ docIdent, value, searchEnglish, searchManx })
        
        this.setState({ 
            forecasts: data, 
            title: data.title, 
            loading: false, 
        })
    }

    onQueryChanged(event: React.ChangeEvent<HTMLInputElement>) {
        // eslint-disable-next-line 
        this.setState({ value: event.target.value }, () => this.populateData())
    }

    onSearchEnglishChanged(event: React.ChangeEvent<HTMLInputElement>) {
        this.setState({ searchEnglish: event.target.checked, searchManx: !event.target.checked }, () => this.populateData())
    }

    onSearchManxChanged(event: React.ChangeEvent<HTMLInputElement>) {
        this.setState({ searchManx: event.target.checked, searchEnglish: !event.target.checked }, () => this.populateData())
    }
}
