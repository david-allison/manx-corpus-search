import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { cleanup, render, screen } from "@testing-library/react"
import { MemoryRouter } from "react-router-dom"
import { HeadwordNav } from "./HeadwordNav"

const fetchMock = vi.fn<typeof fetch>()
vi.stubGlobal("fetch", fetchMock)

beforeEach(() => fetchMock.mockReset())
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
        fetchMock.mockReturnValueOnce(new Promise(() => {}))
        rerender(nav("faar-y-chaagh"))

        expect(
            screen.getByRole("navigation", { name: "Headwords" }),
        ).toBeTruthy()
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
