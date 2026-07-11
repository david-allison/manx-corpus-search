import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { cleanup, render, renderHook } from "@testing-library/react"
import { useDocumentHeadElements } from "./useDocumentHeadElements"

const DEFAULT_DESCRIPTION =
    "Search for words & phrases within over 800 translated texts, from 1610 to the present era. Free & Open Source"

const description = () =>
    document.querySelector('meta[name="description"]')?.getAttribute("content")
const canonical = () =>
    document.querySelector('link[rel="canonical"]')?.getAttribute("href")

// the static index.html head that the hook mutates
beforeEach(() => {
    document.title = "Manx Corpus Search"
    const meta = document.createElement("meta")
    meta.setAttribute("name", "description")
    meta.setAttribute("content", DEFAULT_DESCRIPTION)
    document.head.appendChild(meta)
})

afterEach(() => {
    cleanup()
    document
        .querySelectorAll('meta[name="description"], link[rel="canonical"]')
        .forEach((x) => x.remove())
    vi.restoreAllMocks()
})

describe("useDocumentHeadElements", () => {
    it("sets a per-document title", () => {
        renderHook(() =>
            useDocumentHeadElements("Yn-Nollick", "Yn Nollick", "1899"),
        )
        expect(document.title).toBe("Yn Nollick | Manx Corpus Search")
    })

    it("keeps the default title until the document loads", () => {
        // " " is the loading placeholder (avoids a layout shift)
        renderHook(() => useDocumentHeadElements("Yn-Nollick", " ", ""))
        expect(document.title).toBe("Manx Corpus Search")
        expect(description()).toBe(DEFAULT_DESCRIPTION)
        expect(canonical()).toBeUndefined()
    })

    it("keeps the default title without a document ident", () => {
        renderHook(() => useDocumentHeadElements(undefined, "Yn Nollick", ""))
        expect(document.title).toBe("Manx Corpus Search")
    })

    it("describes the document, with its year", () => {
        renderHook(() =>
            useDocumentHeadElements("Yn-Nollick", "Yn Nollick", "1899"),
        )
        expect(description()).toBe(
            "“Yn Nollick” (1899) — Manx and English parallel text from the Manx corpus.",
        )
    })

    it("omits an unknown year from the description", () => {
        renderHook(() =>
            useDocumentHeadElements("Yn-Nollick", "Yn Nollick", ""),
        )
        expect(description()).toBe(
            "“Yn Nollick” — Manx and English parallel text from the Manx corpus.",
        )
    })

    it("links the canonical /docs URL, escaping the ident", () => {
        renderHook(() => useDocumentHeadElements("Carn 130", "Carn 130", ""))
        expect(canonical()).toBe(`${window.location.origin}/docs/Carn%20130`)
    })

    it("restores the head on unmount", () => {
        const { unmount } = renderHook(() =>
            useDocumentHeadElements("Yn-Nollick", "Yn Nollick", "1899"),
        )
        unmount()
        expect(document.title).toBe("Manx Corpus Search")
        expect(description()).toBe(DEFAULT_DESCRIPTION)
        expect(canonical()).toBeUndefined()
    })

    it("replaces the previous document's head when navigating", () => {
        const { rerender } = renderHook(
            ({ ident, title }) => useDocumentHeadElements(ident, title, ""),
            { initialProps: { ident: "Yn-Nollick", title: "Yn Nollick" } },
        )
        rerender({ ident: "A-Manx-Wedding", title: "A Manx Wedding" })
        expect(document.title).toBe("A Manx Wedding | Manx Corpus Search")
        expect(document.querySelectorAll('link[rel="canonical"]')).toHaveLength(
            1,
        )
        expect(canonical()).toBe(
            `${window.location.origin}/docs/A-Manx-Wedding`,
        )
    })
})

// React 19 hoisted <title> into <head>, but only applies it when children are a single string.
describe("React 19 <title> handling", () => {
    it("never applies a <title> with expression + text children", () => {
        const BrokenTitle = ({ name }: { name: string }) => (
            <title>{name} | Manx Corpus Search</title>
        )

        render(<BrokenTitle name="Yn Nollick" />)

        // React hoists the tag but silently refuses its children: blank title
        expect(document.title).toBe("")
    })

    it("keeps a stale title when a valid <title> re-renders into the broken form", () => {
        // the exact pre-2026-07 DocumentView structure: a valid generic
        // fallback while loading, the broken two-children form once loaded
        const Doc = ({ title }: { title: string | null }) =>
            title == null ? (
                <title>Manx Corpus Search</title>
            ) : (
                <title>{title} | Manx Corpus Search</title>
            )

        const { rerender } = render(<Doc title={null} />)
        expect(document.title).toBe("Manx Corpus Search")

        rerender(<Doc title="Yn Nollick" />)

        // the document "loads", yet the title stays generic — as seen on every
        // page of the live site (and in Google's crawled-page capture)
        expect(document.title).toBe("Manx Corpus Search")
        expect(document.title).not.toContain("Yn Nollick")
    })

    it("applies a <title> whose child is a single string", () => {
        const FixedTitle = ({ name }: { name: string }) => (
            <title>{`${name} | Manx Corpus Search`}</title>
        )

        render(<FixedTitle name="Yn Nollick" />)

        expect(document.title).toBe("Yn Nollick | Manx Corpus Search")
    })
})
