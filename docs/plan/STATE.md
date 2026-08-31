# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Phase:** 3, the engine. **S22 is DONE** and committed; the tree is clean. Pending queue: S23-S26.

**Current slice:** none in flight. **Next is S23** (`reverse matching`), delivering `right-to-left`
(49 tests) - though `find-all` (181, S25) is now much the biggest tag left.

**Where the port stands:** ratchet GREEN, 4702 passing, 5627 total, nothing failing; overall parity
**53.0%** (was 42.2%). `Various` 95.4%, `CaseFolding` 97.1%, `Quantifiers` 98.1%, `Groups` 98.5%,
`UnicodeProperties` 74.3%, `Boundaries` 58.5%, `Regressions` 23.9%. The matcher now has every
forward `_IGN` and `_FLD` opcode on top of S16-S21. Remaining wins: `find-all` 181 (S25),
`substitution` 104 (S24), `right-to-left` 49 (S23).

**Oracle:** `pwsh -File tools/run-oracle.ps1` before committing any engine slice; its default list
now has nine generators, `case-folding` included. Latest: `agree 13500 unsupported 0 diverge 0` at
1500 rows each, at seeds 22, 555 and 777. Of five negative controls, four fired (15, 7, 5 and 6
divergences of 600, each re-run at a second seed) and one did not fire reliably - all five are
reproducible from the S22 closing notes.

**Blockers:** none.

**S23 inherits these duties:**

- **Probe upstream with `regex.match`, never `regex.search`.** Upstream's `search_start` prefilter,
  which this port defers to Phase 7, screens start positions with predicates its own matcher does
  not use, so `search` can answer differently from `match` on the same pair. An S22 probe that used
  `search` alone made a correct transliteration look wrong and cost most of the slice.
- **The wave does not cover `REF_GROUP_FLD`'s captured-side folding.** S22's control D fired 0, 4
  and 1 at three seeds after three attempts to widen the generator. The gap tests catch it
  deterministically; do not trust the wave for that opcode.
- **Re-take every measurement you quote, after the last change to the thing measured.** The
  recorder seeds a generator with `random.Random(f"{seed}:{name}")`, so a figure measured with
  `random.Random(seed)` describes a wave it never produces.
- **`baseline:` in the ratchet's verdict is a count of *unique* ids, so it reads below `passing:`
  by design.** Not a stale baseline; do not "fix" it.
- **When a slice delivers an opcode family, take it out of `Matcher.Tag`.** The rule and its reason
  are in the comment at `Matcher.cs:88`.
