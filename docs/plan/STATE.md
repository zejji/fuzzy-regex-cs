# State

**S82 is done (2026-09-22).** Branch resets now follow the maintainer's option 3: in a branch reset,
a group never takes a number another group in the same branch will use. A source-level pre-scan,
`ParseFunctions.ReserveBranchGroupNumbers`, pre-seeds `Info.BranchGroupNumbers` at each branch start;
`Info.OpenGroup` already skipped that set, so the numbering code itself is unchanged. This replaces
S50's option 2 and closes ledger entry 17 (upstream issue 425).

**It costs two pins, both deliberate and both verified:** upstream's own `test_branch_reset` row at
`test_regex.py:1653-1662` now disagrees with us, and compile-parity row #614 emits a different pair of
`Group` operands. Neither is a port bug and neither is to be inverted later.

**Carry forward:** `(?'name'...)` is not a spelling either engine accepts, whatever a spec says; the
pre-scan reads the whitespace setting a branch starts with, and a mid-branch `(?x)` is the documented
ceiling (`SHORTCUT:` in the source). A control that deletes a call site can fail the build on
`IDE0052` and be reported as fired against an empty list - mutate the body instead.

**Measured at this commit:** ratchet GREEN at 6563 tests (6455 distinct ids), default oracle wave
GREEN at three seeds, doc examples clean, controls `S82-A` (15 red of 6563) and `S50-C` (16) both
FIRED. Blind review returned "No defects found."; the verifier confirmed all 16 judged numbers.
**Next:** the queue's lowest is `S60b-search-start-and-the-researched-prefilters.md`.

**Phase 6's four gate items are green, and Phase 6 is NOT closed.** One inherited ledger entry is
still reproduced: **S61 item 7** is entry 18.

**Environment:** a wedged VBCSCompiler (PID 39948, killing it is not authorised) holds
`src/FuzzyRegex/obj/Release/net10.0/FuzzyRegex.sourcelink.json`, so Release builds run with
`$env:IntermediateOutputPath` set - never for the ratchet, which times out under it.

**Maintenance still open:** `_leak_free_fuzzy` starves a reversed row whose lookahead reads past the
match end (S57b sitting 4). **Owner, both from S73:** the `docs/demo/` reference layouts, and
publishing (push `phase9-demo`, then Pages > GitHub Actions).
