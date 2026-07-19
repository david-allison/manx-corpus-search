import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import {
    cleanup,
    fireEvent,
    render,
    screen,
    waitFor,
} from "@testing-library/react"
import { DictionaryFeedback } from "./DictionaryFeedback"

const fetchMock = vi.fn<typeof fetch>()
vi.stubGlobal("fetch", fetchMock)

beforeEach(() => {
    fetchMock.mockReset()
    localStorage.clear()
})
afterEach(cleanup)

const respond = (status: number) =>
    fetchMock.mockResolvedValue({ ok: status < 400, status } as Response)

const openDialog = (dict?: string) => {
    render(<DictionaryFeedback word="aa" dict={dict} />)
    fireEvent.click(
        screen.getByRole("button", { name: "✏️ suggest an improvement" }),
    )
}

const typeSuggestion = (text: string) =>
    fireEvent.change(screen.getByLabelText("Your suggestion"), {
        target: { value: text },
    })

describe("DictionaryFeedback", () => {
    it("keeps the form behind the offer, in a dialog named for the word", () => {
        render(<DictionaryFeedback word="aa" />)
        expect(screen.queryByLabelText("Your suggestion")).toBe(null)

        fireEvent.click(
            screen.getByRole("button", { name: "✏️ suggest an improvement" }),
        )
        expect(
            screen.getByRole("heading", { name: "Improve “aa”" }),
        ).toBeTruthy()
        expect(screen.getByLabelText("Your suggestion")).toBeTruthy()
    })

    it("sends the page's word and scope along, and thanks by name", async () => {
        respond(204)
        openDialog("cregeen")
        fireEvent.change(screen.getByLabelText("Name (optional)"), {
            target: { value: "Juan" },
        })
        typeSuggestion("the plural is missing")
        fireEvent.click(screen.getByRole("button", { name: "Send" }))

        await waitFor(() =>
            expect(
                screen.getByText("Thanks for the suggestion, Juan!"),
            ).toBeTruthy(),
        )
        // the note asks them back if nothing changes, and signs off
        expect(screen.getByRole("link", { name: "get in touch" })).toBeTruthy()
        expect(screen.getByText("-- David")).toBeTruthy()

        expect(fetchMock).toHaveBeenCalledTimes(1)
        const [url, init] = fetchMock.mock.calls[0]
        expect(url).toBe("api/Feedback")
        expect(JSON.parse(init!.body as string)).toEqual({
            name: "Juan",
            comments: "the plural is missing",
            dictionary: "cregeen",
            headword: "aa",
        })
    })

    it("reports the whole page as 'all' when no dictionary is scoped", async () => {
        respond(204)
        openDialog()
        typeSuggestion("wrong")
        fireEvent.click(screen.getByRole("button", { name: "Send" }))

        await waitFor(() => expect(fetchMock).toHaveBeenCalled())
        const body = JSON.parse(
            fetchMock.mock.calls[0][1]!.body as string,
        ) as Record<string, unknown>
        expect(body.dictionary).toBe("all")
        expect("name" in body).toBe(false)
    })

    it("remembers the reader's name for the next suggestion", () => {
        openDialog()
        fireEvent.change(screen.getByLabelText("Name (optional)"), {
            target: { value: "Juan" },
        })
        cleanup()

        openDialog()
        expect(screen.getByLabelText("Name (optional)")).toHaveProperty(
            "value",
            "Juan",
        )
    })

    it("will not send an empty suggestion", () => {
        openDialog()
        fireEvent.click(screen.getByRole("button", { name: "Send" }))
        expect(fetchMock).not.toHaveBeenCalled()
    })

    it("sends on Cmd+Enter from the suggestion", async () => {
        respond(204)
        openDialog()
        typeSuggestion("the plural is missing")
        fireEvent.keyDown(screen.getByLabelText("Your suggestion"), {
            key: "Enter",
            metaKey: true,
        })

        await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1))
    })

    it("sends on Ctrl+Enter, and from the name field too", async () => {
        respond(204)
        openDialog()
        typeSuggestion("the plural is missing")
        fireEvent.keyDown(screen.getByLabelText("Name (optional)"), {
            key: "Enter",
            ctrlKey: true,
        })

        await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1))
    })

    it("keeps a bare Enter as a newline, never a send", () => {
        openDialog()
        typeSuggestion("line one")
        fireEvent.keyDown(screen.getByLabelText("Your suggestion"), {
            key: "Enter",
        })

        expect(fetchMock).not.toHaveBeenCalled()
    })

    it("sends nothing on Cmd+Enter while the suggestion is empty", () => {
        openDialog()
        fireEvent.keyDown(screen.getByLabelText("Your suggestion"), {
            key: "Enter",
            metaKey: true,
        })

        expect(fetchMock).not.toHaveBeenCalled()
    })

    it("keeps the reader's text when the send fails", async () => {
        respond(502)
        openDialog()
        typeSuggestion("the plural is missing")
        fireEvent.click(screen.getByRole("button", { name: "Send" }))

        await waitFor(() =>
            expect(
                screen.getByText("Could not send. Please try again."),
            ).toBeTruthy(),
        )
        expect(screen.getByLabelText("Your suggestion")).toHaveProperty(
            "value",
            "the plural is missing",
        )
    })

    it("says a full budget is the site's doing, not the reader's", async () => {
        respond(429)
        openDialog()
        typeSuggestion("wrong")
        fireEvent.click(screen.getByRole("button", { name: "Send" }))

        await waitFor(() =>
            expect(
                screen.getByText(
                    "Too many suggestions just now. Please try again later.",
                ),
            ).toBeTruthy(),
        )
    })

    it("offers a fresh form after a sent suggestion, keeping the name", async () => {
        respond(204)
        openDialog()
        fireEvent.change(screen.getByLabelText("Name (optional)"), {
            target: { value: "Juan" },
        })
        typeSuggestion("first thought")
        fireEvent.click(screen.getByRole("button", { name: "Send" }))
        await waitFor(() =>
            expect(
                screen.getByText("Thanks for the suggestion, Juan!"),
            ).toBeTruthy(),
        )

        // the dialog must close before the offer is reachable again
        fireEvent.keyDown(screen.getByRole("presentation"), { key: "Escape" })
        await waitFor(() =>
            expect(screen.queryByText("Thanks for the suggestion, Juan!")).toBe(
                null,
            ),
        )
        fireEvent.click(
            screen.getByRole("button", { name: "✏️ suggest an improvement" }),
        )
        expect(screen.getByLabelText("Your suggestion")).toHaveProperty(
            "value",
            "",
        )
        expect(screen.getByLabelText("Name (optional)")).toHaveProperty(
            "value",
            "Juan",
        )
    })
})
