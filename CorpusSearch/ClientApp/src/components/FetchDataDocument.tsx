/* eslint @typescript-eslint/no-misused-promises: 0 */  
import "./FetchDataDocument.css"

import React, {useEffect, useState} from "react"
import qs from "qs"
import Highlighter from "react-highlight-words"
import {Link, useLocation, useMatch} from "react-router-dom"
import {searchWork, SearchWorkResponse} from "../api/SearchWorkApi"
import {Translations} from "../api/SearchApi"
import {SearchLanguage} from "./Home"
import {Box, CircularProgress, Modal} from "@mui/material"
import {ManxEnglishSelector} from "./ManxEnglishSelector"
import Typography from "@mui/material/Typography"
import {manxDictionaryLookup} from "../api/DictionaryApi"


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
    
    const onClick = () => {
        const s = window.getSelection()
        if (s == null) {
            return
        }
        
        const range = s.getRangeAt(0).cloneRange() // clone to ensure we don't modify selection
        const node = s.anchorNode
        
        if (node == null) {
            return
        }
        
        // Find starting point
        while(range.toString().indexOf(" ") != 0 && range.startOffset > 0) {
            range.setStart(node,(range.startOffset -1))
        }
        if (range.startOffset != 0 || range.toString()[0] == " ") {
            // if we reached a space, ignore it
            range.setStart(node, range.startOffset + 1)    
        }

        // Find ending point
        try {
            do {
                range.setEnd(node,range.endOffset + 1)
            } while(range.toString().indexOf(" ") == -1 && range.toString().trim() != "")
        } catch (e) {
            // TODO: find a less hacky way to end if at the end
        }
        
        // remove notes/citations '[1]' at the end of the string
        const stringToSearch = range.toString().replace(/\[\d+]/g, " ")
        
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
             const finalString = summaries.map(x => x.summary).join("<br><br>")
             setModalValue(finalString)
         })
             .catch(e => { console.warn(e)})
    }, [modalText])


    const getTranslations = (key: string) => {
        if (translations == null) return []
        return translations[key] ?? []
    }
        return (
            <>
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
                                return <span onClick={() => onClick()} key={key}><Highlighter
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
                            {!modalValue && <span>Could not find definition</span>}
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

export default FetchDataDocument