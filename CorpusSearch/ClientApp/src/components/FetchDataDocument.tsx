/* eslint @typescript-eslint/no-misused-promises: 0 */  
import "./FetchDataDocument.css"

import React, {useEffect, useState} from "react"
import qs from "qs"
import Highlighter from "react-highlight-words"
import {Link, useLocation, useMatch} from "react-router-dom"
import {searchWork, SearchWorkResponse} from "../api/SearchWorkApi"
import {Translations} from "../api/SearchApi"
import {SearchLanguage} from "./Home"
import {CircularProgress} from "@mui/material"
import {ManxEnglishSelector} from "./ManxEnglishSelector"


export const FetchDataDocument = () => {
    const location = useLocation()
    const match = useMatch("/docs/:docId")

    const [loading, setLoading] = useState(true)
    const [title, setTitle] = useState("Work Search")
    
    const docIdent = match?.params.docId
    
    // the 'q' parameter from the querystring
    const { q } = qs.parse(location.search, { ignoreQueryPrefix: true })
    
    const [value, setValue] = useState(q?.toString() ?? "*")
    
    const getInitialSearchLanguage = (): SearchLanguage => {
        // eslint-disable-next-line
        switch (location.state?.searchLanguage) {
            case "English": return "English"
            case "Manx": return "Manx"
            default: return "Manx"
        }
    }
    const [searchLanguage, setSearchLanguage] = useState<SearchLanguage>(getInitialSearchLanguage)
    const searchManx = searchLanguage == "Manx"
    const searchEnglish = searchLanguage == "English"
    
    const [searchWorkResponse, setSearchWorkResponse] = useState<SearchWorkResponse | null>(null)


    // load the data
    useEffect(() => {
        const getData = async () => {
            if (!docIdent) {
                throw new Error("no identifier provided")
            }
            return await searchWork({ docIdent, value, searchEnglish, searchManx })
        }

        setLoading(true)
        getData()
            .then(data => {
                setSearchWorkResponse(data)
                setTitle(data.title)
                setLoading(false)
            })
            .catch(e => {
                setLoading(false)
                console.error(e)
            })

    }, [value, searchEnglish, searchManx])


    return (
        <div>
            <h1 id="tabelLabel" ><Link to={`/?q=${value}`} style={{ textDecoration: "none" }}>⇦</Link>  { title }</h1>

            <div style={{display: "flex", flex: 1, flexGrow: 2}}>
                <input size={5} id="corpus-search-box" style={{flexGrow: 1, marginRight: 12}} placeholder="Enter search term" type="text" value={value} onChange={(x) => setValue(x.target.value)} />
                <ManxEnglishSelector onLanguageChange={setSearchLanguage}/>
            </div>
            {loading && <div style={{
                marginTop: 40,
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
            }}>
                <CircularProgress style={{alignSelf: "center"}} />
            </div>}
            {loading || searchWorkResponse == null || <ComparisonTable
                response={searchWorkResponse}
                value={value}
                highlightManx={searchManx}
                highlightEnglish={searchEnglish}
                translations={searchWorkResponse.translations}/> }
        </div>
    )

}


function escapeRegex(s: string) {
    return s.replace(/[/\-\\^$*+?.()|[\]{}]/g, "\\$&")
}

const ComparisonTable = (props: {
    response: SearchWorkResponse, 
    value: string, 
    highlightManx: boolean, 
    highlightEnglish: boolean, 
    translations?: Translations }) => {
    const {response, value, highlightManx, highlightEnglish, translations } = props
    
    const getTranslations = (key: string) => {
        if (translations == null) return []
        return translations[key] ?? []
    }
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
                        
                            
                            const englishValue = highlightEnglish ? [value] : getTranslations("en")
                            const manxValue = highlightManx ? [value] : getTranslations("gv")
                            const english = englishValue.map(x => `(${escapeRegex(x)})`).join("|")
                            const manx = manxValue.map(x => `(${escapeRegex(x)})`).join("|")
                            const englishHighlight = englishValue.length > 0 ? [` [,\\.!]?(${english})[,\\.!]?[ (—)]`] : []
                            const manxHighlight = manxValue.length > 0 ? [` [,\\.!]?(${manx})[,\\.!]?[ (—)]`] : []
                        

                            // TODO: replace \n with <br/>: https://kevinsimper.medium.com/react-newline-to-break-nl2br-a1c240ba746
                            const englishText = line.english.split("\n").map((item, key) => {
                                return <span key={key}><Highlighter
                                    highlightClassName={highlightEnglish ? "textHighlight" : "textHighlightAlternate"}
                                    searchWords={englishHighlight}
                                    autoEscape={false}
                                    textToHighlight={item} /><br /></span>
                            })
                            const manxText = line.manx.split("\n").map((item, key) => {
                                return <span key={key}><Highlighter
                                    highlightClassName={highlightManx ? "textHighlight" : "textHighlightAlternate"}
                                    searchWords={manxHighlight}
                                    autoEscape={false}
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
                                {line.notes ? <tr><td colSpan={3} className="noteRow">{line.notes}</td></tr> : null}
                            </>
                        }
                    )}
                    </tbody>
                </table>
            </div>
        )
}
