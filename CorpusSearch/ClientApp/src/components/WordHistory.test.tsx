import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { cleanup, render, screen } from "@testing-library/react"
import { MemoryRouter } from "react-router-dom"
import { WordHistory } from "./WordHistory"
import { DictionaryHistoryResponse } from "../api/DictionaryApi"

const fetchMock = vi.fn<typeof fetch>()
vi.stubGlobal("fetch", fetchMock)

beforeEach(() => fetchMock.mockReset())
afterEach(cleanup)

const history: DictionaryHistoryResponse = {
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

describe("WordHistory", () => {
    it("renders the attestation, era split and forms cluster", async () => {
        fetchMock.mockResolvedValue({
            ok: true,
            json: () => Promise.resolve(history),
        } as Response)
        render(
            <MemoryRouter>
                <WordHistory word="billey" />
            </MemoryRouter>,
        )

        expect(await screen.findByText(/First attested/)).toBeTruthy()
        expect(screen.getByText("1748")).toBeTruthy()
        expect(screen.getByText("Mian 1748")).toBeTruthy()
        expect(screen.getByText(/experimental/)).toBeTruthy()
        expect(screen.getByText(/unreviewed\. Expect/)).toBeTruthy()
        expect(screen.queryByText(/Traditional/)).toBeNull()
        // the shared mutation is starred
        expect(screen.getByText("villey*")).toBeTruthy()
        expect(screen.getByText(/Ir\. bile/)).toBeTruthy()
    })

    it("renders nothing when the corpus has no attestations", async () => {
        fetchMock.mockResolvedValue({
            ok: true,
            json: () => Promise.resolve({ ...history, forms: [] }),
        } as Response)
        const { container } = render(
            <MemoryRouter>
                <WordHistory word="xyzzy" />
            </MemoryRouter>,
        )

        await vi.waitFor(() => expect(fetchMock).toHaveBeenCalled())
        expect(container.querySelector(".word-history")).toBeNull()
    })
})
