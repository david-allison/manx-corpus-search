import { describe, expect, it } from "vitest"
import { resolveSpeaker } from "./Speakers"

// real author fields from the audio documents' manifests
const skeealyn =
    "Annie Kneale, Ballagarrett, Bride, J.W. (Bill) Radcliffe, Mark Braide, Kevin Danaher."
const boyde = "Harry Boyde, Ballaugh and Tom (Thobm) Braide"
const uosh =
    "John Kneen, John William ('Bill') Radcliffe, Walter Clarke and Doug Faragher"

describe("resolveSpeaker", () => {
    it("finds the name whose capitals spell the code", () => {
        expect(resolveSpeaker("AK", skeealyn)).toBe("Annie Kneale")
        expect(resolveSpeaker("MB", skeealyn)).toBe("Mark Braide")
        expect(resolveSpeaker("WC", uosh)).toBe("Walter Clarke")
    })

    it("steps past nicknames and initials inside a name", () => {
        // J.W. (Bill) Radcliffe answers as JWR and as WR alike
        expect(resolveSpeaker("JWR", skeealyn)).toBe("J.W. (Bill) Radcliffe")
        expect(resolveSpeaker("WR", uosh)).toBe(
            "John William ('Bill') Radcliffe",
        )
    })

    it("steps past an address to the name", () => {
        // "Ballaugh" is where Harry Boyde lives, not who TB is
        expect(resolveSpeaker("TB", boyde)).toBe("Tom (Thobm) Braide")
    })

    it("keeps the code when several names could answer", () => {
        // John Kneen and John William Radcliffe are both a J
        expect(resolveSpeaker("J", uosh)).toBe("J")
    })

    it("keeps the code when no name answers", () => {
        expect(resolveSpeaker("RT", "Leslie Quirk")).toBe("RT")
        expect(resolveSpeaker("??", skeealyn)).toBe("??")
    })

    it("reads a code's punctuation as transcription noise", () => {
        expect(resolveSpeaker("NM.", "Ned Maddrell, Glen Chass, Rushen")).toBe(
            "Ned Maddrell",
        )
    })

    it("keeps the code without an author to ask", () => {
        expect(resolveSpeaker("AK", undefined)).toBe("AK")
        expect(resolveSpeaker("AK", "")).toBe("AK")
    })

    it("sheds the author list's trailing period", () => {
        expect(resolveSpeaker("KD", skeealyn)).toBe("Kevin Danaher")
    })
})
