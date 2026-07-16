import "./UnverifiedMark.css"

/** Marks a link the lemma table only reaches by rule or hand-assertion: the
 * dictionaries document the entry, but nothing documents the link. Shown
 * wherever a root chain or a form tree is (the popup, the word page, the lemma
 * page), so a generated guess never reads as documentation. */
export const UnverifiedMark = ({
    unverified,
    title,
}: {
    unverified?: boolean | null
    /** the claim being guessed at; the root chain's wording unless said */
    title?: string
}) =>
    unverified ? (
        <>
            {" "}
            <abbr
                className="dict-abbr dict-unverified"
                title={
                    title ??
                    "Unverified: no dictionary records this as a root of the word you looked up. It was worked out by rule and may be wrong"
                }
            >
                unverified
            </abbr>
        </>
    ) : null
