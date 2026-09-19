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
- **The AOT test gate, back to green.** `tools/run-aot-tests.ps1` has been RED since S65 added
  `tests/FuzzyRegex.Tests/Conventions/PublicApiDocumentationTests.cs` (`148c3bf`, 2026-09-18 -
  NOT `3b09b76`, which adds the same file with the same subject and author date but is a
  pre-rebase duplicate that `git merge-base --is-ancestor 3b09b76 HEAD` rejects; the `dfa8767`
  trap in DECISIONS, hit again by S59's verifier):
  `ilc` reports exactly one trim error, `IL2065` at that file's line 62, on a
  `type.GetMembers(BindingFlags)` over types that are not statically known, and the publish then
  fails with `MSB3077`. Reproduced identically by S58 and again by S59 from a DELETED
  `tests/FuzzyRegex.Tests/obj` and `bin`, so it is neither an intermediate-directory artefact nor
  either slice's doing. `src/FuzzyRegex` itself is clean - `tools/run-aot-smoke.ps1` is GREEN -
  so this is the test project only. The fix is a real decision, which is why neither slice took
  it mid-flight: annotate the scan with `DynamicallyAccessedMembers`, suppress it with
  `UnconditionalSuppressMessage` and a written reason, or exclude that one convention test from
  the native publish. Whichever is chosen, the gate must be GREEN at the end of this slice, and
  the reason recorded. **This bullet exists because the record alone was not enough**: S58 wrote
  the failure into its closing notes, and S59 still spent a full AOT publish re-deriving it.
- **Gap-test provenance audit** (owner rule 2026-09-15): an independent Opus agent samples the
  gap tests written before the provenance rule existed (`tests/FuzzyRegex.Tests/Gaps/**`, at least
  one assertion per file and every assertion in the fuzzy, BESTMATCH, verb and partial files),
  re-runs each expected value against upstream 2026.9.10, and reports CONFIRMED / DIFFERENT /
  DELIBERATE (a DIVERGENCES row explains it). Every DIFFERENT is a bug in the test or in the port
  and is fixed before the phase closes; every sampled assertion gains its provenance comment.
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
- [ ] `tools/run-aot-tests.ps1` GREEN again, the IL2065 decided one of the three ways and the
      reason written down.
- [ ] Symbol accounting, controls, bookkeeping, Phase 7 handover.
- [ ] Ratchet GREEN, blind review (hunt: a gate item ticked from an earlier slice's numbers rather
      than re-run; a ledger entry whose "fixed" has no test), commit.
