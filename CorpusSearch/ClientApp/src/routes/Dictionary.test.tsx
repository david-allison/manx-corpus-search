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

const respondWith = (page: DictionaryPageResponse) =>
    fetchMock.mockImplementation((url) =>
        Promise.resolve({
            ok: true,
            json: () =>
                Promise.resolve(
                    hrefOf(url).includes("/history") ? emptyHistory : page,
                ),
        } as Response),
    )

const hrefOf = (url: string | URL | Request): string =>
    typeof url === "string" ? url : url instanceof URL ? url.href : url.url

const renderAt = (path: string) =>
    render(
        <MemoryRouter initialEntries={[path]}>
            <Routes>
                <Route path="/dictionary/:word?" element={<Dictionary />} />
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

        expect(await screen.findByText("J Kelly Manx to English")).toBeTruthy()
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
})
