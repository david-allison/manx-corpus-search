import {SearchWorkResponse} from "../api/SearchWorkApi"
import {Translations} from "../api/SearchApi"
import {getSelectedWordOrPhrase} from "../utils/Selection"
import React, {useEffect, useState} from "react"
import {manxDictionaryLookup} from "../api/DictionaryApi"
import Highlighter from "react-highlight-words"
import {Box, CircularProgress, Modal} from "@mui/material"
import Typography from "@mui/material/Typography"
import {diffChars} from "diff"

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
                                        <><a href={response.pdfLink + "#page=" + line.page} target="_blank" rel="noreferrer">p{line.page}</a>{" "}</> }
                                    {line.page != null && response.googleBooksId &&
                                        <><a href={`https://books.google.im/books?id=${response.googleBooksId}&pg=PA${line.page}`} target="_blank" rel="noreferrer">p{line.page}</a>{" "}</> }
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
