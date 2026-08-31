# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Phase:** 3, the engine. **S16 is DONE** and committed; the tree is clean. Pending queue: S17-S26.

**Current slice:** none in flight. **Next is S17** (`classes at match time`): `RANGE`, the four
`SET_*` families, `PROPERTY`, and the `ascii-flag` behaviour.

**Where the port stands:** ratchet GREEN, 3665 passing, 5505 total, nothing failing; overall parity
6.5%. **Patterns match.** `src/FuzzyRegex/Engine/` holds `ByteStack`, `MatchState` and `Matcher` -
`basic_match`'s two switches with literals, `.`, `STRING` and the plain anchors real and every other
opcode throwing a tagged seam. `Match`/`Group`/`Capture` report real spans for group 0; groups 1+,
`Matches`, `Replace`, `Split` and `partial: true` are still seams.

**The oracle earned its keep in S16 and rule 7 still applies.** `pwsh -File tools/run-oracle.ps1`
before committing any engine slice. Latest: `agree 3600  unsupported 0  diverge 0` over `literals`,
`literal-dot` and the new `anchors` generator, 1200 rows each.

**Blockers:** none.

**S17 inherits these three duties:**

- **Read `node.Step`, do not assume 1.** `BuildRange` and `BuildSet` clear the step to 0 on
  `RE_ZEROWIDTH_OP`, exactly as `BuildCharacter` does. Assuming 1 for `CHARACTER` cost S16 41
  oracle rows - every pattern with a leading anchor failed. `ANY` and `STRING` genuinely do step
  unconditionally upstream.
- **Pin `OracleComparer.Run`'s `whileMatching: true` attribution.** Still marked at the line and
  still unreached: S16's engine throws `NotImplementedException` for an unported opcode, which the
  catch above takes. The first slice whose matcher can throw something else must pin it.
- **Add a generator per capability it delivers.** S16 added `anchors` and it found the slice's only
  real defect. A slice that lands opcodes without one is trusting the ported suite alone.

**Worth knowing before the next slice:**

- **`advance:` is absent from `BasicMatch`** - nothing jumps to it yet and C# rejects an
  unreferenced label. The slice with the first real backtrack case puts it back.
- **The span convention:** engine internals are `(start, end)`; the public API is `(Index, Length)`.
  Converted in exactly two places - `Capture`'s accessors and the oracle recorder.
- **Run the ratchet AFTER committing as well as before.** The pre-commit CSharpier hook rewrites
  files, and in S16 its reindenting of the `goto` labels broke a commit that had been green a
  minute earlier (fixed by `csharp_indent_labels = no_change`; DECISIONS 2026-08-31).
- **Do not edit a C# file with Python's `write_text`** - CRLF becomes LF and IDE0055 fails the
  build. Use the `Edit` tool. (Hit again in S16, caught before the build.)
- **Scratch lives in `.scratch/`** (gitignored). The driver fails any slice whose tree is dirty.
