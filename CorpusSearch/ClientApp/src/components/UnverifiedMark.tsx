import { Summary } from "../api/DictionaryApi"
import "./UnverifiedMark.css"

/** Marks a root the lemma table only reaches by rule: the dictionaries
 * document the entry, but nothing documents it as this word's root. Shown
 * wherever a root chain is (the popup and the word page), so a generated
 * guess never reads as documentation. */
export const UnverifiedMark = ({ summary }: { summary: Summary }) =>
    summary.unverifiedLink ? (
        <>
            {" "}
            <abbr
                className="dict-abbr dict-unverified"
                title="Unverified: no dictionary records this as a root of the word you looked up. It was worked out by rule and may be wrong"
            >
                unverified
            </abbr>
        </>
    ) : null
