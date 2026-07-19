import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { cleanup, render, screen, waitFor } from "@testing-library/react"
import { MemoryRouter } from "react-router-dom"
import { DictionarySpoken } from "./DictionarySpoken"

const fetchMock = vi.fn<typeof fetch>()
vi.stubGlobal("fetch", fetchMock)

beforeEach(() => fetchMock.mockReset())
afterEach(cleanup)

const page = {
    dictionary: "Heard spoken",
    slug: "spoken",
    letters: ["A", "D"],
    letter: "D",
    chapters: [
        {
            key: "DOO",
            words: [
                { word: "dooinney", attested: true },
                { word: "dooyrt", attested: true },
            ],
        },
    ],
}

const respond = (status: number, body?: unknown) =>
    fetchMock.mockResolvedValue({
        ok: status < 400,
        status,
        json: () => Promise.resolve(body),
    } as Response)

const renderSpoken = () =>
    render(
        <MemoryRouter initialEntries={["/dictionary/spoken?at=D"]}>
            <DictionarySpoken />
        </MemoryRouter>,
    )

describe("DictionarySpoken", () => {
    it("lists the heard words under their letter, each a way to its page", async () => {
        respond(200, page)
        renderSpoken()

        await waitFor(() =>
            expect(screen.getByText("🔊 Heard spoken")).toBeTruthy(),
        )
        const word = screen.getByRole("link", { name: "dooinney" })
        // the walk rides along: the page's back/forward steps the heard words
        expect(word.getAttribute("href")).toBe(
            "/dictionary/dooinney?nav=spoken",
        )
        expect(screen.getByText("DOO")).toBeTruthy()
        // the open letter is marked in the bar
        const active = screen.getAllByRole("link", { name: "D" })[0]
        expect(active.getAttribute("aria-current")).toBe("page")
    })

    it("says the recordings are still being read while the server reads them", async () => {
        respond(404)
        renderSpoken()

        await waitFor(() =>
            expect(
                screen.getByText(/recordings are still being read/),
            ).toBeTruthy(),
        )
    })
})
