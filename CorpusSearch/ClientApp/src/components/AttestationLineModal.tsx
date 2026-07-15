import { useEffect, useState } from "react"
import { Box, CircularProgress, Modal } from "@mui/material"
import { Link } from "react-router-dom"
import Highlighter from "react-highlight-words"
import { searchWork, SearchWorkResult } from "../api/SearchWorkApi"
import { defaultSearchOptions } from "../api/SearchOptions"
import { segmentChunks } from "./LineText"
import "./AttestationLineModal.css"

const style = {
    position: "absolute",
    top: "50%",
    left: "50%",
    transform: "translate(-50%, -50%)",
    maxWidth: 560,
    width: "calc(100% - 32px)",
    maxHeight: "80vh",
    overflowY: "auto",
    bgcolor: "var(--c-surface, #fff)",
    color: "var(--c-ink, inherit)",
    borderRadius: 2,
    boxShadow: 24,
    p: 3,
}

/** The line a first-attestation claim rests on: shown whole, in both languages,
 * with a way through to the text it sits in.
 *
 * The line is fetched rather than carried by the history: the scan's sample is
 * the Manx alone, and a dialog nobody opens should not cost every word page an
 * English column it will not show.
 */
export const AttestationLineModal = ({
    form,
    ident,
    title,
    year,
    onClose,
}: {
    /** the spelling to find in the text; null while closed */
    form: string | null
    ident?: string | null
    title?: string | null
    year?: number | null
    onClose: () => void
}) => {
    const [line, setLine] = useState<SearchWorkResult | null>(null)
    const [failed, setFailed] = useState(false)

    useEffect(() => {
        setLine(null)
        setFailed(false)
        if (form == null || !ident) return
        const abort = new AbortController()
        searchWork(
            {
                ...defaultSearchOptions,
                docIdent: ident,
                value: form,
                searchManx: true,
                searchEnglish: false,
            },
            abort.signal,
        )
            .then((response) => {
                // the claim is the text's first use of the spelling, and the
                // results are in document order
                setLine(response.results[0] ?? null)
                if (response.results.length === 0) setFailed(true)
            })
            .catch((e) => {
                if (!abort.signal.aborted) {
                    console.warn(e)
                    setFailed(true)
                }
            })
        return () => abort.abort()
    }, [form, ident])

    return (
        <Modal
            open={form != null}
            onClose={onClose}
            aria-labelledby="attest-line-title"
        >
            <Box sx={style}>
                <h2 id="attest-line-title" className="attest-modal-title">
                    {title}
                    {year != null && (
                        <span className="attest-modal-year">{` ${year}`}</span>
                    )}
                </h2>

                {line == null && !failed && (
                    <div className="attest-modal-loading">
                        <CircularProgress />
                    </div>
                )}
                {failed && (
                    <p className="attest-modal-failed">
                        This line could not be loaded.
                    </p>
                )}

                {line != null && (
                    <>
                        <p className="attest-modal-manx">
                            <Highlighter
                                highlightClassName="textHighlight"
                                searchWords={[]}
                                autoEscape={false}
                                findChunks={() =>
                                    segmentChunks(
                                        line.manxHighlights ?? [],
                                        0,
                                        line.manx.length,
                                    )
                                }
                                textToHighlight={line.manx}
                            />
                        </p>
                        {line.english && (
                            <p className="attest-modal-english">
                                {line.english}
                            </p>
                        )}
                    </>
                )}

                {ident && (
                    <p className="attest-modal-link">
                        <Link
                            to={`/docs/${ident}?q=${encodeURIComponent(form ?? "")}`}
                            onClick={onClose}
                        >
                            {"Open in the corpus ›"}
                        </Link>
                    </p>
                )}
            </Box>
        </Modal>
    )
}
