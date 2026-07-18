/** The manifest's own words for who is speaking.
 *
 * A transcript names its speakers by code ("AK"), and only the manifest's
 * author field knows them in full ("Annie Kneale, Ballagarrett, Bride, J.W.
 * (Bill) Radcliffe, Mark Braide"). The code's letters are matched, in order,
 * against the capitals of each listed segment — so "AK" finds Annie Kneale,
 * "JWR" and "WR" both find J.W. (Bill) Radcliffe, and "TB" steps past the
 * address "Ballaugh" to Tom (Thobm) Braide.
 *
 * The code stands unresolved unless exactly one segment answers: "J" is John
 * Kneen and J.W. Radcliffe alike, and a wrong name on a real person is worse
 * than initials.
 */
export const resolveSpeaker = (
    code: string,
    author?: string | null,
): string => {
    if (!author) {
        return code
    }
    // "NM." files under NM: a code's punctuation is transcription noise
    const wanted = code.replace(/[^a-z]/gi, "").toUpperCase()
    if (wanted.length === 0) {
        return code
    }
    const segments = author
        .split(/[,;]|\band\b|&/)
        .map((s) => s.trim())
        .filter(Boolean)
    const matches = segments.filter((segment) => {
        const capitals = [...segment].filter((c) => c >= "A" && c <= "Z")
        let at = 0
        for (const c of capitals) {
            if (at < wanted.length && c === wanted[at]) {
                at++
            }
        }
        return at === wanted.length
    })
    // trailing punctuation belongs to the author list, not the name
    return matches.length === 1 ? matches[0].replace(/[.]$/, "") : code
}
