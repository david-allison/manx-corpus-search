import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import {
    cleanup,
    fireEvent,
    render,
    screen,
    waitFor,
} from "@testing-library/react"
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

describe("ignore hyphens option", () => {
    it("is sent to the server when toggled", async () => {
        renderWithQuery("lhiam-lhiat")

        await screen.findByText(/Something went wrong/)
        expect(searchRequests().at(-1)?.[0]).toContain("ignoreHyphens=false")

        fireEvent.click(screen.getByLabelText("Ignore hyphens"))

        await waitFor(() => expect(searchRequests()).toHaveLength(2))
        expect(searchRequests().at(-1)?.[0]).toContain("ignoreHyphens=true")
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

// #158
describe("did-you-mean suggestion", () => {
    const hyphenatedResults: SearchResponse = {
        ...emptySearchResponse("lum-lane"),
        numberOfResults: 2,
        numberOfDocuments: 1,
        results: [
            {
                startDate: "1900-01-01",
                endDate: "1900-01-01",
                documentName: "Test Doc",
                count: 2,
                ident: "doc1",
                sample: "yn lum-lane mooar",
            },
        ],
    }

    it("suggests the alternate forms the server returned", async () => {
        fetchMock.mockImplementation((url) =>
            Promise.resolve(
                Response.json(
                    typeof url === "string" && url.includes("lum-lane")
                        ? hyphenatedResults
                        : {
                              ...emptySearchResponse("lumlane"),
                              suggestions: [{ query: "lum-lane", count: 2 }],
                          },
                ),
            ),
        )
        renderWithQuery("lumlane")

        const suggestion = await screen.findByRole("button", {
            name: "lum-lane",
        })
        screen.getByText(/2 matches/)

        fireEvent.click(suggestion)

        // the suggestion becomes the query
        await screen.findByText("Test Doc")
        expect(searchRequests().at(-1)?.[0]).toContain("lum-lane")
    })

    it("shows no suggestion when the server offers none", async () => {
        fetchMock.mockImplementation(() =>
            Promise.resolve(Response.json(emptySearchResponse("xyzzy"))),
        )
        renderWithQuery("xyzzy")

        await screen.findByText(/No matches/)
        expect(screen.queryByText(/Did you mean/)).toBeNull()
    })
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
