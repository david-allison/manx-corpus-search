import { expect, it, vi } from "vitest"
import { render, screen } from "@testing-library/react"
import { ErrorBoundary } from "./ErrorBoundary"

const ThrowError = (): never => {
    throw new Error("boom")
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
