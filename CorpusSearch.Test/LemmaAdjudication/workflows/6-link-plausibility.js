export const meta = {
  name: 'link-plausibility',
  description: 'Audit rule-made lemma links: sonnet triage over every open edge, opus panel on the doubted',
  phases: [
    { title: 'Triage', detail: 'one sonnet vote per request file' },
    { title: 'Panel', detail: '2 opus + 1 sonnet over triage implausible/unsure' },
  ],
}

// Requests: cregeen-nvh `make audit-links` with
//   --requests /Users/davidallison/Work/manx-lemma-data/work
// Merge:     audit-links ... --merge-verdicts <verdict dir>
// args: { files: ["001", ...], out: "<verdict dir>" }

const IN_DIR = '/Users/davidallison/Work/manx-lemma-data/work'
const OUT_DIR = args.out
const FILES = args.files

const prompt = (inPath, outPath) => `You are a Manx Gaelic lexicographer auditing machine-guessed dictionary links.

Read the file ${inPath} with the Read tool. Each line is a JSON record: {"key","form","formPos","formGloss","linkType","via","lemma","lemmaId","lemmaPos","lemmaGloss"}. The link claims the spelling "form" can be read as a form of the lexeme "lemma" - for "demutated", that "form" is an initial-mutated spelling of "lemma" (cheau -> ceau "threw"; chree -> cree "heart"); for "variant"/"compSup"/"plural"/"typo"/"contraction", that the entry printed at "form" folds into "lemma"'s lexeme; for the s'-/er n'- "inflected" rows, that the stem after the prefix is a spelling of "lemma".

For EVERY record decide, from the two glosses and your knowledge of Manx initial mutation (c/ch, k/ch, t/h, s/h, t/çh, s/th, b/v, m/v, m/w, f/w, j/y, f/-), whether the claim is real:
- "plausible": the form genuinely can be this lexeme - the glosses describe one word (possibly in grammatical dress), or a real paradigm/variant relation.
- "implausible": spelling coincidence - the form's own gloss and the lexeme's gloss are different words (thaa "weld, solder" is no mutated saa "younger, youngest").
- "unsure": you cannot rule either way (empty or formula-only glosses on a shape you cannot identify, or near-synonyms that might be one word).

Glosses of the SAME word can use different English vocabulary (aarkey "sea" / faarkey "billow, wave"): judge the lexeme, not word overlap. A mutation guess whose meanings match is plausible even if the book never prints the mutated spelling. An empty formGloss means the spelling has no printed entry of its own - then the claim is usually plausible (the rule minted the spelling FOR that lexeme); implausible needs the form to visibly be a different word.

Use the Write tool to create ${outPath} containing ONLY JSONL - one line per record, no commentary, no markdown fences:
{"key":"<key verbatim>","verdict":"implausible","note":"<up to 12 words of reasoning>"}
Every input record must have exactly one output line.

Your final message: one line, "N records: X plausible, Y implausible, Z unsure".`

log(`${FILES.length} triage files (sonnet)`)
await parallel(FILES.map(n => () =>
  agent(prompt(`${IN_DIR}/link-requests-${n}.jsonl`, `${OUT_DIR}/link-verdicts-${n}.jsonl`), {
    label: `triage:${n}`,
    model: 'sonnet',
    phase: 'Triage',
  })))

// the panel re-votes only what triage doubted: read the triage verdicts,
// collect implausible/unsure keys, and re-request those records
const doubted = await agent(
  `Read every link-verdicts-*.jsonl file in ${OUT_DIR} and every link-requests-*.jsonl file in ${IN_DIR}. ` +
  `Collect the request records whose key got verdict "implausible" or "unsure" in the triage verdicts. ` +
  `Write them to ${OUT_DIR}/panel-requests.jsonl (the original request lines, verbatim). ` +
  `Your final text: ONLY the count of records written, as a number.`,
  { label: 'collect-doubted', phase: 'Panel', effort: 'low' },
)
log(`panel pool: ${String(doubted).trim()} doubted records`)

await parallel([
  ['opus', 'a'], ['opus', 'b'], ['sonnet', 'c'],
].map(([model, tag]) => () =>
  agent(prompt(`${OUT_DIR}/panel-requests.jsonl`, `${OUT_DIR}/link-panel-${tag}.jsonl`), {
    label: `panel:${model}:${tag}`,
    model,
    phase: 'Panel',
  })))

return `triage ${FILES.length} files; panel of 3 over ${String(doubted).trim()} doubted records; verdicts in ${OUT_DIR}`
