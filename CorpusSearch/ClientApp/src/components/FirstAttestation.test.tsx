import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { cleanup, fireEvent, render, screen } from "@testing-library/react"
import { MemoryRouter } from "react-router-dom"
import { FirstAttestation } from "./FirstAttestation"
import {
    DictionaryHistoryResponse,
    EarliestAttestation,
} from "../api/DictionaryApi"

const fetchMock = vi.fn<typeof fetch>()
vi.stubGlobal("fetch", fetchMock)

beforeEach(() => fetchMock.mockReset())
afterEach(cleanup)

const billeyHistory: DictionaryHistoryResponse = {
    word: "billey",
    lemmas: ["billey"],
    revivalBoundaryYear: 1900,
    truncatedForms: 0,
    forms: [
        {
            form: "billey",
            total: 159,
            documents: 40,
            sharedWithOtherLemmas: false,
            earliestYear: 1748,
            earliestIdent: "MatthewGospel1748",
            earliestTitle: "Mian 1748",
            sample: "yn billey mooar",
        },
        {
            form: "villey",
            total: 31,
            documents: 12,
            sharedWithOtherLemmas: true,
            earliestYear: 1763,
        },
    ],
    earliest: {
        form: "billey",
        total: 159,
        documents: 40,
        sharedWithOtherLemmas: false,
        earliestYear: 1748,
        earliestIdent: "MatthewGospel1748",
        earliestTitle: "Mian 1748",
    },
    decades: [
        { decade: 1740, count: 12 },
        { decade: 1900, count: 3 },
    ],
    traditionalCount: 150,
    revivedCount: 40,
    undatedCount: 0,
    dictionaries: [
        { name: "Cregeen", era: "traditional (1835)" },
        { name: "LearnManx Spoken Dictionary", era: "revived" },
    ],
    cognates: ["Ir. bile"],
}

const renderBand = (
    history: DictionaryHistoryResponse | null,
    classes: string[] = [],
    sureClaim?: EarliestAttestation,
) =>
    render(
        <MemoryRouter>
            <FirstAttestation
                history={history}
                classes={classes}
                sureClaim={sureClaim}
            />
        </MemoryRouter>,
    )

describe("FirstAttestation", () => {
    it("collapses to one fact when the looked-up spelling is the earliest", () => {
        // 'billey' 1748 is both the spelling and the lexeme's earliest: saying
        // it twice would imply two findings, and labelling the survivor "this
        // spelling" would imply another spelling says otherwise
        renderBand(billeyHistory)

        expect(screen.getByText("1748")).toBeTruthy()
        expect(screen.getByText("Mian 1748")).toBeTruthy()
        expect(screen.queryByText("This spelling")).toBeNull()
        expect(screen.queryByText("Any form")).toBeNull()
    })

    it("collapses when the lexeme's earliest spelling is the word itself", () => {
        // real case: 'ass' is shared with another lexeme, so both the spelling
        // and the lexeme headline the same starred 1610 in the same text —
        // "This spelling 1610* / Any form 1610 as ass*" is one fact, twice
        renderBand({
            ...billeyHistory,
            word: "ass",
            forms: [
                {
                    form: "ass",
                    total: 40,
                    documents: 9,
                    sharedWithOtherLemmas: true,
                    earliestYear: 1610,
                    earliestIdent: "Psalms1610",
                    earliestTitle: "Psalms",
                },
            ],
        })

        expect(screen.queryByText("This spelling")).toBeNull()
        expect(screen.queryByText("Any form")).toBeNull()
        expect(screen.getAllByText("Psalms")).toHaveLength(1)
        // the shared-spelling doubt still has to be carried
        expect(
            screen.getAllByTitle(/shared with another word/).length,
        ).toBeGreaterThan(0)
    })

    it("collapses when another spelling merely ties the same year", () => {
        // real case: 'billey' is first seen 1610, and so is the cluster's
        // 'bilchyn'. An "Any form" row would restate one fact as two.
        renderBand({
            ...billeyHistory,
            word: "billey",
            forms: [
                {
                    ...billeyHistory.forms[0],
                    form: "billey",
                    earliestYear: 1610,
                },
                {
                    form: "bilchyn",
                    total: 3,
                    documents: 2,
                    sharedWithOtherLemmas: false,
                    earliestYear: 1610,
                    earliestIdent: "Phillips",
                    earliestTitle: "Phillips 1610",
                },
            ],
        })

        expect(screen.queryByText("This spelling")).toBeNull()
        expect(screen.queryByText("Any form")).toBeNull()
    })

    it("keeps the lexeme row when the spelling's own date is ambiguous", () => {
        // 'vee' looks 1610 but is shared: the lexeme's secure 1748 still counts
        renderBand({
            ...billeyHistory,
            word: "vee",
            forms: [
                {
                    form: "vee",
                    total: 9,
                    documents: 4,
                    sharedWithOtherLemmas: true,
                    earliestYear: 1610,
                    earliestIdent: "Phillips",
                    earliestTitle: "Phillips 1610",
                },
                { ...billeyHistory.forms[0], form: "bee", earliestYear: 1748 },
            ],
        })

        expect(screen.getByText("This spelling")).toBeTruthy()
        expect(screen.getByText("Any form")).toBeTruthy()
        expect(screen.getAllByText("1610").length).toBeGreaterThan(0)
        expect(screen.getByText("1748")).toBeTruthy()
        // the earlier reading IS this spelling: restating it would be noise
        expect(screen.queryByText(/possibly/)).toBeNull()
    })

    it("separates the spelling from the lexeme when they differ", () => {
        renderBand({
            ...billeyHistory,
            word: "biljyn",
            forms: [
                ...billeyHistory.forms,
                {
                    form: "biljyn",
                    total: 4,
                    documents: 2,
                    sharedWithOtherLemmas: false,
                    earliestYear: 1819,
                    earliestIdent: "Doc1819",
                    earliestTitle: "A Later Text",
                },
            ],
        })

        expect(screen.getByText("This spelling")).toBeTruthy()
        expect(screen.getByText("Any form")).toBeTruthy()
        expect(screen.getByText("1819")).toBeTruthy()
        expect(screen.getByText("1748")).toBeTruthy()
    })

    it("says so when the corpus has the lexeme but not this spelling", () => {
        renderBand({
            ...billeyHistory,
            word: "biljyn",
            forms: [
                ...billeyHistory.forms,
                {
                    form: "biljyn",
                    total: 0,
                    documents: 0,
                    sharedWithOtherLemmas: false,
                    earliestYear: null,
                },
            ],
        })

        expect(screen.getByText(/not attested/)).toBeTruthy()
        expect(screen.getByText("Any form")).toBeTruthy()
    })

    it("offers an earlier shared spelling as a possibility, not a claim", () => {
        // 'billey' 1748 is the earliest unambiguous spelling, so the rows
        // collapse — but 'villey' 1610 must not be collapsed away with them
        renderBand({
            ...billeyHistory,
            word: "billey",
            forms: [
                billeyHistory.forms[0], // billey 1748, unambiguous
                {
                    form: "villey",
                    total: 31,
                    documents: 12,
                    sharedWithOtherLemmas: true,
                    earliestYear: 1610,
                },
            ],
        })

        // the claim stays on the unambiguous spelling...
        expect(screen.getByText("1748")).toBeTruthy()
        // ...and the earlier, ambiguous reading survives as "Possibly"
        expect(screen.getByText(/Possibly/)).toBeTruthy()
        expect(screen.getByText("1610")).toBeTruthy()
        expect(screen.getByText("villey")).toBeTruthy()
        expect(
            screen.getAllByTitle(/shared with another word/).length,
        ).toBeGreaterThan(0)
    })

    it("warns when the scan was bounded and an earlier spelling may exist", () => {
        renderBand({ ...billeyHistory, truncatedForms: 6 })

        expect(
            screen.getByText(/6 more spellings were not scanned/),
        ).toBeTruthy()
    })

    it("quotes a few words around the word, not the head of the verse", () => {
        renderBand({
            ...billeyHistory,
            word: "billey",
            forms: [
                {
                    ...billeyHistory.forms[0],
                    sample: "one two three four five billey six seven eight nine",
                    sampleHighlights: [{ start: 24, end: 30 }],
                },
            ],
        })

        // three words each side, and ellipses where the line runs on
        const quote = screen.getByTitle("Read the whole line")
        expect(quote.textContent).toContain(
            "three four five billey six seven eight",
        )
        expect(quote.textContent).toContain("…")
        expect(quote.textContent).not.toContain("one two")
    })

    it("opens the whole line in a dialog, in both languages", async () => {
        fetchMock.mockResolvedValue({
            ok: true,
            json: () =>
                Promise.resolve({
                    results: [
                        {
                            manx: "yn billey mooar ayns y gharee",
                            english: "the big tree in the garden",
                            manxHighlights: [{ start: 3, end: 9 }],
                            csvLineNumber: 2,
                        },
                    ],
                }),
        } as Response)
        renderBand({
            ...billeyHistory,
            forms: [
                {
                    ...billeyHistory.forms[0],
                    sample: "yn billey mooar ayns y gharee",
                    sampleHighlights: [{ start: 3, end: 9 }],
                },
            ],
        })

        fireEvent.click(screen.getByTitle("Read the whole line"))

        // the Manx whole, its translation, and a way into the text
        expect(await screen.findByText(/mooar ayns y gharee/)).toBeTruthy()
        expect(screen.getByText("the big tree in the garden")).toBeTruthy()
        expect(
            screen.getByText(/Open in the corpus/).getAttribute("href"),
        ).toBe("/docs/MatthewGospel1748?q=billey")
    })

    it("warns when one spelling is carrying more than one sense", () => {
        // 'ass' is a weasel (noun) and 'out' (adverb/preposition): the corpus
        // indexes the spelling, so its 1610 belongs to whichever came first
        renderBand({ ...billeyHistory, word: "ass" }, [
            "Adverb",
            "Noun",
            "Preposition",
        ])

        const warning = screen.getByText(/covers more than one sense/)
        expect(warning.textContent).toContain("“ass”")
        expect(warning.textContent).toContain("adverb, noun, preposition")
        expect(warning.textContent).toMatch(/earliest of any of them/)
    })

    it("says nothing when the entries agree on one class", () => {
        renderBand(billeyHistory, ["Noun"])

        expect(screen.queryByText(/covers more than one sense/)).toBeNull()
    })

    it("says nothing when no entry declares a class", () => {
        // silence is "no evidence of a split", not "only one sense"
        renderBand(billeyHistory, [])

        expect(screen.queryByText(/covers more than one sense/)).toBeNull()
    })

    it("lets the walk's settled evidence take the assertion over", () => {
        // the history reads 1748 from spellings alone; the walk stands behind
        // a Villey of 1707, so 1707 is asserted — plainly, with no mark
        renderBand(billeyHistory, [], {
            year: 1707,
            ident: "Coyrle",
            title: "Coyrle Sodjey",
            form: "Villey",
        })

        // the spelling keeps its own row and date...
        expect(screen.getByText("This spelling")).toBeTruthy()
        expect(screen.getByText("1748")).toBeTruthy()
        // ...and the lexeme's claim is the walk's, year bold and form italic
        expect(screen.getByText("1707").className).toContain("first-seen-year")
        expect(screen.getByText("Villey").tagName).toBe("EM")
        expect(
            screen
                .getByRole("link", { name: "Coyrle Sodjey" })
                .getAttribute("href"),
        ).toBe("/docs/Coyrle?q=Villey")
        expect(screen.queryByTitle(/shared with another word/)).toBeNull()
    })

    it("ignores settled evidence the history's own claim precedes", () => {
        renderBand(billeyHistory, [], {
            year: 1800,
            ident: "Later",
            title: "A Later Text",
            form: "billey",
        })

        // the one 1748 fact, collapsed as before: a later settled year has
        // nothing to add
        expect(screen.getByText("1748")).toBeTruthy()
        expect(screen.queryByText("1800")).toBeNull()
        expect(screen.queryByText("Any form")).toBeNull()
    })

    it("carries the band alone when the history has no claim", () => {
        renderBand(null, [], {
            year: 1707,
            ident: "Coyrle",
            title: "Coyrle Sodjey",
            form: "villey",
        })

        expect(screen.getByText("First seen")).toBeTruthy()
        expect(screen.getByText("1707")).toBeTruthy()
        expect(
            screen
                .getByRole("link", { name: "Coyrle Sodjey" })
                .getAttribute("href"),
        ).toBe("/docs/Coyrle?q=villey")
    })

    it("keeps the earlier-shared offer while it still precedes the settled year", () => {
        renderBand(
            {
                ...billeyHistory,
                forms: [
                    billeyHistory.forms[0], // billey 1748, unambiguous
                    {
                        form: "villey",
                        total: 31,
                        documents: 12,
                        sharedWithOtherLemmas: true,
                        earliestYear: 1610,
                    },
                ],
            },
            [],
            { year: 1707, ident: "Coyrle", title: "Coyrle Sodjey" },
        )

        // 1707 is asserted, and the genuinely-earliest 1610 is still worth
        // offering behind it
        expect(screen.getByText("1707")).toBeTruthy()
        expect(screen.getByText(/Possibly/)).toBeTruthy()
        expect(screen.getByText("1610")).toBeTruthy()
    })

    it("drops the earlier-shared offer once the settled claim covers it", () => {
        renderBand(
            {
                ...billeyHistory,
                forms: [
                    billeyHistory.forms[0], // billey 1748, unambiguous
                    {
                        form: "villey",
                        total: 31,
                        documents: 12,
                        sharedWithOtherLemmas: true,
                        earliestYear: 1610,
                    },
                ],
            },
            [],
            { year: 1600, ident: "Early", title: "An Early Text" },
        )

        // asserting 1600 leaves a possible 1610 with nothing to say
        expect(screen.getByText("1600")).toBeTruthy()
        expect(screen.queryByText(/Possibly/)).toBeNull()
        expect(screen.queryByText("1610")).toBeNull()
    })

    it("renders nothing while loading or with no attestations", () => {
        const { container: loading } = renderBand(null)
        expect(loading.querySelector(".first-seen")).toBeNull()

        cleanup()
        const { container: empty } = renderBand({
            ...billeyHistory,
            forms: [],
        })
        expect(empty.querySelector(".first-seen")).toBeNull()
    })
})
