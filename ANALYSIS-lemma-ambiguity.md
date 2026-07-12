# Lemma ambiguity over the corpus — sizing for translation-based disambiguation

Follow-up unlocked by the UD agreement eval (HANDOFF-ud-eval.md §Follow-up).
Measured 2026-07-12 by `CorpusSearch.Test/CorpusAmbiguityAnalysis.cs` (an
`[Explicit]` harness; rerun with
`dotnet test CorpusSearch.Test --filter FullyQualifiedName~CorpusAmbiguityAnalysis --logger "console;verbosity=detailed"`).

Pipeline mirrors `manx_lemma` indexing exactly: gv lines only, `NormalizedManx`,
uncased `ManxTokenizer`+`ManxTokenFilter`, direct table lookup with clitic
fallback. Corpus = OpenData + ClosedData (808 documents). Table =
manx-lemma-data `a3ae0a9`; treebank = UD_Manx-Cadhan `9069716`.

## Headline numbers

| Metric | Value |
|---|---|
| gv tokens | 2,113,793 |
| covered by the table | 1,810,891 (85.67%; 31,555 via clitic fallback) |
| ambiguous (≥2 candidates) | 666,394 tokens — 36.80% of covered — across only **3,328 forms** |
| lemma-id postings | 2,705,660 |
| **wrong-reading postings** (≤1 candidate right per token) | **894,769 = 33.07% of the manx_lemma field** |
| mean candidates per covered token | 1.494 |
| ambiguous occurrences on lines with an English translation | 613,096 = **92.0%** (53,298 without) |

Histogram (token-weighted): 0 candidates 14.33%, 1 → 54.14%, 2 → 22.22%,
3 → 7.83%, 4 → 1.44%, 5 → 0.03%.

## The problem is very concentrated

Cumulative share of ambiguous-token mass:

| top N ambiguous forms | share |
|---|---|
| 10 | 37.6% |
| 25 | 52.0% |
| 50 | 63.0% |
| 100 | 73.7% |
| 200 | 83.1% |
| 500 | 93.3% |
| 1000 | 98.0% |

## The treebank already resolves most of the head

- 663 of the 3,328 ambiguous forms have UD evidence, covering **82.9%** of
  ambiguous mass.
- **246 forms are decisive by UD majority (≥3 observations, ≥80% one reading)
  and alone cover 65.4% of ambiguous mass.** A `lemma.overrides.tsv` seeded
  automatically from treebank majorities resolves two-thirds of the noise
  before any human or translation-based step runs.

Reading classes visible in the top-30 (corpus freq, candidates, UD readings):

1. **Near-deterministic** — a default reading wins essentially always:
   `ta`→bee×437, `eh`→eh×2262 (e.x never), `va`/`vel`/`row`→bee, `ren`→jean×168,
   `dooyrt`→abbyr×41, `haink`→tar×92, `dooinney`→dooinney×20 (deiney never).
   These are one-line overrides.
2. **Genuinely split** — needs context (this is where translation-based
   disambiguation earns its place): `ny` ny×255/yn×179 (58/42, 53.8k tokens),
   `y` yn×352/y×38, `hug` cur×137/hug×13, `hie` gow×40/thie×5, `nee` jean×23/she×6.
3. **Mutation candidates** — the additive demutation design (`chur`[chur.v,cur.v],
   `vod`[vod.x,fod.v], `veih`[meih.n,veih.x]); UD usually picks one side
   decisively, so most fall into class 1.

## Sizing conclusion

A precomputed sidecar in manx-lemma-data is small and high-yield:

- **Seed ~250 rows from treebank majorities → 65% of noise gone.**
- Human skim of the remaining top ~200–500 forms (many are class-1 lookalikes
  with <3 UD observations) → ~85–93% addressable by a file of a few hundred rows.
- Translation-based disambiguation is then the refiner for the genuinely split
  function words (`ny`, `y`, `hug`, `hie`, `nee` ≈ 12% of ambiguous mass on
  their own) and the ~17% tail with no UD evidence — not the workhorse.
- Corpus coverage (85.7%) is lower than treebank coverage (88.9%): the gap is
  names, older orthography and typos; a separate concern from ambiguity.

Caveat: "≤1 candidate right per token" slightly overstates noise for clitic
contractions (both parts' readings are legitimate); they are 31.6k tokens
(1.7% of covered), so the headline barely moves.

## Generated artifacts (2026-07-12)

`CorpusSearch.Test/LemmaSidecarGenerator.cs` (same run pattern as the analysis
harness; `LEMMA_SIDECAR_OUT` sets the output directory) wrote to the
manx-lemma-data checkout:

- **`lemma.overrides.seed.tsv`** — 171 form-level resolutions from decisive UD
  majorities, resolving **55.8%** of ambiguous token mass (e.g. `eh` 2262/2262,
  `ta` 437/437). 76 further decisive forms were skipped because the UD majority
  reading is not among the table's candidates (these are the agreement eval's
  disagreement cases — fix them in the table, not here). High confidence;
  human-skim then adopt as `lemma.overrides.tsv`.
- **`lemma.sidecar.tsv`** — 28,458 per-occurrence resolutions
  (manifest `Ident`, SHA-256[..16] of the raw Manx cell, token index → lemma ids),
  gloss-scored against each line's English cell (manx.json glosses,
  discriminative words only, common words >5% of lines excluded, candidates
  with overlapping glosses never adjudicated, UD-attested readings never
  dropped). Resolves a further 4.5% of ambiguous mass.
  **Measured precision on held-out UD text_en: 86.7% (39/45)** — stamped in
  the file header. EXPERIMENTAL: a wrong row hides a true reading from lemma
  search, so apply behind a flag or after review. Residual failures are
  initial-mutation variant pairs both meaning the same thing
  (`rank`/`frank`, `cheaghil`/`ceaghil`), where gloss evidence is
  definitionally uninformative.
- 39.7% of ambiguous mass stays fully ambiguous (safe default — mostly the
  genuinely split function words `ny`/`y`/`hug` plus the same-lexeme variant
  pairs above).

Consumer application (index-time: overrides → sidecar → all candidates) is
the follow-up code change in manx-corpus-search.
