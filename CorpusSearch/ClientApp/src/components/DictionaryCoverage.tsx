import { useEffect, useState } from "react"
import { Link } from "react-router-dom"
import { DictionaryStats, dictionaryStats } from "../api/DictionaryApi"
import "./DictionaryCoverage.css"

const count = (n: number) => n.toLocaleString("en-GB")

const share = (part: number, whole: number) =>
    whole > 0 ? (100 * part) / whole : 0

const percent = (part: number, whole: number) =>
    share(part, whole).toFixed(1) + "%"

/** One coverage figure: the number as the card's title, the claim beneath it,
 * a small bar drawing the share, and the counts it is made of last - no
 * figure asks to be taken on trust */
const Card = ({
    number,
    label,
    value,
    detail,
    to,
}: {
    number: string
    label: string
    /** 0-100 fills the bar; null draws an empty track (an unread answer);
     * undefined draws no bar (a count is no share) */
    value?: number | null
    detail?: string
    /** where the share's own index lives: the label becomes the way in */
    to?: string
}) => (
    <div className="dict-coverage-stat">
        <span className="dict-coverage-number">{number}</span>
        {to ? (
            <Link className="dict-coverage-label" to={to}>
                {label} ›
            </Link>
        ) : (
            <span className="dict-coverage-label">{label}</span>
        )}
        {value !== undefined && (
            <span
                className="dict-coverage-track"
                role="progressbar"
                aria-label={label}
                aria-valuemin={0}
                aria-valuemax={100}
                aria-valuenow={value ?? undefined}
            >
                {value != null && (
                    <span
                        className="dict-coverage-fill"
                        style={{ width: `${value}%` }}
                    />
                )}
            </span>
        )}
        {detail && <span className="dict-coverage-detail">{detail}</span>}
    </div>
)

/** The coverage numbers under the dictionary host's front door: how far the
 * books, the recordings and the lemma table reach into the corpus. The
 * headline shares weigh the whole text, because that is the claim a front
 * page makes: not that a book is large, but that the word met in a text will
 * probably have an entry - the distinct-words pair rides beneath, where the
 * long tail of one-off spellings and names tells its own story. The audio
 * card counts distinct words instead: the recordings saying 'as' does not
 * make two million tokens hearable, and a token-weighted share dressed 23
 * recordings up as three-quarters of the corpus. It also waits for the
 * server's read of the recordings rather than claim a zero. */
export const DictionaryCoverage = () => {
    const [stats, setStats] = useState<DictionaryStats | null>(null)

    useEffect(() => {
        const abort = new AbortController()
        dictionaryStats(abort.signal)
            .then(setStats)
            .catch((e) => {
                if (!abort.signal.aborted) console.warn(e)
            })
        return () => abort.abort()
    }, [])

    if (!stats) {
        return null
    }
    const audioRead =
        stats.recordings != null &&
        stats.audioWords != null &&
        stats.audioRunningWords != null
    return (
        <>
            <section
                className="dict-coverage-grid"
                aria-label="Coverage of the corpus"
            >
                <Card
                    number={`${count(stats.entries)} entries`}
                    label={`across ${stats.books} dictionaries`}
                />
                <Card
                    number={percent(
                        stats.definedRunningWords,
                        stats.runningWords,
                    )}
                    label="of the corpus text has an entry"
                    value={share(stats.definedRunningWords, stats.runningWords)}
                    detail={`${count(stats.definedWords)} of ${count(
                        stats.distinctWords,
                    )} distinct words (${percent(
                        stats.definedWords,
                        stats.distinctWords,
                    )})`}
                />
                {audioRead ? (
                    <Card
                        number={`🔊 ${percent(
                            stats.audioWords!,
                            stats.distinctWords,
                        )}`}
                        label="of the corpus's words can be heard spoken"
                        value={share(stats.audioWords!, stats.distinctWords)}
                        detail={`${count(stats.audioWords!)} of ${count(
                            stats.distinctWords,
                        )} distinct words, across ${stats.recordings} recordings`}
                        to="/dictionary/spoken"
                    />
                ) : (
                    <Card
                        number="🔊 …"
                        label="the recordings are still being read"
                        value={null}
                    />
                )}
                <Card
                    number={percent(stats.attestedLemmas, stats.lemmas)}
                    label="of the word families appear in the texts"
                    to="/dictionary/lemma"
                    value={share(stats.attestedLemmas, stats.lemmas)}
                    detail={`${count(stats.attestedLemmas)} of ${count(
                        stats.lemmas,
                    )} lemmas`}
                />
            </section>
            <p className="dict-coverage-corpus">
                Measured against the corpus: {count(stats.texts)} texts,{" "}
                {count(stats.runningWords)} words, {count(stats.distinctWords)}{" "}
                of them distinct.
            </p>
        </>
    )
}
