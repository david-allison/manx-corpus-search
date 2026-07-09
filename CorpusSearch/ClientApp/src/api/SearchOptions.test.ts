import { describe, expect, it } from "vitest"
import {
    defaultSearchOptions,
    parseSearchOptions,
    searchOptionsLinkQuery,
    searchOptionsQuery,
} from "./SearchOptions"

describe("searchOptionsQuery", () => {
    it("emits every option explicitly, in canonical order", () => {
        expect(searchOptionsQuery(defaultSearchOptions)).toBe(
            "&ignoreHyphens=false&caseSensitive=false&accentSensitive=false",
        )
        expect(
            searchOptionsQuery({
                ignoreHyphens: true,
                caseSensitive: false,
                accentSensitive: true,
            }),
        ).toBe("&ignoreHyphens=true&caseSensitive=false&accentSensitive=true")
    })
})

describe("searchOptionsLinkQuery", () => {
    it("emits only enabled options", () => {
        expect(searchOptionsLinkQuery(defaultSearchOptions)).toBe("")
        expect(
            searchOptionsLinkQuery({
                ignoreHyphens: false,
                caseSensitive: true,
                accentSensitive: true,
            }),
        ).toBe("&caseSensitive=true&accentSensitive=true")
    })
})

describe("parseSearchOptions", () => {
    it("defaults everything when the query string is empty", () => {
        expect(parseSearchOptions("")).toEqual(defaultSearchOptions)
    })

    it("treats an absent param as false", () => {
        expect(parseSearchOptions("?q=moddey&caseSensitive=true")).toEqual({
            ignoreHyphens: false,
            caseSensitive: true,
            accentSensitive: false,
        })
    })

    it("round-trips a link query", () => {
        const options = {
            ignoreHyphens: true,
            caseSensitive: false,
            accentSensitive: true,
        }
        expect(
            parseSearchOptions(`?q=moddey${searchOptionsLinkQuery(options)}`),
        ).toEqual(options)
    })
})
