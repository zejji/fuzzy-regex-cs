---
slice: S52d
phase: 6
title: A reversed partial match runs out of text at the slice start - ledger 24 fixed here on the owner's ruling
delivers: []
---

# S52d - Reversed partials honour the slice start

Owner ruling, 2026-09-15, Option B of `docs/plan/upstream-reports/ledger-24-briefing.md`: for a
reversed match asked with `partial=True`, the engine has run out of text when it reaches `pos`
(our `beginning`), and it reports a partial match there. Nothing else about `pos` changes: `^` and
`\A` still refuse a non-zero `pos`, lookbehind and `\b` still see the character before it, exactly
as Python `re` documents and the ported suite asserts. Only the partial run-out question moves from
the whole-string start to the slice start.

This is spec amendment 16 outcome (c): upstream contradicts itself (its node handlers read
`text_start`, its optimiser and reversed string helpers read `slice_start`) and this port
inherited both rules. Fixed here, recorded in ledger entry 24 with the mechanism already at the
line, nothing filed until Phase 8.

## Before launch (orchestrator)

S52c has landed, so the metamorphic invariant checker exists and can prove this fix over a whole
wave rather than over the hand-built grid.

## Scope

1. **Red first, from the grid.** `tools/probes/port-reversed-partial-ignores-the-slice-start.ps1`
   has 33 cells; write them as gap tests with the Option B answer as the expectation, each
   assertion carrying its provenance (the briefing's ruling, and where upstream's Rule B path
   already gives the answer, the upstream run). Run: the cells where the port follows Rule A go
   red. Provenance comments name the rule, not the port's current output.
2. **The nine sites.** Every `TextStart` read in `Engine/Matcher.cs` that sits under a partial
   check (`:967`, `:3273`, `:5788`, `:6644`, `:6705`, `:6787`, `:7393`, `:7458`, `:7556` at the
   time of the ledger; re-locate them, the file has moved) asks `SliceStart` instead. Reads of
   `TextStart` that serve `^`, `\A`, `\b`, `\B` and lookbehind are NOT touched; list them in the
   notes to prove they were seen and left. Upstream's `text_start`/`slice_start` comment block
   (`_regex.c:18435-18446`) is quoted at the port's equivalent with the ruling.
3. **The port's second rule.** Ledger 24 records that the port also answers a partial at `pos` on
   some shapes, so it carries an equivalent of upstream's optimiser bound. Find it, confirm it now
   agrees with the nine sites, and make the two paths share one helper so the question is asked
   in one place (`RanOutOnTheLeft(state, textPos)` or similar), which is what stops the two rules
   diverging again.
4. **The whole wave, not the grid.** Run the default wave at three seeds and 99991 with S52c's
   checker on: the greedy-versus-lazy and minimum-width invariants must report no violation on the
   port side for reversed partial rows, and any row where upstream's Rule A answer now differs from
   the port is classified into one pin, `reversed-partial-runs-out-at-the-slice-start`, whose
   predicate is the mechanism (a reversed pattern, `partial=True`, a non-zero `beginning`, upstream
   answering None where the port answers a partial positioned at `beginning`) and nothing wider.
   Gate row 104366 is the worked example; its answer is recorded.
5. **Records.** Ledger 24 rewritten: ruled, fixed here, the sites, the pin, `Reproduce:` naming
   both probes. `docs/DIVERGENCES.md` gains a Behaviour row (SHIPPED, this slice) with the
   one-sentence rule and the upstream behaviour a user cannot get back. DECISIONS entry. PORTMAP's
   rows for the nine sites note the deliberate departure from upstream's `text_start`.

## Verification

- Ratchet GREEN; the 33 grid tests green with red-first evidence; waves at three seeds and 99991
  with the checker on and no port-side violation of the two invariants; upstream's own 72
  `partial=True` tests still green in the ported suite (none passes a `pos`, so none may move).
- Blind review (hunt: a `TextStart` read changed that served `\b` or lookbehind; a pin predicate
  that would also classify a forward partial or a non-partial reversed row), then the verifier
  pass re-running both probes and the wave summary.

## Done when

- [ ] Grid tests red first then green; nine sites changed; untouched `TextStart` reads listed.
- [ ] One helper for the run-out question; the port's second rule found and unified.
- [ ] Wave green with S52c's checker; one narrow pin; row 104366 recorded.
- [ ] Ledger 24, DIVERGENCES, DECISIONS, PORTMAP updated.
- [ ] Ratchet GREEN, blind review, verifier, commit.
