# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Phase:** 3, the engine. **S24 is DONE** and committed; the tree is clean. Pending queue: S25-S26.

**Current slice:** none in flight. **Next is S25** (`find-all`), the biggest tag left at 195 tests,
which also carries `splitting` (38) and `overlapped` (14).

**Where the port stands:** ratchet GREEN, 4890 passing, 5691 total, nothing failing; overall parity
**59.3%** (was 53.5%). `Format` 100%, `Escapes` 100%, `FullMatch` 100%, `Groups` 98.5%,
`Quantifiers` 98.1%, `CaseFolding` 97.1%, `Substitution` 95.5%, `Various` 95.4%, `Anchors` 91.7%,
`UnicodeProperties` 74.3%, `Boundaries` 58.5%, `Regressions` 27.8%. `substitution` and `format` are
off the board. Remaining wins: `find-all` 195 (S25), `partial` 82 (Phase 4), `lookaround` 61.

**Oracle:** `pwsh -File tools/run-oracle.ps1` before committing any engine slice; its default list
now has eleven generators, `substitution` included. Latest: `agree 16500 unsupported 0 diverge 0`
at 1500 rows each, seed 606. Of six negative controls all six fired (163, 37, 94, 130, 16 and 21
divergences of 600, each re-run at seed 55) and all six are reproducible from the S24 closing notes.

**Blockers:** none.

**S25 inherits these duties:**

- **The scan-advance S25 needs is already ported.** `Engine.Substitution.Subx` carries the
  empty-match policy (`state.MustAdvance = state.MatchPos == state.TextPos` after each match);
  `scanner_next` (`:20897`) and `pattern_findall` (`:22470`) are the same loop plus an overlapped
  variant that steps one character and clears `MustAdvance`. Reuse it, do not re-derive it.
- **The V0/V1 empty-match difference does not exist in this release.** No version test appears in
  `pattern_subx` at all. Pinned in `Gaps/Substitution/SubstitutionRulesTests.cs`; do not go hunting.
- **A count/limit convention is inverted against upstream at BOTH ends** - upstream 0 is no limit
  and upstream negative is none at all, where ours are -1 and 0. S24 translated one end and the
  oracle reported a correct port as RED. `maxsplit` will have the same shape.
- **Probe upstream with `regex.match`, never `regex.search`** - the `search_start` prefilter this
  port defers to Phase 7 screens start positions with predicates its own matcher does not use.
- **Re-take every measurement you quote, after the last change to the thing measured.** S24 quoted
  generator figures taken before a one-constant widening that shifts the whole RNG stream; four of
  the five were wrong and the second blind pass caught it.
