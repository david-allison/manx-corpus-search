export const meta = {
  name: 'lemma-pair-classification',
  description: 'Classify candidate lemma pairs: same lexeme (variant/mutation) vs distinct words',
  phases: [
    { title: 'Classify', detail: '1,425 pairs in 10 files, sonnet' },
  ],
}

const IN_DIR = '/Users/davidallison/Work/manx-lemma-data/work'
const OUT_DIR = '/private/tmp/claude-501/-Users-davidallison-StudioProjects-manx-corpus-search/4f62ea7c-baae-4ce6-b7df-6d787ee79242/scratchpad/verdicts'

const FILES = args.files

const prompt = (inPath, outPath) => `You are a Manx Gaelic lexicographer classifying pairs of dictionary headwords.

Read the file ${inPath} with the Read tool. Each line is a JSON record: {"pair", "a", "b", "forms", "occurrences"} where "a" and "b" are two dictionary entries (id, lemma = headword, gloss = English meanings, link = how the entry matched: "demutated" marks an initial-mutation guess), and "forms" are surface tokens that can be read as either entry.

For EVERY pair decide:
- "same": a and b are the SAME Manx lexeme. This covers: initial-mutation pairs where one headword is the mutated spelling of the other (f- lost: aarkey/faarkey, eer/feer; c/ch: cast/chast; g/gh: glass/ghlass; t/h: taaue/haaue; s/h: soilshee/hoilshee; b/v: baase/vaaish; d/gh: dreeym/ghreeym; j/y: yannoo/jannoo), spelling variants (jeeg/jeeig), and suffix/verbal-noun variants of one word (yns/ynsee, firrin/firriney, kionn/kionnee, taau/taaue). Meanings equal or near-identical is the strong signal - but glosses of the SAME word can use different English vocabulary (aarkey "sea" / faarkey "billow, wave" are the same word), so judge the lexeme, not the gloss overlap.
- "distinct": different lexemes with different meanings, however similar the spellings (er "on" vs fer "man/one"; laa "day" vs slaa "daub"; veg "nothing" vs beg "small"; jee "god" vs jee imperative of "look").
- "unsure": you cannot tell (e.g. an entry with an empty gloss you cannot identify).

An empty gloss means the dictionary entry has no definition text - judge from the headword shape, the link type, and your knowledge of Manx.

Use the Write tool to create ${outPath} containing ONLY JSONL - one line per pair, no commentary:
{"pair":"<pair value verbatim>","verdict":"same","note":"<up to 8 words of reasoning>"}
Every input pair must have exactly one output line.

Your final message: one line, "N pairs: X same, Y distinct, Z unsure".`

log(`${FILES.length} pair-classification agents`)
const results = await parallel(FILES.map(n => () =>
  agent(prompt(`${IN_DIR}/pair-requests-${n}.jsonl`, `${OUT_DIR}/pair-verdicts-${n}.jsonl`), {
    label: `pairs:${n}`,
    model: 'sonnet',
    phase: 'Classify',
  })))
const failed = results.filter(r => r === null).length
return `${results.length - failed}/${results.length} agents completed${failed ? `, ${failed} failed` : ''}`