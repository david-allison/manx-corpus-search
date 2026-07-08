import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { cleanup, render, screen } from "@testing-library/react"
import { MemoryRouter } from "react-router-dom"
import { Home } from "./Home"
import { MAX_QUERY_LENGTH, SearchResponse } from "../api/SearchApi"

// stub fetch so tests never hit the network; callers route to their error state
const fetchMock = vi.fn<typeof fetch>(() =>
    Promise.reject(new Error("fetch is disabled in tests")),
)
vi.stubGlobal("fetch", fetchMock)

beforeEach(() => {
    fetchMock.mockClear()
})

afterEach(cleanup)

const renderWithQuery = (query: string) =>
    render(
        <MemoryRouter initialEntries={[`/?q=${encodeURIComponent(query)}`]}>
            <Home />
        </MemoryRouter>,
    )

const searchRequests = () =>
    fetchMock.mock.calls.filter(
        ([url]) => typeof url === "string" && url.includes("search/"),
    )

// #229
describe("query too long", () => {
    it("shows a message and does not call the server", () => {
        renderWithQuery("a".repeat(MAX_QUERY_LENGTH + 1))

        expect(screen.getByText(/too long/)).toBeTruthy()
        expect(searchRequests()).toHaveLength(0)
    })

    it("searches when the query is at the limit", async () => {
        renderWithQuery("a".repeat(MAX_QUERY_LENGTH))

        // the stubbed fetch rejects, so a search attempt shows the error state
        await screen.findByText(/Something went wrong/)
        expect(screen.queryByText(/too long/)).toBeNull()
        expect(searchRequests()).toHaveLength(1)
    })
})

const emptySearchResponse = (query: string): SearchResponse => ({
    results: [],
    query,
    numberOfResults: 0,
    numberOfDocuments: 0,
    timeTaken: "0ms",
    definedInDictionaries: {},
    translations: {},
})

// #134
describe("multidict lookup", () => {
    it("links to Multidict when no dictionary knows the word", async () => {
        fetchMock.mockImplementation(() =>
            Promise.resolve(Response.json(emptySearchResponse("cabbyldereen"))),
        )
        renderWithQuery("cabbyldereen")

        const link = await screen.findByRole("link", { name: "Multidict" })
        expect(link.getAttribute("href")).toBe(
            "https://multidict.net/multidict/?word=cabbyldereen&sl=gv&tl=en",
        )
    })

    it("does not link to Multidict for phrases", async () => {
        fetchMock.mockImplementation(() =>
            Promise.resolve(Response.json(emptySearchResponse("moghrey mie"))),
        )
        renderWithQuery("moghrey mie")

        await screen.findByText(/No matches/)
        expect(screen.queryByRole("link", { name: "Multidict" })).toBeNull()
    })

    it("does not link to Multidict when a dictionary knows the word", async () => {
        const response: SearchResponse = {
            ...emptySearchResponse("moghrey"),
            definedInDictionaries: {
                Cregeen: { entries: ["morning"], allowLookup: false },
            },
        }
        fetchMock.mockImplementation(() =>
            Promise.resolve(Response.json(response)),
        )
        renderWithQuery("moghrey")

        await screen.findByText(/morning/)
        expect(screen.queryByRole("link", { name: "Multidict" })).toBeNull()
    })
})
