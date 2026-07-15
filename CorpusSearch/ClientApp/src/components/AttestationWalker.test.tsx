import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import {
    cleanup,
    fireEvent,
    render,
    screen,
    waitFor,
} from "@testing-library/react"
import { MemoryRouter } from "react-router-dom"
import { AttestationWalker } from "./AttestationWalker"

const fetchMock = vi.fn<typeof fetch>()
vi.stubGlobal("fetch", fetchMock)

beforeEach(() => {
    fetchMock.mockReset()
    localStorage.clear()
})
afterEach(cleanup)

/** Not every call through the stubbed global arrives with a string url, and a
 * Request would stringify to '[object Object]' */
const hrefOf = (url: unknown): string =>
    typeof url === "string"
        ? url
        : url instanceof URL
          ? url.href
          : ((url as Request | undefined)?.url ?? "")

const walk = {
    word: "aase",
    lemmas: ["aase"],
    documents: [
        { ident: "Psalms1610", title: "Psalms", year: 1610 },
        { ident: "Coyrle", title: "Coyrle Sodjey", year: 1707 },
    ],
    undatedDocuments: 0,
}

const lines = {
    ident: "Psalms1610",
    title: "Psalms",
    year: 1610,
    useCount: 9,
    groups: [
        {
            lemmaId: "aase.v",
            lemma: "aase",
            count: 9,
            lines: [
                {
                    manx: "Daase yn billey",
                    english: "The tree grew",
                    manxHighlights: [{ start: 0, end: 5 }],
                    csvLineNumber: 2,
                },
            ],
        },
    ],
}

const respond = () =>
    fetchMock.mockImplementation((url) => {
        const href = hrefOf(url)
        const body = href.includes("/attestations/") ? lines : walk
        return Promise.resolve({
            ok: true,
            json: () => Promise.resolve(body),
        } as Response)
    })

/** the walk's own tests: the first-seen band it hosts has its own file, and
 * an unattested history keeps it out of the way here */
const renderWalker = () =>
    render(
        <MemoryRouter initialEntries={["/dictionary/aase"]}>
            <AttestationWalker word="aase" history={null} classes={[]} />
        </MemoryRouter>,
    )

describe("AttestationWalker", () => {
    it("starts open: the evidence is the point of the section", async () => {
        respond()
        renderWalker()

        // the surface word is its own <mark>, so the line is not one text node
        expect(await screen.findByText("Daase")).toBeTruthy()
        expect(await screen.findByText(/2 texts, 1610–1707/)).toBeTruthy()
        // the earliest text leads, and its uses are counted once each
        // (the "All 9 uses in this text" link says it too)
        expect(screen.getAllByText(/9 uses/).length).toBeGreaterThan(0)
    })

    it("shuts on click and remembers the choice", async () => {
        respond()
        renderWalker()
        await screen.findByText("Daase")

        fireEvent.click(screen.getByRole("button", { name: /In the corpus/ }))

        expect(
            screen
                .getByRole("button", { name: /In the corpus/ })
                .getAttribute("aria-expanded"),
        ).toBe("false")
        await waitFor(() =>
            expect(localStorage.getItem("dictionary.corpus.open")).toBe(
                "false",
            ),
        )
    })

    it("starts shut when the reader has shut it before", async () => {
        localStorage.setItem("dictionary.corpus.open", "false")
        respond()
        renderWalker()
        await screen.findByText(/2 texts/)

        expect(screen.queryByText("Daase")).toBeNull()
        // shut: the uses are not worth fetching until they would be shown
        expect(
            fetchMock.mock.calls.filter(([u]) =>
                hrefOf(u).includes("/attestations/"),
            ),
        ).toHaveLength(0)
    })

    it("names the reading beside its lines", async () => {
        respond()
        renderWalker()
        await screen.findByText("Daase")

        expect(screen.getByText("aase")).toBeTruthy()
        expect(screen.getByText(/×9/)).toBeTruthy()
    })

    it("names each reading when the word is ambiguous", async () => {
        fetchMock.mockImplementation((url) => {
            const href = hrefOf(url)
            const body = href.includes("/attestations/")
                ? {
                      ...lines,
                      useCount: 3,
                      groups: [
                          {
                              ...lines.groups[0],
                              lemmaId: "bee.v",
                              lemma: "bee",
                              count: 2,
                          },
                          {
                              ...lines.groups[0],
                              lemmaId: "mee.n",
                              lemma: "mee",
                              count: 1,
                          },
                      ],
                  }
                : walk
            return Promise.resolve({
                ok: true,
                json: () => Promise.resolve(body),
            } as Response)
        })
        renderWalker()

        expect(await screen.findByText("bee")).toBeTruthy()
        expect(screen.getByText("mee")).toBeTruthy()
    })
})
