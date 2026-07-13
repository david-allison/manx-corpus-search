export const meta = {
  name: 'lemma-adjudication-pilot2',
  description: 'Pilot round 2: sonnet on the cleaned eval pool (gap/split forms excluded)',
  phases: [
    { title: 'Adjudicate', detail: '15 eval files, sonnet' },
  ],
}

const IN_DIR = '/Users/davidallison/Work/manx-lemma-data/work'
const OUT_DIR = '/private/tmp/claude-501/-Users-davidallison-StudioProjects-manx-corpus-search/4f62ea7c-baae-4ce6-b7df-6d787ee79242/scratchpad/verdicts'

const FILES = args.files

const prompt = (inPath, outPath) => `You are disambiguating Manx Gaelic lemmas using parallel English translations.

Read the file ${inPath} with the Read tool. Each line is a JSON record with:
- "manx": a Manx sentence
- "english": its English translation
- "tokens": ambiguous tokens from the sentence. Each has "i" (position), "form" (the token), and "candidates" - possible lemma readings, each with "id", "lemma" (dictionary headword), "gloss" (English meanings), "link" (how the reading arises: "self" = the token is this headword's own form; "demutated" = a guess that the token is an initial-mutated form of this headword; "particle"/"inflected"/"plural"/"irregular"/"mutation" etc. = paradigm links).

For EVERY token in EVERY record, decide which candidate reading(s) the sentence actually uses, from Manx grammar and the English translation:
- Usually exactly one candidate is right. Example: "er" in a line whose English says "on/upon" is the preposition er, NOT fer ("man, one"); but if the English says "one of them", fer is right.
- Initial mutations are real: a "demutated" candidate is correct when the token genuinely is a lenited/eclipsed form of that headword here (e.g. "cheau" meaning threw -> ceau; "chree" meaning heart -> cree).
- If two candidates are the same lexeme reached different ways (a form's own entry plus its verb/paradigm root, e.g. ta + bee "to be"), and both are true of this token, include both ids.
- "confidence": "high" when the translation or grammar clearly settles it; "low" when fairly sure but indirect; "unsure" when you cannot decide. NEVER guess: a wrong resolution is harmful, an "unsure" is free. If NO candidate's meaning fits the sentence, the answer is "unsure" - do not pick the least-bad one. Choosing ALL candidates is not a resolution - use "unsure" instead.

Then use the Write tool to create ${outPath} containing ONLY JSONL - one line per token, no commentary, no markdown fences:
{"key":"<record key>","i":<token i>,"chosenIds":["<id>"],"confidence":"high"}
For unsure tokens: {"key":"...","i":...,"chosenIds":[],"confidence":"unsure"}
Every token in the input must have exactly one verdict line - do not skip any.

Your final message: one line, "N verdicts written, M unsure".`

log(`${FILES.length} sonnet adjudication agents`)
const results = await parallel(FILES.map(n => () =>
  agent(prompt(`${IN_DIR}/eval-requests-${n}.jsonl`, `${OUT_DIR}/eval-verdicts-sonnet2-${n}.jsonl`), {
    label: `sonnet2:${n}`,
    model: 'sonnet',
    phase: 'Adjudicate',
  })))
const failed = results.filter(r => r === null).length
return `${results.length - failed}/${results.length} agents completed${failed ? `, ${failed} failed` : ''}`