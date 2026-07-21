import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import {
    cleanup,
    fireEvent,
    render,
    screen,
    waitFor,
} from "@testing-library/react"
import { MemoryRouter, Route, Routes } from "react-router-dom"
import { DictionaryLemma } from "./DictionaryLemma"
import {
    DictionaryBrowseResponse,
    LemmaTreeResponse,
} from "../api/DictionaryApi"

const fetchMock = vi.fn<typeof fetch>()
vi.stubGlobal("fetch", fetchMock)

beforeEach(() => {
    fetchMock.mockReset()
    // the never-said filter is remembered: one test's choice must not
    // grey out another's expectations
    localStorage.clear()
})
afterEach(cleanup)

const index: DictionaryBrowseResponse = {
    dictionary: "Lemmas",
    slug: "lemma",
    letters: ["A", "B"],
    letter: "A",
    chapters: [
        {
            key: "AAS",
            words: [
                { word: "aase", attested: true },
                { word: "aasit", attested: false },
            ],
        },
    ],
}

const tree: LemmaTreeResponse = {
    lemma: "peiagh",
    attestations: 12,
    attested: true,
    unverified: true,
    groups: [
        {
            linkType: "inflected",
            forms: [
                {
                    form: "peiaghyn",
                    attestations: 0,
                    attested: false,
                    unverified: true,
                    sharedWithOtherLemmas: false,
                },
                {
                    form: "pyee",
                    attestations: 0,
                    attested: false,
                    unverified: false,
                    source: "cregeen",
                    sharedWithOtherLemmas: false,
                },
            ],
        },
        {
            linkType: "mutation",
            forms: [
                {
                    form: "pheiagh",
                    attestations: 2,
                    attested: true,
                    unverified: true,
                    sharedWithOtherLemmas: false,
                },
            ],
        },
        {
            linkType: "made-up-link",
            forms: [
                {
                    form: "pyagh",
                    attestations: null,
                    attested: true,
                    unverified: false,
                    sharedWithOtherLemmas: false,
                    // full depth: what hangs off pyagh nests inside its node
                    groups: [
                        {
                            linkType: "inflected",
                            forms: [
                                {
                                    form: "pyaghyn",
                                    attestations: 3,
                                    attested: true,
                                    unverified: true,
                                    sharedWithOtherLemmas: false,
                                },
                            ],
                        },
                    ],
                },
            ],
        },
    ],
}

const respondWith = (body: unknown, ok = true) =>
    fetchMock.mockResolvedValue({
        ok,
        status: ok ? 200 : 404,
        json: () => Promise.resolve(body),
    } as Response)

/** The route as App.tsx declares it: one path, the index without a lemma */
const renderAt = (path: string) =>
    render(
        <MemoryRouter initialEntries={[path]}>
            <Routes>
                <Route
                    path="/dictionary/lemma/:lemma?"
                    element={<DictionaryLemma />}
                />
            </Routes>
        </MemoryRouter>,
    )

describe("lemma index", () => {
    it("lists the letter's lemmas as links to their trees", async () => {
        respondWith(index)
        renderAt("/dictionary/lemma")

        const link = await screen.findByRole("link", { name: "aase" })
        expect(link.getAttribute("href")).toBe("/dictionary/lemma/aase")
        expect(screen.getByText("AAS")).toBeTruthy()
    })

    it("greys a lemma no text uses", async () => {
        respondWith(index)
        renderAt("/dictionary/lemma")

        const unattested = await screen.findByRole("link", { name: "aasit" })
        expect(unattested.className).toContain("dict-unattested")
        expect(
            screen.getByRole("link", { name: "aase" }).className,
        ).not.toContain("dict-unattested")
    })

    it("can hide the greyed words, and the checkbox is their key", async () => {
        respondWith(index)
        renderAt("/dictionary/lemma")
        await screen.findByRole("link", { name: "aasit" })

        // the label wears the very grey it explains
        const filter = screen.getByText("unattested words")
        expect(filter.className).toContain("dict-unattested")
        fireEvent.click(screen.getByRole("checkbox"))

        expect(screen.queryByRole("link", { name: "aasit" })).toBeNull()
        expect(screen.getByRole("link", { name: "aase" })).toBeTruthy()

        fireEvent.click(screen.getByRole("checkbox"))
        expect(await screen.findByRole("link", { name: "aasit" })).toBeTruthy()
    })

    it("asks for the letter on the query string, and marks it in the bar", async () => {
        respondWith({ ...index, letter: "B", chapters: [] })
        renderAt("/dictionary/lemma?at=b")

        await waitFor(() =>
            expect(fetchMock).toHaveBeenCalledWith(
                "/api/Dictionary/lemmas?at=b",
                expect.anything(),
            ),
        )
        // both bars mark the open letter, and each letter links via ?at=
        const active = await screen.findAllByRole("link", { current: "page" })
        expect(active[0].textContent).toBe("B")
        expect(active[0].getAttribute("href")).toBe("/dictionary/lemma?at=B")
    })
})

describe("lemma tree", () => {
    it("draws a branch per link type, under the reader's name for it", async () => {
        respondWith(tree)
        renderAt("/dictionary/lemma/peiagh")

        expect(
            (await screen.findAllByText("Inflected forms")).length,
        ).toBeGreaterThan(0)
        expect(screen.getByText("Mutations")).toBeTruthy()
        // a link type the data grows shows under its own name, not nothing
        expect(screen.getByText("made-up-link")).toBeTruthy()
        // the branches nest inside the tree, a list per branch
        expect(
            document.querySelectorAll(".dict-lemma-tree > li > ul > li"),
        ).toHaveLength(4)
        // a form is a link to its own word page
        expect(
            screen.getByRole("link", { name: "peiaghyn" }).getAttribute("href"),
        ).toBe("/dictionary/peiaghyn")
    })

    it("marks the guesses and greys the unattested, separately", async () => {
        respondWith(tree)
        renderAt("/dictionary/lemma/peiagh")

        // peiaghyn: unverified and unattested; pheiagh: unverified but said
        const unspoken = await screen.findByRole("link", { name: "peiaghyn" })
        expect(unspoken.className).toContain("dict-unattested")
        expect(
            screen.getByRole("link", { name: "pheiagh" }).className,
        ).not.toContain("dict-unattested")
        // one mark per unverified link — nested pyaghyn's included — and one
        // for the hand-asserted root
        expect(screen.getAllByText("unverified")).toHaveLength(4)
    })

    it("nests what hangs off a form inside its node, to full depth", async () => {
        respondWith(tree)
        renderAt("/dictionary/lemma/peiagh")

        const nested = await screen.findByRole("link", { name: "pyaghyn" })
        // pyaghyn inflects pyagh, not peiagh: its list sits inside pyagh's li
        const pyagh = screen.getByRole("link", { name: "pyagh" })
        expect(pyagh.parentElement!.contains(nested)).toBe(true)
        // and it wears its own branch chip on the way down
        expect(screen.getAllByText("Inflected forms")).toHaveLength(2)
    })

    it("counts each node's attestations, silent at 0 and unknown", async () => {
        respondWith(tree)
        renderAt("/dictionary/lemma/peiagh")

        // the root's count, pheiagh's and nested pyaghyn's; peiaghyn is a
        // known 0 (the greying says it) and pyagh's phrase count has not
        // landed — neither shows
        expect(await screen.findByText("×12")).toBeTruthy()
        expect(screen.getByText("×2")).toBeTruthy()
        expect(screen.getByText("×3")).toBeTruthy()
        expect(document.querySelectorAll(".dict-lemma-count")).toHaveLength(3)
    })

    it("marks a form whose spelling another word also uses", async () => {
        respondWith({
            ...tree,
            groups: [
                {
                    linkType: "mutation",
                    forms: [
                        {
                            form: "pheiagh",
                            attestations: 2,
                            attested: true,
                            unverified: false,
                            sharedWithOtherLemmas: true,
                        },
                    ],
                },
            ],
        })
        renderAt("/dictionary/lemma/peiagh")

        // the * rides the count: the count is of the spelling, and some of it
        // may be the other word's
        await screen.findByText("×2")
        expect(
            screen.getByTitle(
                "Another word also uses this spelling: some of these occurrences may be its",
            ).textContent,
        ).toBe("*")
        // the root's count carries no mark: the response does not say it of
        // the lemma itself
        expect(
            screen.getAllByTitle(/Another word also uses this spelling/),
        ).toHaveLength(1)
    })

    it("reads back up: the root names what it hangs off", async () => {
        respondWith({
            ...tree,
            lemma: "aa-ghiennaghtyn",
            parents: [
                { lemma: "giennaghtyn", linkTypes: ["inflected", "plural"] },
                { lemma: "aa-", linkTypes: ["prefixed"] },
            ],
        })
        renderAt("/dictionary/lemma/aa-ghiennaghtyn")

        // a table parent reads as the reverse of the downward chips...
        const parent = await screen.findByRole("link", {
            name: "giennaghtyn",
        })
        expect(parent.getAttribute("href")).toBe(
            "/dictionary/lemma/giennaghtyn",
        )
        expect(screen.getByText(/inflected · plural/)).toBeTruthy()
        // ...and the spelling parent as the prefix it is written with
        expect(
            screen.getByRole("link", { name: "aa-" }).getAttribute("href"),
        ).toBe("/dictionary/lemma/aa-")
        expect(screen.getByText(/Written with the prefix/)).toBeTruthy()
    })

    it("names the book behind a form no text uses", async () => {
        respondWith(tree)
        renderAt("/dictionary/lemma/peiagh")

        // pyee: greyed, but Cregeen prints it and the note says so.
        // Nothing else earns one — not the attested forms (the corpus
        // vouches), not the guesses (marked unverified instead)
        const note = await screen.findByText("Cregeen")
        expect(note.getAttribute("title")).toContain("Cregeen records “pyee”")
        expect(screen.getAllByText("Cregeen")).toHaveLength(1)
    })

    it("says when the tables know no such lemma, with a way back", async () => {
        respondWith({}, false)
        renderAt("/dictionary/lemma/xyzzy")

        expect(await screen.findByText(/No lemma “xyzzy”/)).toBeTruthy()
        expect(
            screen
                .getByRole("link", { name: /Back to the lemma index/ })
                .getAttribute("href"),
        ).toBe("/dictionary/lemma")
    })
})
