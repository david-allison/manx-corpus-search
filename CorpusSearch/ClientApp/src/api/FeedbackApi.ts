/** A reader's report on one dictionary entry. Entries have no stable ids
 * (identity is positional in the books), so the report names its entry by
 * dictionary and headword only. */
export type FeedbackRequest = {
    name?: string
    comments: string
    dictionary: string
    headword: string
}

export class FeedbackError extends Error {
    readonly status: number

    constructor(status: number) {
        super(`feedback failed: ${status}`)
        this.status = status
    }
}

/** Sends the report; the server relays it to an external sheet. A 429 means
 * the site-wide report budget is spent for now, not that anything is wrong
 * with this report. */
export const submitFeedback = async (
    feedback: FeedbackRequest,
): Promise<void> => {
    const response = await fetch("api/Feedback", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(feedback),
    })
    if (!response.ok) {
        throw new FeedbackError(response.status)
    }
}
