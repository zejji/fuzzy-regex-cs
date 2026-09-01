# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Phase:** 3, the engine. **S23 is DONE** and committed; the tree is clean. Pending queue: S24-S26.

**Current slice:** none in flight. **Next is S24** (`substitution`), delivering `substitution`
(105 tests) - though `find-all` (195, S25) is now much the biggest tag left.

**Where the port stands:** ratchet GREEN, 4752 passing, 5666 total, nothing failing; overall parity
**53.5%** (was 53.0%). `FullMatch` 100%, `Groups` 98.5%, `Quantifiers` 98.1%, `CaseFolding` 97.1%,
`Various` 95.4%, `UnicodeProperties` 74.3%, `Boundaries` 58.5%, `Regressions` 24.6%. Every `_REV`
opcode now runs on top of S16-S22, so `right-to-left` is off the board. Remaining wins: `find-all`
195 (S25), `substitution` 105 (S24), `partial` 82 (Phase 4).

**Oracle:** `pwsh -File tools/run-oracle.ps1` before committing any engine slice; its default list
now has ten generators, `reverse` included. Latest: `agree 15000 unsupported 0 diverge 0` at 1500
rows each, at seeds 923 and 23. Of six negative controls, five fired (274, 9, 23, 21 and 12
divergences of 600, each re-run at a second seed) and one deliberately did not - all six are
reproducible from the S23 closing notes.

**Blockers:** none.

**S24 inherits these duties:**

- **Probe upstream with `regex.match`, never `regex.search`.** Upstream's `search_start` prefilter,
  which this port defers to Phase 7, screens start positions with predicates its own matcher does
  not use, so `search` can answer differently from `match` on the same pair.
- **A backreference must be written before its group under `(?r)`.** Reverse execution takes the
  sequence from the right, so `(?r)(x)\1` reaches `\1` while the group is empty and never matches -
  upstream answers `None` too. Seeing no match there is not a bug.
- **S23's controls B and D are the thin ones.** `STRING_REV` fires 9 of 600 and `STRING_FLD_REV`
  needed a dedicated generator to reach 21. A slice widening the reverse wave should widen those.
- **Re-take every measurement you quote, after the last change to the thing measured.** The
  recorder seeds a generator with `random.Random(f"{seed}:{name}")`, so a figure measured with
  `random.Random(seed)` describes a wave it never produces.
- **`baseline:` in the ratchet's verdict is a count of *unique* ids, so it reads below `passing:`
  by design.** S23 fixed the culture-sensitive comparer that was collapsing 51 of them; what is
  left (4752 against 4645) is genuinely duplicate ids from identical `[Arguments]` rows.
- **When a slice delivers an opcode family, take it out of `Matcher.Tag`.** The rule and its reason
  are in the comment at `Matcher.cs:88`.
