import {SearchWorkResponse, SearchWorkResult} from "../api/SearchWorkApi"
import {Translations} from "../api/SearchApi"
import {getSelectedWordOrPhrase} from "../utils/Selection"
import React, {CSSProperties, Fragment, useEffect, useRef, useState} from "react"
import {manxDictionaryLookup} from "../api/DictionaryApi"
import Highlighter from "react-highlight-words"
import {Box, CircularProgress, Modal} from "@mui/material"
import Typography from "@mui/material/Typography"
import {diffChars} from "diff"
import YouTuber, {Player} from "./YouTuber"
import useInterval from "../vendor/use-interval/UseInterval"
import "./ComparisonTable.css"
import {useLanguageVisibility} from "../hooks/LanguageVisibility"

function escapeRegex(s: string) {
    return s.replace(/[/\-\\^$*+?.()|[\]{}]/g, "\\$&")
}

export const ComparisonTable = (props: {
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

        if (range == null || range.split(" ").length > 4) {
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
        return lineValue.split("\n").map((item, key) => <div onClick={() => {
                if (languageCode == "gv") {
                    onClickWordForDictionaryLookup()
                }

            }} style={{textAlign: "justify"}} key={key}><Highlighter
                highlightClassName={shouldHighlight ? "textHighlight" : "textHighlightAlternate"}
                searchWords={manxHighlight}
                autoEscape={false}
                textToHighlight={item} /><br /></div>
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
        // TODO: Also apply justify to 'browse' screen
        return <div onClick={() => {
            onClickWordForDictionaryLookup()
        }} style={{textAlign: "justify"}}>
            {value != "*" && value != "" && <div style={{textAlign: "center", backgroundColor: "rgba(255,255,0,0.3)" }}> highlighting disabled </div>}
            {result.map(part => {
                const color = part.added ? "rgba(0, 128, 0, 0.3)" : part.removed ? "rgba(255, 0, 0, 0.3)" : ""
                const className = part.added ? "part-added" : part.removed ? "part-removed" : ""
                return <span className={className} style={{backgroundColor: color}}>{part.value}</span>
            })}
        </div>
    }

    const getTranslations = (key: string) => {
        if (translations == null) return []
        return translations[key] ?? []
    }

    const [videoTime, setVideoTime] = useState(0)
    
    useInterval(() => setVideoTime(player.current?.getCurrentTime() ?? 0), 10)

    const getVideoId = (source:string) => {
        try {
            return new URL(source).searchParams.get("v")
        } catch (e) {
            console.warn(e)
            return ""
        }
    }

    let isVideo = response?.source?.startsWith("https://www.youtube") || response?.source?.startsWith("https://youtube.com")
    const videoId = !isVideo ? "" : getVideoId(response.source)
    if (!videoId) {
        isVideo = false
    }
    const player = useRef<Player>(null)
    
    const getLineStyle = (line: SearchWorkResult): CSSProperties => {
        if (!isVideo || !line.subStart || !line.subEnd) return {}
        if (videoTime < line.subStart || videoTime > line.subEnd) return {}
        return {
            backgroundColor: "aliceblue",
        }
    }
    
    const tableStyle = (): CSSProperties => {
        if (!isVideo) return {}
        return {
            display: "block",
            overflowY: "scroll",
            maxHeight: "400px"
        }
    }

    const languageVisibility = useLanguageVisibility()
    const leftVisible = languageVisibility.manxVisible && originalManx || languageVisibility.englishVisible && !originalManx
    const rightVisible = languageVisibility.englishVisible && originalManx || languageVisibility.manxVisible && !originalManx
    // TODO: optimise this - no need to iterate each render
    const linkVisible = response.gitHubLink || response.results.filter(x => x.page != null && (response.pdfLink || response.googleBooksId)).length > 0
    const leftLang = originalManx ? "gv" : "en"
    const rightLang = originalManx ? "en" : "gv"
    return (
        <>
            <div>
                {/*TODO: Lazy Load Youtube player*/}
                {isVideo && videoId != null && <div className={"youtube-container center"}><YouTuber ref={player} videoId={videoId} /></div>}
                <div>
                <table className='table table-striped' style={{tableLayout: "fixed", ...tableStyle()}} aria-labelledby="tabelLabel">
                    <thead>
                    <tr>
                        {isVideo && <th>{""}</th>}
                        {leftVisible && <th>{originalManx ? "Manx" : "English"}</th>}
                        {rightVisible && <th>{originalManx ? "English" : "Manx"}</th>}
                        {linkVisible && <th style={{width: 45}}>Link</th>}
                    </tr>
                    </thead>
                    <tbody>
                    {response.results.map(line => {
                            // TODO: Only due to technical reasons, we can't mix highlights and diffs. 
                            // This should be fixed via vendoring react-highlight-words's `Highlighter` class
                            const manxText = diffCorrectedText(line.manxOriginal, line.manx) ?? highlightText(highlightManx, "gv", line.manx)
                            const englishText = diffCorrectedText(line.englishOriginal, line.english) ?? highlightText(highlightEnglish, "en", line.english)

                            return <Fragment key={response.title + line.csvLineNumber.toString()}>
                                <tr key={line.date} style={getLineStyle(line)}>
                                {isVideo && <td style={{cursor: "pointer"}} onClick={() => {
                                    if (line.subStart && player.current) {
                                    player.current.seek(line.subStart)
                                }
                                }}>▶️</td>}
                                {leftVisible && <td lang={leftLang}>
                                    {originalManx ? manxText : englishText}
                                </td>}
                                {rightVisible && <td lang={rightLang}>
                                    {originalManx ? englishText : manxText }
                                </td>}
                                {linkVisible && <td>
                                    {line.page != null && response.pdfLink &&
                                        <><a href={response.pdfLink + "#page=" + line.page} target="_blank" rel="noreferrer">p{line.page}</a>{" "}</> }
                                    {line.page != null && response.googleBooksId &&
                                        <><a href={`https://books.google.im/books?id=${response.googleBooksId}&pg=PA${line.page}`} target="_blank" rel="noreferrer">p{line.page}</a>{" "}</> }
                                    {response.gitHubLink && <a href={`${response.gitHubLink}#L${line.csvLineNumber}`}>
                                        edit
                                    </a>}
                                </td>}
                            </tr>
                                {line.notes ? <tr><td colSpan={3} className="noteRow">{line.notes}</td></tr> : null}
                            </Fragment>
                        }
                    )}
                    </tbody>
                </table>
                </div>
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
