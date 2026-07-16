import { useState } from "react"
import { Link } from "react-router-dom"
import { DictionaryHistoryResponse } from "../api/DictionaryApi"
import {
    AttestationClaim,
    earliestForLemma,
    earliestForWord,
} from "../utils/Attestation"
import { buildKwic } from "../utils/Kwic"
import { AttestationLineModal } from "./AttestationLineModal"
import "./FirstAttestation.css"

/** A spelling whose occurrences may belong to another lexeme: the year is
 * offered, not asserted */
const SharedMark = () => (
    <abbr
        className="dict-abbr first-seen-shared"
        title="This spelling is shared with another word: the occurrence may not be this one"
    >
        *
    </abbr>
)

/** A verse runs to any length, and the head of it is rarely where the word is:
 * the band quotes a few words either side, and the whole line opens in a dialog */
const CONTEXT_WORDS = 3

const Sample = ({
    claim,
    onOpen,
}: {
    claim: AttestationClaim
    onOpen: () => void
}) => {
    const sample = claim.form.sample
    const highlight = claim.form.sampleHighlights?.[0]
    if (!sample) {
        return null
    }
    const kwic = highlight
        ? buildKwic(sample, [highlight], CONTEXT_WORDS)
        : null
    // no offsets to centre on (the scan could not highlight it): the line as it
    // came, and no window to open out of
    if (kwic == null) {
        return <span className="first-seen-sample">{`“${sample}”`}</span>
    }
    // the window's own bounds say whether the line runs on past it
    const runsOnBefore = highlight!.start - kwic.pre.length > 0
    const runsOnAfter = highlight!.end + kwic.post.length < sample.length

    return (
        <button
            type="button"
            className="first-seen-sample"
            title="Read the whole line"
            onClick={onOpen}
        >
            {"“"}
            {runsOnBefore && "…"}
            {kwic.pre}
            <strong>{kwic.match}</strong>
            {kwic.post}
            {runsOnAfter && "…"}
            {"”"}
        </button>
    )
}

/** "1748 as billey in Pargeys Caillit: “…yn billey mooar…”": the year, the
 * spelling it was found under, the text it links to, and a glimpse of the line.
 * The spelling is always named: on a shared form it is what the * attaches to,
 * and on the lexeme's row it is the whole point. */
const ClaimText = ({
    claim,
    onOpen,
}: {
    claim: AttestationClaim
    onOpen: (claim: AttestationClaim) => void
}) => (
    <>
        <strong className="first-seen-year">{claim.year}</strong>
        {" as "}
        <em className="first-seen-form">{claim.form.form}</em>
        {claim.uncertain && <SharedMark />}
        {claim.form.earliestIdent && (
            <>
                {" in "}
                <Link
                    to={`/docs/${claim.form.earliestIdent}?q=${encodeURIComponent(claim.form.form)}`}
                >
                    {claim.form.earliestTitle}
                </Link>
            </>
        )}
        {/* the colon punctuates the citation, so it stays against the title's
            last word — it used to open the quote below, which is a button and so
            an atomic inline: the line could break in front of it and carry the
            colon off to sit alone under "…yn Noo Mian".
            The space rides with it rather than leading the quote, because
            whitespace opening an inline-block is collapsed away — and it is what
            gives the line somewhere to break instead. */}
        {claim.form.sample && ": "}
        <Sample claim={claim} onOpen={() => onOpen(claim)} />
    </>
)

/** The lexeme's genuinely-earliest reading, when it rests on a spelling
 * another word also uses: "— possibly 1610 as villey*". Worth knowing, not
 * worth claiming, so it trails the claim rather than replacing it. */
const EarlierShared = ({ claim }: { claim: AttestationClaim }) => (
    <span className="first-seen-caveat">
        {". Possibly "}
        <strong>{claim.year}</strong>
        {" as "}
        <em>{claim.form.form}</em>
        <SharedMark />
    </span>
)

/** When the word was first seen, up front: the spelling that was looked up, and
 * the lexeme in any spelling.
 *
 * Every year here is bounded by what the corpus holds - an earlier source that
 * has not been ingested cannot be counted - so each one is shown with the text
 * it came from rather than on its own. It is a reading of the evidence, not an
 * origin. */
export const FirstAttestation = ({
    history,
    classes = [],
}: {
    history: DictionaryHistoryResponse | null
    /** the word classes the entries declare: more than one and the date below
     * belongs to whichever of them came first */
    classes?: string[]
}) => {
    // the claim whose line is open in the dialog; null while closed
    const [reading, setReading] = useState<AttestationClaim | null>(null)
    if (history == null || history.forms.length === 0) {
        return null
    }
    const word = earliestForWord(history)
    const lemma = earliestForLemma(history)
    if (lemma == null) {
        return null
    }

    // Splitting the spelling from the lexeme is only worth doing when they say
    // different things. They don't when the lexeme's earliest spelling IS the
    // one looked up ('ass' 1610* is both), nor when an unambiguous spelling
    // already carries the earliest year ('billey' 1610, cluster-mate 'bilchyn'
    // also 1610) — an unambiguous spelling cannot post-date its own lexeme.
    // A shared spelling with an unambiguous lexeme behind it is the case worth
    // splitting: 'vee' looks 1610 but may be another word, so the lexeme's own
    // secure 1748 stands beside it.
    const single =
        word != null &&
        (word.form.form === lemma.claim.form.form ||
            (!word.uncertain && lemma.claim.year >= word.year))
            ? word
            : null

    // the earlier-shared reading survives the collapse, but is dropped when it
    // would merely restate the spelling's own line ('vee' as 'vee's earlier
    // reading)
    const earlierShared =
        lemma.earlierShared?.form.form === word?.form.form
            ? null
            : lemma.earlierShared

    return (
        <div className="first-seen" role="note">
            <span className="first-seen-label">First seen</span>
            {single ? (
                // one fact: labelling it "this spelling" would imply another
                // spelling says something else
                <p className="first-seen-single">
                    <ClaimText claim={single} onOpen={setReading} />
                    {earlierShared && <EarlierShared claim={earlierShared} />}
                </p>
            ) : (
                <dl className="first-seen-rows">
                    <dt>This spelling</dt>
                    <dd>
                        {word ? (
                            <ClaimText claim={word} onOpen={setReading} />
                        ) : (
                            <span className="first-seen-none">
                                not attested: the corpus has the word only in
                                other spellings
                            </span>
                        )}
                    </dd>
                    <dt>Any form</dt>
                    <dd>
                        <ClaimText claim={lemma.claim} onOpen={setReading} />
                        {earlierShared && (
                            <EarlierShared claim={earlierShared} />
                        )}
                    </dd>
                </dl>
            )}
            {classes.length > 1 && (
                // the entries below are more than one word sharing a spelling,
                // and neither the lemma table nor the corpus separates them:
                // the date above is whichever of them was written down first
                <p className="first-seen-warning" role="note">
                    <span
                        className="first-seen-warning-mark"
                        aria-hidden="true"
                    >
                        !
                    </span>
                    {`“${history.word}” covers more than one sense (${classes
                        .map((x) => x.toLowerCase())
                        .join(
                            ", ",
                        )}): this date is the earliest of any of them.`}
                </p>
            )}
            {history.truncatedForms > 0 && (
                <p className="first-seen-caveat">
                    {`${history.truncatedForms} more spellings were not scanned: an earlier one may exist.`}
                </p>
            )}
            <AttestationLineModal
                form={reading?.form.form ?? null}
                ident={reading?.form.earliestIdent}
                title={reading?.form.earliestTitle}
                year={reading?.year}
                onClose={() => setReading(null)}
            />
        </div>
    )
}
