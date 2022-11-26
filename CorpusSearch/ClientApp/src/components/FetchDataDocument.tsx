/* eslint @typescript-eslint/no-misused-promises: 0 */  
import "./FetchDataDocument.css"

import React, {useEffect, useState} from "react"
import qs from "qs"
import Highlighter from "react-highlight-words"
import {Link, useLocation, useMatch} from "react-router-dom"
import {searchWork, SearchWorkResponse, SourceLink} from "../api/SearchWorkApi"
import {Translations} from "../api/SearchApi"
import {SearchLanguage} from "./Home"
import {Box, CircularProgress, Modal} from "@mui/material"
import {ManxEnglishSelector} from "./ManxEnglishSelector"
import Typography from "@mui/material/Typography"
import {manxDictionaryLookup} from "../api/DictionaryApi"
import {metadataLookup} from "../api/MetadataApi"
import RecursiveProperty from "../vendor/react-json-component/RecursiveProperty"
import {diffChars} from "diff"
import {getSelectedWordOrPhrase} from "../utils/Selection"

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
    
    const onClickWordForDictionaryLookup = () => {
        const selection = window.getSelection()
        if (selection == null) {
            return
        }
        
        const range = getSelectedWordOrPhrase(selection)
        
        if (range == null) {
            return
        }
        
        // remove notes/citations '[1]' at the end of the string
        const stringToSearch = range.replace(/\[\d+]/g, " ")
        
        setModalOpen(true)
        setModalText(stringToSearch.trim())
    }

    const [modalOpen, setModalOpen] = React.useState(false)
    const [modalText, setModalText] = useState("")
    const handleModalClose = () => setModalOpen(false)
    
    const [modalValue, setModalValue] = useState<string | null>(null)
    
    useEffect(() => {
        setModalValue(null)
        if (!modalText) return
         manxDictionaryLookup(modalText).then((summaries) => {
             // we need a primaryWord as we something match 'dy hroggal' -> 'hroggal'
             // This also matches 'cha greck' -> 'greck' and we need to differentiate this.
             const finalString = summaries.map(x => `<strong>${x.primaryWord}</strong>: ${x.summary}`).join("<br><br>")
             setModalValue(finalString)
         })
             .catch(e => { console.warn(e)})
    }, [modalText])

    // TODO: We use original for two concepts:
    // The Original text (compared to a corrected text)
    // The original text (whether Manx -> English or English -> Manx)
    const originalManx = response.original != "English" // anything other than English is Manx

    
    const highlightText = (shouldHighlight: boolean, languageCode: "gv" | "en", lineValue: string) => {
        const manxValue = shouldHighlight ? [value] : getTranslations(languageCode)
        const manx = manxValue.map(x => `(${escapeRegex(x)})`).join("|")
        // no highlighting if we don't have a value 
        const manxHighlight = manxValue.length > 0 && value ? [` [,\\.!]?(${manx})[,\\.!]?[ (—)]`] : []
        return lineValue.split("\n").map((item, key) => <span onClick={() => {
            if (languageCode == "gv") {
                onClickWordForDictionaryLookup()
            }
           }} key={key}><Highlighter
            highlightClassName={shouldHighlight ? "textHighlight" : "textHighlightAlternate"}
            searchWords={manxHighlight}
            autoEscape={false}
            textToHighlight={item} /><br /></span>
        )
    }
    
    /** 
     * If originalText exists, perform a diff and display this to the user
     * This displays changes we made to the document
     */
    const diffCorrectedText = (originalText: string | undefined, currentText: string): React.ReactNode | null => {
        if (!originalText) {
            return null
        }
        const result = diffChars(originalText, currentText)

        // TODO: This only handles the correction, not the original
        return <span onClick={() => {
            onClickWordForDictionaryLookup()
        }}>
            {value != "*" && value != "" && <div style={{textAlign: "center", backgroundColor: "rgba(255,255,0,0.3)" }}>highlighting disabled</div>}
            {result.map(part => {
                const color = part.added ? "rgba(0, 128, 0, 0.3)" : part.removed ? "rgba(255, 0, 0, 0.3)" : ""
                return <span style={{backgroundColor: color}}>{part.value}</span>
            })}
        </span>
    }
    
    const getTranslations = (key: string) => {
        if (translations == null) return []
        return translations[key] ?? []
    }
        return (
            <>
            <div>
                <table className='table table-striped' style={{tableLayout: "fixed"}} aria-labelledby="tabelLabel">
                    <thead>
                    <tr>
                        <th>{originalManx ? "Manx" : "English"}</th>
                        <th>{originalManx ? "English" : "Manx"}</th>
                        <th style={{width: 45}}>Link</th>
                    </tr>
                    </thead>
                    <tbody>
                    {response.results.map(line => {
                            // TODO: Only due to technical reasons, we can't mix highlights and diffs. 
                            // This should be fixed via vendoring react-highlight-words's `Highlighter` class
                            const manxText = diffCorrectedText(line.manxOriginal, line.manx) ?? highlightText(highlightManx, "gv", line.manx)
                            const englishText = diffCorrectedText(line.englishOriginal, line.english) ?? highlightText(highlightEnglish, "en", line.english)

                            return <><tr key={line.date}>
                                <td>
                                    {originalManx ? manxText : englishText}
                                </td>
                                <td>
                                    {originalManx ? englishText : manxText }
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

                <Modal
                    open={modalOpen}
                    onClose={handleModalClose}
                    aria-labelledby="modal-modal-title"
                    aria-describedby="modal-modal-description"
                >
                    <Box sx={style}>
                        <Typography id="modal-modal-title" variant="h6" component="h2">
                            {modalText}
                        </Typography>
                        <Typography id="modal-modal-description" sx={{ mt: 2 }}>
                            {modalValue == null && <div style={{
                                marginTop: 40,
                                display: "flex",
                                alignItems: "center",
                                justifyContent: "center",
                            }}>
                                <CircularProgress style={{alignSelf: "center"}} />
                            </div>}
                            
                            {modalValue && <span dangerouslySetInnerHTML={{__html: modalValue}} />}
                            {modalValue == "" && <span>Could not find definition</span>}
                        </Typography>
                    </Box>
                </Modal>
            </>
        )
}


const style = {
    position: "absolute" as const,
    top: "50%",
    left: "50%",
    transform: "translate(-50%, -50%)",
    width: 400,
    bgcolor: "background.paper",
    border: "2px solid #000",
    boxShadow: 24,
    p: 4,
}
