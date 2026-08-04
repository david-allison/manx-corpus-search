import { useEffect, useState } from "react"
import { Link } from "react-router-dom"
import { CircularProgress } from "@mui/material"
import {
    lemmaTree,
    LemmaTreeGroup,
    LemmaTreeParent,
    LemmaTreeResponse,
} from "../api/DictionaryApi"
import { dictionaryWordUrl } from "../utils/DictionaryEntries"
import { SharedMark } from "./FirstAttestation"
import { UnverifiedMark } from "./UnverifiedMark"
import "./LemmaTree.css"

/** The index at a letter. A letter rides on the query string rather than the
 * path because the path names a lemma, and 'e' is one: /dictionary/lemma/e is
 * that word's tree, and the letter E is ?at=e. */
export const lemmaIndexUrl = (at?: string | null) =>
    at ? `/dictionary/lemma?at=${encodeURIComponent(at)}` : "/dictionary/lemma"

export const lemmaTreeUrl = (lemma: string) =>
    `/dictionary/lemma/${encodeURIComponent(lemma)}`

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
    contracts: "Contractions",
    variant: "Variants",
    mutation: "Mutations",
    demutated: "Possible mutations",
    particle: "With a particle",
    derived: "Printed in the entry",
    univerbated: "Written as one word",
    phillips: "Phillips (c. 1610) spellings",
    prefixed: "Written with the prefix",
    undecided: "Undecided",
}

const FORM_UNVERIFIED_TITLE =
    "Unverified: no dictionary records this form under this lemma. It was " +
    "worked out by rule or asserted by hand, and may be wrong"

/** A spelling as a reader reads it: case and apostrophe style folded, the
 * two ways one word arrives spelled differently */
export const readerSpelling = (s: string) => s.toLowerCase().replace(/’/g, "'")

/** Whether two spellings are the same word to a reader: the page's word may
 * differ from the table's form only in case and apostrophe style */
const sameWord = (a: string | undefined, b: string) =>
    a != null && readerSpelling(a) === readerSpelling(b)

const groupsCarry = (word: string, groups: LemmaTreeGroup[]): boolean =>
    groups.some((group) =>
        group.forms.some(
            (form) =>
                sameWord(word, form.via ?? form.form) ||
                sameWord(word, form.form) ||
                (form.groups != null && groupsCarry(word, form.groups)),
        ),
    )

/** Whether the word stands anywhere in the tree — at its root or among the
 * forms, however deep: where the here-mark would land. What a form's page
 * asks before drawing a tree, since a family the word is no part of reads
 * about another word. */
export const wordStandsIn = (word: string, tree: LemmaTreeResponse): boolean =>
    sameWord(word, tree.lemma) || groupsCarry(word, tree.groups)

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
    contracts: "contraction",
    variant: "variant",
    mutation: "mutation",
    demutated: "possible mutation",
    particle: "with a particle",
    phillips: "Phillips spelling",
    undecided: "undecided",
}

/** The upward reading of the graph: what this lemma hangs off, drawn above
 * the root so the family can be climbed from either end. The name opens the
 * word's dictionary page — where the reader's answers are, and which draws
 * this same family at its foot — as every downward row does. */
const ParentLine = ({ parent }: { parent: LemmaTreeParent }) => (
    <p className="dict-lemma-parent">
        {parent.linkTypes.includes("prefixed") ? (
            <>
                {"Written with the prefix "}
                <Link to={dictionaryWordUrl(parent.lemma)}>{parent.lemma}</Link>
                {" ›"}
            </>
        ) : (
            <>
                {"A form of "}
                <Link to={dictionaryWordUrl(parent.lemma)}>{parent.lemma}</Link>
                <span className="dict-lemma-parent-types">
                    {`: ${parent.linkTypes
                        .map((type) => PARENT_LABELS[type] ?? type)
                        .join(" · ")}`}
                    {/* the phrase the contraction spells out: 'contraction
                        of va oc', not a bare 'contraction' */}
                    {parent.expansion ? ` of ${parent.expansion}` : ""}
                </span>
                {" ›"}
            </>
        )}
    </p>
)

/** The "a form of" claims above the root. The book's word-family edges
 * (where the book prints this word) draw as the tree's root instead: see
 * <see cref="FamilyHeads"/>. */
const ParentLines = ({ parents }: { parents?: LemmaTreeParent[] | null }) => (
    <>
        {parents
            ?.filter((x) => !x.linkTypes.includes("derived"))
            .map((parent) => (
                <ParentLine parent={parent} key={parent.lemma} />
            ))}
    </>
)

/** The word-family heads among a lemma's parents: the entries whose
 * paragraphs print it (camstram sits in both cammey's and stramlag's).
 * The word is its own lexeme, so the edge claims the print location and
 * nothing else: never derivation, never "a form of". */
const printedUnderOf = (parents?: LemmaTreeParent[] | null) =>
    parents?.filter((x) => x.linkTypes.includes("derived")) ?? []

/** The word prints UNDER its heads, so the heads root the tree: one node,
 * however many heads, with the word's own tree nested beneath it. A head
 * opens its dictionary page, like every other word in the tree. */
const FamilyHeads = ({ parents }: { parents: LemmaTreeParent[] }) => (
    <p className="dict-lemma-root dict-lemma-root-embedded">
        {parents.map((parent, index) => (
            <span key={parent.lemma}>
                {index > 0 && " · "}
                <Link to={dictionaryWordUrl(parent.lemma)}>{parent.lemma}</Link>
            </span>
        ))}
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
 * server sends no source for one). Shared with the lemma index, whose greyed
 * rows make the same claim. */
export const SourceNote = ({
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
    highlight,
}: {
    groups: LemmaTreeGroup[]
    className?: string
    ariaLabel?: string
    /** the page's own word, marked where the tree says it: the reader sees
     * where they stand in the family */
    highlight?: string
}) => (
    <ul className={className} aria-label={ariaLabel}>
        {groups.map((group) => (
            <li key={group.linkType}>
                <span className="dict-lemma-branch">
                    {GROUP_LABELS[group.linkType] ?? group.linkType}
                </span>
                <ul>
                    {group.forms.map((form) => {
                        const here = sameWord(highlight, form.via ?? form.form)
                        return (
                            <li key={form.form}>
                                {/* a particle row is the phrase itself ('e
                                gheiney'), counted as the phrase: the bare
                                form's count answers for every particle at
                                once, and 'With a particle' alone never says
                                which. The link still opens the form's page. */}
                                <Link
                                    className={
                                        [
                                            form.attested
                                                ? null
                                                : "dict-unattested",
                                            here ? "dict-lemma-here" : null,
                                        ]
                                            .filter(Boolean)
                                            .join(" ") || undefined
                                    }
                                    aria-current={here ? "true" : undefined}
                                    title={
                                        form.via
                                            ? `The form ${form.form}, after its particle${form.attested ? "" : "; in no text in the corpus"}`
                                            : form.attested
                                              ? undefined
                                              : `${form.form}: by this spelling, in no text in the corpus`
                                    }
                                    to={dictionaryWordUrl(form.form)}
                                >
                                    {form.via ?? form.form}
                                </Link>
                                {/* only the form rows can carry the shared
                                    mark: the response does not say it of the
                                    root */}
                                <Count
                                    attestations={form.attestations}
                                    shared={form.sharedWithOtherLemmas}
                                />
                                {/* the phrase a contraction spells out:
                                    'v'oc (va oc)' */}
                                {form.expansion ? (
                                    <span className="dict-lemma-also">
                                        {` (${form.expansion})`}
                                    </span>
                                ) : null}
                                {/* the other ways the same form is linked:
                                    one row, however many links the tables
                                    hold */}
                                {form.alsoLinkedAs?.length ? (
                                    <span className="dict-lemma-also">
                                        {` · also ${form.alsoLinkedAs
                                            .map(
                                                (type) =>
                                                    PARENT_LABELS[type] ?? type,
                                            )
                                            .join(" · ")}`}
                                    </span>
                                ) : null}
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
                                    <TreeGroups
                                        groups={form.groups}
                                        highlight={highlight}
                                    />
                                ) : null}
                            </li>
                        )
                    })}
                </ul>
            </li>
        ))}
    </ul>
)

/** How often the corpus says a node's spelling, as the walk counts uses
 * ("×96"). Silent at a known 0 — the greying already says it — and while a
 * phrase's count is not yet known. */
const Count = ({
    attestations,
    shared,
}: {
    attestations?: number | null
    /** another lexeme also uses the spelling: the count wears the
     * shared-spelling *, since some of it may be the other word's. Riding on
     * the count keeps the mark off the rows with nothing counted — with no
     * occurrences there is nothing for the doubt to be about. */
    shared?: boolean
}) =>
    attestations != null && attestations > 0 ? (
        <>
            <span
                className="dict-lemma-count"
                title={`Said ${attestations.toLocaleString()} ${attestations === 1 ? "time" : "times"} in the corpus, by this spelling`}
            >
                {` ×${attestations.toLocaleString()}`}
            </span>
            {shared && (
                <SharedMark title="Another word also uses this spelling: some of these occurrences may be its" />
            )}
        </>
    ) : null

/** One family drawn inside the word page: the same tree the lemma page
 * draws, under a root sized to head a section rather than a page. A word
 * the book prints under other entries nests beneath them: the heads root
 * the tree, since that is where the word sits on the page. */
const EmbeddedTree = ({
    tree,
    highlight,
}: {
    tree: LemmaTreeResponse
    highlight?: string
}) => {
    const heads = printedUnderOf(tree.parents)
    const here = sameWord(highlight, tree.lemma)
    const root = (
        <p
            className={[
                "dict-lemma-root dict-lemma-root-embedded",
                tree.attested ? null : "dict-unattested",
                here ? "dict-lemma-here" : null,
            ]
                .filter(Boolean)
                .join(" ")}
            title={
                tree.attested
                    ? undefined
                    : `${tree.lemma}: by this spelling, in no text in the corpus`
            }
        >
            {/* the root opens its word's page like every other name in the
                tree — except the page's own word, a landmark, not a way out */}
            {here ? (
                tree.lemma
            ) : (
                <Link to={dictionaryWordUrl(tree.lemma)}>{tree.lemma}</Link>
            )}
            {/* the book's homograph number, tying tree e¹ to entry e¹ */}
            {tree.homograph != null && (
                <sup className="dict-lemma-homograph">{tree.homograph}</sup>
            )}
            {/* the book's class label, so the tree says whose family it
                is: 'ee v.' eats, 'ee pro.' is her, and the lone tree beside
                another reading's entries does not pose as theirs */}
            {tree.pos ? (
                <span className="dict-lemma-pos"> {tree.pos}</span>
            ) : null}
            <Count attestations={tree.attestations} />
            <SourceNote
                form={tree.lemma}
                attested={tree.attested}
                source={tree.source}
            />
        </p>
    )
    return (
        <div className="dict-lemma-embedded">
            <ParentLines parents={tree.parents} />
            {heads.length > 0 ? (
                <>
                    <FamilyHeads parents={heads} />
                    <ul
                        className="dict-lemma-tree"
                        aria-label={`Forms of ${tree.lemma}`}
                    >
                        <li>
                            {root}
                            <TreeGroups
                                groups={tree.groups}
                                highlight={highlight}
                            />
                        </li>
                    </ul>
                </>
            ) : (
                <>
                    {root}
                    <TreeGroups
                        groups={tree.groups}
                        className="dict-lemma-tree"
                        ariaLabel={`Forms of ${tree.lemma}`}
                        highlight={highlight}
                    />
                </>
            )}
        </div>
    )
}

/** The word page's family trees: one per lexeme of every reading, fetched
 * once and shared — the page seats each tree under the lemma section it
 * belongs to, and whatever no section claims ends the page. Trees with
 * nothing hanging off them are dropped: an empty table under a heading would
 * only say the feature exists. Quiet on failure too, for the same reason the
 * page's other extras are. */
export const useWordFamilyTrees = (lemmas: string[]): LemmaTreeResponse[] => {
    const [trees, setTrees] = useState<LemmaTreeResponse[]>([])

    useEffect(() => {
        setTrees([])
        if (lemmas.length === 0) {
            return
        }
        const abort = new AbortController()
        Promise.all(
            lemmas.map((lemma) =>
                lemmaTree(lemma, abort.signal).catch((e: unknown) => {
                    if (!abort.signal.aborted) console.warn(e)
                    return null
                }),
            ),
        )
            .then((results) => {
                if (abort.signal.aborted) {
                    return
                }
                // a reading may name several lexemes (ee the verb, ee the
                // pronoun): one tree each, in the book's order, never a merge
                setTrees(
                    results
                        .filter((r): r is LemmaTreeResponse[] => r != null)
                        .flat()
                        .filter(
                            (tree) =>
                                tree.groups.length > 0 ||
                                (tree.parents?.length ?? 0) > 0,
                        ),
                )
            })
            .catch((e: unknown) => console.warn(e))
        return () => abort.abort()
    }, [lemmas])

    return trees
}

/** The sense-section key a tree's printed class files under, the same split
 * senseGroupsIn makes of the entries: what seats a tree beneath the section
 * that reads about its lexeme. Null where the label constrains nothing. */
export const senseKeyOfPos = (pos: string): string | null => {
    const label = pos.trimStart().toLowerCase()
    if (label.startsWith("v")) return "verb"
    if (label.startsWith("s.") || label.startsWith("n.")) return "noun"
    if (label.startsWith("adv")) return "particle"
    if (label.startsWith("a")) return "adjective"
    // Cregeen abbreviates the interjection "in."
    if (label.startsWith("int") || label.startsWith("in.")) {
        return "interjection"
    }
    if (
        label.startsWith("pro") ||
        label.startsWith("pre") ||
        label.startsWith("c")
    ) {
        return "particle"
    }
    // Cregeen's one-letter p: doubled ("p. p.") it is his prepositional
    // pronoun, alone ("p.") pronominal — the function-word cluster either
    // way. Matched whole, so "pt." (participle) and "pl." claim nothing.
    if (/^p[.,]?( ?p\.?)?$/.test(label.trimEnd())) {
        return "particle"
    }
    return null
}

const FamilyTreeList = ({
    trees,
    word,
}: {
    trees: LemmaTreeResponse[]
    word?: string
}) => (
    <>
        {trees.map((tree) => (
            <EmbeddedTree
                key={tree.lemmaId ?? tree.lemma}
                tree={tree}
                highlight={word}
            />
        ))}
    </>
)

/** The "Word family" section ending the word page: the trees no lemma
 * section claimed. Nothing to show draws nothing. */
export const WordFamily = ({
    trees,
    word,
}: {
    trees: LemmaTreeResponse[]
    /** the page's own word: marked in the trees, so the reader sees where
     * they stand in the family */
    word?: string
}) =>
    trees.length === 0 ? null : (
        <section className="dict-page-group">
            <h3 className="dict-page-dictionary">
                Word family
                <span className="attest-experimental">
                    experimental &amp; incomplete
                </span>
            </h3>
            <FamilyTreeList trees={trees} word={word} />
        </section>
    )

/** A lemma section's own family, folded: the tree belongs beside the entries
 * that read about its lexeme, but expanded it would push the next reading
 * off the screen — the reader opens it where they want it. */
export const WordFamilyDetails = ({
    trees,
    word,
}: {
    trees: LemmaTreeResponse[]
    word?: string
}) =>
    trees.length === 0 ? null : (
        <details className="dict-lemma-details">
            <summary className="dict-page-dictionary">
                Word family
                <span className="attest-experimental">
                    experimental &amp; incomplete
                </span>
            </summary>
            <FamilyTreeList trees={trees} word={word} />
        </details>
    )

/** The lemma page's form trees: one per lexeme answering to the name (ee the
 * verb and ee the pronoun stand apart, each wearing the book's class label),
 * the lemma at each root, its forms grouped by how each hangs off it, every
 * guess marked and every unattested spelling greyed.
 *
 * One level deep on purpose: the link graph carries book-true cycles (fee
 * inflects to feeagh, feeagh pluralizes to fee), and a tree of leaves cannot
 * be walked in a circle — each form is a link to its own word page instead. */
export const LemmaTree = ({ lemma }: { lemma: string }) => {
    const [trees, setTrees] = useState<LemmaTreeResponse[] | null>(null)
    const [failed, setFailed] = useState(false)

    useEffect(() => {
        setTrees(null)
        setFailed(false)
        const abort = new AbortController()
        lemmaTree(lemma, abort.signal)
            .then(setTrees)
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
                    <Link to={lemmaIndexUrl()}>Back to the lemma index.</Link>
                </p>
            )}
            {!failed && trees == null && (
                <div className="dict-page-loading">
                    <CircularProgress />
                </div>
            )}

            {trees?.map((tree, index) => (
                <div key={tree.lemmaId ?? tree.lemma}>
                    {/* what the root itself hangs off, drawn above it: the
                        graph climbs both ways */}
                    <ParentLines parents={tree.parents} />
                    {/* the heads whose paragraphs print this word sit above
                        it, as they do on the book's page */}
                    {printedUnderOf(tree.parents).length > 0 && (
                        <FamilyHeads parents={printedUnderOf(tree.parents)} />
                    )}

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
                                : `${tree.lemma}: by this spelling, in no text in the corpus`
                        }
                    >
                        {tree.lemma}
                        {tree.homograph != null && (
                            <sup className="dict-lemma-homograph">
                                {tree.homograph}
                            </sup>
                        )}
                        {/* the book's class label, so the tree says whose
                            family it is */}
                        {tree.pos ? (
                            <span className="dict-lemma-pos"> {tree.pos}</span>
                        ) : null}
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
                        {/* the tree is as experimental as the corpus walk,
                            and says so the same way — once, on the first */}
                        {index === 0 && (
                            <span className="attest-experimental">
                                experimental &amp; incomplete
                            </span>
                        )}
                    </h1>

                    {tree.groups.length === 0 &&
                        printedUnderOf(tree.parents).length === 0 && (
                            <p className="dict-browse-empty">
                                No forms hang off this lemma.
                            </p>
                        )}
                    <TreeGroups
                        groups={tree.groups}
                        className="dict-lemma-tree"
                        ariaLabel={`Forms of ${tree.lemma}`}
                    />

                    {/* one way out, under the last tree: the word page reads
                        every lexeme's entries at once */}
                    {index === trees.length - 1 && (
                        <p className="dict-lemma-note">
                            <Link to={dictionaryWordUrl(tree.lemma)}>
                                Read the dictionary entries for “{tree.lemma}” ›
                            </Link>
                        </p>
                    )}
                </div>
            ))}
        </>
    )
}
