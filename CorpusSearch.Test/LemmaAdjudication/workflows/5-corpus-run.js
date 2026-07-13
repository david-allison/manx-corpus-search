export const meta = {
  name: 'lemma-adjudication-corpus',
  description: 'Corpus adjudication: resolve true-homograph tokens on translated lines (sonnet)',
  phases: [
    { title: 'Adjudicate', detail: '144 corpus files, sonnet, group-aware' },
  ],
}

const IN_DIR = '/Users/davidallison/Work/manx-lemma-data/work'
const OUT_DIR = '/private/tmp/claude-501/-Users-davidallison-StudioProjects-manx-corpus-search/4f62ea7c-baae-4ce6-b7df-6d787ee79242/scratchpad/verdicts'

const FILES = args.files

const prompt = (inPath, outPath) => `You are disambiguating Manx Gaelic lemmas using parallel English translations.

Read the file ${inPath} with the Read tool. Each line is a JSON record with:
- "manx": a Manx line from a historical corpus
- "english": its English translation
- "tokens": ambiguous tokens from the line. Each has "i" (position), "form" (the token), and "candidates" - possible lemma readings, each with "id", "lemma" (dictionary headword), "gloss" (English meanings), "link" (how the reading arises; "demutated" = the token may be an initial-mutated form of this headword), and "group".

Candidates sharing the same "group" are the SAME Manx word (spelling/mutation/suffix variants of one lexeme) - never choose between them. Your job is to choose between GROUPS: genuinely different words (er "on" vs fer "man/one"; veg "nothing" vs beg "small" vs meg "pet lamb"; laa "day" vs slaa "daub").

For EVERY token in EVERY record:
- Decide which group's meaning this line uses, from the Manx grammar and the English translation.
- "chosenIds" = ALL ids of the chosen group (every id, not just one).
- "confidence": "high" when the translation or grammar clearly settles it; "low" when fairly sure but indirect; "unsure" when you cannot decide. NEVER guess: a wrong resolution is harmful, an "unsure" is free. If NO group's meaning fits the line, answer "unsure" - do not pick the least-bad one.

Then use the Write tool to create ${outPath} containing ONLY JSONL - one line per token, no commentary, no markdown fences:
{"key":"<record key>","i":<token i>,"chosenIds":["<id>","<id>"],"confidence":"high"}
For unsure tokens: {"key":"...","i":...,"chosenIds":[],"confidence":"unsure"}
Every token in the input must have exactly one verdict line - do not skip any.

Your final message: one line, "N verdicts written, M unsure".`

log(`${FILES.length} corpus adjudication agents (sonnet, group-aware)`)
const results = await parallel(FILES.map(n => () =>
  agent(prompt(`${IN_DIR}/corpus-requests-${n}.jsonl`, `${OUT_DIR}/corpus-verdicts-${n}.jsonl`), {
    label: `corpus:${n}`,
    model: 'sonnet',
    phase: 'Adjudicate',
  })))
const failed = results.filter(r => r === null).length
return `${results.length - failed}/${results.length} agents completed${failed ? `, ${failed} failed` : ''}`