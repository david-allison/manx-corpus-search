import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { cleanup, fireEvent, render, screen } from "@testing-library/react"
import { MemoryRouter } from "react-router-dom"
import { Ref } from "react"
import { AudioAttestationModal } from "./AudioAttestationModal"
import { Player } from "./YouTuber"

const mockPlayer = vi.hoisted(() => ({
    seek: vi.fn(),
    getCurrentTime: vi.fn((): number | null => null),
}))

/** what the popup asked of the player: the moment, and to start speaking */
const mockYouTuberProps = vi.hoisted(
    (): { videoId?: string; startSeconds?: number; autoplay?: boolean } => ({}),
)

vi.mock("./YouTuber", async () => {
    const { useImperativeHandle } = await import("react")
    const MockYouTuber = ({
        videoId,
        startSeconds,
        autoplay,
        ref,
    }: {
        videoId: string
        startSeconds?: number
        autoplay?: boolean
        ref?: Ref<Player>
    }) => {
        useImperativeHandle(ref, () => mockPlayer)
        mockYouTuberProps.videoId = videoId
        mockYouTuberProps.startSeconds = startSeconds
        mockYouTuberProps.autoplay = autoplay
        return null
    }
    return { default: MockYouTuber }
})

const fetchMock = vi.fn<typeof fetch>()
vi.stubGlobal("fetch", fetchMock)

beforeEach(() => {
    fetchMock.mockReset()
    mockPlayer.seek.mockClear()
    mockYouTuberProps.videoId = undefined
    mockYouTuberProps.startSeconds = undefined
    mockYouTuberProps.autoplay = undefined
})
afterEach(cleanup)

const hrefOf = (url: unknown): string =>
    typeof url === "string"
        ? url
        : url instanceof URL
          ? url.href
          : ((url as Request | undefined)?.url ?? "")

const doc = { ident: "YouTube-SV", title: "🎥 Skeealyn Vannin", year: 1948 }

const spokenLine = {
    manx: "jean er yn greesagh",
    english: "done on the embers",
    manxHighlights: [{ start: 11, end: 19 }],
    csvLineNumber: 4,
    subStart: 12.09,
    speaker: "AK",
}

const linesResponse = {
    ident: doc.ident,
    title: doc.title,
    year: doc.year,
    lemma: "greesagh",
    useCount: 2,
    groups: [
        {
            lemmaIds: ["greesagh.n"],
            lemma: "greesagh",
            classes: ["n"],
            count: 2,
            lines: [spokenLine],
        },
        // a second reading claiming the very same line: one row, not two
        {
            lemmaIds: ["greesagh.v"],
            lemma: "greesagh",
            classes: ["v"],
            count: 2,
            lines: [spokenLine],
        },
    ],
}

const respond = (source = "https://www.youtube.com/watch?v=abc123") =>
    fetchMock.mockImplementation((url) =>
        Promise.resolve({
            ok: true,
            json: () =>
                Promise.resolve(
                    hrefOf(url).includes("/attestations/")
                        ? linesResponse
                        : {
                              source,
                              author: "Annie Kneale, Ballagarrett, Bride, J.W. (Bill) Radcliffe",
                          },
                ),
        } as Response),
    )

const renderModal = (at?: number, docs = [doc]) =>
    render(
        <MemoryRouter>
            <AudioAttestationModal
                word="greesagh"
                docs={docs}
                openAt={{ ident: doc.ident, at }}
                onClose={() => undefined}
            />
        </MemoryRouter>,
    )

describe("AudioAttestationModal", () => {
    it("plays the recording from the word's moment, unasked", async () => {
        respond()
        renderModal()
        await screen.findByText("▶ 0:12")

        expect(mockYouTuberProps.videoId).toBe("abc123")
        expect(mockYouTuberProps.startSeconds).toBe(12.09)
        // the one player on the site that autoplays: this click asked to hear
        expect(mockYouTuberProps.autoplay).toBe(true)
        // the one recording there is: nothing to leaf through, so no arrows
        expect(screen.queryByRole("button", { name: /recording/ })).toBeNull()
    })

    it("shows the line of dialog once, with its speaker", async () => {
        respond()
        renderModal()
        await screen.findByText("▶ 0:12")

        // the surface word is its own <mark> inside the line (the title
        // says the word too, so ask for the mark alone)
        expect(
            screen.getAllByText("greesagh").filter((x) => x.tagName === "MARK"),
        ).toHaveLength(1)
        // "AK" is Annie Kneale, by the manifest's own author list
        expect(screen.getByText("Annie Kneale")).toBeTruthy()
        expect(screen.getByText("done on the embers")).toBeTruthy()
        // two readings claim the same line: one row, not two
        expect(screen.getAllByTitle("Play from this line")).toHaveLength(1)
    })

    it("seeks the player when a line is tapped", async () => {
        respond()
        renderModal()
        const row = await screen.findByTitle("Play from this line")

        fireEvent.click(row)

        expect(mockPlayer.seek).toHaveBeenCalledWith(12.09)
    })

    it("links to the whole recording, at the line", async () => {
        respond()
        renderModal()
        await screen.findByText("▶ 0:12")

        expect(
            screen
                .getByRole("link", { name: "Full document ›" })
                .getAttribute("href"),
        ).toBe("/docs/YouTube-SV?line=4")
    })

    it("opens at the tapped line, not the first", async () => {
        const later = {
            ...spokenLine,
            csvLineNumber: 9,
            subStart: 44.2,
        }
        fetchMock.mockImplementation((url) =>
            Promise.resolve({
                ok: true,
                json: () =>
                    Promise.resolve(
                        hrefOf(url).includes("/attestations/")
                            ? {
                                  ...linesResponse,
                                  groups: [
                                      {
                                          ...linesResponse.groups[0],
                                          lines: [spokenLine, later],
                                      },
                                  ],
                              }
                            : {
                                  source: "https://www.youtube.com/watch?v=abc123",
                              },
                    ),
            } as Response),
        )
        renderModal(9)
        await screen.findByText("▶ 0:44")

        expect(mockYouTuberProps.startSeconds).toBe(44.2)
        expect(
            screen
                .getByRole("link", { name: "Full document ›" })
                .getAttribute("href"),
        ).toBe("/docs/YouTube-SV?line=9")
    })

    /** Skeealyn Vannin Track 12 says the word four times and wrote no clock
     * down: the popup must show the evidence, not claim the transcript lacks
     * the word */
    it("shows an untimed transcript's lines, and says why it cannot jump", async () => {
        const untimed = { ...spokenLine, subStart: undefined }
        fetchMock.mockImplementation((url) =>
            Promise.resolve({
                ok: true,
                json: () =>
                    Promise.resolve(
                        hrefOf(url).includes("/attestations/")
                            ? {
                                  ...linesResponse,
                                  groups: [
                                      {
                                          ...linesResponse.groups[0],
                                          lines: [untimed],
                                      },
                                  ],
                              }
                            : {
                                  source: "https://www.youtube.com/watch?v=abc123",
                              },
                    ),
            } as Response),
        )
        renderModal()
        await screen.findByText("done on the embers")

        // the line is evidence, not a seek control: there is no moment to
        // jump to, and ??:?? holds the clock's place to say so
        expect(screen.queryByTitle("Play from this line")).toBeNull()
        expect(screen.getByText("??:??")).toBeTruthy()
        // the recording still plays, from the start
        expect(mockYouTuberProps.videoId).toBe("abc123")
        expect(mockYouTuberProps.startSeconds).toBeUndefined()
        expect(
            screen
                .getByRole("link", { name: "Full document ›" })
                .getAttribute("href"),
        ).toBe("/docs/YouTube-SV?line=4")
    })

    it("keeps the lines when the source is not an embeddable video", async () => {
        respond("https://www.youtube.evil.com/watch?v=abc123")
        renderModal()
        await screen.findByText("▶ 0:12")

        expect(mockYouTuberProps.videoId).toBeUndefined()
    })

    it("leafs through the recordings with the edge arrows", async () => {
        respond()
        const later = {
            ident: "YouTube-NM",
            title: "🎥 Ned Maddrell",
            year: 1975,
        }
        renderModal(undefined, [doc, later])
        await screen.findByText("▶ 0:12")

        // the opened recording is the first of two: the way back stays put
        // but greyed — an edge, not an absence
        expect(screen.getByText("1 of 2")).toBeTruthy()
        expect(
            screen.getByRole("button", {
                name: "Previous recording",
            }),
        ).toHaveProperty("disabled", true)
        fireEvent.click(screen.getByRole("button", { name: /Next recording/ }))

        expect(await screen.findByText("🎥 Ned Maddrell")).toBeTruthy()
        expect(screen.getByText("2 of 2")).toBeTruthy()
        expect(
            screen.getByRole("button", { name: "Next recording" }),
        ).toHaveProperty("disabled", true)
        // the next recording's own lines were asked for
        expect(
            fetchMock.mock.calls
                .map(([u]) => hrefOf(u))
                .some((u) => u.includes("/attestations/YouTube-NM")),
        ).toBe(true)
        // and the way back is open
        expect(
            screen.getByRole("button", { name: /Previous recording:/ }),
        ).toHaveProperty("disabled", false)
    })
})
