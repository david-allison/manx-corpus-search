import { Fragment } from "react"
import "./GrammarAbbr.css"

/** The abbreviations the printed dictionaries use (Cregeen 1835 and Kelly
 * 1866 share most of them), expanded on hover teanglann-style. Only entries
 * we are confident about are listed: an unexplained abbreviation is better
 * than a wrong explanation. */
const GLOSSARY: Record<string, string> = {
    // word class and gender
    "s. m. f.": "noun, masculine or feminine",
    "s. f. m.": "noun, feminine or masculine",
    "s. m.": "noun, masculine",
    "s. f.": "noun, feminine",
    "s. pl.": "noun, plural",
    "a. pl.": "adjective, plural",
    "v. i.": "verb, intransitive",
    "p. p.": "past participle",
    "s.": "noun (substantive)",
    "a.": "adjective",
    "adj.": "adjective",
    "v.": "verb",
    "adv.": "adverb",
    "pro.": "pronoun",
    "pron.": "pronoun",
    "pre.": "preposition",
    "prep.": "preposition",
    "conj.": "conjunction",
    "part.": "participle",
    // inflection
    "pl.": "plural",
    "sing.": "singular",
    "gen.": "genitive",
    "s. imp.": "subjunctive imperfect (conditional)",
    "imp.": "imperfect (past tense)",
    "imper.": "imperative",
    "fut.": "future",
    "comp.": "comparative",
    "dim.": "diminutive",
    // editorial
    "lit.": "literally",
    "cf.": "compare",
    "q. v.": "which see",
    "q. d.": "as if to say",
    "i.e.": "that is",
    "&c.": "et cetera",
    "Vid.": "see",
    "vid.": "see",
    "Cr.": "Cregeen's dictionary",
    // cognate languages in the etymologies
    "Ir.": "Irish",
    "S.G.": "Scottish Gaelic",
    "Sc.G.": "Scottish Gaelic",
    "Gal.": "Scottish Gaelic (Galic)",
    "W.": "Welsh",
    "Lat.": "Latin",
    "lat.": "Latin",
    "Gr.": "Greek",
    "Heb.": "Hebrew",
    "Arm.": "Armoric (Breton)",
    "Cor.": "Cornish",
}

/** Label-only expansions: safe beside a headword, too ambiguous to tag
 * inside running English prose ("the state he was in.") */
const LABEL_ONLY: Record<string, string> = {
    "in.": "interjection",
    "int.": "interjection",
    "p.": "participle",
    "art.": "article",
    "em.": "emphatic",
    "id.": "the same",
    "syn.": "synonymous with",
    "prov.": "proverb",
}

const escape = (s: string) => s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")

// longest first, so 's. m.' wins over 's.'; boundaries keep 'Ps.' intact
const pattern = new RegExp(
    "(?<![\\p{L}\\p{N}])(" +
        Object.keys(GLOSSARY)
            .sort((a, b) => b.length - a.length)
            .map(escape)
            .join("|") +
        ")(?![\\p{L}\\p{N}])",
    "gu",
)

/** "s. m." -> "noun, masculine"; a compound label falls back to expanding
 * token by token ("pro. adv." -> "pronoun, adverb"); undefined when any part
 * is unknown - no tooltip is better than a wrong one */
export const expandGrammarLabel = (label: string): string | undefined => {
    const direct = GLOSSARY[label] ?? LABEL_ONLY[label]
    if (direct) {
        return direct
    }
    const parts = label.split(/\s+/).map((t) => GLOSSARY[t] ?? LABEL_ONLY[t])
    return parts.every((x) => x != null) && parts.length > 0
        ? parts.join(", ")
        : undefined
}

/** The printed grammar label beside a headword ("s. f."), expansion on hover */
export const GrammarLabel = ({ label }: { label?: string | null }) => {
    if (!label) return null
    const expansion = expandGrammarLabel(label)
    return expansion ? (
        <abbr className="dict-abbr dict-grammar-label" title={expansion}>
            {label}
        </abbr>
    ) : (
        <span className="dict-grammar-label">{label}</span>
    )
}

/** Definition text with the printed abbreviations explained on hover:
 * "s. pl. YN. a covering. (Ir. cuid.)" gets tooltips on s., pl. and Ir. */
export const DefinitionText = ({ text }: { text: string }) => (
    <>
        {text.split(pattern).map((piece, index) =>
            index % 2 ? (
                <abbr className="dict-abbr" title={GLOSSARY[piece]} key={index}>
                    {piece}
                </abbr>
            ) : (
                <Fragment key={index}>{piece}</Fragment>
            ),
        )}
    </>
)
