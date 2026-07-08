import { expect, it } from "vitest"
import { iMuseumUrl } from "./IMuseum"

it("links a manuscript reference to a catalogue search", () => {
    expect(iMuseumUrl("MS 14725")).toBe(
        "https://imuseum.im/search//all/search?tab=all&view=&subm=s&term=%22MS+14725%22",
    )
})

it("uses a record URL as-is", () => {
    const url =
        "https://imuseum.im/search/archive_record/view/40?id=mnh-museum-675916"
    expect(iMuseumUrl(url)).toBe(url)
})
