# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S51 IS CLOSED (2026-09-15, one sitting).** Suite 6,083 / 6,083 passing / **0 skipped**, ratchet
GREEN, baseline 5,922 -> **5,975**, oracle GREEN at seeds 7, 4242 and 20260915. **Next slice is S52**
(oracle hardening).

**Every input-dependent public method now takes `TimeSpan? timeout` and `CancellationToken`** - 27
members, 17 instance and 10 static, held by a reflection test rather than by a list in prose. `null`
means the pattern's `MatchTimeout`, so no existing caller moved. The token is polled at ONE site,
`Matcher.SafeCheckCancel`, which is upstream's `safe_check_cancel` and its `PyErr_CheckSignals` half
that the port's own comment had recorded as having no counterpart.

**Measured, not recalled:** `tools/probes/bcl-sync-cancellationtoken-apis.ps1` says 28 of 28
synchronous BCL methods put the token last, and `Regex` takes a `matchTimeout` on 11 static methods
and **0 instance methods**. Cost of the poll: **+8 bytes per operation** and no resolvable time.

**Two traps this slice hit, both worth carrying forward.** (1) **A BenchmarkDotNet run in the main
tree measures the WORKTREE's binary when a worktree exists** - the first AFTER pair reported
allocation identical to BEFORE, which the change cannot produce; the independent verifier caught it.
Remove the worktree first and read the Allocated column as the tell. **S54 inherits this directly.**
(2) Python `write_text` on Windows rewrites an LF file as CRLF; use `write_bytes` on this repo.

**One scope bullet was deliberately not done and the reason is in the slice file:** comparing oracle
rows where upstream timed out is argued against by `record-oracle.py`'s own `timed_out` docstring -
a port that answers where upstream hangs is better, not diverging. The seven `OracleComparer` call
sites pass the deadline per call instead, so every wave row exercises the new plumbing.

**Owed, carried from S50b:** `.claude/skills/port-tests/SKILL.md` still teaches
`FuzzyRegex.Search(...)` and never mentions `Ported.Upstream`; the edit needs write permission under
`.claude/skills/`. **Owed maintenance (unchanged):** `tools/run-controls.py` cannot measure a control
that mutates the recorder, so **S42-2A is owed**; broken control sites S32-B and S38-A;
`FOLD_TURKIC`'s share of the `case-folding` rotation; S35-A and S29-A/D are thin; PORTMAP's
`_regex.c` line references stale after the sync; `record-oracle.py --self-check` exits 1 on a
pre-S46 message.
**Open for the owner:** `slice-log.jsonl` marks S26 `failed`; `origin/main` needs a push.
**Housekeeping:** empty gitignored dir `.claude/worktrees/s51-bench-head/` (the harness refused to
delete it this session); `BenchmarkDotNet.Artifacts/` holds this slice's logs, gitignored.
