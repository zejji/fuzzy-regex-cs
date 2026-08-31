# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Phase:** 2 is **COMPLETE**. All eight slices (S06-S13) are in `docs/plan/slices/done/`, the
pending queue is empty, and the driver stops here at the phase boundary. The owner reviews, then
authors and approves the Phase 3 slices (`docs/plan/OPERATIONS.md`).

**Current slice:** none. The tree is clean.

**Where Phase 2 leaves the port:** ratchet GREEN, 2046 passing (baseline 2046, recorded as 2040
ids), 3926 total, nothing failing; overall parity 4.5%. **The parser is finished** - every
construct upstream's parser accepts is parsed and compiled here - and **all 1659 compile-parity
corpus rows pass with none skipped**, which is the first slice at which that is true.
`parse-errors` and `fuzzy-syntax` have left the status board.

**Next action:** owner checkpoint, then Phase 3. Its handover notes are the closing notes of
`docs/plan/slices/done/S13-fuzzy-syntax-named-lists-phase-close.md`; read them before authoring
the Phase 3 slices. The roadmap now records Phase 2's measured rate: budget **1.35 driver sessions
per slice**, so Phase 3's 11-16 slices is 15-22 sessions.

**Blockers:** none.

**Worth knowing before the next slice:**

- **Phase 3 opens with the differential oracle, not the VM** (ROADMAP, design spec amendment 10).
  Then `re_compile` (`_regex.c:25863-26121`), which turns the code list into the node graph - and
  which **must reject five patterns this port currently compiles**, listed in S13's closing notes.
- **Two blind review passes are the sweet spot and both earn their keep.** S13's first pass found 4
  real defects and its second, over the fixes only, found 2 more. Neither would have been caught by
  the corpus.
- **A differential wave beats the corpus wherever upstream's own suite is thin.** Recipe:
  `.scratch/s13_record.py` + `.scratch/wave/` + `.scratch/s13_compare.py` + `.scratch/s13_classify.py`.
  **Sort each named list before handing it to upstream** - `record-compile-corpus.py`'s
  `_canonical_kwargs` does, and not doing so cost 58 false divergences. Always run a negative control.
- **`Match.Result` is still unwired** and `Match` is a fieldless stub, so nothing can compile a
  replacement template against a match until Phase 3.
- **The span convention:** upstream is `(start, end)`, this port is `(Index, Length)`
  (DECISIONS 2026-08-30). Getting that wrong in the engine would be silent and pervasive.
- **`\uXXXX` written through any agent tool is decoded before it reaches disk.** Build such text
  with `chr(0x5c) + 'u2028'` in a Python one-liner instead.
- **Do not edit a C# file with Python's `write_text`** - it converts CRLF to LF and IDE0055 then
  fails the build. Use `Edit`, or run `dotnet format` afterwards.
- **Scratch lives in `.scratch/`** (gitignored). The driver fails any slice whose tree is dirty.
