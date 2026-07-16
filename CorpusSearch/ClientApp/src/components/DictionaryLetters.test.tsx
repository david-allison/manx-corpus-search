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
    it("offers the lemma index beside the letters", async () => {
        respond(["A", "B"])
        render(
            <MemoryRouter>
                <DictionaryLetters />
            </MemoryRouter>,
        )

        expect(await screen.findByRole("link", { name: "A" })).toBeTruthy()
        expect(
            screen
                .getByRole("link", { name: /lemma index/ })
                .getAttribute("href"),
        ).toBe("/dictionary/lemma")
    })

    it("offers the lemma index even with no letters to show", async () => {
        // the browse letters come from cregeen.json, downloaded at deploy; the
        // lemma tables are a submodule and stand on their own
        respond([])
        render(
            <MemoryRouter>
                <DictionaryLetters />
            </MemoryRouter>,
        )

        expect(
            await screen.findByRole("link", { name: /lemma index/ }),
        ).toBeTruthy()
    })
})
