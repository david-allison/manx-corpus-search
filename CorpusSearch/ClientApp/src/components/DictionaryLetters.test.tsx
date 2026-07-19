import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { cleanup, render, screen } from "@testing-library/react"
import { MemoryRouter } from "react-router-dom"
import { DictionaryLetters } from "./DictionaryLetters"

const fetchMock = vi.fn<typeof fetch>()
vi.stubGlobal("fetch", fetchMock)

beforeEach(() => fetchMock.mockReset())
afterEach(cleanup)

const hrefOf = (url: unknown): string =>
    typeof url === "string"
        ? url
        : url instanceof URL
          ? url.href
          : ((url as Request | undefined)?.url ?? "")

const respond = (letters: string[]) =>
    fetchMock.mockImplementation((url) =>
        Promise.resolve({
            ok: true,
            json: () =>
                Promise.resolve(
                    hrefOf(url).includes("/browse")
                        ? {
                              dictionary: "Cregeen",
                              slug: "cregeen",
                              letters,
                              letter: letters[0] ?? null,
                              chapters: [],
                          }
                        : [],
                ),
        } as Response),
    )

describe("DictionaryLetters", () => {
    /* First in the file: the dictionary list is cached at module scope once
       fetched, and any earlier test would cache the empty list this one needs
       to be a row of books */
    it("marks no book active: the landing has selected nothing", async () => {
        fetchMock.mockImplementation((url) =>
            Promise.resolve({
                ok: true,
                json: () =>
                    Promise.resolve(
                        hrefOf(url).includes("/browse")
                            ? {
                                  dictionary: "Cregeen",
                                  slug: "cregeen",
                                  letters: ["A"],
                                  letter: "A",
                                  chapters: [],
                              }
                            : hrefOf(url).includes("/dictionaries")
                              ? [
                                    { slug: "cregeen", name: "Cregeen" },
                                    { slug: "kelly-m2e", name: "J Kelly" },
                                ]
                              : [],
                    ),
            } as Response),
        )
        render(
            <MemoryRouter>
                <DictionaryLetters />
            </MemoryRouter>,
        )

        // the letters are Cregeen's, but the book is not thereby chosen: the
        // active mark is the browse pages' "you are here"
        const cregeen = await screen.findByRole("link", { name: "Cregeen" })
        expect(cregeen.getAttribute("aria-current")).toBeNull()
        expect(cregeen.className).not.toContain("active")
    })

    it("offers the letters as links into the browse", async () => {
        respond(["A", "B"])
        render(
            <MemoryRouter>
                <DictionaryLetters />
            </MemoryRouter>,
        )

        expect(await screen.findByRole("link", { name: "A" })).toBeTruthy()
        expect(screen.getByRole("link", { name: "B" })).toBeTruthy()
    })

    it("offers no word-families line: dropped until it earns its place", async () => {
        respond(["A"])
        render(
            <MemoryRouter>
                <DictionaryLetters />
            </MemoryRouter>,
        )

        await screen.findByRole("link", { name: "A" })
        expect(screen.queryByRole("link", { name: /word families/ })).toBeNull()
    })
})
