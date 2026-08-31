# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Phase:** 3, the engine. **S14 is DONE** and committed; the tree is clean. Pending queue: S15-S26.

**Current slice:** none in flight. **Next is S15** (`re_compile`, `_regex.c:25863-26121`): the code
list becomes the node graph, and it **must reject the five patterns this port still compiles** -
listed in S13's closing notes, and now visible to the oracle as divergences (see below).

**Where the port stands:** ratchet GREEN, 2046 passing (baseline 2040 recorded ids - the two
legitimately differ, DECISIONS 2026-08-31), 3926 total, nothing failing; overall parity 4.5%. The
parser is finished and all 1659 compile-parity corpus rows pass. The engine does not exist: every
match method throws `NotImplementedException`, and `Match` is a fieldless stub.

**The oracle is live, and rule 7 now applies.** `pwsh -File tools/run-oracle.ps1` records a wave
from Python `regex` and diffs this port's matching against it. **Run it before committing any slice
that touches the engine**, and minimise every divergence into a permanent test in
`tests/FuzzyRegex.Tests/Gaps/`. Latest run: `agree 0  unsupported 1000  diverge 0`.

**Blockers:** none.

**Worth knowing before the next slice:**

- **The oracle can already fail on a fuzzy pattern.** A pattern upstream rejects and this port
  compiles is reported as a divergence, not as unsupported - so a generator emitting fuzzy patterns
  before S15 lands `re_compile` will legitimately turn the wave red. Today's generators emit
  literals only. S15 closing this is what makes such a generator safe to add.
- **S16 owes one test the oracle cannot have yet:** `OracleComparer.Run`'s `whileMatching: true`
  attribution is unreachable while every match entry point throws, and is marked at the line. The
  first slice with a matcher must pin it, or a crash in the engine can be scored as agreement.
- **Add a generator per slice**, matched to what that slice ported. A generator ahead of the engine
  produces an all-`unsupported` wave and proves nothing.
- **Three blind passes earned their keep on S14** (7 findings, all real): pass 2 found a defect in
  pass 1's fix, pass 3 was a first pass over code no reviewer had seen. Prove each fix by deleting
  the guard and watching the assertion fail, not by re-running the reviewer.
- **The span convention:** engine internals are `(start, end)`; the public API is `(Index, Length)`.
  Converted in exactly two places - `Match`/`Group`'s accessors and the oracle recorder. A third is
  a defect by definition (DECISIONS 2026-08-31).
- **`\uXXXX` written through any agent tool is decoded before it reaches disk.** Build such text
  with `chr(0x5c) + 'u2028'` in a Python file instead.
- **Do not edit a C# file with Python's `write_text`** - CRLF becomes LF and IDE0055 fails the build.
- **Scratch lives in `.scratch/`** (gitignored). The driver fails any slice whose tree is dirty.
