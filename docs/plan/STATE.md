# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Phase:** 1 is COMPLETE. Phase 2 - parser, compiler and public API - has no slice files yet.

**Last completed:** S05, upstream tests for lines 3084-4540 (2026-08-30).

**Current slice:** none. `docs/plan/slices/` is empty, so `tools/run-slices.ps1` stops here at the
phase boundary. This is the owner checkpoint described in `docs/plan/OPERATIONS.md`.

**Next action:** owner reviews `docs/STATUS.md`, then authors and approves the Phase 2 slices.
Do not start engine work before those exist.

**Blockers:** none.

**Where phase 1 finished:** 1966 ported upstream tests across 31 feature areas, all skipped, plus
15 gap tests and 21 convention tests. Ratchet GREEN at 2002 tests, baseline 34. All 102 upstream
test methods accounted for: 91 ported, 11 recorded in `docs/PORTMAP.md` as not ported with a
reason. The biggest capabilities waiting are at the bottom of `docs/STATUS.md`; that table is what
Phase 2 slice authoring should work from.

**Worth knowing before the next slice:**

- **Non-BMP data exists after all, at two sites**, both in S05: `test_hg_bugs` #373 and #433-434.
  A range scan must resolve `\N{...}` names via `unicodedata.lookup`, not just look for astral
  literals and `\U` escapes - that omission hid the second site from S05's own measurement.
- **`cat -A` is the only cheap way to see a backslash or an invisible character.** The Read tool
  renders `\\X` and `\X` identically, and Edit/Write decode a `\uXXXX` escape before it reaches
  disk. Write escape text from a Python script; prefer `char.ConvertFromUtf32` for astral chars.
- **Build centrally, once, at the end.** Parallel agents each running `dotnet build` in a shared
  tree see each other's half-written files and one of them "fixed" that by renaming siblings to
  `*.bak`. The single central build is also what caught every analyzer failure.
- **Phase 2 must not un-skip by tag alone** - read the skip prose first (DECISIONS, 2026-08-29).
- `docs/plan/slice-log.jsonl` is still empty: no slice has ever run under the driver, so there is
  no measured tokens-per-slice figure. S05's closing notes give the transcript-derived estimate.
