# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Phase:** 3, the engine. **S15 is DONE** and committed; the tree is clean. Pending queue: S16-S26.

**Current slice:** none in flight. **Next is S16** (`engine spine, literals`): the first matcher.
It inherits three duties named below.

**Where the port stands:** ratchet GREEN, 3612 passing (baseline 3606 recorded ids - the six-id gap
is the duplicate corpus rows, DECISIONS 2026-08-31), 5492 total, nothing failing; overall parity
4.5%. The parser is finished, all 1547 compile-parity rows build a **node graph**, and
`src/FuzzyRegex/Engine/` now holds the whole of `_regex.c`'s compiler. Nothing matches yet: every
match entry point still throws `NotImplementedException`, and `Match` is a fieldless stub.

**The oracle is live, and rule 7 applies.** `pwsh -File tools/run-oracle.ps1` before committing any
slice that touches the engine; minimise every divergence into a permanent test in
`tests/FuzzyRegex.Tests/Gaps/`. Latest run: `agree 0  unsupported 600  diverge 0`.

**Blockers:** none.

**S16 inherits these three duties:**

- **Store the node graph.** `FuzzyRegex`'s constructor builds it and discards it (IDE0052 forbids a
  field nothing reads). S16 is the slice with a reader, so S16 keeps it.
- **Pin `OracleComparer.Run`'s `whileMatching: true` attribution.** Unreachable while every match
  entry point throws, and marked at the line. Without it a crash in the engine can be scored as
  agreement.
- **Decide CA1051/S1104/MA0008** per the `.editorconfig` note. S15 measured it and none fired -
  `Node` is a class with internal fields, `CompileArgs` a struct with reference-typed fields - so
  the note is still untested against the public-field struct it was written for. S16 lands one.

**Worth knowing before the next slice:**

- **A generator is only worth adding once something can match it.** S15 added none: an all-`unsupported`
  wave proves nothing. S16 is the first slice that can add a real one.
- **The span convention:** engine internals are `(start, end)`; the public API is `(Index, Length)`.
  Converted in exactly two places - `Match`/`Group`'s accessors and the oracle recorder.
- **`\uXXXX` written through any agent tool is decoded before it reaches disk.** Build such text
  with `chr(0x5c) + 'u2028'` in a Python file instead.
- **Do not edit a C# file with Python's `write_text`** - CRLF becomes LF and IDE0055 fails the build.
- **Scratch lives in `.scratch/`** (gitignored). The driver fails any slice whose tree is dirty.
