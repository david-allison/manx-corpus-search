import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { cleanup, render, screen, waitFor } from "@testing-library/react"
import { MemoryRouter } from "react-router-dom"
import { DictionarySamples } from "./DictionarySamples"

const fetchMock = vi.fn<typeof fetch>()
vi.stubGlobal("fetch", fetchMock)

beforeEach(() => fetchMock.mockReset())
afterEach(cleanup)

const samples = [
    { word: "cur", summary: "put, give", attestations: 41245, attested: true },
    { word: "goan", summary: "scarce, rare", attestations: 3, attested: true },
    {
        word: "ynrican",
        summary: "only, sole",
        attestations: 0,
        attested: false,
    },
]

const respond = () =>
    fetchMock.mockResolvedValue({
        ok: true,
        json: () => Promise.resolve(samples),
    } as Response)

const renderSamples = () =>
    render(
        <MemoryRouter>
            <DictionarySamples dict="cregeen" />
        </MemoryRouter>,
    )

describe("DictionarySamples", () => {
    it("deals bare words as links to their pages, under a plain header", async () => {
        respond()
        renderSamples()

        const common = await screen.findByRole("link", { name: "cur" })
        expect(common.getAttribute("href")).toBe("/dictionary/cur")
        expect(screen.getByText("Or try these:")).toBeTruthy()
        // the gloss belongs to the word's page: the list is a door, not an entry
        expect(screen.queryByText(/put, give/)).toBeNull()
        expect(screen.queryByText(/×/)).toBeNull()
    })

    it("greys the dictionary-only word, and says what it is", async () => {
        respond()
        renderSamples()

        const unattested = await screen.findByRole("link", { name: "ynrican" })
        expect(unattested.className).toContain("dict-unattested")
        expect(unattested.getAttribute("title")).toContain("a dictionary word")
    })

    it("is a quieter page, not a broken one, when the deal fails", async () => {
        fetchMock.mockResolvedValue({ ok: false, status: 404 } as Response)
        const { container } = renderSamples()

        await waitFor(() => expect(fetchMock).toHaveBeenCalled())
        expect(container.querySelector(".dict-samples")).toBeNull()
    })
})
