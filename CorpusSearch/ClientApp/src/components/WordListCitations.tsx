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
 * The English name is the page's own wording, typos and all; where the page is
 * wrong, its note reads the word back rather than quietly correcting the
 * quotation. */
export const WordListCitations = ({
    citations,
}: {
    citations: WordListCitation[]
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
                        <span className="dict-wordlist-headword">
                            {citation.headword}
                        </span>
                        <span className="dict-wordlist-gloss">
                            {citation.gloss}
                        </span>
                        {citation.binomial && (
                            <span className="dict-wordlist-binomial">
                                {citation.binomial}
                            </span>
                        )}
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
                        {citation.note && (
                            <span className="dict-wordlist-note">
                                {citation.note}
                            </span>
                        )}
                    </li>
                ))}
            </ul>
        </section>
    )
}
