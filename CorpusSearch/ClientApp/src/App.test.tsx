import { it, vi } from "vitest"
import { render } from "@testing-library/react"
import { MemoryRouter } from "react-router-dom"
import App from "./App"

// App's components fetch on mount; stub fetch so the smoke test never hits the network.
// (Every caller has a .catch, so a rejection just routes them to their error/empty state.)
vi.stubGlobal(
    "fetch",
    vi.fn(() => Promise.reject(new Error("fetch is disabled in tests"))),
)

it("renders without crashing", () => {
    render(
        <MemoryRouter>
            <App />
        </MemoryRouter>,
    )
})
