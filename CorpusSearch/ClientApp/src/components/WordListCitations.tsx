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
 * Set out like the entries above it: the head in bold, then what the page calls
 * the plant, then who says so. A reader should not have to learn a second shape
 * to read the last line on the page. The comma between the English and the
 * Latin is the page's own — it prints them as one phrase. */
export const WordListCitations = ({
    citations,
    word,
}: {
    citations: WordListCitation[]
    /** the word whose page this is: unused for display (the head is always
     * shown, as on an entry) but kept for callers that pass it */
    word?: string
}) => {
    void word
    if (citations.length === 0) {
        return null
    }
    return (
        <section className="dict-wordlists">
            <h3 className="dict-page-dictionary">Listed in</h3>
            {citations.map((citation, index) => (
                <div className="dict-wordlist-item" key={index}>
                    <strong>{citation.headword}</strong>
                    {": "}
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
                        {citation.source.date && ` (${citation.source.date})`}
                    </span>
                    {citation.note && (
                        <p className="dict-wordlist-note">{citation.note}</p>
                    )}
                </div>
            ))}
        </section>
    )
}
