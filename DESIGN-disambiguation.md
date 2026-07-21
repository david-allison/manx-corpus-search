# Lemma and sense disambiguation — design

Written 2026-07-21, from the moddey/voddey bug (`/dictionary/moddey` walk asserting
1707 Coyrle Sodjey — a *foddey* line — as the dog's first text, against the first-seen
band's 1730). Sizing and baseline: `ANALYSIS-lemma-ambiguity.md`. Harness method:
`CorpusSearch.Test/LemmaAdjudication/METHOD.md`.

## The bug's causal chain

1. `voddey` legitimately has two lexeme candidates, `moddey.n` and `foddey.a`
   (cregeen.tsv:25505-25506; corpus contexts split ~30 dog / ~20 foddey).
2. The UD seed generator minted `voddey → moddey.n` from just 3 treebank sentences,
   all of which happened to be dogs (`lemma.overrides.seed.tsv:125`) —
   decisive-in-sample, wrong-in-language.
3. The seed row **poisoned the sidecar**: the corpus LLM run was exported with the
   seed as its overrides layer (`METHOD.md:236`), and `AdjudicationExporter.Adjudicable`
   excludes any form the overrides claim — so `voddey`'s 52 occurrences were never
   adjudicated per-line. Zero sidecar rows.
4. With nothing narrowing it, the walk's `SpanOr` over `manx_lemma` counts every
   `voddey` for the dog, and `DictionaryAttestationService` asserts the earliest
   document with no concept of uncertainty — while `FirstAttestation` (per-form,
   sharedness-aware) correctly hedges. The page contradicts itself.

Note the runtime resolver is NOT empty: `lemma.sidecar.tsv` (15,852 rows) is adopted
and live (csproj link + `tools/init.sh`, prod tracks manx-lemma-data HEAD daily).
What has never been adopted is the form-level `lemma.overrides.tsv`.

## Design principles

1. **Every claim carries its evidence tier.** A token is *sure* (single candidate, or
   resolved by override/sidecar), *ambiguous* (multiple lexemes possible), or
   *fallback* (no lexeme known). Aggregates keep the split; UI asserts sure evidence
   and *offers* ambiguous evidence with the existing `*` grammar from `FirstAttestation`.
2. **Form-level overrides are claims about the language, not about a sample.** Only
   safe when the form is form-deterministic (suppletives: `haink → tar.v`) or when
   every corpus occurrence has been individually validated. A cross-lexeme mutation
   collision (`voddey`, `voir`, `vlaa`, …) can never be adopted from treebank
   majorities alone.
3. **Seeds are hypotheses, not ground truth.** An unadopted seed must never gate what
   the finer layer examines. Pool exclusion keys on *adopted* overrides only; adopted
   overrides deserve periodic re-validation as the corpus grows.
4. **Senses are a display refinement, not a recall mechanism.** Lemma resolution
   decides what the search index says; sense resolution decides what the page shows.
   Sense artifacts never touch `manx_lemma` — popup/display tier only.

## Phase 0 — Data triage (fixes moddey; do first)

- **0a.** Delete `voddey moddey.n 3/3` from `lemma.overrides.seed.tsv`. Audit the other
  rows: flag every row whose form's table candidates span >1 display lemma AND whose
  UD evidence denominator is < 10 (`vlaa` 3/3, `voir` 5/6, `varr`, `voalley` 9/9 are
  the voddey failure mode; suppletives pass). Gate lives beside the generator so it
  runs on every regeneration.
- **0b.** Fix pool poisoning: `AdjudicationExporter` excludes only *adopted* overrides;
  seed rows get adjudicated per-line like any other form, and the importer compares
  verdicts against the seed — unanimous agreement upgrades the row's evidence,
  disagreement kills it and keeps the per-line rows.
- **0c.** Adjudicate `voddey`'s 52 occurrences with the existing workflow machinery
  (line + parallel English + candidate glosses; left context nearly splits it).
  Lines where neither candidate fits (`nagh voddey` is plausibly the verb *fod*) are
  table gaps — record via cregeen-nvh sidecar NVH, never force a wrong candidate.
- **0d.** Re-key, commit, submodule bump. Prod picks it up on the next daily restart;
  the moddey walk's earliest document becomes 1730 with no code change.

## Phase 1 — Walk honesty (no index change)

- **1a.** Per-line shared marker in `InDocument`: recover the form from the highlight;
  if `DisplayLemmasFor(form).Count > 1` and no sidecar row resolves it to the walked
  lemma, stamp `uncertain: true`. Client renders `FirstAttestation`'s `SharedMark`
  asterisk with the tooltip naming the other lexeme.
- **1b.** Document-row hedging by cross-reference: any walk document strictly older
  than the band's asserted year is by definition resting on shared spellings only —
  grey + `*` + "possibly"; the summary asserts the sure range ("37 texts, 1730–2026 ·
  possibly from 1707*"). Restores walk ⇔ band consistency presentationally.
- **1c.** Name the invisible reading: render the alternative display lemma on
  uncertain lines ("or *foddey*?*"); add `sharedWithOtherLemmas` to `LemmaTreeForm`
  so the word-family tree can mark "voddey ×52*".

## Phase 2 — First-ever overrides adoption, behind a real gate

High-leverage (seed alone resolves 55.8% of ambiguous mass), high-risk (prod tracks
HEAD daily; `OverrideFor` short-circuits the sidecar at index AND popup time).

- **Adoption rule.** A row enters `lemma.overrides.tsv` only if: single-display-lemma
  candidates (class picks — always safe), OR form-deterministic suppletion, OR
  cross-lexeme WITH full-corpus per-line validation unanimous at ≥10 occurrences.
  Everything else stays per-line. `.candidates` rows (≥10 LLM-unanimous by
  construction) pass once human-skimmed.
- **Data CI in manx-lemma-data** (no deploy step exists between repo and prod):
  override ids strict non-empty subset of table candidates (mirror of the runtime
  drop); no cross-lexeme row without a `validated` tag; sidecar keys well-formed.
- **Re-validation hook.** New corpus lines containing overridden forms are
  unvalidated by definition; the exporter's next run reports disagreements, which
  demote back to sidecar.

## Phase 3 — Certainty in the index; one source of truth for "first seen"

- **3a. `manx_lemma_sure` field.** At index time, when a token's final id set maps to
  a **single display lemma**, write its ids there too. `{jaagh.n, jaagh.v}` is sure at
  the lexeme level; `{moddey.n, foddey.a}` is not. ~63% of covered tokens; no Lucene
  surgery; positions preserved.
- **3b. `ScanLemma` returns `{sure, possible}`** per document. The walk asserts the
  earliest sure document (replacing 1b's heuristic with ground truth); sure counts
  are exact (singletons can't double-count); counts can read "×30 (+22 shared)".
- **3c. Converge band and walk.** FirstSeen = earliest sure hit (form recovered from
  the highlight) + earliest possible hit as the "Possibly … *" trailer. History keeps
  per-form scans for the All-forms table and decade chart. The walk ⇔ band invariant
  becomes a one-line property test.

## Phase 4 — The sense layer

- **Sense inventory** (generated in cregeen-nvh, vendored as `senses.tsv`):
  `senseId, lemmaId, dict, entryPath, glossSnippet`; `senseId = <lemmaId>#<n>`
  (`foddey.a#1` "far, distance" / `foddey.a#2` "long, of time"). Minted only where an
  entry's printed senses are discriminable (disjoint glosses). Entry refs = headword
  + book-order ordinal (file order is book order). Curation in sidecar NVH, never in
  cregeen.nvh.
- **Sense sidecar** (`sense.sidecar.tsv`): identical schema/keying to the lemma
  sidecar, same rekeyer (generalized), same narrow-only rule (senseIds must belong to
  the token's resolved lemmaId).
- **Adjudication = same harness, one layer deeper.** Prompt already carries line,
  English, candidate glosses; sense candidates = the entry's sense glosses. Only for
  tokens whose lemma is already sure; popup tier ONLY — a wrong sense verdict can
  mislabel a gloss but never hide a line from search.
- **UI, incrementally:** popup leads with the resolved sense's gloss; walk lines carry
  a sense tag ("*foddey* — of time"); eventually per-sense first-seen rows replace the
  blunt "covers more than one sense (n, v)" warning.

## Hardening the adjudication

- Production lemma verdicts are single-annotator (`METHOD.md:270`): for `tier=index`
  promotions, re-run disagreement-prone categories with 3 votes, require 2/3;
  splits stay `popup`.
- The equivalence layer is single-pass unreviewed (`METHOD.md:263-266`) and sits
  upstream of everything: one adversarial re-check pass over its `same` verdicts.
- `humanVerified` is 0 on all 15,852 rows. The walk's `*` marks double as a review
  funnel: a "report this reading" affordance feeds the queue with the rows readers
  actually see.

## Invariants and tests

1. **Walk ⇔ band:** asserted first year identical for every headword (property test;
   regression pin: moddey asserts 1730, offers 1707*).
2. **No unmarked ambiguity in first position** (AttestationWalker component test).
3. **Loader/CI parity:** data CI rejects exactly what `LemmaResolver.Load` drops.
4. **Gate tests:** adoption script refuses a voddey-shaped row (cross-lexeme, 3 obs),
   accepts a haink-shaped one.
5. **Progress metric:** rerun `CorpusAmbiguityAnalysis` per phase; baseline 33.07%
   wrong-reading postings.

## Ordering rationale

Phase 0 kills the reported bug in one submodule bump. Phase 1 makes the class of bug
visible-but-honest without touching the index. Phase 2 is the biggest quality win,
blocked only by the missing gate. Phase 3 removes the over-count/compensation debt
and unifies the page's contradictory claims at the source. Phase 4 reuses every piece
of the pipeline one granularity down — lemma and sense as the same problem at
different resolutions.
