# Translation-based lemma disambiguation: method and experimental record

Run 2026-07-12/13. This documents the LLM adjudication step that produced
`lemma.sidecar.tsv` v2 and `lemma.equivalences.seed.tsv` in manx-lemma-data:
the pipeline, the verbatim prompts, every pilot round with its numbers, the
error taxonomy that reshaped the design, and threats to validity. Companion
docs: `ANALYSIS-lemma-ambiguity.md` (sizing), `SIZING-lemma-disambiguation.md`
(pool decomposition), `LEMMAS.md` in cregeen-nvh (table conventions).

## 1. Task and data

**Task.** The lemma table (cregeen.tsv, generated from Cregeen's 1835
dictionary) is additive: a surface form maps to every candidate lemma,
including initial-mutation guesses. 28.2% of covered corpus tokens carry ≥2
display-distinct readings. The sidecar assigns per-occurrence resolutions
using each line's existing English translation (a hard constraint: no
machine translation — 95.2% of corpus lines are already parallel).

**Corpus.** 808 documents, 2.09M Manx tokens (OpenData + ClosedData),
88,309 translated lines. **Gold.** UD_Manx-Cadhan (pinned submodule):
2,336 sentences, 2,110 with `text_en`; lemma comparison via normalized
display keys (case-folded, hyphen/space-collapsed).

**Compute.** No API access: all LLM calls ran as Claude Code workflow
subagents (model aliases `haiku` and `sonnet`, July 2026), reading request
JSONL files and writing verdict JSONL files. A C# harness
(`AdjudicationExporter`/`AdjudicationImporter`) owns everything
deterministic: pool construction, layer application, scoring, emit.

## 2. Layered architecture (final form)

Ambiguity is routed to the cheapest layer that can decide it:

1. **Overrides layer** (form-level, UD-decisive majorities ≥3 obs ≥80%):
   125 forms ≈ 59.3% of ambiguous token mass. No LLM.
2. **Un-adjudicable exclusions** (stay fully ambiguous):
   *table gaps* — any UD-attested reading absent from the candidate set
   (`ching→kione`, `nee→she`; the veto cannot protect a reading the table
   does not offer); *convention splits* — UD itself is inconsistent
   (`ny` as DET: lemma `ny` 185×, `yn` 179× — ungradeable).
3. **Equivalence layer** (the layer over Cregeen; this session's central
   design change): candidate id pairs classified once, form-level, as
   *same lexeme* (mutation/spelling/suffix variants: `aarkey/faarkey`,
   `jeeg/jeeig`) vs *distinct words* (`er/fer`). Same-lexeme ids collapse
   into one group (ids sharing a display headword group implicitly);
   within-lexeme direction is a lemmatization convention, decided in the
   layer, never per line. Tokens with <2 groups leave the pool.
4. **Per-line LLM adjudication** (the sidecar): only true cross-lexeme
   homographs — the class where the parallel translation genuinely
   discriminates.

## 3. Scoring: what "precision" must mean

Two measurement corrections mattered more than any prompt change:

- **Production resolution semantics.** A verdict counts as a resolution
  only if it is a strict, known subset of the candidates. Choose-all
  verdicts are non-resolutions (round-2 naive scoring had counted them,
  inflating precision to 96.9%).
- **Discriminating tokens only.** Single-display tokens (all candidates
  share one headword spelling, e.g. `vod → fod.x/fod.v`) auto-score
  correct whatever is chosen, because gold is display-keyed. They were
  >80% of resolutions. The gate metric is precision on multi-display
  tokens. (These tokens also have no effect on the current popup or query
  path, so they were later removed from the pool entirely by the
  display-grouping rule.)

An attestation **veto** ("never drop a UD-attested reading") was ported
from the gloss-scorer prototype and then retired with evidence: at form
level it blocks 72% of resolutions — precisely the context-split forms
per-occurrence resolution exists for — and a per-form deterministic
variant fires zero times, because UD-deterministic forms already live in
the overrides layer.

## 4. Experimental record

Gate: ≥97% precision on discriminating tokens (baseline: 86.7% gloss
scorer, 39/45; oracle ceiling 99.4%).

| Round | Config | Pool (tokens) | Naive precision | Production, discriminating | Notes |
|---|---|---|---|---|---|
| 1 | haiku, prompt A | 1,511 | 84.4% (1229/1456) | ~85% winnable | 36 agents, 3.29M tok (with sonnet arm) |
| 1 | sonnet, prompt A | 1,511 | 90.2% (1347/1493) | — | `ny` alone = 58% of errors |
| 2 | sonnet, prompt B | 1,073¹ | 97.8% (916/937) | **86.6%** (136/157) | high-conf 86.8% (131/151) |
| 3 | sonnet, prompt C | 1,073 | 98.0% (953/972) | **89.8%** (167/186) | convention-taught; high-conf 90.1% |
| 3′ | round-3 verdicts re-scored on grouped pool | 71² | — | **100.0%** (70/70) | held-out test 32/32; coverage 98.6% |

¹ after excluding un-adjudicable forms (gap ≥1 obs + `ny`).
² after the equivalence layer: true cross-lexeme homographs only.

**Error taxonomy (what each round taught):**

- Round 1 → errors were structural, not judgment: `ny` (UD-internal
  convention split) + 45 unwinnable tokens (gold not a candidate) →
  exclusion rules 1–2.
- Round 2 → residual errors were within-lexeme direction calls:
  mutation pairs where UD wants the radical (`aarkey→faarkey`,
  `chast→cast`) and suffix/spelling variants where UD keeps the token's
  own form (`jeeig`, `firriney`, `ynsee`, `taaue`). Gloss-overlap cannot
  detect these (same word, disjoint gloss vocabulary: *sea* vs *billow,
  wave*).
- Round 3 → convention teaching helped (+3.2pp) but cannot finish the
  class: direction is a convention, not a per-line fact → the
  equivalence layer, after which the class cannot reach a verdict at all.

**Pair classification.** 1,425 cross-display candidate pairs (from the
full pool), 10 sonnet agents, 1.38M tokens: 995 same-lexeme / 418
distinct / 12 unsure. Output = `lemma.equivalences.seed.tsv`
(skim-then-adopt; destination cregeen-nvh beside `lemma-overrides.nvh` /
`lemma-paradigms.nvh`).

**Corpus run.** Grouped pool: 22,610 tokens on 17,205 unique translated
lines (deduplicated by a hash of the normalized token stream; 175,386 →
22,610 after all layers). 144 sonnet agents, 18.8M tokens, ~3.5h,
zero failures. 22,609 verdicts → 22,055 resolutions (97.5%): 21,887
index-tier (high confidence), 168 popup-tier (low), 554 unsure.
87 forms resolved LLM-unanimously across ≥10 occurrences each →
`lemma.overrides.candidates.tsv` (form-level promotion candidates).

**Spot outcomes.** All 1,613 `veg` lines resolved to the veg/beg group
(`meg` "pet lamb" eliminated); `voddagh` → fod/foddagh (`moddagh`
"doggish" eliminated); `er` absent by design (owned by the overrides
layer); `laa/slaa` classified distinct but `laa` owned by the overrides
layer.

**Total LLM spend:** ~27.8M subagent tokens across 219 agents in five
workflows (pilots 7.6M, pairs 1.4M, corpus 18.8M).

## 5. Prompts (verbatim)

Placeholders: `${inPath}`/`${outPath}` are absolute JSONL paths given per
agent. One agent per file; agents used Read/Write plus optional read-only
exploration (some grepped the lemma table before deciding).

### 5.1 Prompt A — pilot round 1 (haiku + sonnet)

```
You are disambiguating Manx Gaelic lemmas using parallel English translations.

Read the file ${inPath} with the Read tool. Each line is a JSON record with:
- "manx": a Manx sentence
- "english": its English translation
- "tokens": ambiguous tokens from the sentence. Each has "i" (position), "form" (the token), and "candidates" - possible lemma readings, each with "id", "lemma" (dictionary headword), "gloss" (English meanings), "link" (how the reading arises: "self" = the token is this headword's own form; "demutated" = a guess that the token is an initial-mutated form of this headword; "particle"/"inflected"/"plural"/"irregular"/"mutation" etc. = paradigm links).

For EVERY token in EVERY record, decide which candidate reading(s) the sentence actually uses, from Manx grammar and the English translation:
- Usually exactly one candidate is right. Example: "er" in a line whose English says "on/upon" is the preposition er, NOT fer ("man, one"); but if the English says "one of them", fer is right.
- Initial mutations are real: a "demutated" candidate is correct when the token genuinely is a lenited/eclipsed form of that headword here (e.g. "cheau" meaning threw -> ceau; "chree" meaning heart -> cree).
- If two candidates are the same lexeme reached different ways (a form's own entry plus its verb/paradigm root, e.g. ta + bee "to be"), and both are true of this token, include both ids.
- "confidence": "high" when the translation or grammar clearly settles it; "low" when fairly sure but indirect; "unsure" when you cannot decide. NEVER guess: a wrong resolution is harmful, an "unsure" is free. Choosing ALL candidates is not a resolution - use "unsure" instead.

Then use the Write tool to create ${outPath} containing ONLY JSONL - one line per token, no commentary, no markdown fences:
{"key":"<record key>","i":<token i>,"chosenIds":["<id>"],"confidence":"high"}
For unsure tokens: {"key":"...","i":...,"chosenIds":[],"confidence":"unsure"}
Every token in the input must have exactly one verdict line - do not skip any.

Your final message: one line, "N verdicts written, M unsure".
```

### 5.2 Prompt B — pilot round 2 (sonnet)

Identical to Prompt A except the confidence bullet adds one sentence:

```
If NO candidate's meaning fits the sentence, the answer is "unsure" - do not pick the least-bad one.
```

### 5.3 Prompt C — pilot round 3 (sonnet, convention-taught)

The decision section of Prompt B is replaced with ordered rules:

```
For EVERY token in EVERY record, decide which candidate reading(s) the sentence uses. Work through these rules IN ORDER:

1. DIFFERENT MEANINGS -> pick by context. When candidates are genuinely different words (er "on" vs fer "man/one"; veg "little/nothing" vs beg "small" vs meg "pet lamb"; laa "day" vs slaa "daub"), use the Manx grammar and the English translation to pick the one this sentence uses. This is your main job.

2. SAME WORD, MUTATION PAIR -> pick the radical. Cregeen lists some mutated spellings as their own headwords, so a token like "aarkey" offers both aarkey (self) and faarkey (demutated) with the same meaning (the sea). When two candidates are really ONE word - the meanings are equivalent and the spellings differ by an initial mutation (f- lost: aarkey/faarkey, eer/feer; c-/ch-: chrogh/crogh, chast/cast; g-/gh-: ghlass/glass; t-/h-: haaue/taaue; s-/h-: hoilshee/soilshee) - the lemma convention is the RADICAL (unmutated) candidate, usually the "demutated" link. Choose it, not the mutated-spelling self entry.

3. SAME WORD, SUFFIX OR SPELLING VARIANT -> pick the token's own form. When candidates are the same word differing only in suffix or spelling WITHOUT an initial mutation (jeeig/jeeg, firriney/firrin, ynsee/yns, kionnee/kionn, taaue/taau), keep the candidate matching the token's own spelling (normally the "self" entry).

4. SAME LEXEME VIA PARADIGM -> include both. If a form's own entry and its paradigm root are both true of this token (ta + bee "to be"), include both ids.

5. "confidence": "high" when the translation or grammar clearly settles it; "low" when fairly sure but indirect; "unsure" when you cannot decide. NEVER guess: a wrong resolution is harmful, an "unsure" is free. If NO candidate's meaning fits the sentence, answer "unsure" - do not pick the least-bad one. Choosing ALL candidates is not a resolution - use "unsure" instead.
```

### 5.4 Pair classification (sonnet)

```
You are a Manx Gaelic lexicographer classifying pairs of dictionary headwords.

Read the file ${inPath} with the Read tool. Each line is a JSON record: {"pair", "a", "b", "forms", "occurrences"} where "a" and "b" are two dictionary entries (id, lemma = headword, gloss = English meanings, link = how the entry matched: "demutated" marks an initial-mutation guess), and "forms" are surface tokens that can be read as either entry.

For EVERY pair decide:
- "same": a and b are the SAME Manx lexeme. This covers: initial-mutation pairs where one headword is the mutated spelling of the other (f- lost: aarkey/faarkey, eer/feer; c/ch: cast/chast; g/gh: glass/ghlass; t/h: taaue/haaue; s/h: soilshee/hoilshee; b/v: baase/vaaish; d/gh: dreeym/ghreeym; j/y: yannoo/jannoo), spelling variants (jeeg/jeeig), and suffix/verbal-noun variants of one word (yns/ynsee, firrin/firriney, kionn/kionnee, taau/taaue). Meanings equal or near-identical is the strong signal - but glosses of the SAME word can use different English vocabulary (aarkey "sea" / faarkey "billow, wave" are the same word), so judge the lexeme, not the gloss overlap.
- "distinct": different lexemes with different meanings, however similar the spellings (er "on" vs fer "man/one"; laa "day" vs slaa "daub"; veg "nothing" vs beg "small"; jee "god" vs jee imperative of "look").
- "unsure": you cannot tell (e.g. an entry with an empty gloss you cannot identify).

An empty gloss means the dictionary entry has no definition text - judge from the headword shape, the link type, and your knowledge of Manx.

Use the Write tool to create ${outPath} containing ONLY JSONL - one line per pair, no commentary:
{"pair":"<pair value verbatim>","verdict":"same","note":"<up to 8 words of reasoning>"}
Every input pair must have exactly one output line.

Your final message: one line, "N pairs: X same, Y distinct, Z unsure".
```

### 5.5 Corpus adjudication (sonnet, group-aware — the production prompt)

```
You are disambiguating Manx Gaelic lemmas using parallel English translations.

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

Your final message: one line, "N verdicts written, M unsure".
```

## 6. Reproduction

```
# 1. export (pool construction; all layers applied)
LEMMA_ADJUDICATION_DIR=<work> \
LEMMA_OVERRIDES_TSV=<manx-lemma-data>/lemma.overrides.seed.tsv \
LEMMA_EQUIVALENCES_TSV=<manx-lemma-data>/lemma.equivalences.seed.tsv \
  dotnet test CorpusSearch.Test --filter FullyQualifiedName~AdjudicationExporter

# 2. adjudicate: one agent per request file, prompt 5.5 (or 5.4 for pairs),
#    writing verdicts-*.jsonl; any orchestration works - agents are
#    stateless over (file in, file out)

# 3. score + emit
LEMMA_ADJUDICATION_DIR=<work> LEMMA_SIDECAR_OUT=<manx-lemma-data> \
  dotnet test CorpusSearch.Test --filter FullyQualifiedName~AdjudicationImporter
```

## 7. Threats to validity

- **Final eval n = 70.** The grouped pool leaves few gradeable UD tokens;
  100.0% has a wide interval (95% CI lower bound ≈ 95%). It stacks with
  the round-2/3 evidence (~2,500 graded verdicts whose cross-lexeme
  subset showed no misreadings), but corpus-wide precision is estimated,
  not measured.
- **Gold conventions.** UD_Manx-Cadhan is small, genre-skewed, and
  internally inconsistent on at least `ny`; display-keyed comparison
  case-folds real homographs (`jee`/`Jee` ungradeable).
- **Circularity guard is partial.** Exclusion rules were derived from
  eval error analysis (all files), then round 3′/corpus measured on the
  same treebank's held-out test files; the test subset (32/32) is the
  clean number.
- **The equivalence layer is single-pass LLM output** (995 "same" pairs,
  unreviewed at run time). A misclassified "same" pair silently removes a
  real distinction from the pool; the seed awaits human skim, and pairs
  can be flipped and the pipeline re-run cheaply.
- **Agents were not sandboxed to the request files** — some consulted the
  lemma table (read-only) before deciding; the measured config includes
  that behavior. Temperature/instability across reruns is unmeasured
  (each verdict is single-annotator).
- **Coverage asymmetry.** 95.2% of lines have translations; untranslated
  lines (and the `ny`/gap/convention classes) remain fully ambiguous by
  design.
