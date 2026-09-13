---
slice: S46
phase: 6
title: Inherited BESTMATCH and POSIX-fuzzy bugs - ledger entries 12, 13 and the port half of 9
delivers: []
---

# S46 - The inherited `BESTMATCH` family

Three ledger entries the oracle cannot see because this port reproduces upstream faithfully. All
in `Matcher.DoBestFuzzyMatch` and the POSIX save/restore, so one slice. Each is judged to amendment
16's standard before it is fixed, and each fix is a deliberate divergence pinned with an entry.

## Scope

- **Entry 12: `BESTMATCH` loses a match whose best fit needs two trailing insertions.** Measured
  mechanism (S41/S42): the guard at `_regex.c:15515-15517` double-counts, and the second pass climbs
  only to `fewest_errors`. Reproduce; confirm the intended answer from the fuzzy definition and
  from plain `{e<=n}` on the same subject, which finds it; fix; pin; enter.
- **Entry 13: `BESTMATCH` loses a partial match that the same pattern's own `match` finds.** Five
  rows, all `(?b)` plus fuzzy plus `(*SKIP)` plus `partial=True` (`bestmatch-loses-a-partial`).
  Self-refutation judges it, as S40b used: an answer the same engine's anchored door beats is wrong.
  Fix so the port answers what its own `match` answers; the entry flips from "upstream's, port
  agrees" to "upstream's, port right" and its `Example` is re-recorded.
- **Entry 9, port half: `best_fuzzy_counts` AND the changes copy in `SaveBestMatch` /
  `RestoreBestMatch`, both or neither** (S41). Upstream copies the counts without the changes and
  segfaults when `fuzzy_changes` is read on a `(?p)` fuzzy match. Port both together so a POSIX
  fuzzy match reports consistent counts and changes; pin; then lift the `interactions` generator's
  POSIX-with-fuzzy exclusion (DECISIONS) and re-run the wave. Upstream's crash must arrive as a
  recorded `error` or `timeout` row, not a dead recorder; prove that first on one row.
- **Do not touch** the cost ranking (S42) or the `same_match` override (S41).

## Verification

- A gap test per entry asserting the corrected answer with upstream's quoted beside it; a narrow
  divergence entry per fix; a negative control per fix reverting it.
- `fuzzy` with `(?b)` and the default wave GREEN at three seeds; `interactions` with POSIX no
  longer excluded, GREEN.

## Done when

- [ ] Entries 12, 13 and 9's port half fixed test-first, each with its definition quoted.
- [ ] Divergence entries, controls, ledger updates; POSIX exclusion lifted.
- [ ] Ratchet GREEN, blind review (hunt: a fix that changes an unweighted `(?b)` answer upstream
      gets right; counts and changes restored from different candidates), commit.
