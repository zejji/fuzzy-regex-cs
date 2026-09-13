---
slice: S43
phase: 5
title: Composed fuzzy wave, symbol accounting, and closing Phase 5
delivers: []
---

# S43 - Composed fuzzy wave, symbol accounting, and closing Phase 5

Shaped like S26 and S36. After it every construct in upstream matches, fuzzy included, the oracle
has swept fuzzy in combination with everything Phases 3 and 4 delivered, and Phase 6's author has
a handover.

## Scope

- **Compose fuzzy into `interactions`** in `tools/record-oracle.py`: fuzzy sections wrapping and
  wrapped by lookaround, conditionals, recursion (`(?R)` inside a fuzzy quantifier was upstream
  issue 607, a segfault), atomic groups, `(*SKIP)`/`(*PRUNE)`, named lists (`\L<name>{e<=1}`,
  which DECISIONS 2026-08-30 records as a distinct code path), POSIX, `(?r)`, `partial=True`,
  `(?i)`/`(?fi)`, and substitution templates reading a fuzzy match's groups. Both `(?e)` and `(?b)`
  in the mix. Nested fuzzy sections with different constraints.
- **The wave at scale**: default list at three seeds, 6000 rows per generator; `fuzzy` and
  `interactions` additionally at 99991. Green, or every divergence judged with the S33 treatment
  and either fixed (this port's), or entered strictly with a quoted probe and a negative control
  (upstream's), or parked as a named blocker in STATE.md if the evidence is not overwhelming
  either way. Two wave slices were added inside Phase 4 by exactly this step; budget for that.
- **Symbol accounting**: rebuild S36's 567-function cross-check (its script is described in
  S36's closing notes and must be committed under `tools/` this time, not left in session
  scratch). PORTMAP's fuzzy bucket (33 functions) and best list (4) rows all named and ported or
  recorded as deliberately not ported with the grep. Zero seams anywhere in `src/`: grep
  `Seam.For` and quote the count.
- **Tag probe**: remove every remaining `[Skip]` in the ported suite, run, and report; the expected
  number of skipped tests after Phase 5 is zero. Anything left is a test to un-skip or a defect
  named in the handover.
- **Negative controls**: re-run all Phase 5 controls at their recorded seed and at 99991
  (`python tools/run-controls.py --slices S37,S38,S39,S40,S41,S42 --seeds 2`); record thin or
  dead ones by name.
- **Ledger** (`docs/plan/upstream-reports/LEDGER.md`): every Phase 5 finding entered; verified
  against `.venvs/regex-2026.9.10` where it says "fixed" or "not fixed"; nothing filed.
- **Bookkeeping**: `CHANGELOG.md`; ROADMAP's Phase 5 measured rate from `slice-log.jsonl`
  against the 7-slice estimate; flag whether Phase 6's 7-12 still looks right given the fix list
  (ledger entry 7, issues 470/563/564/596/607/608, anything S37-S42 added); STATE.md saying Phase
  5 is complete; docs/STATUS.md regenerated; the spec's phase table annotated as Phase 4's was.
- **Phase 6 handover** in the closing notes: the upstream sync procedure (latest *release*, diff
  release..head, DECISIONS 2026-09-12), the fix list in priority order, the oracle hardening
  items (all Unicode planes, longer subjects, timeout rows), the AOT gate, and the mutation-testing
  gate item.

## Verification

- Everything above is verification; the bar is the same as S36's: default wave green at three
  seeds at 6000 rows, `fuzzy` and `interactions` also at 99991, ratchet GREEN, parity 100% of
  ported tests or every exception named.

## Done when

- [x] `interactions` composes fuzzy. **Sitting 1.** Three piece kinds (`fuzzy`, `fuzzy-wrapped`,
      `fuzzy-list`), `(?e)`/`(?b)` at the row level, named lists emitted for the first time by any
      generator. `partial` and `partial-sliced` proven byte-identical to HEAD; every other generator
      too. Re-measured docstring figures. **The wave is NOT green: 7 rows diverge, all judged, none
      classified yet** - see STATE.md.
- [ ] Symbol accounting committed as a tool and reproduced; zero `Seam.For` in `src/`.
- [ ] Tag probe: zero skipped, or each remainder named.
- [ ] Controls re-run and recorded; **ledger: entry 9 closed and sharpened, 13 and 14 added**
      (sitting 1); nothing filed.
- [ ] `CHANGELOG.md`, ROADMAP measured rate, STATE.md saying Phase 5 is complete, Phase 6
      handover written.
- [x] Ratchet GREEN and **two blind passes done, both acted on** (sitting 1). The hunt items were
      both answered: the fuzzy rows do diverge from exact (103 of 156 matches charge an error over
      2000 rows at seed 7), and the second pass killed an overclaimed judgement rather than a
      control. Commit is a **checkpoint** - the slice stays here.
