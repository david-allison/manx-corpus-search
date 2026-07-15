import { DictionaryHistoryResponse, HistoryForm } from "../api/DictionaryApi"

/** An earliest-attestation claim the page is willing to make.
 *
 * `uncertain` marks a claim resting on a spelling another lexeme also uses
 * ('vee' is both bee and mee): the occurrence may not be this word at all, so
 * the year is shown marked rather than asserted. */
export type AttestationClaim = {
    form: HistoryForm
    year: number
    uncertain: boolean
}

const dated = (forms: HistoryForm[]): HistoryForm[] =>
    forms.filter((f) => f.earliestYear != null)

const earliestOf = (forms: HistoryForm[]): HistoryForm | null =>
    dated(forms).reduce<HistoryForm | null>(
        (best, f) =>
            best == null || f.earliestYear! < best.earliestYear! ? f : best,
        null,
    )

const claimOf = (form: HistoryForm | null): AttestationClaim | null =>
    form == null
        ? null
        : {
              form,
              year: form.earliestYear!,
              uncertain: form.sharedWithOtherLemmas,
          }

/** Compares a looked-up word to a scanned form: the server normalizes forms,
 * so only case and edge punctuation can differ */
const sameSpelling = (a: string, b: string): boolean =>
    a.trim().toLowerCase() === b.trim().toLowerCase()

/** Whether the corpus dates the word at all: what both the first-seen band and
 * the walk above it need before either has anything to say */
export const hasAttestation = (
    history: DictionaryHistoryResponse | null,
): boolean => history != null && dated(history.forms).length > 0

/** When the exact spelling that was looked up was first seen in the corpus.
 * Null when the corpus never attests it (the lexeme may still be attested in
 * other spellings). */
export const earliestForWord = (
    history: DictionaryHistoryResponse,
): AttestationClaim | null =>
    claimOf(
        dated(history.forms).find((f) => sameSpelling(f.form, history.word)) ??
            null,
    )

/** When the lexeme was first seen in the corpus, in any spelling.
 *
 * `claim` prefers an unambiguous spelling over an earlier one — the shipped
 * rule (DictionaryHistoryService.Earliest): a shared form may be another word
 * entirely, so it cannot carry the headline on its own. `earlierShared` is the
 * genuinely-earliest reading when a shared spelling beats the claim: the
 * interesting fact, offered as a marked possibility rather than a claim. When
 * no unambiguous spelling is attested at all, `claim` falls back to the
 * earliest shared one and is marked `uncertain`.
 */
export const earliestForLemma = (
    history: DictionaryHistoryResponse,
): {
    claim: AttestationClaim
    earlierShared: AttestationClaim | null
} | null => {
    const unambiguous = earliestOf(
        history.forms.filter((f) => !f.sharedWithOtherLemmas),
    )
    const overall = earliestOf(history.forms)
    const claim = claimOf(unambiguous ?? overall)
    if (claim == null || overall == null) {
        return null
    }
    const earlierShared =
        !claim.uncertain && overall.earliestYear! < claim.year
            ? claimOf(overall)
            : null
    return { claim, earlierShared }
}
