# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Phase:** 1 - port the upstream test suite.

**Last completed:** S04, upstream tests for lines 1741-3083 (2026-08-30).

**Current slice:** none. Next is `docs/plan/slices/S05-port-tests-hg-bugs.md`, the last of phase 1.

**Next action:** run `tools/run-slices.ps1`, or open a fresh session and invoke `port-slice`.
When the queue empties the driver stops at the phase boundary and phase 2 slices need authoring.

**Blockers:** none.

**Worth knowing before the next slice:**

- **Non-BMP data has still not appeared.** Lines 1-3083 contain no code point above U+FFFF,
  measured slice by slice. S03 predicted it for S04 and was wrong. Measure the S05 range first;
  if it is BMP too, phase 1 never needed the surrogate work at all.
- **Generate a table, do not transcribe it.** `test_various` was 524 rows: extracted with `ast`,
  re-run through the oracle for ground truth, emitted as C#, then read back out of the built DLL
  by reflection and compared row by row. If `test_hg_bugs` is table-shaped, do the same.
- **A delegation brief that enumerates the allowed API can silently narrow the port.** S04's
  brief omitted `Replace` and `Groups.Count`, and the agent duly reported two portable assertions
  as unportable. Point the agent at the source and name the *exclusions* instead.
- **Build centrally.** Third slice running that whole-project build caught what agents could not:
  two `<see cref>` references broken by a signature change, Sonar S4144 on identical method
  bodies, CA1716 on a namespace named `Partial`.
- **Reuse the 49-tag vocabulary** before coining (bottom of `docs/STATUS.md`). S04 added 17.
- **Machine-check every expected value against the local `regex` oracle**, with
  `PYTHONDONTWRITEBYTECODE=1` and `PYTHONIOENCODING=utf-8` set. These tests never execute, so
  reading them against Python is the only gate.
- **Confusable BMP characters go in as `(char)0xNNNN`**, not literal UTF-8, and are verified by
  reflecting the built assembly - ß/İ/ı/ﬆ all appear in S04 and none are distinguishable by eye.
