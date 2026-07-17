import { useEffect, useState } from "react"
import { Link, useParams, useSearchParams } from "react-router-dom"
import { CircularProgress } from "@mui/material"
import {
    DictionaryBrowseResponse,
    lemmaIndex,
    lemmaTree,
    LemmaTreeGroup,
    LemmaTreeParent,
    LemmaTreeResponse,
} from "../api/DictionaryApi"
import { dictionaryWordUrl } from "../utils/DictionaryEntries"
import { UnverifiedMark } from "../components/UnverifiedMark"
import { WordSearch } from "../components/WordSearch"
import "./DictionaryBrowse.css"
import "./DictionaryLemma.css"

/** The index at a letter. A letter rides on the query string rather than the
 * path because the path names a lemma, and 'e' is one: /dictionary/lemma/e is
 * that word's tree, and the letter E is ?at=e. */
const indexUrl = (at?: string | null) =>
    at ? `/dictionary/lemma?at=${encodeURIComponent(at)}` : "/dictionary/lemma"

const treeUrl = (lemma: string) =>
    `/dictionary/lemma/${encodeURIComponent(lemma)}`

/** The letters, with the open one marked: the browse page's bar, over lemmas */
const Bar = ({
    items,
    active,
    ariaLabel,
}: {
    items: string[]
    active?: string | null
    ariaLabel: string
}) => (
    <nav className="dict-browse-bar" aria-label={ariaLabel}>
        {items.map((item) => (
            <Link
                key={item}
                to={indexUrl(item)}
                className={item === active ? "active" : undefined}
                aria-current={item === active ? "page" : undefined}
            >
                {item}
            </Link>
        ))}
    </nav>
)

/** One letter of the lemma index, whole: the same adaptive chapters the
 * dictionary browse files its headwords under, over every lemma the tables
 * link a form to. */
const LemmaIndex = ({ at }: { at?: string | null }) => {
    const [page, setPage] = useState<DictionaryBrowseResponse | null>(null)
    const [failed, setFailed] = useState(false)

    useEffect(() => {
        setPage(null)
        setFailed(false)
        const abort = new AbortController()
        lemmaIndex(at, abort.signal)
            .then(setPage)
            .catch((e) => {
                if (!abort.signal.aborted) {
                    console.warn(e)
                    setFailed(true)
                }
            })
        return () => abort.abort()
    }, [at])

    return (
        <>
            {failed && <p>Something went wrong. Try again.</p>}
            {!failed && page == null && (
                <div className="dict-page-loading">
                    <CircularProgress />
                </div>
            )}

            {page != null && (
                <>
                    <h1 className="dict-page-word">Lemmas</h1>
                    <p className="dict-lemma-note">
                        The words the corpus search groups spellings under,
                        whatever form a text uses. Open one for its forms.
                    </p>
                    <Bar
                        items={page.letters}
                        active={page.letter}
                        ariaLabel="Letters"
                    />
                    {page.letters.length === 0 && (
                        // the lemma tables are a submodule: without them the
                        // index is empty rather than broken
                        <p className="dict-browse-empty">
                            No lemmas are loaded.
                        </p>
                    )}
                    <div
                        className="dict-browse-chapters dict-lemma-chapters"
                        aria-label={`Lemmas under ${page.letter}`}
                    >
                        {page.chapters.map((chapter, index) => (
                            <div
                                className="dict-browse-chapter"
                                key={`${chapter.key}-${index}`}
                            >
                                <span className="dict-browse-key">
                                    {chapter.key}
                                </span>
                                <p className="dict-browse-words">
                                    {chapter.words.map((entry, position) => (
                                        <span key={`${entry.word}-${position}`}>
                                            {position > 0 && (
                                                <span
                                                    className="dict-browse-sep"
                                                    aria-hidden="true"
                                                >
                                                    {" · "}
                                                </span>
                                            )}
                                            <Link
                                                className={
                                                    entry.attested
                                                        ? undefined
                                                        : "dict-unattested"
                                                }
                                                title={
                                                    entry.attested
                                                        ? undefined
                                                        : `${entry.word} — in no text in the corpus`
                                                }
                                                to={treeUrl(entry.word)}
                                            >
                                                {entry.word}
                                            </Link>
                                        </span>
                                    ))}
                                </p>
                            </div>
                        ))}
                    </div>

                    {/* the letters again, as the browse repeats them: a reader
                        at the foot of a long letter can turn without scrolling
                        back up for the bar */}
                    {page.letters.length > 0 && (
                        <Bar
                            items={page.letters}
                            active={page.letter}
                            ariaLabel="Letters, again"
                        />
                    )}
                </>
            )}
        </>
    )
}

/** The reader's words for the tables' link types. A type the data grows that
 * is not named here shows under its own name rather than hiding. */
const GROUP_LABELS: Record<string, string> = {
    self: "Also entered as",
    inflected: "Inflected forms",
    plural: "Plurals",
    compSup: "Comparative & superlative",
    irregular: "Irregular forms",
    emphatic: "Emphatic forms",
    contraction: "Contractions",
    variant: "Variants",
    mutation: "Mutations",
    demutated: "Possible mutations",
    particle: "With a particle",
    univerbated: "Written as one word",
    phillips: "Phillips (c. 1610) spellings",
    prefixed: "Written with the prefix",
    undecided: "Undecided",
}

const FORM_UNVERIFIED_TITLE =
    "Unverified: no dictionary records this form under this lemma. It was " +
    "worked out by rule or asserted by hand, and may be wrong"

/** How a link type reads climbing UP the tree, where the chips above read
 * down: 'deiney — inflected · plural of dooinney'. Raw type for the rest. */
const PARENT_LABELS: Record<string, string> = {
    self: "also entered there",
    inflected: "inflected",
    plural: "plural",
    compSup: "comparative/superlative",
    irregular: "irregular",
    emphatic: "emphatic",
    contraction: "contraction",
    variant: "variant",
    mutation: "mutation",
    demutated: "possible mutation",
    particle: "with a particle",
    phillips: "Phillips spelling",
    undecided: "undecided",
}

/** The upward reading of the graph: what this lemma hangs off, drawn above
 * the root so the family can be climbed from either end. */
const ParentLine = ({ parent }: { parent: LemmaTreeParent }) => (
    <p className="dict-lemma-parent">
        {parent.linkTypes.includes("prefixed") ? (
            <>
                {"Written with the prefix "}
                <Link to={treeUrl(parent.lemma)}>{parent.lemma}</Link>
                {" ›"}
            </>
        ) : (
            <>
                {"A form of "}
                <Link to={treeUrl(parent.lemma)}>{parent.lemma}</Link>
                <span className="dict-lemma-parent-types">
                    {` — ${parent.linkTypes
                        .map((type) => PARENT_LABELS[type] ?? type)
                        .join(" · ")}`}
                </span>
                {" ›"}
            </>
        )}
    </p>
)

/** The reader's name for a source file. Only the book earns a note: the
 * Phillips rows wear their group label already, the names supplement is
 * corpus-driven, and the vocab supplement's rows are guesses with a mark of
 * their own. */
const SOURCE_NAMES: Record<string, string> = { cregeen: "Cregeen" }

/** Names the book behind a node no text uses. Greyed, it would otherwise read
 * as a phantom — when in fact Cregeen prints it, and only the corpus is
 * silent. An attested node needs no vouching, and a guess names no book (the
 * server sends no source for one). */
const SourceNote = ({
    form,
    attested,
    source,
}: {
    form: string
    attested: boolean
    source?: string | null
}) => {
    const name = !attested && source != null ? SOURCE_NAMES[source] : undefined
    return name ? (
        <>
            {" "}
            <abbr
                className="dict-abbr dict-lemma-source"
                title={`${name} records “${form}”, though no text in the corpus uses this spelling`}
            >
                {name}
            </abbr>
        </>
    ) : null
}

/** The branches under one node: its forms by how each hangs off it, every
 * form nesting in turn what hangs off *it* — the rows deriving through it
 * ('pyaghyn' inflects the variant 'pyagh'), and a lexeme it heads itself
 * ('deiney' under dooinney carries 'e gheiney'). The server draws each form
 * once, so a book-true cycle (fee ↔ feeagh) closes as a leaf. */
const TreeGroups = ({
    groups,
    className,
    ariaLabel,
}: {
    groups: LemmaTreeGroup[]
    className?: string
    ariaLabel?: string
}) => (
    <ul className={className} aria-label={ariaLabel}>
        {groups.map((group) => (
            <li key={group.linkType}>
                <span className="dict-lemma-branch">
                    {GROUP_LABELS[group.linkType] ?? group.linkType}
                </span>
                <ul>
                    {group.forms.map((form) => (
                        <li key={form.form}>
                            <Link
                                className={
                                    form.attested
                                        ? undefined
                                        : "dict-unattested"
                                }
                                title={
                                    form.attested
                                        ? undefined
                                        : `${form.form} — by this spelling, in no text in the corpus`
                                }
                                to={dictionaryWordUrl(form.form)}
                            >
                                {form.form}
                            </Link>
                            <Count attestations={form.attestations} />
                            <SourceNote
                                form={form.form}
                                attested={form.attested}
                                source={form.source}
                            />
                            <UnverifiedMark
                                unverified={form.unverified}
                                title={FORM_UNVERIFIED_TITLE}
                            />
                            {form.groups?.length ? (
                                <TreeGroups groups={form.groups} />
                            ) : null}
                        </li>
                    ))}
                </ul>
            </li>
        ))}
    </ul>
)

/** How often the corpus says a node's spelling, as the walk counts uses
 * ("×96"). Silent at a known 0 — the greying already says it — and while a
 * phrase's count is not yet known. */
const Count = ({ attestations }: { attestations?: number | null }) =>
    attestations != null && attestations > 0 ? (
        <span
            className="dict-lemma-count"
            title={`Said ${attestations.toLocaleString()} ${attestations === 1 ? "time" : "times"} in the corpus, by this spelling`}
        >
            {` ×${attestations.toLocaleString()}`}
        </span>
    ) : null

/** One lemma's form tree: the lemma at the root, its forms grouped by how each
 * hangs off it, every guess marked and every unattested spelling greyed.
 *
 * One level deep on purpose: the link graph carries book-true cycles (fee
 * inflects to feeagh, feeagh pluralizes to fee), and a tree of leaves cannot
 * be walked in a circle — each form is a link to its own word page instead. */
const LemmaTree = ({ lemma }: { lemma: string }) => {
    const [tree, setTree] = useState<LemmaTreeResponse | null>(null)
    const [failed, setFailed] = useState(false)

    useEffect(() => {
        setTree(null)
        setFailed(false)
        const abort = new AbortController()
        lemmaTree(lemma, abort.signal)
            .then(setTree)
            .catch((e) => {
                if (!abort.signal.aborted) {
                    console.warn(e)
                    setFailed(true)
                }
            })
        return () => abort.abort()
    }, [lemma])

    return (
        <>
            {failed && (
                <p>
                    No lemma “{lemma}”.{" "}
                    <Link to={indexUrl()}>Back to the lemma index.</Link>
                </p>
            )}
            {!failed && tree == null && (
                <div className="dict-page-loading">
                    <CircularProgress />
                </div>
            )}

            {tree != null && (
                <>
                    {/* what the root itself hangs off, drawn above it: the
                        graph climbs both ways */}
                    {tree.parents?.map((parent) => (
                        <ParentLine parent={parent} key={parent.lemma} />
                    ))}

                    {/* the root of the tree: the trunk below hangs off it */}
                    <h1
                        className={
                            tree.attested
                                ? "dict-page-word dict-lemma-root"
                                : "dict-page-word dict-lemma-root dict-unattested"
                        }
                        title={
                            tree.attested
                                ? undefined
                                : `${tree.lemma} — by this spelling, in no text in the corpus`
                        }
                    >
                        {tree.lemma}
                        <Count attestations={tree.attestations} />
                        <SourceNote
                            form={tree.lemma}
                            attested={tree.attested}
                            source={tree.source}
                        />
                        <UnverifiedMark
                            unverified={tree.unverified}
                            title={
                                "Unverified: this lemma was asserted by hand " +
                                "and no dictionary page attests it. It may be " +
                                "wrong"
                            }
                        />
                    </h1>

                    {tree.groups.length === 0 && (
                        <p className="dict-browse-empty">
                            No forms hang off this lemma.
                        </p>
                    )}
                    <TreeGroups
                        groups={tree.groups}
                        className="dict-lemma-tree"
                        ariaLabel={`Forms of ${tree.lemma}`}
                    />

                    <p className="dict-lemma-note">
                        <Link to={dictionaryWordUrl(tree.lemma)}>
                            Read the dictionary entries for “{tree.lemma}” ›
                        </Link>
                    </p>
                </>
            )}
        </>
    )
}

/** The lemma pages at /dictionary/lemma: the index of every lemma without a
 * path segment, one lemma's form tree with one. */
export const DictionaryLemma = () => {
    const { lemma } = useParams()
    const [params] = useSearchParams()
    const at = params.get("at")

    return (
        <div className="dict-page">
            {/* a reader who knows the word they want should not have to find
                it in the index first */}
            <WordSearch indexUrl={lemma ? indexUrl(lemma) : undefined} />
            {lemma ? <LemmaTree lemma={lemma} /> : <LemmaIndex at={at} />}
        </div>
    )
}
