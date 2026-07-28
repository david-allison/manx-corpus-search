import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { cleanup, render, screen } from "@testing-library/react"
import { MemoryRouter, Route, Routes } from "react-router-dom"
import { Dictionary } from "./Dictionary"
import { DictionaryLookupModal } from "../components/DictionaryLookupModal"
import type { Summary, WordListCitation } from "../api/DictionaryApi"

/**
 * The two ways to look a word up must answer alike.
 *
 * A reader meets the dictionary twice: on a word's own page, and by tapping a
 * word in a text. They are separate components over separate endpoints, and
 * three times running they came to disagree — the word lists reached the page
 * and not the tap; the compound-parts marker reached the page and not the tap;
 * the reworded "nothing found" reached the page and not the tap. Each was found
 * by a reader, not by a test.
 *
 * So the invariants live here and are asserted of both. A fixture is declared
 * once and rendered twice: what a reader may conclude from one surface, they
 * must be able to conclude from the other. This file failing means the two have
 * drifted again — fix the drift, not the test.
 *
 * It does not assert they look alike. A popup is not a page, and their layouts
 * are their own business; only what they claim about the word is shared.
 */

const mockLookup = vi.hoisted(() => vi.fn())

// only the tap's call is mocked: the page still goes through the real fetch
// path, so the two really are exercised by their own code
vi.mock("../api/DictionaryApi", async (importOriginal) => ({
    ...(await importOriginal<typeof import("../api/DictionaryApi")>()),
    manxDictionaryLookup: mockLookup,
}))

const fetchMock = vi.fn<typeof fetch>()
vi.stubGlobal("fetch", fetchMock)

beforeEach(() => {
    fetchMock.mockReset()
    mockLookup.mockReset()
})
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

const source = {
    listId: "morrison-plants",
    name: "Manx Plant Names",
    credit: "Sophia Morrison",
    date: "1908",
    documentIdent: "Manx-Plant-Names",
    url: "https://example.invalid",
    citation: "Manx Wild Flowers, 1908",
}

/** One look-up, as both surfaces would receive it */
type Fixture = {
    word: string
    entries: Summary[]
    wordLists: WordListCitation[]
}

const naming = (headword: string, gloss: string): WordListCitation => ({
    source,
    headword,
    gloss,
})

/** Renders the word's own page from the fixture */
const showPage = async (fixture: Fixture) => {
    fetchMock.mockImplementation((url) => {
        const href =
            typeof url === "string"
                ? url
                : url instanceof URL
                  ? url.href
                  : ((url as Request | undefined)?.url ?? "")
        const body = href.includes("/history")
            ? emptyHistory
            : href.includes("/dictionaries")
              ? []
              : href.includes("/attestations")
                ? {
                      word: "",
                      lemmas: [],
                      documents: [],
                      undatedDocuments: 0,
                      undatedUses: 0,
                  }
                : href.includes("/samples")
                  ? []
                  : {
                        word: fixture.word,
                        isSuggestionTier:
                            fixture.entries.length > 0 &&
                            fixture.entries.every((e) => e.nearMatchOf),
                        attested: true,
                        answering: [],
                        groups: fixture.entries.length
                            ? [
                                  {
                                      dictionary: "Cregeen",
                                      entries: fixture.entries,
                                  },
                              ]
                            : [],
                        wordLists: fixture.wordLists,
                    }
        return Promise.resolve({
            ok: true,
            json: () => Promise.resolve(body),
        } as Response)
    })
    render(
        <MemoryRouter initialEntries={[`/dictionary/${fixture.word}`]}>
            <Routes>
                <Route path="/dictionary/:word?" element={<Dictionary />} />
            </Routes>
        </MemoryRouter>,
    )
    await screen.findByText(new RegExp(fixture.word, "i"))
}

/** Renders the tap popup from the same fixture */
const showTap = async (fixture: Fixture) => {
    mockLookup.mockResolvedValue({
        entries: fixture.entries,
        wordLists: fixture.wordLists,
    })
    render(
        <MemoryRouter>
            <DictionaryLookupModal
                open
                word={fixture.word}
                context={fixture.word}
                onClose={() => {}}
            />
        </MemoryRouter>,
    )
    await screen.findByText(new RegExp(fixture.word, "i"))
}

const surfaces: [string, (f: Fixture) => Promise<void>][] = [
    ["word page", showPage],
    ["tap popup", showTap],
]

describe.each(surfaces)("a look-up on the %s", (_name, show) => {
    /** çhee "seeking" is not what 'Bolan-y-chee' means. Whichever surface a
     * reader met it on, the part must not wear the whole word's name. */
    it("never reads a word's parts as the word itself", async () => {
        await show({
            word: "Bolan-y-chee",
            entries: [
                {
                    primaryWord: "çhee",
                    summary: "seeking",
                    dictionaryName: "Cregeen",
                    rootDepth: 0,
                    partOf: "chee",
                    partsOfSpeech: ["v."],
                },
            ],
            wordLists: [naming("Bolan-y-chee", "Nipplewort")],
        })

        // the part is shown, and shown as a part
        expect(screen.getByText(/seeking/)).toBeTruthy()
        expect(screen.getByText(/No dictionary lists/)).toBeTruthy()
    })

    /** A printed list defines the word; neither surface may say otherwise */
    it("shows a naming where no dictionary defines the word", async () => {
        await show({
            word: "Ollyssyn",
            entries: [],
            wordLists: [naming("Ollyssyn", "Alexanders")],
        })

        expect(screen.getByText(/Alexanders/)).toBeTruthy()
        expect(screen.queryByText(/Could not find/i)).toBeNull()
        expect(screen.queryByText(/Nothing found/i)).toBeNull()
    })

    /** Where a book does define the word, the naming is still carried */
    it("carries a naming beside a book's entry", async () => {
        await show({
            word: "keirn",
            entries: [
                {
                    primaryWord: "keirn",
                    summary: "the mountain ash",
                    dictionaryName: "Cregeen",
                    rootDepth: 0,
                },
            ],
            wordLists: [naming("Keirn", "Ash (mountain)")],
        })

        expect(screen.getByText(/the mountain ash/)).toBeTruthy()
        expect(screen.getByText(/Ash \(mountain\)/)).toBeTruthy()
    })

    /** A word nothing answers for says so, on either surface */
    it("admits a miss when nothing answers for the word", async () => {
        await show({ word: "xyzzy", entries: [], wordLists: [] })

        expect(screen.getByText(/Could not find/i)).toBeTruthy()
    })
})
