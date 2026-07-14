import { useEffect, useState } from "react"
import { Link } from "react-router-dom"
import {
    dictionaryHistory,
    DictionaryHistoryResponse,
} from "../api/DictionaryApi"
import "./WordHistory.css"

/** A small inline bar chart: corpus uses per decade */
const DecadeSparkline = ({
    decades,
}: {
    decades: { decade: number; count: number }[]
}) => {
    if (decades.length === 0) return null
    const first = decades[0].decade
    const last = decades[decades.length - 1].decade
    const byDecade = new Map(decades.map((d) => [d.decade, d.count]))
    const bars: { decade: number; count: number }[] = []
    for (let d = first; d <= last; d += 10) {
        bars.push({ decade: d, count: byDecade.get(d) ?? 0 })
    }
    const max = Math.max(...bars.map((b) => b.count))
    const w = 7
    return (
        <svg
            className="word-history-sparkline"
            width={bars.length * w + 2}
            height={44}
            role="img"
            aria-label={`Texts attesting the word per decade, ${first}s to ${last}s`}
        >
            {bars.map((b, i) => {
                const h = b.count === 0 ? 0 : Math.max(2, (b.count / max) * 30)
                return (
                    <rect
                        key={b.decade}
                        x={i * w + 1}
                        y={32 - h}
                        width={w - 2}
                        height={h}
                        className="word-history-bar"
                    >
                        <title>{`${b.decade}s: ${b.count} ${b.count === 1 ? "text" : "texts"}`}</title>
                    </rect>
                )
            })}
            <text x={0} y={43} className="word-history-axis">
                {first}s
            </text>
            <text
                x={bars.length * w}
                y={43}
                textAnchor="end"
                className="word-history-axis"
            >
                {last}s
            </text>
        </svg>
    )
}

/** The lexeme's corpus history: earliest attestation, spelling cluster, use
 * over time. Experimental: automatically derived and unreviewed, presented
 * as a curiosity only. */
export const WordHistory = ({ word }: { word: string }) => {
    const [history, setHistory] = useState<DictionaryHistoryResponse | null>(
        null,
    )

    useEffect(() => {
        setHistory(null)
        if (!word) return
        const abort = new AbortController()
        dictionaryHistory(word, abort.signal)
            .then(setHistory)
            .catch((e) => {
                if (!abort.signal.aborted) console.warn(e)
            })
        return () => abort.abort()
    }, [word])

    if (history == null || history.forms.length === 0) {
        return null
    }

    return (
        <section className="word-history">
            <h3 className="dict-page-dictionary">
                History
                <span className="word-history-experimental">
                    experimental &amp; incomplete
                </span>
            </h3>
            <p className="word-history-warning" role="note">
                <span className="word-history-warning-mark" aria-hidden="true">
                    !
                </span>
                Automatically derived from the corpus and unreviewed. Expect
                errors.
            </p>

            {history.earliest?.earliestYear && (
                <p className="word-history-earliest">
                    {"First attested "}
                    <strong>{history.earliest.earliestYear}</strong>
                    {" as "}
                    <strong>{history.earliest.form}</strong>
                    {" in "}
                    <Link
                        to={`/docs/${history.earliest.earliestIdent}?q=${encodeURIComponent(history.earliest.form)}`}
                    >
                        {history.earliest.earliestTitle}
                    </Link>
                    {history.earliest.sample && (
                        <span className="word-history-sample">
                            {" — “"}
                            {history.earliest.sample}
                            {"”"}
                        </span>
                    )}
                </p>
            )}

            <DecadeSparkline decades={history.decades} />

            <div className="word-history-forms">
                {history.forms.map((f) => (
                    <Link
                        className="word-history-form"
                        key={f.form}
                        to={`/?q=${encodeURIComponent(f.form)}`}
                        title={
                            f.sharedWithOtherLemmas
                                ? "this spelling is shared with another word: counts include both"
                                : undefined
                        }
                    >
                        {f.form}
                        {f.sharedWithOtherLemmas ? "*" : ""}
                        <span className="word-history-form-meta">
                            {f.earliestYear ? ` ${f.earliestYear}–` : ""}
                            {` ×${f.total.toLocaleString()}`}
                        </span>
                    </Link>
                ))}
                {history.truncatedForms > 0 && (
                    <span className="word-history-form-meta">
                        {` +${history.truncatedForms} more forms not scanned`}
                    </span>
                )}
            </div>

            {history.dictionaries.length > 0 && (
                <p className="word-history-dictionaries">
                    {"Documented in: "}
                    {history.dictionaries.map((d) => d.name).join(", ")}
                </p>
            )}

            {history.cognates.length > 0 && (
                <p className="word-history-cognates">
                    {"Cognates cited: "}
                    {history.cognates.join("; ")}
                </p>
            )}
        </section>
    )
}
