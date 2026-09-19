# State

**S59 is DONE and committed** - a bounded pattern cache behind `FuzzyRegex.CacheSize`, default 15.
No slice is in flight. The next slice is the lowest-numbered file in `docs/plan/slices/`: **S57**,
the coverage backstop and the Phase 6 close.

Green at this commit: ratchet 6432/6432 (baseline 6324 ids); oracle clean at seeds 7 and 4242 and
1 divergence of 6380 at seed 20260919, row 3655, the already-triaged
`docs/plan/2026-09-19-oracle-divergence-fuzzy-edit-attribution.md`; `run-aot-smoke.ps1` GREEN at
6,982,144 bytes; benchmarks GREEN against this machine's committed baseline, which was NOT updated
because the machine was shared for the run.

## One thing is red, it is not S59's, and S57 owns it

`tools/run-aot-tests.ps1` fails on exactly one trim error - `IL2065` at
`tests/FuzzyRegex.Tests/Conventions/PublicApiDocumentationTests.cs(62)`, then `MSB3077`. It has
been red since S65 added that convention test (`148c3bf`), S58 recorded it, and S59 reproduced it
from a deleted `tests/FuzzyRegex.Tests/obj` and `bin`, so it is not an intermediate-directory
artefact. `src/FuzzyRegex` is clean under AOT; this is the test project only. The fix is a real
choice - annotate, suppress with a reason, or exclude that test from the native publish - so it is
a scope bullet and a "Done when" box in `docs/plan/slices/S57-coverage-backstop-and-phase-close.md`
rather than a note somebody has to find again.

## Next actions

1. Start S57. Read `docs/plan/slices/S57-coverage-backstop-and-phase-close.md` in full; the AOT
   bullet is new.

## Blockers

None. The two wedged processes sitting 1 reported (PIDs 33360 and 37192) are gone - `tasklist`
finds neither. No process was killed by any S59 sitting.

## Worth carrying forward

**Check a SHA is an ancestor before quoting it.** `git merge-base --is-ancestor <sha> HEAD`. Three
pre-rebase duplicates bit in two days: `dfa8767`, `e30a4e8` and `3b09b76`, each adding the same
content with the same subject as an ancestor commit that has a different SHA. A diff quoted as
empty was not, and a "28 commits" count was over a branch nobody is on.
