export const meta = {
  name: 'lemma-adjudication-pilot3',
  description: 'Pilot round 3: sonnet with lemma-convention guidance (mutation->radical, variant->self)',
  phases: [
    { title: 'Adjudicate', detail: '14 eval files, sonnet, convention-taught prompt' },
  ],
}

const IN_DIR = '/Users/davidallison/Work/manx-lemma-data/work'
const OUT_DIR = '/private/tmp/claude-501/-Users-davidallison-StudioProjects-manx-corpus-search/4f62ea7c-baae-4ce6-b7df-6d787ee79242/scratchpad/verdicts'

const FILES = args.files

const prompt = (inPath, outPath) => `You are disambiguating Manx Gaelic lemmas using parallel English translations.

Read the file ${inPath} with the Read tool. Each line is a JSON record with:
- "manx": a Manx sentence
- "english": its English translation
- "tokens": ambiguous tokens from the sentence. Each has "i" (position), "form" (the token), and "candidates" - possible lemma readings, each with "id", "lemma" (dictionary headword), "gloss" (English meanings), "link" (how the reading arises: "self" = the token is this headword's own form; "demutated" = the token may be an initial-mutated form of this headword; other values = paradigm links).

For EVERY token in EVERY record, decide which candidate reading(s) the sentence uses. Work through these rules IN ORDER:

1. DIFFERENT MEANINGS -> pick by context. When candidates are genuinely different words (er "on" vs fer "man/one"; veg "little/nothing" vs beg "small" vs meg "pet lamb"; laa "day" vs slaa "daub"), use the Manx grammar and the English translation to pick the one this sentence uses. This is your main job.

2. SAME WORD, MUTATION PAIR -> pick the radical. Cregeen lists some mutated spellings as their own headwords, so a token like "aarkey" offers both aarkey (self) and faarkey (demutated) with the same meaning (the sea). When two candidates are really ONE word - the meanings are equivalent and the spellings differ by an initial mutation (f- lost: aarkey/faarkey, eer/feer; c-/ch-: chrogh/crogh, chast/cast; g-/gh-: ghlass/glass; t-/h-: haaue/taaue; s-/h-: hoilshee/soilshee) - the lemma convention is the RADICAL (unmutated) candidate, usually the "demutated" link. Choose it, not the mutated-spelling self entry.

3. SAME WORD, SUFFIX OR SPELLING VARIANT -> pick the token's own form. When candidates are the same word differing only in suffix or spelling WITHOUT an initial mutation (jeeig/jeeg, firriney/firrin, ynsee/yns, kionnee/kionn, taaue/taau), keep the candidate matching the token's own spelling (normally the "self" entry).

4. SAME LEXEME VIA PARADIGM -> include both. If a form's own entry and its paradigm root are both true of this token (ta + bee "to be"), include both ids.

5. "confidence": "high" when the translation or grammar clearly settles it; "low" when fairly sure but indirect; "unsure" when you cannot decide. NEVER guess: a wrong resolution is harmful, an "unsure" is free. If NO candidate's meaning fits the sentence, answer "unsure" - do not pick the least-bad one. Choosing ALL candidates is not a resolution - use "unsure" instead.

Then use the Write tool to create ${outPath} containing ONLY JSONL - one line per token, no commentary, no markdown fences:
{"key":"<record key>","i":<token i>,"chosenIds":["<id>"],"confidence":"high"}
For unsure tokens: {"key":"...","i":...,"chosenIds":[],"confidence":"unsure"}
Every token in the input must have exactly one verdict line - do not skip any.

Your final message: one line, "N verdicts written, M unsure".`

log(`${FILES.length} sonnet adjudication agents (round 3, convention-taught)`)
const results = await parallel(FILES.map(n => () =>
  agent(prompt(`${IN_DIR}/eval-requests-${n}.jsonl`, `${OUT_DIR}/eval-verdicts-sonnet3-${n}.jsonl`), {
    label: `sonnet3:${n}`,
    model: 'sonnet',
    phase: 'Adjudicate',
  })))
const failed = results.filter(r => r === null).length
return `${results.length - failed}/${results.length} agents completed${failed ? `, ${failed} failed` : ''}`