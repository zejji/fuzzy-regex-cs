# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Phase:** 3, the engine. **S25 is DONE** and committed; the tree is clean. Pending queue: S26 only,
and it is the phase close - the driver stops at the phase boundary after it.

**Current slice:** none in flight. **Next is S26** (`phase-close`).

**Where the port stands:** ratchet GREEN, 5161 passing, 5715 total, nothing failing; overall parity
**71.8%** (was 59.3%). Eleven areas at 100%, including `FindAll`, `Splitting`, `Overlapped`,
`Boundaries`, `CharacterClasses` and `UnicodeProperties`. `Reverse` 86.5%, `Substitution` 95.5%,
`Various` 95.4%, `ZeroWidth` 64.3%, `Regressions` 43.1%. **Phase 3 has no behaviour tag left**:
every remaining win is Phase 4 or 5 - `fuzzy-matching` 98, `partial` 82, `lookaround` 61,
`recursion` 60. `needs:partial` is the only `NotImplementedException` seam on the public surface.

**Oracle:** `pwsh -File tools/run-oracle.ps1` before committing any engine slice; its default list
now has **twelve** generators, `iteration` included, and the wave compares whole *sequences* for the
three new operations (`finditer`, `finditer-overlapped`, `split`). Latest:
`agree 18000 unsupported 0 diverge 0` at 1500 rows each, seed 606. Of six negative controls all six
fired (54, 103, 92, 21, 33 and 61 divergences of 600, each re-run at seed 4242) and all six are
reproducible from the S25 closing notes.

**Blockers:** none.

**S26 inherits these duties:**

- **Re-run the build yourself after any review pass that edited files.** S25's reviewer left
  `Engine/Iteration.cs` with CRLF endings, which fails `IDE0055` and so the build; its own report
  said the ratchet was green. `dotnet csharpier format src/FuzzyRegex` fixes it.
- **Format a mutated file before running a control wave, and never pipe a long wave into `grep`.**
  An unformatted mutation fails the build and the harness reports RED with no verdict line, which
  looks exactly like a control that fired; `grep` buffers, so a running wave and a hung one look
  alike. `.scratch/run-controls.py` in the S25 session did both correctly - the shape is in the
  closing notes.
- **`baseline: N` from the ratchet counts distinct test ids, not results.** 5054 ids and 5161
  passing results are the same run; do not read the difference as a corrupted baseline.
- **Re-take every measurement you quote, after the last change to the thing measured.** S25 quoted
  seven parity figures from memory of the pre-slice board and all seven were wrong; the generated
  `docs/STATUS.md` is the only source for them.
