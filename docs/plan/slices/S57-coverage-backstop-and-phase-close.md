---
slice: S57
phase: 6
title: Line coverage as a backstop, the exit gate walked in order, and closing Phase 6
delivers: []
---

# S57 - Coverage backstop and the Phase 6 close

Shaped like S36 and S43. Walks the exit gate in the ROADMAP's order - skips, oracle, mutation,
coverage - and hands Phase 7 what it regresses against.

## Scope

- **Line coverage, last and only as a backstop** (ROADMAP gate item 4): `dotnet test` with the
  Microsoft Testing Platform coverage extension, report per file and branch, and a list of every
  file or branch with NO test at all. Each is either given a test or recorded as fidelity dead
  code, as S56 did for survivors. Never a percentage target; the number goes in the notes for
  Phase 7's comparison only.
- **The gate, walked**: zero skips in the ported suite (confirm nothing regressed since S42);
  default wave at three seeds, 6000 rows, plus `fuzzy` and `interactions` at 99991 and one fresh
  seed; S55/S56's mutation scores quoted; coverage backstop done. Anything red is judged, not
  deferred.
- **The known-bug list is EMPTY.** Table every ledger entry with its final state - fixed here,
  port right and pinned, upstream-only, or owner decision pending with the evidence - and confirm
  no entry is "inherited, unfixed". If one is, the phase is not closed: park it as a blocker.
- **Symbol accounting** with `tools/check-symbols.py`: every `_regex.c` function named in PORTMAP
  or in the deliberately-not-ported table; the count unchanged from S43 or the change explained.
- **Controls** re-run for S44-S56 at their seed and 99991; thin or dead ones named.
- **Bookkeeping**: CHANGELOG, ROADMAP's Phase 6 measured rate and the 10-15 estimate judged,
  STATE.md saying Phase 6 is complete, STATUS.md regenerated, spec table annotated.
- **Phase 7 handover**: the benchmark baselines and compare script (S54), the edge pins and the
  PERMANENT tests, the rule that upstream's start optimisations are ported without importing their
  answers (`locate_required_string`, `search_start_*`, the `prefilter-free` wrapper to delete and
  the `search-start-*` entries to re-judge), the mutation scores as the floor Phase 7 must not
  lower, and the timeout poll's measured cost (S51).

## Verification

- Everything above is verification; the bar is S43's.

## Done when

- [ ] Coverage backstop run; every untested file or branch tested or recorded.
- [ ] Gate walked in order with numbers; ledger table shows no inherited-unfixed entry.
- [ ] Symbol accounting, controls, bookkeeping, Phase 7 handover.
- [ ] Ratchet GREEN, blind review (hunt: a gate item ticked from an earlier slice's numbers rather
      than re-run; a ledger entry whose "fixed" has no test), commit.
