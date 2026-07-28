import { Link } from "react-router-dom"
import type { WordListCitation } from "../api/DictionaryApi"
import "./WordListCitations.css"

/** The printed word lists that define the word.
 *
 * "Defined in", because for a word like Ollyssyn the naming is the only thing
 * there is: a list that says what a plant is called is what defines it, and a
 * heading hedging that would be the page doubting its own answer.
 *
 * It keeps its own section, and the lists get no tab in the scope picker,
 * because a list is not a book: it gives no word class and no root, so there is
 * nothing to scope to and nothing for the lemma table to take. That is a fact
 * about the source, not a doubt about the definition.
 *
 * Set out like the entries above it: the head in bold, then what the page calls
 * the plant, then who says so. A reader should not have to learn a second shape
 * to read the last line on the page. The comma between the English and the
 * Latin is the page's own; it prints them as one phrase. */
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
            <h3 className="dict-page-dictionary">Defined in</h3>
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
