import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { cleanup, render, screen, waitFor } from "@testing-library/react"
import { MemoryRouter } from "react-router-dom"
import { DictionaryCoverage } from "./DictionaryCoverage"
import { DictionaryStats } from "../api/DictionaryApi"

const fetchMock = vi.fn<typeof fetch>()
vi.stubGlobal("fetch", fetchMock)

beforeEach(() => fetchMock.mockReset())
afterEach(cleanup)

const stats: DictionaryStats = {
    texts: 400,
    runningWords: 1_000_000,
    distinctWords: 50_000,
    books: 3,
    entries: 90_000,
    definedWords: 40_000,
    definedRunningWords: 950_000,
    lemmas: 8_000,
    attestedLemmas: 6_000,
    recordings: 23,
    audioWords: 7_500,
    audioRunningWords: 751_000,
}

const respond = (body: DictionaryStats) =>
    fetchMock.mockResolvedValue({
        ok: true,
        json: () => Promise.resolve(body),
    } as Response)

describe("DictionaryCoverage", () => {
    it("leads with the whole text's share, a bar beneath, the counts last", async () => {
        respond(stats)
        render(
            <MemoryRouter>
                <DictionaryCoverage />
            </MemoryRouter>,
        )

        // the headline weighs the text a reader meets, not the word list:
        // the distinct-words pair rides in the detail line
        await waitFor(() => expect(screen.getByText("95.0%")).toBeTruthy())
        const entry = screen.getByRole("progressbar", {
            name: "of the corpus text has an entry",
        })
        expect(entry.getAttribute("aria-valuenow")).toBe("95")
        expect(
            screen.getByText(/40,000 of 50,000 distinct words \(80\.0%\)/),
        ).toBeTruthy()
        // the audio card wears the icon in its title, and counts distinct
        // words: a token-weighted share would dress the recordings' handful
        // of common words up as most of the corpus
        expect(screen.getByText("🔊 15.0%")).toBeTruthy()
        const audio = screen.getByRole("progressbar", {
            name: "of the corpus's words can be heard spoken",
        })
        expect(audio.getAttribute("aria-valuenow")).toBe("15")
        expect(
            screen.getByText(
                /7,500 of 50,000 distinct words, across 23 recordings/,
            ),
        ).toBeTruthy()
        // the share's own index hangs off the label
        const spoken = screen.getByRole("link", {
            name: "of the corpus's words can be heard spoken ›",
        })
        expect(spoken.getAttribute("href")).toBe("/dictionary/spoken")
        // the lemma table, the books, and the corpus measured against
        expect(screen.getByText("75.0%")).toBeTruthy()
        expect(
            screen.getByRole("progressbar", {
                name: "of the word families appear in the texts",
            }),
        ).toBeTruthy()
        expect(screen.getByText("90,000 entries")).toBeTruthy()
        expect(screen.getByText(/across 3 dictionaries/)).toBeTruthy()
        expect(screen.getByText(/400 texts/)).toBeTruthy()
    })

    it("says the recordings are unread rather than counting them at zero", async () => {
        respond({
            ...stats,
            recordings: null,
            audioWords: null,
            audioRunningWords: null,
        })
        render(
            <MemoryRouter>
                <DictionaryCoverage />
            </MemoryRouter>,
        )

        await waitFor(() =>
            expect(
                screen.getByText("the recordings are still being read"),
            ).toBeTruthy(),
        )
        expect(screen.getByText("🔊 …")).toBeTruthy()
        const audio = screen.getByRole("progressbar", {
            name: "the recordings are still being read",
        })
        expect(audio.getAttribute("aria-valuenow")).toBeNull()
    })
})
