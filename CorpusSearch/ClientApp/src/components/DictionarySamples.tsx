import { useEffect, useState } from "react"
import { Link } from "react-router-dom"
import { DictionarySample, dictionarySamples } from "../api/DictionaryApi"
import { dictionaryWordUrl } from "../utils/DictionaryEntries"
import "./DictionarySamples.css"

/** A handful of the book's words spanning the range of corpus use — a couple
 * common, some middling, one no text says — dealt fresh each visit: the
 * letter bar invites A-and-onward reading, and this invites opening the book
 * anywhere.
 *
 * Bare bold words in a row: the gloss belongs to the word's page, and the
 * list is a door, not an entry. The links are unscoped on purpose — a sample
 * is an invitation, and the unscoped word page answers with every book at
 * once. Callers keep it off any page where a letter is already open: a
 * reader who has chosen where to be is not looking for somewhere to start. */
export const DictionarySamples = ({ dict }: { dict: string }) => {
    const [samples, setSamples] = useState<DictionarySample[]>([])

    useEffect(() => {
        const abort = new AbortController()
        dictionarySamples(dict, 6, abort.signal)
            .then(setSamples)
            .catch((e) => {
                // no sampler is a quieter page, never a broken one
                if (!abort.signal.aborted) console.warn(e)
            })
        return () => abort.abort()
    }, [dict])

    if (samples.length === 0) {
        return null
    }

    return (
        <section className="dict-samples" aria-label="Entries to start from">
            <p className="dict-samples-lead">Or try these:</p>
            <ul className="dict-samples-list">
                {samples.map((sample) => (
                    <li key={sample.word}>
                        <Link
                            className={
                                sample.attested ? undefined : "dict-unattested"
                            }
                            title={
                                sample.attested
                                    ? undefined
                                    : `${sample.word} — in no text in the corpus: a dictionary word`
                            }
                            to={dictionaryWordUrl(sample.word)}
                        >
                            {sample.word}
                        </Link>
                    </li>
                ))}
            </ul>
        </section>
    )
}
