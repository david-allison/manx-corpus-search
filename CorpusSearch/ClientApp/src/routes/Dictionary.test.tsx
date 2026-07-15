import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { cleanup, render, screen, waitFor } from "@testing-library/react"
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

/** the walk renders nothing without documents: these tests are about the page */
const emptyAttestations = {
    word: "",
    lemmas: [],
    documents: [],
    undatedDocuments: 0,
    undatedUses: 0,
}

const respondWith = (page: DictionaryPageResponse) =>
    fetchMock.mockImplementation((url) => {
        const href = hrefOf(url)
        const body = href.includes("/history")
            ? emptyHistory
            : href.includes("/dictionaries")
              ? dictionaries
              : href.includes("/attestations")
                ? emptyAttestations
                : page
        return Promise.resolve({
            ok: true,
            json: () => Promise.resolve(body),
        } as Response)
    })

/** Not every call through the stubbed global arrives with a string url, so the
 * routing below must not assume one: an unrecognised call falls through to the
 * page body, as it did when every response was the page. */
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
    it("renders the entries, crediting each to its dictionary", async () => {
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

        // the homograph heading carries both spellings; the plural is metadata
        expect(await screen.findByText("BILL, BILLEY")).toBeTruthy()
        expect(screen.getByText(/BILJIN/)).toBeTruthy()
        expect(screen.getAllByTitle("plural")).not.toHaveLength(0)
        // the printed abbreviations explain themselves on hover
        expect(screen.getAllByTitle("noun (substantive)")).not.toHaveLength(0)
        // the sense heading gathers several dictionaries, so each entry says
        // which one it came from
        expect(document.querySelectorAll(".dict-page-credit")).toHaveLength(2)
        expect(screen.getByText(/Search the corpus for/)).toBeTruthy()
    })

    it("splits the entries into the senses they declare", async () => {
        // 'ass' is a weasel and 'out': Cregeen's adverb and Kelly's preposition
        // are the same sense, so they head one group and the weasel another
        respondWith({
            word: "ass",
            isSuggestionTier: false,
            groups: [
                {
                    dictionary: "Cregeen",
                    entries: [
                        {
                            primaryWord: "ass",
                            summary: "out; out of him",
                            dictionaryName: "Cregeen",
                            rootDepth: 0,
                            partsOfSpeech: ["Adverb"],
                        },
                    ],
                },
                {
                    dictionary: "J Kelly Manx to English",
                    entries: [
                        {
                            primaryWord: "ASS",
                            summary: "a weasel",
                            dictionaryName: "J Kelly Manx to English",
                            rootDepth: 0,
                            partsOfSpeech: ["Noun"],
                        },
                        {
                            primaryWord: "ASS",
                            summary: "out, without",
                            dictionaryName: "J Kelly Manx to English",
                            rootDepth: 0,
                            partsOfSpeech: ["Preposition"],
                        },
                    ],
                },
            ],
        })
        renderAt("/dictionary/ass")

        expect(await screen.findByText("n.")).toBeTruthy()
        // the merged sense names both classes, so a wrong merge is visible
        expect(screen.getByText("adv., prep.")).toBeTruthy()
        expect(
            screen.getAllByText("ass", { selector: ".dict-page-sense-word" }),
        ).toHaveLength(2)
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

    it("marks a root the lemma table only reached by rule", async () => {
        respondWith({
            word: "gheiney",
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
                            unverifiedLink: true,
                        },
                    ],
                },
            ],
        })
        renderAt("/dictionary/gheiney")

        // the page must not present a rule-derived guess as documentation
        expect(await screen.findByText("unverified")).toBeTruthy()
    })

    it("leaves a documented root unmarked", async () => {
        respondWith({
            word: "deiney",
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
                        },
                    ],
                },
            ],
        })
        renderAt("/dictionary/deiney")

        expect(await screen.findByText("dooinney")).toBeTruthy()
        expect(screen.queryByText("unverified")).toBeNull()
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

        expect(await screen.findByText(/Near spellings/)).toBeTruthy()
    })

    it("shows the search box and the letters without a word", async () => {
        respondWith({ word: "", isSuggestionTier: false, groups: [] })
        renderAt("/dictionary")

        expect(screen.getByLabelText("Look up a Manx word")).toBeTruthy()
        // the letters are fetched, but no word is looked up: there is none
        await waitFor(() => expect(fetchMock).toHaveBeenCalled())
        expect(
            fetchMock.mock.calls
                .map(([url]) => hrefOf(url))
                .filter((href) => href.includes("/page")),
        ).toHaveLength(0)
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
