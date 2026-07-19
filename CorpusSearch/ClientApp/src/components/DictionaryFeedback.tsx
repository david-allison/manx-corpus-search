import { FormEvent, KeyboardEvent, useState } from "react"
import { Box, Modal } from "@mui/material"
import { FeedbackError, submitFeedback } from "../api/FeedbackApi"
import { usePersistedState } from "../hooks/usePersistedState"
import "./DictionaryFeedback.css"

const style = {
    position: "absolute",
    top: "50%",
    left: "50%",
    transform: "translate(-50%, -50%)",
    maxWidth: 440,
    width: "calc(100% - 32px)",
    maxHeight: "80vh",
    overflowY: "auto",
    bgcolor: "var(--c-surface, #fff)",
    color: "var(--c-ink, inherit)",
    borderRadius: 2,
    boxShadow: 24,
    p: 3,
}

type Status = "idle" | "submitting" | "sent" | "failed" | "throttled"

/** The page's way to make the dictionary better: an offer in the title row
 * opening a dialog for anything at all — corrections, additions, context,
 * better wording — not only mistakes. The report carries the page's word and
 * dictionary scope; the reader's name rides along and is remembered for the
 * next suggestion. */
export const DictionaryFeedback = ({
    word,
    dict,
}: {
    word: string
    /** the page's dictionary scope; absent = all dictionaries */
    dict?: string
}) => {
    const [open, setOpen] = useState(false)
    const [name, setName] = usePersistedState(
        "feedbackName",
        (stored) => stored ?? "",
        (value) => value,
    )
    const [comments, setComments] = useState("")
    const [status, setStatus] = useState<Status>("idle")

    const openDialog = () => {
        if (status === "sent") {
            // the last suggestion went: reopening offers a fresh form
            setStatus("idle")
            setComments("")
        }
        setOpen(true)
    }

    const submit = (e: FormEvent) => {
        e.preventDefault()
        if (!comments.trim() || status === "submitting") {
            return
        }
        setStatus("submitting")
        submitFeedback({
            name: name.trim() || undefined,
            comments: comments.trim(),
            dictionary: dict ?? "all",
            headword: word,
        })
            .then(() => setStatus("sent"))
            .catch((error: unknown) =>
                setStatus(
                    error instanceof FeedbackError && error.status === 429
                        ? "throttled"
                        : "failed",
                ),
            )
    }

    // Cmd/Ctrl+Enter sends from anywhere in the form: Enter alone must not —
    // in the textarea it is a newline, and a suggestion is a place for them
    const submitKey = (e: KeyboardEvent) => {
        if (e.key === "Enter" && (e.metaKey || e.ctrlKey)) {
            submit(e)
        }
    }

    return (
        <>
            <button
                type="button"
                className="dict-feedback-open"
                onClick={openDialog}
            >
                ✏️ suggest an improvement
            </button>
            <Modal
                open={open}
                onClose={() => setOpen(false)}
                aria-labelledby="dict-feedback-title"
            >
                <Box sx={style}>
                    <h2
                        id="dict-feedback-title"
                        className="dict-feedback-title"
                    >
                        {`Improve “${word}”`}
                    </h2>
                    {status === "sent" ? (
                        <div role="status">
                            <p className="dict-feedback-sent">
                                {name.trim()
                                    ? `Thanks for the suggestion, ${name.trim()}!`
                                    : "Thanks for the suggestion!"}
                            </p>
                            <p className="dict-feedback-sent">
                                {
                                    "I'll aim to get this done within the week. Please "
                                }
                                <a href="mailto:corpus-submissions@gaelg.im">
                                    get in touch
                                </a>
                                {" if things haven't moved."}
                            </p>
                            <p className="dict-feedback-signature">-- David</p>
                        </div>
                    ) : (
                        <form
                            className="dict-feedback-form"
                            onSubmit={submit}
                            onKeyDown={submitKey}
                        >
                            <p className="dict-feedback-hint">
                                Anything helps: a correction, a missing word or
                                meaning, a better translation, or context this
                                page should have.
                            </p>
                            <input
                                type="text"
                                aria-label="Name (optional)"
                                placeholder="Name (optional)"
                                maxLength={200}
                                value={name}
                                onChange={(e) => setName(e.target.value)}
                            />
                            <textarea
                                aria-label="Your suggestion"
                                placeholder="Your suggestion"
                                required
                                rows={4}
                                maxLength={2000}
                                value={comments}
                                onChange={(e) => setComments(e.target.value)}
                            />
                            <button
                                type="submit"
                                disabled={status === "submitting"}
                            >
                                {status === "submitting" ? "Sending…" : "Send"}
                            </button>
                            {status === "failed" && (
                                <p className="dict-feedback-error" role="alert">
                                    Could not send. Please try again.
                                </p>
                            )}
                            {status === "throttled" && (
                                <p className="dict-feedback-error" role="alert">
                                    Too many suggestions just now. Please try
                                    again later.
                                </p>
                            )}
                        </form>
                    )}
                </Box>
            </Modal>
        </>
    )
}
