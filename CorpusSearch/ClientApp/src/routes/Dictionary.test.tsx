import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { cleanup, render, screen } from "@testing-library/react"
import { MemoryRouter, Route, Routes } from "react-router-dom"
import { Dictionary } from "./Dictionary"
import { DictionaryPageResponse } from "../api/DictionaryApi"

const fetchMock = vi.fn<typeof fetch>()
vi.stubGlobal("fetch", fetchMock)

beforeEach(() => fetchMock.mockReset())
afterEach(cleanup)

const emptyHistory = {
    word: "",
    lemmas: [],
    revivalBoundaryYear: 1900,
    truncatedForms: 0,
    forms: [],
    decades: [],
    traditionalCount: 0,
    revivedCount: 0,
    undatedCount: 0,
    dictionaries: [],
    cognates: [],
}

const dictionaries = [
    { slug: "cregeen", name: "Cregeen" },
    { slug: "kelly-m2e", name: "J Kelly Manx to English" },
]

const respondWith = (page: DictionaryPageResponse) =>
    fetchMock.mockImplementation((url) => {
        const href = hrefOf(url)
        const body = href.includes("/history")
            ? emptyHistory
            : href.includes("/dictionaries")
              ? dictionaries
              : page
        return Promise.resolve({
            ok: true,
            json: () => Promise.resolve(body),
        } as Response)
    })

/** Not every call through the stubbed global arrives with a string url, so the
 * routing above must not assume one */
const hrefOf = (url: unknown): string =>
    typeof url === "string"
        ? url
        : url instanceof URL
          ? url.href
          : ((url as Request | undefined)?.url ?? "")

/** The dictionary routes as App.tsx declares them: the scoped route is a
 * separate path onto the same component */
const renderAt = (path: string) =>
    render(
        <MemoryRouter initialEntries={[path]}>
            <Routes>
                <Route path="/dictionary/:word?" element={<Dictionary />} />
                <Route
                    path="/dictionary/in/:dict/:word"
                    element={<Dictionary />}
                />
            </Routes>
        </MemoryRouter>,
    )

describe("Dictionary page", () => {
    it("renders per-dictionary groups with the root chain nested", async () => {
        respondWith({
            word: "billey",
            isSuggestionTier: false,
            groups: [
                {
                    dictionary: "J Kelly Manx to English",
                    entries: [
                        {
                            primaryWord: "BILLEY",
                            summary: "s. a tree",
                            dictionaryName: "J Kelly Manx to English",
                            rootDepth: 0,
                            plurals: ["BILJIN"],
                        },
                        {
                            primaryWord: "BILL",
                            summary: "s. a bill.",
                            dictionaryName: "J Kelly Manx to English",
                            rootDepth: 0,
                            words: ["BILL", "BILLEY"],
                        },
                    ],
                },
            ],
        })
        renderAt("/dictionary/billey")

        // by role: the scope picker also names every dictionary
        expect(
            await screen.findByRole("heading", {
                name: "J Kelly Manx to English",
            }),
        ).toBeTruthy()
        // the homograph heading carries both spellings; the plural is metadata
        expect(screen.getByText("BILL, BILLEY")).toBeTruthy()
        expect(screen.getByText(/BILJIN/)).toBeTruthy()
        expect(screen.getAllByTitle("plural")).not.toHaveLength(0)
        // the printed abbreviations explain themselves on hover
        expect(screen.getAllByTitle("noun (substantive)")).not.toHaveLength(0)
        expect(screen.getByText(/Search the corpus for/)).toBeTruthy()
    })

    it("a Phillips spelling gets a bridge line, not implied dictionary entries", async () => {
        respondWith({
            word: "dwyne",
            isSuggestionTier: false,
            groups: [
                {
                    dictionary: "Cregeen",
                    entries: [
                        {
                            primaryWord: "dooinney",
                            summary: "a man;",
                            dictionaryName: "Cregeen",
                            rootDepth: 1,
                            phillipsSpellingOf: "dooinney",
                        },
                    ],
                },
            ],
        })
        renderAt("/dictionary/dwyne")

        expect(
            await screen.findByText(/is a c\. 1610 spelling \(Phillips\) of/),
        ).toBeTruthy()
    })

    it("marks the near-spelling tier as suggestions", async () => {
        respondWith({
            word: "costlagh",
            isSuggestionTier: true,
            groups: [
                {
                    dictionary: "Cregeen",
                    entries: [
                        {
                            primaryWord: "coastagh",
                            summary: "…",
                            dictionaryName: "Cregeen",
                            rootDepth: 0,
                            nearMatchOf: "coastagh",
                        },
                    ],
                },
            ],
        })
        renderAt("/dictionary/costlagh")

        expect(await screen.findByText(/near spellings/)).toBeTruthy()
    })

    it("shows the search box without a word", () => {
        renderAt("/dictionary")

        expect(screen.getByLabelText("Look up a Manx word")).toBeTruthy()
        expect(fetchMock).not.toHaveBeenCalled()
    })

    it("offers every dictionary as a scope, alongside all-at-once", async () => {
        respondWith({ word: "billey", isSuggestionTier: false, groups: [] })
        renderAt("/dictionary/billey")

        expect(await screen.findByText("All dictionaries")).toBeTruthy()
        expect(screen.getByText("Cregeen")).toBeTruthy()
        // listed even though the response defines no entry for it: that a
        // dictionary lacks the word is itself worth being able to find out
        expect(screen.getByText("J Kelly Manx to English")).toBeTruthy()
    })

    it("scopes the lookup to the dictionary in the URL", async () => {
        respondWith({ word: "billey", isSuggestionTier: false, groups: [] })
        renderAt("/dictionary/in/cregeen/billey")

        await screen.findByText("All dictionaries")
        const pageCall = fetchMock.mock.calls
            .map(([url]) => hrefOf(url))
            .find((href) => href.includes("/page"))
        expect(pageCall).toContain("dict=cregeen")
    })

    it("keeps the scope when looking up another word", async () => {
        respondWith({ word: "billey", isSuggestionTier: false, groups: [] })
        renderAt("/dictionary/in/cregeen/billey")

        const scoped = await screen.findByText("Cregeen")
        expect(scoped.getAttribute("href")).toBe(
            "/dictionary/in/cregeen/billey",
        )
        expect(screen.getByText("All dictionaries").getAttribute("href")).toBe(
            "/dictionary/billey",
        )
    })
})
