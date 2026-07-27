import { Link } from "react-router-dom"
import type { WordListCitation } from "../api/DictionaryApi"
import "./WordListCitations.css"

/** The printed word lists that name the word.
 *
 * A citation, not an entry. A list prints a spelling against a thing and says
 * nothing about what class the word is or what it means beyond the naming, so
 * this reads as "a page lists this" and links the page — it is never dressed as
 * a dictionary's own reading, and the lists have no tab in the scope picker for
 * the same reason.
 *
 * Written as a sentence rather than laid out in columns: the English name, the
 * Latin name and the book are one statement ("Alexanders, Smyrnium olusatrum,
 * in Manx Plant Names"), and splitting them into fields leaves the reader
 * holding four fragments to reassemble. The comma between the English and the
 * Latin is the page's own — it prints them as one phrase.
 *
 * The English is the page's wording, typos and all; where the page is wrong its
 * note reads the word back rather than quietly correcting the quotation. */
export const WordListCitations = ({
    citations,
    word,
}: {
    citations: WordListCitation[]
    /** the word whose page this is: the printed head is only worth saying when
     * it differs, and on 'Ollyssyn' the reader can already see the word */
    word?: string
}) => {
    if (citations.length === 0) {
        return null
    }
    return (
        <section className="dict-wordlists">
            <h3 className="dict-page-dictionary">Listed in</h3>
            <ul className="dict-wordlist-items">
                {citations.map((citation, index) => (
                    <li className="dict-wordlist-item" key={index}>
                        <p className="dict-wordlist-line">
                            {/* the page sets a head the reader did not type
                            ("Yn luss" reached from "luss"): say so, or the
                            citation quietly answers a different word */}
                            {differs(citation.headword, word) && (
                                <>
                                    as{" "}
                                    <span className="dict-wordlist-headword">
                                        {citation.headword}
                                    </span>
                                    {": "}
                                </>
                            )}
                            <span className="dict-wordlist-gloss">
                                {citation.gloss}
                            </span>
                            {citation.binomial && (
                                <>
                                    {", "}
                                    <span className="dict-wordlist-binomial">
                                        {citation.binomial}
                                    </span>
                                </>
                            )}
                            {" — "}
                            <span className="dict-wordlist-source">
                                {citation.source.documentIdent ? (
                                    <Link
                                        to={`/docs/${citation.source.documentIdent}`}
                                        title={citation.source.citation}
                                    >
                                        {citation.source.name}
                                    </Link>
                                ) : (
                                    citation.source.name
                                )}
                                {citation.source.credit &&
                                    `, ${citation.source.credit}`}
                                {citation.source.date &&
                                    ` (${citation.source.date})`}
                            </span>
                        </p>
                        {citation.note && (
                            <p className="dict-wordlist-note">
                                {citation.note}
                            </p>
                        )}
                    </li>
                ))}
            </ul>
        </section>
    )
}

/** Whether the printed head says something the looked-up word does not. Folded
 * the way the table is keyed (case, hyphens and spaces), so "Lus-ny-Geayee"
 * reached from "lus ny geayee" is the same head, not a different one. */
const differs = (headword: string, word: string | undefined) => {
    if (!word) {
        return true
    }
    const fold = (s: string) =>
        s.toLowerCase().replace(/[-‑]/g, " ").replace(/\s+/g, " ").trim()
    return fold(headword) !== fold(word)
}
