# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Current slice:** S37 is next. **Phase 5 is authored (S37-S43) and approved by the owner**
(2026-09-12). Two decisions taken at authoring, in DECISIONS: `(?e)`'s dead `same_match` check is
not ported live; `(?b)`/`(?e)` rank by cost, not error count. Launch: `pwsh -File tools/launch-slice.ps1 s37`.

**The Phase 5 plan in one line each.** S37 judges the thirteen `interactions` rows red at 6000
rows (Phase 4's leftover, no engine work expected). S38 fuzzy spine: state, constraints,
`FUZZY`/`END_FUZZY`, one-character and zero-width items, insertions, `do_simple_fuzzy_match`,
`Match.FuzzyCounts`/`FuzzyChanges`, and the `fuzzy` oracle generator. S39 strings, backreferences,
full-fold arms and the `*_REPEAT_ONE` fuzzy loops. S40 the `{...:test}` constraint and delivery of
the seven plain tags. S41 `ENHANCEMATCH`. S42 `BESTMATCH`. S43 phase close (fuzzy composed into
`interactions`, symbol accounting, tag probe, Phase 6 handover). S38 and S39 deliver no tag by
name: they run S36's tag probe at their close.

**Blockers:** none.

**Where the port stands:** ratchet GREEN, 5770 tests, 5587 passing, parity **90.7%**, 28 areas at
100%. Default oracle GREEN at three seeds, 6000 rows each. Every remaining skipped test (183) is
fuzzy; no seam left anywhere in `src/` is anything but a fuzzy one (27 `Seam.For(Opcode.Fuzzy)`
in `Matcher.cs` plus the three entry-point seams at `Matcher.cs:6582-6592`).

**Oracle:** `pwsh -File tools/run-oracle.ps1` (three seeds). Controls:
`python tools/run-controls.py --slices S27,...,S35 --seeds 2`. Upstream 2026.9.10 for probes is in
`.venvs/regex-2026.9.10` (git-ignored).

**Upstream is a ledger, not a queue** (`docs/plan/upstream-reports/LEDGER.md`, seven entries):
nothing filed until everything else in the plan is done. Entry 7 is on Phase 6's fix list.

**Still open for the owner:** `slice-log.jsonl` marks S26 `failed` though its commit is real;
`origin/main` trails local by four commits (S35, S36 and two checkpoints) and needs a push.
