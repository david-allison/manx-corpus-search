import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import {
    cleanup,
    render,
    screen,
    waitFor,
    within,
} from "@testing-library/react"
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

/** A word one text uses: what the history's scan reports for an attested word */
const usedOnce = { ...emptyHistory, traditionalCount: 1 }

/** A page fixture. `answering` — which dictionaries the picker leaves un-greyed
 * — is only the picker's business, so it is optional here and defaults to every
 * dictionary: a test about plurals should not have to answer for the tabs. */
type PageFixture = Omit<DictionaryPageResponse, "answering"> & {
    answering?: string[]
}

const respondWith = (
    page: PageFixture,
    history: typeof emptyHistory = emptyHistory,
) =>
    fetchMock.mockImplementation((url) => {
        const href = hrefOf(url)
        const body = href.includes("/history")
            ? history
            : href.includes("/dictionaries")
              ? dictionaries
              : href.includes("/attestations")
                ? emptyAttestations
                : href.includes("/samples")
                  ? []
                  : {
                        answering: dictionaries.map((d) => d.slug),
                        ...page,
                    }
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
            attested: true,
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
    })

    /** One word of one book, with the picker's answer as the caller likes it */
    const caag = (answering: string[]): PageFixture => ({
        word: "caag",
        isSuggestionTier: false,
        attested: true,
        answering,
        groups: [
            {
                dictionary: "Cregeen",
                entries: [
                    {
                        primaryWord: "caag",
                        summary: "a forelock",
                        dictionaryName: "Cregeen",
                        rootDepth: 0,
                    },
                ],
            },
        ],
    })

    /** The offer is only worth making where there is something to find. The
     * browse page's cheap guess meets a phrase one word at a time and would call
     * 'geinnagh vane' attested for using 'geinnagh' and 'vane' apart; the history
     * really scanned, so the link asks it instead. */
    describe("the corpus link", () => {
        it("is offered for a word the corpus uses", async () => {
            respondWith(caag(["cregeen"]), usedOnce)
            renderAt("/dictionary/caag")

            expect(
                await screen.findByText(/Search the corpus for/),
            ).toBeTruthy()
        })

        it("is withheld where no text uses the word", async () => {
            respondWith(caag(["cregeen"]), emptyHistory)
            renderAt("/dictionary/caag")

            expect(await screen.findByText(/a forelock/)).toBeTruthy()
            expect(screen.queryByText(/Search the corpus for/)).toBeNull()
        })
    })

    /** Out of the word and back to the index it is filed in. A control on the
     * page's own top row rather than a step in the headword walk: stepping out
     * of the walk is not a step in it. */
    describe("the way back to the index", () => {
        it("sits with the search box, on the word's own letter", async () => {
            respondWith(caag(["cregeen"]))
            renderAt("/dictionary/caag")

            const index = await screen.findByLabelText("Back to the index")
            expect(index.getAttribute("href")).toBe(
                "/dictionary/browse/cregeen/caag",
            )
            expect(index.closest("form.dict-page-search")).toBeTruthy()
        })

        it("keeps the book you are reading in", async () => {
            respondWith(caag(["kelly-m2e"]))
            renderAt("/dictionary/in/kelly-m2e/caag")

            expect(
                (
                    await screen.findByLabelText("Back to the index")
                ).getAttribute("href"),
            ).toBe("/dictionary/browse/kelly-m2e/caag")
        })

        it("is not offered where there is no word to be filed", () => {
            respondWith(caag([]))
            renderAt("/dictionary")

            // /dictionary is the index: there is nowhere up from it
            expect(screen.queryByLabelText("Back to the index")).toBeNull()
        })
    })

    /** The picker lists every dictionary, because "Cregeen has no entry for it"
     * is itself worth being able to find out — but a reader should not have to
     * click each in turn to find it out. */
    describe("the scope picker", () => {
        const scope = () =>
            screen.getByRole("navigation", { name: "Dictionary" })

        it("greys the dictionaries with nothing for the word", async () => {
            respondWith(caag(["cregeen"]))
            renderAt("/dictionary/caag")
            await screen.findByText(/a forelock/)

            const kelly = within(scope()).getByText("J Kelly Manx to English")
            expect(kelly.className).toContain("dict-scope-empty")
            // the grey is a colour, and not every reader gets one
            expect(kelly.getAttribute("title")).toBe(
                "Nothing for “caag” in J Kelly Manx to English",
            )
            expect(
                within(scope()).getByText("Cregeen").className,
            ).not.toContain("dict-scope-empty")
        })

        it("still lets you go and see the empty page", async () => {
            respondWith(caag(["cregeen"]))
            renderAt("/dictionary/caag")
            await screen.findByText(/a forelock/)

            // greyed, not disabled: the answer is "not in this book", and a
            // reader is entitled to see that for themselves
            expect(
                within(scope())
                    .getByText("J Kelly Manx to English")
                    .getAttribute("href"),
            ).toBe("/dictionary/in/kelly-m2e/caag")
        })

        it("greys All dictionaries only when none of them answer", async () => {
            respondWith(caag(["cregeen"]))
            renderAt("/dictionary/caag")
            await screen.findByText(/a forelock/)

            expect(
                within(scope()).getByText("All dictionaries").className,
            ).not.toContain("dict-scope-empty")
        })

        it("greys every link where the word is in no dictionary at all", async () => {
            respondWith({
                word: "xyzzy",
                isSuggestionTier: false,
                attested: false,
                answering: [],
                groups: [],
            })
            renderAt("/dictionary/xyzzy")
            await screen.findByText(/Could not find a definition/)

            const links = within(scope()).getAllByRole("link")
            expect(links).toHaveLength(3)
            expect(
                links.every((x) => x.className.includes("dict-scope-empty")),
            ).toBe(true)
        })
    })

    it("splits the entries into the senses they declare", async () => {
        // 'ass' is a weasel and 'out': Cregeen's adverb and Kelly's preposition
        // are the same sense, so they head one group and the weasel another
        respondWith({
            word: "ass",
            isSuggestionTier: false,
            attested: true,
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
        // the merged sense names both classes, so a wrong merge is visible.
        // Each class is its own <abbr>, so the label is read whole rather than
        // matched as one string
        expect(
            [...document.querySelectorAll(".dict-page-sense-label")].map(
                (x) => x.textContent,
            ),
        ).toEqual(["n.", "adv., prep."])
        expect(
            screen.getAllByText("ass", { selector: ".dict-page-sense-word" }),
        ).toHaveLength(2)
    })

    /** Every printed abbreviation on the page explains itself on hover, and the
     * class beside a title is no different for having been worked out from the
     * entries rather than read off a page */
    it("expands each class of a sense label on hover", async () => {
        respondWith({
            word: "ass",
            isSuggestionTier: false,
            attested: true,
            groups: [
                {
                    dictionary: "Cregeen",
                    entries: [
                        {
                            primaryWord: "ass",
                            summary: "out",
                            dictionaryName: "Cregeen",
                            rootDepth: 0,
                            partsOfSpeech: ["Adverb"],
                        },
                        {
                            primaryWord: "ASS",
                            summary: "a weasel",
                            dictionaryName: "Cregeen",
                            rootDepth: 0,
                            partsOfSpeech: ["Noun"],
                        },
                    ],
                },
            ],
        })
        renderAt("/dictionary/ass")

        // 'n.' is the page's own word for a noun: the books print 's.'
        expect((await screen.findByTitle("noun")).textContent).toBe("n.")
        expect(screen.getByTitle("adverb").textContent).toBe("adv.")
        // each is its own abbr: a tooltip cannot be hung on half a string
        expect(screen.getByTitle("noun").tagName).toBe("ABBR")
    })

    it("a Phillips spelling gets a bridge line, not implied dictionary entries", async () => {
        respondWith({
            word: "dwyne",
            isSuggestionTier: false,
            attested: true,
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
            attested: true,
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
            attested: true,
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
            attested: true,
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
        respondWith({
            word: "",
            isSuggestionTier: false,
            attested: true,
            groups: [],
        })
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
        respondWith({
            word: "billey",
            isSuggestionTier: false,
            attested: true,
            groups: [],
        })
        renderAt("/dictionary/billey")

        expect(await screen.findByText("All dictionaries")).toBeTruthy()
        expect(screen.getByText("Cregeen")).toBeTruthy()
        // listed even though the response defines no entry for it: that a
        // dictionary lacks the word is itself worth being able to find out
        expect(screen.getByText("J Kelly Manx to English")).toBeTruthy()
    })

    it("scopes the lookup to the dictionary in the URL", async () => {
        respondWith({
            word: "billey",
            isSuggestionTier: false,
            attested: true,
            groups: [],
        })
        renderAt("/dictionary/in/cregeen/billey")

        await screen.findByText("All dictionaries")
        const pageCall = fetchMock.mock.calls
            .map(([url]) => hrefOf(url))
            .find((href) => href.includes("/page"))
        expect(pageCall).toContain("dict=cregeen")
    })

    it("keeps the scope when looking up another word", async () => {
        respondWith({
            word: "billey",
            isSuggestionTier: false,
            attested: true,
            groups: [],
        })
        renderAt("/dictionary/in/cregeen/billey")

        const scoped = await screen.findByText("Cregeen")
        expect(scoped.getAttribute("href")).toBe(
            "/dictionary/in/cregeen/billey",
        )
        expect(screen.getByText("All dictionaries").getAttribute("href")).toBe(
            "/dictionary/billey",
        )
    })

    it("offers audio in the title when a recording says the word", async () => {
        // respondWith, except the walk holds a recording (the 🎥 name)
        fetchMock.mockImplementation((url) => {
            const href = hrefOf(url)
            const body = href.includes("/history")
                ? usedOnce
                : href.includes("/dictionaries")
                  ? dictionaries
                  : href.includes("/attestations")
                    ? {
                          ...emptyAttestations,
                          documents: [
                              {
                                  ident: "YouTube-SV",
                                  title: "🎥 Skeealyn Vannin",
                                  year: 1948,
                              },
                          ],
                      }
                    : href.includes("/samples")
                      ? []
                      : {
                            answering: dictionaries.map((d) => d.slug),
                            word: "greesagh",
                            isSuggestionTier: false,
                            attested: true,
                            groups: [],
                        }
            return Promise.resolve({
                ok: true,
                json: () => Promise.resolve(body),
            } as Response)
        })
        renderAt("/dictionary/greesagh")

        const audio = await screen.findByRole("button", { name: "🔊 audio" })
        // the popup names the recording before it is opened
        expect(audio.getAttribute("title")).toContain("Skeealyn Vannin")
    })

    /** Skeealyn Vannin Track 12's transcript carries no timings, so its
     * player can only start from the top: the title's link passes over it
     * for a recording it can jump into, however much older the untimed one */
    it("leads with a timed recording over an earlier untimed one", async () => {
        fetchMock.mockImplementation((url) => {
            const href = hrefOf(url)
            const body = href.includes("/history")
                ? usedOnce
                : href.includes("/dictionaries")
                  ? dictionaries
                  : href.includes("/attestations")
                    ? {
                          ...emptyAttestations,
                          documents: [
                              {
                                  ident: "Track-12",
                                  title: "🎥 Skeealyn Vannin, Track 12",
                                  year: 1948,
                                  timed: false,
                              },
                              {
                                  ident: "SA0001",
                                  title: "🎥 HOYFM SA0001",
                                  year: 1951,
                                  timed: true,
                              },
                          ],
                      }
                    : href.includes("/samples")
                      ? []
                      : {
                            answering: dictionaries.map((d) => d.slug),
                            word: "dooinney",
                            isSuggestionTier: false,
                            attested: true,
                            groups: [],
                        }
            return Promise.resolve({
                ok: true,
                json: () => Promise.resolve(body),
            } as Response)
        })
        renderAt("/dictionary/dooinney")

        const audio = await screen.findByRole("button", { name: "🔊 audio" })
        expect(audio.getAttribute("title")).toContain("SA0001")
    })

    it("offers no audio in the title when no recording uses the word", async () => {
        respondWith({
            word: "billey",
            isSuggestionTier: false,
            attested: true,
            groups: [],
        })
        renderAt("/dictionary/billey")

        await screen.findByText("All dictionaries")
        expect(screen.queryByRole("button", { name: "🔊 audio" })).toBeNull()
    })
})
