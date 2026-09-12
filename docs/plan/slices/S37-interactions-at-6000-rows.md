---
slice: S37
phase: 5
title: Judge the thirteen composed-wave rows and make interactions green at 6000 rows
delivers: []
---

# S37 - The composed wave at 6000 rows

Phase 4's one deliberately unfinished item, placed first because Phase 5 will widen the same
`interactions` generator with fuzzy constraints (S43), and a generator that is red before the
widening cannot tell a new divergence from an old one. No engine work is expected: S36 measured
that every one of the thirteen rows belongs to a family somebody has already judged.

## Scope

- **The thirteen rows.** `interactions` at 6000 rows is red at all five seeds S36 tried
  (the seeds are in S36's closing notes; the default three are 7, 4242 and the run date). S36's table: 9 rows are a group call inside a
  lookaround running the other way from the pattern (S30's pinned KNOWN DIVERGENCE, upstream loses
  matches this port finds; confirmed NOT fixed by upstream issue 614); 3 are `(*SKIP)` with
  `partial=True`, where upstream's `search_start` partial arms start the partial earlier than the
  slow path does (the `search-start-partial` family, in a shape its predicate does not reach); 1 is
  `(?r)BB([^\d]??)` over `' B.\r.'`, a bounded lazy repeat losing its partial
  (`bounded-lazy-repeat-partial`, keyed on individually judged rows).
- **Judge each row the ordinary way**: reproduce it in isolation against upstream (probe under
  `tools/probes/`), confirm which side is right against the research already on file
  (`docs/plan/2026-09-12-divergence-research.md`, PCRE2 10.47 via ctypes where the construct
  exists there), and pin the answer. A row that turns out NOT to be the family S36 assigned it to is
  the finding this slice exists for: minimise it and, if it is this port's, fix it.
- **Widen or add entries** in `tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs`. Every entry
  stays strict: a predicate that names the shape, and a remark that quotes the probe. The
  staleness alarm `Every_expected_divergence_still_diverges` must still pass, so an entry that
  matches nothing at three seeds is wrong.
- **Two shortcuts are already known to fail** and are not to be retried: recording `interactions`
  prefilter-free (removes one row, adds another, leaves the three `(*SKIP)`-plus-partial rows
  unchanged, because those are `search_start` and it is not reachable from Python), and
  retargeting the thin controls at `interactions` before the wave is green.

## Verification

- `pwsh -File tools/run-oracle.ps1 -Generator interactions -Rows 6000` GREEN at the three default
  seeds, and additionally at 99991. Then the default wave (`pwsh -File tools/run-oracle.ps1`) still
  GREEN, so no widened entry has swallowed a row it should not.
- Negative controls in `tools/controls.json`: one per new or widened entry, each a mutation of the
  port that the entry's predicate must NOT excuse (for example: make the lookaround-direction group
  call agree with upstream, and the S30 pinned test must go red). Record with
  `python tools/run-controls.py --slices S37 --seeds 2`.
- The five thin-or-dead Phase 4 controls S36 recorded: re-run at `interactions` now that it is green
  and record whether they came to life. Do not retarget them; just measure.

## Done when

- [ ] `interactions` GREEN at 6000 rows at four seeds; the default wave GREEN at three.
- [ ] Every new or widened entry has a quoted probe, a permanent pinned test where the port is
      right, and a negative control that fires.
- [ ] Any row that is this port's bug is fixed, test first, and named in the closing notes.
- [ ] Ratchet GREEN, baseline updated only if a test was added, blind review (hunt: an entry
      predicate wider than its remark; a control that passes because the wave is red for another
      reason), commit.
