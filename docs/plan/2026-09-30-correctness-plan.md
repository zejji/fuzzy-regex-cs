# Correctness plan after the complete matrix (agreed 2026-09-30)

The complete interaction matrix (`docs/plan/2026-09-30-complete-matrix.md`) sized the remaining
correctness work. This note records the order in which it will be done, the tests that narrow what
the matrix could not check, and the rule for deciding when correctness work is finished. The owner
agreed all three on 2026-09-30. `docs/KNOWN-DEFECTS.md` stays the source of truth for each row's
status.

## 1. Order of work

Shared causes and small independent items come first, so each later fix is measured on a cleaner
baseline.

1. **Small, independent items.**
   - D55: a one-line optimiser fix (`Optimiser.cs:194-200`, upstream `_regex.c:23203-23208`), with
     negative-lookahead and lookbehind witnesses.
   - D60: the `FuzzyChanges.Deletions` doc comment and the GUIDE paragraph.
   - Oracle keys: widen the ledger-44 and POSIX entries (D58), and the 7 C1 key gaps the triage
     lists.
   - Pins for D56, D57 and D59:
     - trace upstream's cause for D59 before drafting its report;
     - test whether D56 and D57 are one `(*SKIP)` cause.
2. **D51.** A capture made inside a call is discarded on return (owner ruling (a)). It accounts for
   559 matrix rows and hides anything else in check C2 until it is fixed.
3. **Conditional tests, together:** D52 (a conditional's lookaround test compiles fuzzy; cause
   traced) and D54 (keep the captures of a failed negative test; OPEN-2).
4. **Partial matching, on `maint/d11-partial-boundary`.** Review chunk 9 (which contains the D41
   fix), then chunks 10-11 and the merge. D53 and the phantom rows (D18, D19, D28) fold into this.
5. **`maint/d4-reach-rule`:** chunks 2b-2e, C2a and C2b, with D33.
6. **The older rows:** D23 (design branch `design/d23-verb-lookbehind`). D26, D29, D36, D38, D39
   and D50 are each sized before they are scheduled.

Each fix follows the builder rules: red test first, one blind review, the ratchet and the oracle at
three seeds.

## 2. Tests that narrow the matrix's gaps

These are the gaps with a clear way to close them. Each is its own small item, done alongside the
fixes, not after them.

| Gap | Test to add | Why it is clear |
|---|---|---|
| C5 has no firing control for the failed-call memo, so its "0 failures" proves only the repeat memo | An oracle-only ablation that deliberately corrupts the memo, for example by dropping one component of its key. Add it as a C5 control that must fire, and as a Gaps test | The memo's key is defined in `PatternObject.cs` and `Matcher.cs` (`UseCallMemo`); corrupting it has a known effect. The ablation follows the same pattern as the existing oracle-only flags |
| 8,312 rows have no second engine; for fuzzy rows only C6 is independent, and 585 of the survey's 730 fuzzy rows are outside the reference matcher's subset | Extend `tools/probes/fuzzy-reference-matcher.py` to the constructs it skips, and cross-check simple fuzzy rows against TRE (`tools/probes/tre-fuzzy-check.py`) as a check C8 | The reference matcher is an ordered search written from the rules, so extending it is mechanical. TRE already runs in WSL |
| Boost.Regex is a fourth engine for verbs, conditionals and captures, but is not in C7 | Add Boost as a third C7 engine (standalone headers, g++ in WSL, measured 2026-09-30) | It was run by hand today and agreed with the rulings. A worker follows `survey_worker.pl`'s shape |
| C2 has no rows with scoped flags, so the rule "a call keeps its definition's flags" is untested | Generator rows with scoped flags around calls, and an expander that writes the group out with its definition's flags (`(abc)(?i:(?-1))` becomes `(abc)(?i:(?-i:abc))`) | Marked `SHORTCUT:` in `tools/matrix/gen.py`; the three engines agree on the witness |
| The recorder refuses finditer with partial and ignores pos/endpos on finditer, so those rows get only the weaker C1x | Teach `tools/record-oracle.py` to record both, so C1 judges them | Upstream answers both; only the recorder skips them |
| C4's phantom counts are lower bounds (continuations up to 5 characters, or 8 over a/b/x/space) | Raise the bound where the row allows it, and report each phantom with the bound it was checked to | The judge's search is bounded by a parameter |

## 3. When correctness work is done

All three conditions are required.

1. **No known bugs.** Every row in `docs/KNOWN-DEFECTS.md` is fixed, or pinned as a place where the
   port is right, with a permanent test. This is the owner's rule of 2026-09-12.
2. **Fresh searching finds nothing new.**
   - After the fixes, rerun the matrix on two new seeds, with extra rows in every cell that failed
     in the 2026-09-30 run. Rerun the oracle at its three seeds with Debug invariants on.
   - A new root cause is fixed, and the run is repeated.
   - Done when two consecutive runs find no new root cause.
   - If three consecutive rounds each find one, stop and re-plan the approach rather than grind.
3. **The remaining blind spots are named and accepted by the owner**, not silently ignored. As of
   2026-09-30:
   - rows no second engine can judge;
   - the reference matcher's subset;
   - the phantom bound.

   Section 2 narrows each of these, and the list is re-stated when condition 2 is met.
