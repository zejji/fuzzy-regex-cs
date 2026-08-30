# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Phase:** 2 - parser, compiler, Unicode tables, pattern-level public API. Eight slices (S06-S13);
S06 and S07 done, six pending.

**Last completed:** S07 (2026-08-30), the parser and compiler skeleton. `src/FuzzyRegex/Parsing/`
now ports `_regex_core.py`'s spine - flags, all 81 opcodes, `Source`, `Info`, the node classes,
the parse functions and `PatternCompiler.Compile`. `FuzzyRegex`'s constructor compiles for real.
Ratchet green at 3685 tests, 514 passing (was 37). Corpus: 436 of 1547 compile rows and 16 of 50
error rows match upstream exactly; no row fails.

**Current slice:** none in flight. Next is `docs/plan/slices/S08-quantifiers-alternation-anchors.md`.

**Next action:** `tools/run-slices.ps1 -MaxSlices 1`. Phase boundary is after S13.

**Blockers:** none.

**Worth knowing before the next slice:**

- **Throw a `needs:` tag where upstream consults a table, not at the top of a branch.** S07's
  review found `\p`, `\P`, `\N` and `\g` throwing on sight when upstream reaches a plain literal
  through all four if the delimiter is absent. Six corpus rows and two ported tests were skipping
  for capabilities they did not need. Port the whole function's control flow first.
- **The driver deletes a slice that does not commit.** Two S07 attempts reached a green ratchet
  and lost everything by ending the turn while a review subagent was still running; under
  `claude -p` that ends the process. `port-slice` now requires the reviewer to be a blocking call
  read in the same turn, and forbids ending a session with uncommitted work. `Undo-FailedSlice`
  now stashes with `-u` before resetting, so the next failure is recoverable - check
  `git stash list` before assuming work is gone.
- **The driver allowlist has two halves.** `Bash(...)` rules do not govern the `PowerShell` tool,
  and `CLAUDE_CODE_USE_POWERSHELL_TOOL=1` is set globally on this machine, so both families must
  be listed or the session is silently denied. Verified 2026-08-30.
- **`is_cased_i`, `str.isdigit`, `str.isidentifier` and `str.isalpha` are ASCII-only** until S09,
  which deletes all four seams. PORTMAP's "Where we diverge" carries the rows to remove.
- **S08 owns `^` and `$`**: `FlagsTests` is retagged `needs:anchors` and turns on there.
