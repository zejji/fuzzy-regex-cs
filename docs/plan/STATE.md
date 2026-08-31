# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Phase:** 3, the engine. **S20 is DONE** and committed; the tree is clean. Pending queue: S21-S26.

**Current slice:** none in flight. **Next is S21** (`backrefs and group conditionals`), delivering
`backrefs` (45 tests) and `conditionals` (58).

**Where the port stands:** ratchet GREEN, 4343 passing, 5542 total, nothing failing; overall parity
**39.0%** (was 34.0%). `Escapes`, `Api`, `Basics` and `Atomic` are at 100%, `Quantifiers` 98.1%,
`Captures` 87.5%, `UnicodeProperties` 70%, `Groups` 69.2%, `Various` 65.8%, `Boundaries` 58.5%. The
matcher now has every zero-width predicate - `\b \B \m \M \K \X`, the `(?w)` DEFAULT_ opcodes and
the UAX #29 WB and GB rule sets - on top of S16-S19's literals, classes, anchors, groups and
repeats. The biggest remaining wins are `find-all` 163 (S25), `ignore-case` 154 (S22) and
`substitution` 104 (S24).

**Oracle:** `pwsh -File tools/run-oracle.ps1` before committing any engine slice. Latest:
`agree 10500  unsupported 0  diverge 0` over seven generators (the new `boundaries` one included) at
1500 rows each. Four negative controls fired: 1, 2, 2 and 3 divergences of 600, all reproducible
from the S20 closing notes.

**Blockers:** none.

**S21 inherits these duties:**

- **Add a generator per capability it delivers, and run a negative control on it.** Three of S20's
  four controls fired on nothing until the generator was widened - budget time for that loop, not
  just for one run. Record the four values the skill asks for.
- **Retag, do not just un-skip.** S20 removed 71 attributes and put 44 back with new tags.

**Worth knowing before the next slice:**

- **Check what a construct actually compiles to before scoping a slice.** `\X` compiles to
  `Atomic(LazyRepeat(AnyAll(),1,None) + GraphemeBoundary())`, so S20 had to port `ATOMIC` and
  `END_ATOMIC` - unscoped work - to deliver the `grapheme` tag at all. DECISIONS 2026-08-31.
- **`(?w)` works inline; `FuzzyRegexOptions.Word` still does not exist.** Adding it is an owner
  decision, not a slice's.
- **`lookaround` blocks more than the 40 tests the board shows**: three S20 retags and two atomic
  tests are waiting on it, filed under other tags.
- **A repeat count is characters; our positions are UTF-16 code units.** `Matcher.StepBy`,
  `CountBetween` and now `CountRegionalIndicatorsLeft` are where that conversion lives.
- **Run the ratchet AFTER committing as well as before** - the pre-commit CSharpier hook rewrites
  files. Scratch lives in `.scratch/`; the driver fails a dirty tree. Delete
  `upstream/regex/__pycache__` if you import the module from the submodule path.
