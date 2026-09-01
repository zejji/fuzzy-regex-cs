---
slice: S26
phase: 3
title: Oracle hardening, symbol accounting, and closing Phase 3
delivers: []
---

# S26 - Oracle hardening, symbol accounting, and closing Phase 3

The last Phase 3 slice. After it, every construct in the phase's scope matches, the oracle has
swept the whole surface at once, and Phase 4's author has a handover.

## Scope

- **A combined oracle hardening wave**: the S16-S25 generators composed rather than run
  per-family - classes inside quantified groups with backreferences under `(?i)`, `(?r)` and
  MULTILINE, over subjects mixing ASCII, expanding-fold characters and astral planes, through
  search, sub and split alike. Feature *interactions* are what per-slice waves structurally miss
  and what this wave exists to catch. Budget it: a bounded row count with the seed recorded, so
  it is reproducible. Zero unexplained divergences; every real one minimised into a permanent
  test before the phase closes.
- **The five rejections re-checked against the CI-built pinned oracle** (2026.8.12 from
  `upstream/`), not just the local 2026.7.19 - the S15 evidence was recorded against the PyPI
  build, and the phase close is the cheap moment to re-run it via the oracle workflow
  (`workflow_dispatch` on oracle.yml) and quote the result.
- **Every in-scope `_regex.c` symbol accounted for.** List every function
  (`grep -n "^Py_LOCAL\|^static " upstream/src/_regex.c`) and check each appears in
  `docs/PORTMAP.md` as ported, deliberately not ported with a reason (locale encoding, bytes
  variants, pickling, GIL/lock machinery, scanner/splitter objects, `match_detach_string`,
  deallocs), ported-in-Phase-3, or **explicitly deferred**: the Phase 4/5 matching opcodes
  (lookaround, atomic, recursion, conditional-with-lookaround, partial, fuzzy, named-list
  matching, POSIX best-match) and the Phase 7 prefilters (`locate_required_string`, the
  `string_search`/`fast_string_search` family, the `try_match` test-node path - DECISIONS
  2026-08-31). S13 did this for `_regex_core.py`; this is the engine's turn.
- **The prefilter contingency, checked**: confirm no ported test is skipped-for-timeout because
  of the deferral (S19's contingency). If any is, port the needed prefilter now rather than hand
  Phase 4 a slow engine with a hidden hole.
  **Widened 2026-09-01: the symptom is not only a skip, it is a test that passes slowly.**
  `MatchAtStart_lazy_dot_star_cd_handles_a_long_repeated_prefix` takes **372 seconds** where
  upstream takes **0.0003**, and with its two siblings accounts for 433 of the suite's 515
  measured seconds - the ratchet went from 1m28s to over 6 minutes on S24's watch, and every
  future slice pays that. The suite's own numbers put the growth at **n^2.29** (20,004 chars in
  30.08s, 60,002 in 372.43s), so this is quadratic behaviour, most likely the UTF-16 position
  walking in the repeat loop rather than a missing prefilter - `MatchAtStart` is anchored, so
  `locate_required_string` is not what saves upstream. Measure it, name the cause, and decide
  explicitly whether Phase 7 fixes it or Phase 3 closes with a documented quadratic. Do not let it
  pass unremarked because the tests are green. DECISIONS 2026-09-01.
- **Un-skip sweep**: walk the remaining skipped tests whose tags Phase 3 delivered - any test
  still skipped on a delivered tag is either a defect (fix it) or mis-tagged (retag with prose),
  the S13 rule that a phase does not close with its own tags still on the board.
- **Phase 4 handover notes** in the closing notes, for whoever authors the Phase 4 slices: where
  the lookaround/atomic/recursion seams sit in `basic_match` and what they throw, what
  `state_init_2` already allocates for them, what partial matching needs of the search loop, the
  named-list matching seam (`STRING_SET` opcodes), and anything this phase learned that changes
  Phase 4's shape.
- **Roadmap and budget**: record Phase 3's measured sessions-per-slice from
  `docs/plan/slice-log.jsonl` against the 13-slice estimate, as the roadmap asks; flag for the
  owner whether the Phase 4 estimate (8-12 slices) still looks right from here.

## Verification

This slice's verification is the phase's: the hardening wave, the symbol accounting, and the
sweep are each checkable and their outputs quoted.

## Done when

- [ ] Hardening wave ran with seed and counts quoted; zero unexplained divergences; every real
      divergence pinned as a permanent test.
- [ ] **Every negative control recorded in a Phase 3 slice's closing notes re-run from those
      notes, AND at a seed the slice never used.** This is the moment the recorded values pay
      for themselves. At the recorded seed, a control that no longer reproduces means either
      the generator has lost its teeth or the notes are wrong, and both are findings. At a
      fresh seed it is the thin margins that show: re-running at the recorded seed reproduces
      the same number forever and can never distinguish a control that catches a fault from
      one that caught a coincidence. S18's are exempt - they predate the rule and are
      unreproducible, as its slice file records.
- [ ] Five rejections confirmed against the CI-built 2026.8.12 oracle, output quoted.
- [ ] PORTMAP complete for `_regex.c`: every symbol ported, not-ported-with-reason, or
      deferred-to-named-phase; prefilter contingency checked and recorded.
- [ ] No test remains skipped on a tag Phase 3 delivered; `docs/STATUS.md` regenerated and the
      overall parity figure quoted in the closing notes.
- [ ] `CHANGELOG.md` gains Phase 3's entry. Pre-1.0 it tracks phases, not slices, so a phase
      close is the only place it is written.
- [ ] Roadmap updated with Phase 3's measured rate; STATE.md says Phase 3 is complete and the
      driver stops at the boundary; Phase 4 handover notes written.
- [ ] Ratchet GREEN, baseline updated, blind review over this slice's unreviewed changes (hunt:
      a hardening-wave divergence explained away in prose instead of minimised into a test, a
      PORTMAP row claiming "ported" for a symbol whose opcode still throws a seam), commit.
