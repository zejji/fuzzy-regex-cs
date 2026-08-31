# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Phase:** 3, the engine. **S19 is DONE** and committed; the tree is clean. Pending queue: S20-S26.

**Current slice:** none in flight. **Next is S20** (`word, default and grapheme boundaries, and
KEEP`), delivering `anchors`, `line-boundaries`, `word-flag`, `keep-marker` and `grapheme` - 136
tests between them.

**Where the port stands:** ratchet GREEN, 4239 passing, 5538 total, nothing failing; overall parity
**34.0%** (was 21.8%). `Escapes`, `Api` and `Basics` are at 100%, `Quantifiers` **98.1%**,
`UnicodeProperties` 70%, `Groups` 69.2%, `Various` 54.6%. The matcher now has the whole repeat family
- `GREEDY_REPEAT`/`LAZY_REPEAT`, both `*_ONE` fast paths, `BODY_*`/`MATCH_*`/`TAIL_START` and the
position guards - on top of S16-S18's literals, classes, plain anchors, `BRANCH` and groups. The
biggest remaining wins are `ignore-case` 154 (S22), `find-all` 143 (S25) and `substitution` 98 (S24).

**Oracle:** `pwsh -File tools/run-oracle.ps1` before committing any engine slice. Latest:
`agree 9000  unsupported 0  diverge 0` over six generators (the new `quantifiers` one included) at
1500 rows each. Three negative controls fired: 148, 54 and 1 divergences of 600.

**Blockers:** none.

**S20 inherits these duties:**

- **Add a generator per capability it delivers**, and run a negative control on it. Two of S19's
  five attempted controls did not fire - one hit a branch that is dead in practice, one hung instead
  of diverging - so budget for replacing one, and record each divergence count.
- **Retag, do not just un-skip.** S19 removed 94 attributes and re-skipped 13 with prose. No fan-out
  method needed splitting this time; if one does, diff its `[Arguments]` rows against
  `git show HEAD:<path>` afterwards.

**Worth knowing before the next slice:**

- **Write the slice file with the grep in hand.** S18's claimed `save_captures`, S19's claimed
  `push_repeats`; both are Phase 4/5 helpers with no Phase 3 caller, and both took one grep.
- **The Phase 7 prefilters are not only a speed matter.** `locate_required_string` is why upstream
  answers `(a|a)*b` instantly on a subject holding no `b`; without the prefilter upstream runs the
  same exponential search this port runs (23.3s at n=26). Never diagnose a performance divergence
  without checking both sides take the same path. DECISIONS 2026-08-31.
- **A repeat count is characters; our positions are UTF-16 code units.** `Matcher.StepBy` and
  `CountBetween` are the only two places that conversion lives.
- **`dotnet test tests/FuzzyRegex.Tests` printed "Zero tests ran" once** and worked from
  `tools/check-ratchet.ps1` a minute later. Use `dotnet run --project` for an ad-hoc run.
- **Run the ratchet AFTER committing as well as before** - the pre-commit CSharpier hook rewrites
  files (DECISIONS 2026-08-31). Scratch lives in `.scratch/`; the driver fails a dirty tree.
