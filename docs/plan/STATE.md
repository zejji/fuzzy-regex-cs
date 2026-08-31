# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Phase:** 3, the engine. **S21 is DONE** and committed; the tree is clean. Pending queue: S22-S26.

**Current slice:** none in flight. **Next is S22** (`case-insensitive matching`), delivering
`ignore-case` (154 tests) and `case-folding` (69) - the biggest pair left.

**Where the port stands:** ratchet GREEN, 4416 passing, 5552 total, nothing failing; overall parity
**42.2%** (was 39.0%). `Escapes`, `Api`, `Basics` and `Atomic` are at 100%, `Quantifiers` 98.1%,
`Captures` 87.5%, `Groups` 87.7%, `UnicodeProperties` 70%, `Various` 68.5%, `Boundaries` 58.5%. The
matcher now has `REF_GROUP` and `GROUP_EXISTS` on top of S16-S20's literals, classes, anchors,
groups, repeats and zero-width predicates. Remaining wins: `find-all` 170 (S25), `ignore-case` 154
(S22), `substitution` 104 (S24).

**Oracle:** `pwsh -File tools/run-oracle.ps1` before committing any engine slice; its default list
now has eight generators, `backrefs` included. Latest: `agree 12000  unsupported 0  diverge 0` at
1500 rows each. Of five negative controls, three fired (7, 59 and 17 divergences of 600) and two
did not - both explained in the S21 closing notes, and all five reproducible from there.

**Blockers:** none.

**S22 inherits these duties:**

- **Re-take every measurement you quote, after the last change to the thing measured.** 9 of the 11
  findings across S21's two blind reviews were stale figures in comments. The recorder seeds a
  generator with `random.Random(f"{seed}:{name}")`, so a figure measured with `random.Random(seed)`
  describes a wave it never produces.
- **Read `git status --porcelain` line by line before committing.** A review subagent runs in this
  tree, and S21's first reviewer left `upstream/regex/__pycache__` behind, dirtying the submodule.
  Brief a reviewer to clean up after itself and to leave `parity-baseline.json` alone.
- **`baseline:` in the ratchet's verdict is a count of *unique* ids, so it reads below `passing:`
  by design** - 4309 against 4416 here, 4238 against 4343 at S20. Parameterised rows that share a
  display id collapse. Not a stale baseline; do not "fix" it.
- **When a slice delivers an opcode family, take it out of `Matcher.Tag`.** S21's reviewer caught
  this again; the rule and its reason are in the comment at `Matcher.cs:88`.
