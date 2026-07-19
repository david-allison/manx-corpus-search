import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { cleanup, render, screen } from "@testing-library/react"
import { MemoryRouter } from "react-router-dom"
import { HeadwordNav } from "./HeadwordNav"

const fetchMock = vi.fn<typeof fetch>()
vi.stubGlobal("fetch", fetchMock)

beforeEach(() => {
    fetchMock.mockReset()
    // anything not answered explicitly stays on its way: the component
    // prefetches the windows either side, and a test that has said all it
    // means to say should leave those hanging rather than erroring
    fetchMock.mockReturnValue(new Promise(() => {}))
})
afterEach(cleanup)

/** Cregeen files 'faar-y-chaagh' among the 'caa' words, so it really is what
 * follows 'caag' in the book: the order is the printed one, not a sort's */
const neighbours = (word: string, previous: string, next: string) => ({
    word,
    previous,
    next,
    attested: true,
    previousAttested: true,
    nextAttested: true,
})

const responding = (body: unknown) =>
    fetchMock.mockResolvedValueOnce({
        ok: true,
        json: () => Promise.resolve(body),
    } as Response)

const nav = (word: string) => (
    <MemoryRouter>
        <HeadwordNav word={word}>{word}</HeadwordNav>
    </MemoryRouter>
)

describe("HeadwordNav", () => {
    it("steps to the headwords either side", async () => {
        responding(neighbours("caag", "caa", "faar-y-chaagh"))
        render(nav("caag"))

        expect((await screen.findByTitle("caa")).getAttribute("href")).toBe(
            "/dictionary/caa",
        )
        expect(screen.getByTitle("faar-y-chaagh").getAttribute("href")).toBe(
            "/dictionary/faar-y-chaagh",
        )
    })

    /** This row is the thing the reader just clicked. Blanking it while the next
     * word's neighbours are on their way drops it to nothing and grows it back a
     * moment later — the arrows go out from under the cursor and the page slides
     * up into the gap, mid-walk. */
    it("holds the row while the next step's neighbours are loading", async () => {
        responding(neighbours("caag", "caa", "faar-y-chaagh"))
        const { rerender } = render(nav("caag"))
        await screen.findByTitle("caa")

        // the step's own neighbours never arrive: the row must ride it out
        rerender(nav("faar-y-chaagh"))

        expect(
            screen.getByRole("navigation", { name: "Headwords" }),
        ).toBeTruthy()
    })

    /** The row used to ride out a step showing the last page's window, whose
     * "next" was the page already on screen: a second tap went nowhere until
     * the fetch landed, and a reader walking briskly was stopped every step */
    it("re-points the arrow back at the page just left, mid-step", async () => {
        responding(neighbours("caag", "caa", "faar-y-chaagh"))
        const { rerender } = render(nav("caag"))
        await screen.findByTitle("caa")

        // the step's own window never arrives; the shift cannot wait for it
        rerender(nav("faar-y-chaagh"))

        expect(screen.getByTitle("caag").getAttribute("href")).toBe(
            "/dictionary/caag",
        )
        // what lies ahead is genuinely unknown: the slot holds empty for the
        // beat rather than pointing at the page already on screen
        expect(screen.queryByTitle("faar-y-chaagh")).toBeNull()
    })

    it("answers a step within the span from memory", async () => {
        responding({
            ...neighbours("caag", "caa", "faar-y-chaagh"),
            // every answer carries the whole windows either side
            nearby: [
                neighbours("caa", "bab", "caag"),
                neighbours("faar-y-chaagh", "caag", "gaaue"),
            ],
        })
        const { rerender } = render(nav("caag"))
        await screen.findByTitle("caa")
        expect(fetchMock).toHaveBeenCalledTimes(1)
        expect(fetchMock.mock.calls[0][0]).toContain("span=5")

        rerender(nav("caa"))

        // synchronously: no fetch could have answered between those two lines
        expect(screen.getByTitle("bab").getAttribute("href")).toBe(
            "/dictionary/bab",
        )
        expect(screen.getByTitle("caag")).toBeTruthy()
    })

    it("re-centres the span as a step nears its edge", async () => {
        responding({
            ...neighbours("caag", "caa", "faar-y-chaagh"),
            // 'faar-y-chaagh' is the span's edge: what follows it is unknown
            nearby: [neighbours("faar-y-chaagh", "caag", "gaaue")],
        })
        const { rerender } = render(nav("caag"))
        await screen.findByTitle("caa")
        expect(fetchMock).toHaveBeenCalledTimes(1)

        rerender(nav("faar-y-chaagh"))

        // the step itself was answered from memory; the walk asks again in
        // the background so the reader never reaches the edge
        await vi.waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2))
        expect(fetchMock.mock.calls[1][0]).toContain("word=faar-y-chaagh")
    })

    it("shows nothing for a word that is in no book to step through", async () => {
        responding({ word: "xyzzy", attested: false })
        render(nav("xyzzy"))

        // nothing either side, so there is no walk this word is a step in
        await vi.waitFor(() =>
            expect(fetchMock).toHaveBeenCalledWith(
                expect.stringContaining("xyzzy"),
                expect.anything(),
            ),
        )
        expect(screen.queryByRole("navigation")).toBeNull()
    })
})
