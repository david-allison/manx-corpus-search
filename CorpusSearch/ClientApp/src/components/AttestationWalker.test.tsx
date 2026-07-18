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
    lemma: "aase",
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
    lemma: "aase",
    useCount: 9,
    groups: [
        {
            lemmaIds: ["aase.v"],
            lemma: "aase",
            classes: ["v"],
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

/** the walk is the same in every case here; the step's uses are what varies */
const respondWith = (uses: unknown) =>
    fetchMock.mockImplementation((url) =>
        Promise.resolve({
            ok: true,
            json: () =>
                Promise.resolve(
                    hrefOf(url).includes("/attestations/") ? uses : walk,
                ),
        } as Response),
    )

const respond = () => respondWith(lines)

/** the walk again, with two recordings among its texts */
const respondWithRecordings = () =>
    fetchMock.mockImplementation((url) =>
        Promise.resolve({
            ok: true,
            json: () =>
                Promise.resolve(
                    hrefOf(url).includes("/attestations/")
                        ? lines
                        : {
                              ...walk,
                              documents: [
                                  ...walk.documents,
                                  {
                                      ident: "YouTube-SV",
                                      title: "🎥 Skeealyn Vannin",
                                      year: 1948,
                                  },
                                  {
                                      ident: "YouTube-NM",
                                      title: "🎥 Ned Maddrell",
                                      year: 1975,
                                  },
                              ],
                          },
                ),
        } as Response),
    )

/** one step's uses, with `groups` swapped for the reading(s) under test */
const withGroups = (
    useCount: number,
    groups: Record<string, unknown>[],
): unknown => ({
    ...lines,
    useCount,
    groups: groups.map((g) => ({ ...lines.groups[0], ...g })),
})

/** the walk's own tests: the first-seen band it hosts has its own file, and
 * an unattested history keeps it out of the way here */
const renderWalker = (word = "aase", search = "") =>
    render(
        <MemoryRouter initialEntries={[`/dictionary/${word}${search}`]}>
            <AttestationWalker word={word} history={null} classes={[]} />
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

        // the group's own name, not the tab above: both say "aase"
        expect(document.querySelector(".attest-group-lemma")?.textContent).toBe(
            "aase",
        )
        expect(screen.getByText(/×9/)).toBeTruthy()
    })

    it("names each reading when the word is ambiguous", async () => {
        respondWith(
            withGroups(3, [
                { lemmaIds: ["bee.v"], lemma: "bee", classes: ["v"], count: 2 },
                { lemmaIds: ["mee.n"], lemma: "mee", classes: ["n"], count: 1 },
            ]),
        )
        renderWalker()

        expect(await screen.findByText("bee")).toBeTruthy()
        expect(screen.getByText("mee")).toBeTruthy()
        // different headwords: each names the row on its own, and a class beside
        // it would be noise
        expect(screen.queryByText("v.")).toBeNull()
    })

    /** The bug this merging fixed: 'jaagh' is smoke (jaagh.n) or the verb, and a
     * line the resolver left as either answered to both queries — two rows, both
     * reading "jaagh ×1", showing the same quote twice under a document that says
     * "1 use". One row, and it says why one row covers two readings. */
    it("names both readings on a row standing for either", async () => {
        respondWith(
            withGroups(1, [
                {
                    lemmaIds: ["jaagh.n", "jaagh.v"],
                    lemma: "jaagh",
                    classes: ["n", "v"],
                    count: 1,
                },
            ]),
        )
        renderWalker()

        expect(await screen.findByText("jaagh")).toBeTruthy()
        expect(screen.getByText("n. or v.")).toBeTruthy()
        // the quote once, not once per reading
        expect(screen.getAllByText("Daase")).toHaveLength(1)
    })

    it("tells two rows of one headword apart by their class", async () => {
        // the resolver read one line as the verb and one as the noun: separate
        // facts, but 'aase' alone tells neither row from the other
        respondWith(
            withGroups(2, [
                {
                    lemmaIds: ["aase.v"],
                    lemma: "aase",
                    classes: ["v"],
                    count: 2,
                },
                {
                    lemmaIds: ["aase.n"],
                    lemma: "aase",
                    classes: ["n"],
                    count: 1,
                },
            ]),
        )
        renderWalker()

        expect(await screen.findByText("v.")).toBeTruthy()
        expect(screen.getByText("n.")).toBeTruthy()
    })

    it("leaves a lone reading unnamed: the headword is its whole name", async () => {
        respond()
        renderWalker()
        await screen.findByText("Daase")

        expect(document.querySelector(".attest-group-lemma")?.textContent).toBe(
            "aase",
        )
        expect(screen.queryByText("v.")).toBeNull()
    })

    /** The lemma table knows no lexeme for 'angaish', so the walk scans the
     * spelling instead: the row stands for no reading, and must not print a class
     * nothing has claimed. */
    it("names no class on a row that is a spelling rather than a reading", async () => {
        respondWith(
            withGroups(2, [
                {
                    lemmaIds: [],
                    lemma: "angaish",
                    classes: [],
                    count: 2,
                },
            ]),
        )
        renderWalker()

        expect(await screen.findByText("angaish")).toBeTruthy()
        expect(screen.getByText(/×2/)).toBeTruthy()
    })

    it("names no class where a reading's has no abbreviation to print", async () => {
        // 'x' is every class that is not noun, verb or adjective: printing only
        // the rest would read as the row's whole story
        respondWith(
            withGroups(2, [
                {
                    lemmaIds: ["mee.x", "mee.n"],
                    lemma: "mee",
                    classes: ["x", "n"],
                    count: 2,
                },
            ]),
        )
        renderWalker()

        expect(await screen.findByText("mee")).toBeTruthy()
        expect(screen.queryByText("n.")).toBeNull()
    })

    it("always shows a tab naming the walked reading", async () => {
        respond()
        renderWalker()
        await screen.findByText("Daase")

        const tab = document.querySelector(".attest-tab-active")
        expect(tab?.textContent).toBe("aase")
        // one reading: nothing to switch to, so the tab is a caption, not a link
        expect(tab?.tagName).not.toBe("A")
    })

    it("offers the reading's form tree beside the tabs", async () => {
        respond()
        renderWalker()
        await screen.findByText("Daase")

        expect(
            screen
                .getByRole("link", { name: "All forms ›" })
                .getAttribute("href"),
        ).toBe("/dictionary/lemma/aase")
    })

    it("offers no tree for a spelling walk: there is no lemma to draw", async () => {
        fetchMock.mockImplementation((url) =>
            Promise.resolve({
                ok: true,
                json: () =>
                    Promise.resolve(
                        hrefOf(url).includes("/attestations/")
                            ? { ...lines, lemma: null }
                            : {
                                  ...walk,
                                  word: "angaish",
                                  lemmas: [],
                                  lemma: null,
                              },
                    ),
            } as Response),
        )
        renderWalker("angaish")
        await screen.findByText(/2 texts/)

        expect(screen.queryByRole("link", { name: /All forms/ })).toBeNull()
    })

    it("fetches a step's uses under the tab's reading", async () => {
        respond()
        renderWalker()
        await screen.findByText("Daase")

        const stepCall = fetchMock.mock.calls
            .map(([u]) => hrefOf(u))
            .find((u) => u.includes("/attestations/Psalms1610"))
        expect(stepCall).toContain("lemma=aase")
    })

    it("links a transcribed line to its moment in the recording", async () => {
        respondWith(
            withGroups(9, [
                {
                    lines: [
                        {
                            ...lines.groups[0].lines[0],
                            subStart: 12.9,
                        },
                    ],
                },
            ]),
        )
        renderWalker()
        await screen.findByText("Daase")

        // the whole line is the control — Manx, timestamp and all — and it
        // opens the listening popup at its own moment: the page, and the
        // walk, stay where the reader left them
        const line = screen.getByTitle("Hear this line")
        expect(line.textContent).toBe("Daase yn billey▶ 0:12")
        fireEvent.click(line)

        expect(
            (
                await screen.findByRole("link", { name: "Full document ›" })
            ).getAttribute("href"),
        ).toBe("/docs/Psalms1610?line=2")
    })

    it("offers no play link where the text is not a recording", async () => {
        respond()
        renderWalker()
        await screen.findByText("Daase")

        expect(screen.queryByRole("button", { name: /▶/ })).toBeNull()
    })

    /** Skeealyn Vannin Track 12's transcript wrote no clock down: its lines
     * still open the popup, which plays the recording from the start */
    it("a recording's untimed line still opens the popup", async () => {
        respondWith({ ...lines, title: "🎥 Skeealyn Vannin" })
        renderWalker()
        await screen.findByText("Daase")

        const line = screen.getByTitle("Hear this line")
        expect(line.textContent).toBe("Daase yn billey▶ ??:??")
        fireEvent.click(line)

        expect(
            (
                await screen.findByRole("link", { name: "Full document ›" })
            ).getAttribute("href"),
        ).toBe("/docs/Psalms1610?line=2")
    })

    it("offers an audio tab when recordings use the word", async () => {
        respondWithRecordings()
        renderWalker()
        await screen.findByText("Daase")

        expect(
            screen
                .getByRole("link", { name: "🔊 audio ×2" })
                .getAttribute("href"),
        ).toBe("/dictionary/aase?audio=1")
    })

    it("walks only the recordings under the audio tab", async () => {
        respondWithRecordings()
        renderWalker("aase", "?audio=1")
        await screen.findByText(/2 recordings, 1948–1975/)

        // the earliest recording leads, and the stepper cycles recordings:
        // the next step is the 1975 tape, never the 1707 text
        expect(screen.getByRole("link", { name: "🎥 Skeealyn Vannin" }))
        expect(
            screen.getByRole("link", { name: "1975 ›" }).getAttribute("href"),
        ).toBe("/dictionary/aase?audio=1&at=YouTube-NM")
        expect(screen.queryByRole("link", { name: /1707/ })).toBeNull()
    })

    it("makes the reading a way back out of the audio tab", async () => {
        respondWithRecordings()
        renderWalker("aase", "?audio=1")
        await screen.findByText(/2 recordings/)

        // the audio tab is the caption now, and the reading is a link again
        expect(document.querySelector(".attest-tab-active")?.textContent).toBe(
            "🔊 audio ×2",
        )
        expect(
            screen.getByRole("link", { name: "aase" }).getAttribute("href"),
        ).toBe("/dictionary/aase?reading=aase")
    })

    it("offers no audio tab when no recording uses the word", async () => {
        respond()
        renderWalker()
        await screen.findByText("Daase")

        expect(screen.queryByRole("link", { name: /audio/ })).toBeNull()
    })

    it("walks an ambiguous word one reading at a time, first tab first", async () => {
        fetchMock.mockImplementation((url) => {
            const href = hrefOf(url)
            const body = href.includes("/attestations/")
                ? { ...lines, lemma: "bee" }
                : {
                      ...walk,
                      word: "vee",
                      lemmas: ["bee", "mee"],
                      // the unfiltered walk of an ambiguous word names no one
                      // reading; the filtered one names what it walked
                      lemma: href.includes("lemma=") ? "bee" : null,
                  }
            return Promise.resolve({
                ok: true,
                json: () => Promise.resolve(body),
            } as Response)
        })
        renderWalker("vee")
        await screen.findByText(/2 texts/)

        // asked once to learn the readings, then again for the first of them
        const walkCalls = fetchMock.mock.calls
            .map(([u]) => hrefOf(u))
            .filter((u) => u.includes("/attestations?"))
        expect(walkCalls[1]).toContain("lemma=bee")
        expect(document.querySelector(".attest-tab-active")?.textContent).toBe(
            "bee",
        )

        // the tree link follows the open tab
        expect(
            screen
                .getByRole("link", { name: "All forms ›" })
                .getAttribute("href"),
        ).toBe("/dictionary/lemma/bee")

        // the other reading is a turn of the tab away, resetting the step
        const other = screen.getByRole("link", { name: "mee" })
        expect(other.getAttribute("href")).toBe("/dictionary/vee?reading=mee")
        fireEvent.click(other)
        await waitFor(() =>
            expect(
                fetchMock.mock.calls
                    .map(([u]) => hrefOf(u))
                    .some((u) => u.includes("lemma=mee")),
            ).toBe(true),
        )
    })
})
