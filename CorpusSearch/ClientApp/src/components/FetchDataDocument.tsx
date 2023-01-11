/* eslint @typescript-eslint/no-misused-promises: 0 */  
import "./FetchDataDocument.css"

import React, {useEffect, useState} from "react"
import qs from "qs"
import {Link, useLocation, useMatch} from "react-router-dom"
import {searchWork, SearchWorkResponse, SourceLink} from "../api/SearchWorkApi"
import {SearchLanguage} from "./Home"
import {CircularProgress} from "@mui/material"
import {ManxEnglishSelector} from "./ManxEnglishSelector"
import {metadataLookup} from "../api/MetadataApi"
import RecursiveProperty from "../vendor/react-json-component/RecursiveProperty"
import {ComparisonTable} from  "./ComparisonTable"

/* eslint-disable @typescript-eslint/no-unsafe-member-access, @typescript-eslint/restrict-template-expressions, @typescript-eslint/no-explicit-any, @typescript-eslint/no-unsafe-assignment */
const enrichSources = (x: any, sourceLinks: SourceLink[] | null) => {
    if (!sourceLinks || sourceLinks.length == 0) {
        return
    }
    
    if ("mnhNewsComponent" in x) {
        delete x.mnhNewsComponent
    }
    
    if ("source" in x && typeof x.source == "string") {
        x.source = { name: x["source"] }

        if (sourceLinks.length == 1) {
            x.source.link =sourceLinks[0]
        } else {
            x.source.links = sourceLinks
        }
        
        return
    }
    
    x.sources = sourceLinks
} 

const enrichGitHub = (x: any) => {
    if (!("gitHubRepo" in x) || !("relativeCsvPath" in x)) {
        return
    }

    // TODO: This is listed as 'Git Hub' due to RecursiveProperty.ts
    let path = `https://github.com/${x.gitHubRepo}/blob/master/${x.relativeCsvPath}`
    if (path.endsWith("document.csv")) {
        path = path.substring(0, path.length - "document.csv".length)
    }
    x.gitHub = {
     url: path,
     text: x.gitHubRepo   
    }
    
    delete x.gitHubRepo
    delete x.relativeCsvPath
}

const removeHandledKeys = (x: any) => {
    if ("googleBooksId" in x) {
        delete x.googleBooksId
    }
}
/* eslint-enable */


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


    // eslint-disable-next-line
    const [testJson, setTestJson] = useState<any>(null)

    useEffect(() => {
        if (searchWorkResponse == null || docIdent == null) {
            return
        }
        
        metadataLookup(docIdent)
            .then(x => {
                enrichGitHub(x)
                enrichSources(x, searchWorkResponse.sourceLinks)
                removeHandledKeys(x)
                setTestJson(x)
            })
            .catch(e => console.warn(e))
    },[searchWorkResponse])


    return (
        <div>
            <h1 id="tabelLabel" ><Link to={`/?q=${q?.toString() ?? ""}`} style={{ textDecoration: "none" }}>⇦</Link>  { title }</h1>

            <div style={{display: "flex", flex: 1, flexGrow: 2}}>
                <input size={5} id="corpus-search-box" style={{flexGrow: 1, marginRight: 12}} placeholder="Enter search term" type="text" value={value} onChange={(x) => setValue(x.target.value)} />
                <ManxEnglishSelector initialLanguage={searchLanguage} onLanguageChange={setSearchLanguage}/>
            </div>
            {loading && <div style={{
                marginTop: 40,
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
            }}>
                <CircularProgress style={{alignSelf: "center"}} />
            </div>}
            {loading || searchWorkResponse == null ||
                <>
                    { searchWorkResponse.totalMatches ? `${searchWorkResponse.totalMatches} results;` : ""} { searchWorkResponse.numberOfResults} lines [{searchWorkResponse.timeTaken}]
                    <RecursiveProperty
                        // eslint-disable-next-line
                        property={testJson}
                        propertyName={"Additional Data "}
                        excludeBottomBorder={false}
                        rootProperty={false}/>

                    { searchWorkResponse.notes && <><br />{searchWorkResponse.notes}</>}

                    <ComparisonTable
                    response={searchWorkResponse}
                    value={value}
                    highlightManx={searchManx}
                    highlightEnglish={searchEnglish}
                    translations={searchWorkResponse.translations}/></> }
        </div>
    )

}