# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Current slice:** S40 is next - the `{...:test}` constraint (`fuzzy_ext_match`'s switch,
`fuzzy_ext_match_group_fld`'s switch and `folded_char_at`, their only caller). Launch:
`pwsh -File tools/launch-slice.ps1 s40`. S39 landed fuzzy strings, backreferences and the folded
forms of both: **every one of the 27 `Seam.For(Opcode.Fuzzy)` sites is now gone**, and ordinary
fuzzy patterns work. It delivers `fuzzy-substitution`, `fuzzy-insertion` and `fuzzy-deletion`.

**Read this before S40.** With every `needs:fuzzy-*` skip removed, 76 of 139 tests pass and 63 fail,
all 63 at a seam: 29 `BESTMATCH` (S42), 25 `ENHANCEMATCH` (S41), 9 `fuzzy_ext_match`'s switch (S40).
So **S40's own share is 9 tests**, and the four plain tags it was meant to land -
`fuzzy-matching` 24/31 methods, `fuzzy-budget` 9/11, `fuzzy-changes` 1/8, `fuzzy-counts` 0/8 - are
mostly `(?e)`/`(?b)` rows and in practice follow S41 and S42. Re-read the phase plan before assuming
S40 can deliver them.

**Two things S39 found that the next slices must not re-derive.** The fuzzy `*_REPEAT_ONE` loops
(`:15881`, `:16500`) are **unreachable and deliberately not ported** - `sequence_matches_one` refuses
a REPEAT_ONE with a fuzzy body, and outside a section the tail test is the never-fuzzy `FUZZY` node;
639 REPEAT_ONE nodes in the corpus, none with a fuzzy test. And S35's `_fix_full_casefold`
divergence is **reachable by a wave for the first time**: two expanding folds in one literal run
(`(?fi)ßaß` against `'ssass'`) diverge, the manifest has no entry, and the `fuzzy`
generator draws at most one expanding fold per section to stay off it. **Phase 6 decides whether
`ExpectedDivergences` gets an entry.**

**Blockers:** none.

**Where the port stands:** ratchet GREEN, 5806 tests, 5632 passing, parity **91.1%**, 29 areas at
100%. 174 skipped, all fuzzy.

**Oracle:** `pwsh -File tools/run-oracle.ps1` (three seeds). Rows per generator is **`-Count`**;
`-Rows` is a path to a JSONL file. Controls: `python tools/run-controls.py --slices S39 --seeds 3`.
**Delete `.scratch/control-waves/<generator>-<count>-<seed>.jsonl` after widening a generator** - the
cache key does not know it changed, and S39's first control run measured S38's wave and read zero
six times. Upstream 2026.9.10 for probes is in `.venvs/regex-2026.9.10` (git-ignored); the oracle
runs the PATH python, regex 2026.7.19.

**Upstream is a ledger, not a queue** (`docs/plan/upstream-reports/LEDGER.md`, nine entries): nothing
filed until everything else in the plan is done. Entry 7 is on Phase 6's fix list.

**Still open for the owner:** `slice-log.jsonl` marks S26 `failed` though its commit is real;
`origin/main` trails local and needs a push.
