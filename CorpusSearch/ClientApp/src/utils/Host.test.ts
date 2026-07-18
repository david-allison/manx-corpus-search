import { describe, expect, it } from "vitest"
import { isDictionaryHost } from "./Host"

describe("isDictionaryHost", () => {
    it("knows the dictionary's own door", () => {
        expect(isDictionaryHost("dictionary.gaelg.im")).toBe(true)
        // *.localhost resolves to loopback in the browser: the dictionary
        // chrome can be tried on a dev machine without touching DNS
        expect(isDictionaryHost("dictionary.localhost")).toBe(true)
    })

    it("leaves the corpus's door alone", () => {
        expect(isDictionaryHost("corpus.gaelg.im")).toBe(false)
        expect(isDictionaryHost("localhost")).toBe(false)
    })
})
