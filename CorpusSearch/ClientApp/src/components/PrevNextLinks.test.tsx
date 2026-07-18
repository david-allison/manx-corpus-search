import { afterEach, describe, expect, it } from "vitest"
import { cleanup, fireEvent, render, screen } from "@testing-library/react"
import { MemoryRouter, useLocation, useNavigate } from "react-router-dom"
import { PrevNextLinks } from "./PrevNextLinks"

afterEach(cleanup)

const Harness = () => {
    const location = useLocation()
    const navigate = useNavigate()
    return (
        <div>
            <span data-testid="at">{location.pathname}</span>
            <button type="button" onClick={() => void navigate(-1)}>
                back
            </button>
            <PrevNextLinks
                ariaLabel="Headwords"
                previous={{ to: "/dictionary/caa", label: "caa" }}
                next={{
                    to: "/dictionary/faar-y-chaagh",
                    label: "faar-y-chaagh",
                }}
            >
                caag
            </PrevNextLinks>
        </div>
    )
}

describe("PrevNextLinks", () => {
    /** A reader skims a lot of entries: each step replaces the history entry
     * rather than stacking one, so Back leaves the skim in one press — to
     * where they came from, not back through every word they passed */
    it("replaces history as it steps: Back leaves the skim in one press", () => {
        render(
            <MemoryRouter
                initialEntries={["/came-from", "/dictionary/caag"]}
                initialIndex={1}
            >
                <Harness />
            </MemoryRouter>,
        )

        fireEvent.click(screen.getByRole("link", { name: /faar-y-chaagh/ }))
        expect(screen.getByTestId("at").textContent).toBe(
            "/dictionary/faar-y-chaagh",
        )

        fireEvent.click(screen.getByRole("button", { name: "back" }))
        expect(screen.getByTestId("at").textContent).toBe("/came-from")
    })
})
