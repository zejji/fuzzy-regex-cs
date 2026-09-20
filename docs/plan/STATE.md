# State

**S57 is IN FLIGHT and checkpointed.** The slice file is still in `docs/plan/slices/`; a fresh
sitting continues it. Sitting 1 of S57 ended on the allowance window, not on a blocker.

Read `docs/plan/slices/notes/S57-sittings.md` first - it holds the 12-item checklist, what each
number was measured with, and the evidence. Do not re-derive any of it.

Done in sitting 1: item 1 (skips: ZERO), item 2 (**AOT gate GREEN again** - it had been red since
S65/`148c3bf` on one IL2065; suppressed with the reason written out, and made honest by a new
member-count floor the native run re-proves), item 3 measured (0 of 39 files unreached - the
question the backstop exists to answer; 911 uncovered lines still to classify), item 4 run (default
wave at three seeds: GREEN at 7 and 4242, **1 divergence at 20260920**; plus a 126,080-row wave at
seed 7 finding 3 more), item 6 (**provenance audit PASSES: 0 different, 0 without provenance**).

Next sitting, in this order:

1. **Judge the four diverging oracle rows.** They are NOT one family - `.scratch/doors-s57.txt` is
   gone with the scratch, so re-run `tools/probes/gate-divergence-doors.py`; the signatures are
   written out in the notes. Rows 5014 and 102670 look alike and answer differently.
2. Classify the 911 uncovered lines, `Engine/GuardList.cs` (36 of 112) first.
3. Items 5, 7, 8, 9, 10, 11 - all untouched.
4. **Spot-check the provenance agent's verdict** before trusting it: start with
   `PartialMatchingTests.cs:~1180`, which rests on a version-drift claim. One probe run.
5. Decide the two timing-coupled `DemoEngineContractTests` (a latent flake, mechanism proven).

**No blind review and no verifier have run on this slice yet.** Both are owed over the whole S57
diff before the slice moves to `done/`. The checkpoint did not have them and must not be read as
if it had.

Standing: check a SHA is an ancestor before quoting it - `git merge-base --is-ancestor <sha> HEAD`.
`-Count` on `run-oracle.ps1` is PER GENERATOR, not a total.
