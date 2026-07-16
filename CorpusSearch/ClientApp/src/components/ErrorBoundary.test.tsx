import { afterEach, expect, it, vi } from "vitest"
import { cleanup, render, screen } from "@testing-library/react"
import { useEffect } from "react"
import { ErrorBoundary } from "./ErrorBoundary"

// vitest globals are off, so testing-library does not clean up by itself: without
// this, one test's crash screen is still in the document during the next
afterEach(cleanup)

const ThrowError = (): never => {
    throw new Error("boom")
}

/** Reports each time it is mounted: what a `key` on the boundary would drive up
 * once per navigation */
const CountMounts = ({ onMount }: { onMount: () => void }) => {
    useEffect(onMount, [onMount])
    return <span>content</span>
}

it("renders children when nothing throws", () => {
    render(
        <ErrorBoundary>
            <span>content</span>
        </ErrorBoundary>,
    )
    expect(screen.getByText("content")).toBeDefined()
})

it("renders the fallback when a child throws", () => {
    // React logs the caught error; keep the test output clean
    const spy = vi.spyOn(console, "error").mockImplementation(() => {})
    try {
        render(
            <ErrorBoundary>
                <ThrowError />
            </ErrorBoundary>,
        )
    } finally {
        spy.mockRestore()
    }
    expect(screen.getByText("Something went wrong")).toBeDefined()
    expect(screen.getByRole("button", { name: "Reload" })).toBeDefined()
})

it("clears the crash when the page changes under it", () => {
    const spy = vi.spyOn(console, "error").mockImplementation(() => {})
    let rerender: ReturnType<typeof render>["rerender"]
    try {
        rerender = render(
            <ErrorBoundary resetOn="/crashes">
                <ThrowError />
            </ErrorBoundary>,
        ).rerender
    } finally {
        spy.mockRestore()
    }
    expect(screen.getByText("Something went wrong")).toBeDefined()

    rerender(
        <ErrorBoundary resetOn="/works">
            <span>content</span>
        </ErrorBoundary>,
    )

    // the crash belonged to the page we have just left
    expect(screen.getByText("content")).toBeDefined()
    expect(screen.queryByText("Something went wrong")).toBeNull()
})

/** Why the path is told rather than keyed on. A key clears the crash by
 * remounting everything beneath it, on every navigation and crash or no: each
 * page loses its state and refetches, and a walk meant to be clicked through
 * blinks out of the page and back between steps. */
it("does not remount the page beneath it when the path changes", () => {
    const onMount = vi.fn()
    const { rerender } = render(
        <ErrorBoundary resetOn="/dictionary/caag">
            <CountMounts onMount={onMount} />
        </ErrorBoundary>,
    )
    expect(onMount).toHaveBeenCalledTimes(1)

    rerender(
        <ErrorBoundary resetOn="/dictionary/caag-airh">
            <CountMounts onMount={onMount} />
        </ErrorBoundary>,
    )

    expect(onMount).toHaveBeenCalledTimes(1)
})
