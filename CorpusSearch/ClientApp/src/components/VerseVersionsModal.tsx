import { useEffect, useState } from "react"
import { Box, CircularProgress, Modal } from "@mui/material"
import { Link } from "react-router-dom"
import {
    fetchVerseAlignment,
    VerseAlignmentResponse,
} from "../api/VerseAlignmentApi"
import "./VerseVersionsModal.css"

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

/** The Ref column's "other versions" popup: the tapped verse in every
 * translation that has it (the corpus holds several scripture versions),
 * each linking into its document at that verse. */
export const VerseVersionsModal = (props: {
    /** canonical key of the verse to align; null while closed */
    refKey: string | null
    /** the document the reader is already in: listed, but not linked */
    docIdent?: string
    onClose: () => void
    /** fired when a version link is followed, on top of onClose: lets a host
     * modal (the dictionary popup) close itself too */
    onNavigate?: () => void
}) => {
    const { refKey, docIdent, onClose, onNavigate } = props

    const [alignment, setAlignment] = useState<VerseAlignmentResponse | null>(
        null,
    )

    useEffect(() => {
        setAlignment(null)
        if (refKey == null) {
            return
        }
        const abort = new AbortController()
        fetchVerseAlignment(refKey, abort.signal)
            .then(setAlignment)
            .catch((e) => {
                console.warn(e)
            })
        return () => {
            abort.abort()
        }
    }, [refKey])

    return (
        <Modal
            open={refKey != null}
            onClose={onClose}
            aria-labelledby="verse-versions-title"
        >
            <Box sx={style}>
                <h2 id="verse-versions-title" className="verse-versions-title">
                    {alignment?.display ?? " "}
                </h2>
                {alignment == null && (
                    <div className="verse-versions-loading">
                        <CircularProgress size={28} />
                    </div>
                )}
                {alignment != null && (
                    <ul className="verse-versions-list">
                        {alignment.documents.map((doc) => {
                            const heading = (
                                <>
                                    <span className="verse-versions-name">
                                        {doc.name}
                                    </span>
                                    {doc.year != null && (
                                        <span className="verse-versions-year">
                                            {doc.year}
                                        </span>
                                    )}
                                </>
                            )
                            return (
                                <li key={doc.ident}>
                                    {doc.ident == docIdent ? (
                                        <span className="verse-versions-current">
                                            {heading}
                                            <span className="verse-versions-here">
                                                this document
                                            </span>
                                        </span>
                                    ) : (
                                        <Link
                                            to={`/docs/${doc.ident}?ref=${encodeURIComponent(alignment.key)}`}
                                            onClick={() => {
                                                onClose()
                                                onNavigate?.()
                                            }}
                                        >
                                            {heading}
                                        </Link>
                                    )}
                                    {doc.manx && (
                                        <div
                                            className="verse-versions-text"
                                            lang="gv"
                                        >
                                            {doc.manx}
                                        </div>
                                    )}
                                </li>
                            )
                        })}
                        {alignment.documents.length == 0 && (
                            <li className="verse-versions-none">
                                No other version carries this verse yet.
                            </li>
                        )}
                    </ul>
                )}
            </Box>
        </Modal>
    )
}
