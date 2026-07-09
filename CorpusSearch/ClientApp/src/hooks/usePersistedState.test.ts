import { afterEach, describe, expect, it, vi } from "vitest"
import { act, renderHook } from "@testing-library/react"
import { usePersistedState } from "./usePersistedState"

const renderPersisted = () =>
    renderHook(() =>
        usePersistedState("pref", (stored) => stored !== "false", String),
    )

afterEach(() => {
    localStorage.clear()
    vi.restoreAllMocks()
})

describe("usePersistedState", () => {
    it("defaults via parse(null) when nothing is stored", () => {
        const { result } = renderPersisted()
        expect(result.current[0]).toBe(true)
    })

    it("reads the stored value", () => {
        localStorage.setItem("pref", "false")
        const { result } = renderPersisted()
        expect(result.current[0]).toBe(false)
    })

    it("stores the serialized value on set", () => {
        const { result } = renderPersisted()
        act(() => {
            result.current[1](false)
        })
        expect(result.current[0]).toBe(false)
        expect(localStorage.getItem("pref")).toBe("false")
    })

    it("still updates state when the storage write fails", () => {
        const { result } = renderPersisted()
        vi.spyOn(localStorage, "setItem").mockImplementation(() => {
            throw new Error("quota exceeded")
        })
        act(() => {
            result.current[1](false)
        })
        expect(result.current[0]).toBe(false)
    })
})
