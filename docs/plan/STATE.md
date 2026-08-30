# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Phase:** 2 - parser, compiler, Unicode tables, pattern-level public API. Eight slices (S06-S13);
S06 to S11 done, two pending.

**Current slice:** none in flight. The tree is clean.

**Where S11 left the port:** ratchet GREEN, 1692 passing tests (baseline 1692), 3865 total, nothing
failing; overall parity 0.7%. **The parser is finished apart from S13's two constructs.** Every
`(?` form, backreferences, group calls, recursion, conditionals, lookaround, atomic groups, branch
reset, the verbs, `\R` and `\X` are ported. 1443 of the corpus's 1662 rows pass and none fails;
all 219 skips are S13's - 127 `fuzzy-syntax`, 62 `substitution`, 30 `named-lists`.

**Next action:** S12, `docs/plan/slices/S12-replacement-templates-and-escape.md`. Nothing blocks it:
`PatternCompiler.CompileReplacement` is the last shape-only seam and the 62 template corpus rows
are its oracle.

**Blockers:** none.

**Worth knowing before the next slice:**

- **The corpus is blind to whole constructs, so measure before trusting it.**
  `.scratch/corpus_coverage.py` (rewrite it, it was scratch) found zero rows for relative group
  calls. A differential wave found two real defects S11 would otherwise have shipped.
- **The wave recipe:** import `tools/record-compile-corpus.py` for its set-order patches and
  `Recorder`, feed your own pattern list, and compare against a scratch console in `.scratch/wave/`
  whose `AssemblyName` is `FuzzyRegex.Benchmarks` so `InternalsVisibleTo` reaches the parser.
- **Three divergence classes are expected and recorded** - UTF-16 offsets, `NotSupportedException`
  where upstream's `ValueError` escapes, and Unicode 17.0 versus the host's 16.0.0. Check PORTMAP's
  "Where we diverge" before treating a wave divergence as a defect.
- **`fuzzy = false` is hard-coded twice in `PatternCompiler.Compile`** and both become real in S13.
- **Regenerating anything Unicode:** `tools/transliterate-unicode.py`, then
  `tools/build-character-names.py`, then `tools/build-lowercase.py`, then
  `tools/record-unicode-fixtures.py` - in that order. `oracle.yml` runs all four with `--check`.
- **Local `regex` is 2026.7.19; upstream is pinned at 2026.8.12.** CI builds the oracle from the
  pinned submodule.
- **Scratch lives in `.scratch/`** (gitignored). The driver fails any slice whose tree is dirty.
