---
slice: S20
phase: 3
title: Word, default and grapheme boundaries, and the KEEP marker
delivers: [anchors, line-boundaries, word-flag, keep-marker, grapheme]
---

# S20 - Word, default and grapheme boundaries, and the KEEP marker

Needs S17 (word boundaries are property lookups). The zero-width predicates S16 did not cover:
everything that asks "is this position at a boundary" rather than "does this character match".

## Scope

All line references are `upstream/src/_regex.c`.

- **Main-switch cases**: `BOUNDARY` (`:12060`), `DEFAULT_BOUNDARY` (`:12255`),
  `DEFAULT_START_OF_WORD` / `DEFAULT_END_OF_WORD` (`:12294`, `:12274`), `START_OF_WORD` /
  `END_OF_WORD` (`:14700`, `:13111`), `GRAPHEME_BOUNDARY` (`:13157`), `KEEP` (`:13543`) with its
  backtrack case (`:16439`); the zero-width entries in the shared backtrack block
  (`:15330-15344`) that S16 did not already land.
- **Word predicates, per encoding**: `unicode_word_left` / `unicode_word_right` (`:1447`,
  `:1453`), `unicode_at_boundary` (`:1460`), `unicode_at_word_start` / `_end` (`:1471`,
  `:1482`); the ASCII counterparts (`:849-893`). The WORD flag (`(?w)`) selects the DEFAULT_
  opcodes at parse time (already done), whose predicates are the Unicode word-break rules:
  `unicode_at_default_boundary` (`:1531-1751`, WB5-WB16 with `is_unicode_vowel` `:1496` and the
  apostrophe special case `:1514`), `unicode_at_default_word_start_or_end` (`:1752-1785`).
- **Grapheme**: `unicode_at_grapheme_boundary` (`:1786-1935`, the GB rules over the GCB
  property, which the transliterated tables already carry - `"GCB"` is in
  `UnicodePropertyNames.g.cs`). This is what makes `\X` match a full cluster, so the `grapheme`
  tests un-skip here.
- **KEEP** (`\K`): moves the reported match start; its interaction with what `Match.Index`
  reports is exactly the two-place span convention again - the engine records `match_pos`,
  the accessor converts.

## Verification

- **Un-skip** the residual `needs:anchors` tests S16 left (word-boundary ones), plus
  `line-boundaries`, `word-flag`, `keep-marker` and `grapheme`, reading each skip's prose first;
  stragglers retag with prose.
- **Oracle wave**: `\b \B \m \M` (word start/end) over scripts with and without cased letters,
  under plain, ASCII and WORD flags; `\X` over combining sequences, ZWJ emoji, regional-indicator
  pairs and CRLF; `\K` in and out of alternations and repeats, comparing the reported span. The
  WB/GB rule tables are the risk - upstream implements a specific Unicode version's rules by
  hand, so probe the boundary cases its code comments name. Zero divergences; negative control.

## Done when

- [x] The five tags delivered or stragglers retagged; counts in closing notes.
- [x] Oracle wave green including the emoji/ZWJ/regional-indicator grapheme probes; counts
      quoted.
- [x] `docs/PORTMAP.md` updated.
- [x] Ratchet GREEN, baseline updated, blind review (hunt: a word-break rule ported with `>=`
      where upstream chains lookups asymmetrically, a grapheme rule consulting the property of
      the wrong side's codepoint, `\K` inside a lookaround changing the span when upstream says
      it must not - and if lookaround is not portable yet, pin that probe as skipped for Phase
      4), commit.

## Closing notes (2026-08-31)

### What landed

`Matcher` gained the whole zero-width word and grapheme family, in one case group with the rest of
the zero-width opcodes: `BOUNDARY`, `DEFAULT_BOUNDARY`, `DEFAULT_START_OF_WORD`,
`DEFAULT_END_OF_WORD`, `START_OF_WORD`, `END_OF_WORD`, `GRAPHEME_BOUNDARY`, plus `KEEP` with its
backtrack case. Behind them: `WordLeft`/`WordRight`/`AtBoundary`/`AtWordStart`/`AtWordEnd` taking the
encoding, `AtDefaultBoundary` (WB1-WB999 verbatim), `AtDefaultWordStartOrEnd`, `AtGraphemeBoundary`
(GB1-GB999 verbatim) and the four small predicates around them.

**The slice also ported `ATOMIC` and `END_ATOMIC`, which it did not scope, because `\X` cannot work
without them.** `_regex_core.Grapheme._compile` (`upstream/regex/_regex_core.py:2919`) compiles `\X`
to `Atomic(Sequence([LazyRepeat(AnyAll(), 1, None), GraphemeBoundary()]))`, so every `\X` pattern
threw `needs:atomic` before it reached a single grapheme rule. The alternative was to port
`GRAPHEME_BOUNDARY` as code no pattern could reach - unreachable, untestable and unverifiable - and
leave `grapheme` undelivered against the slice's own done-criteria. `ATOMIC` cost about 90 lines:
the two forward cases, the two backtrack cases and `push_captures`/`pop_captures` (only each group's
`count` and `current`, never the spans). `push_fuzzy_counts` is left out of both ends, marked
`NOT PORTED`, so the stack block is the same shape at both. The knock-on is that `atomic` is
delivered too and `\R` now works, which is where six of the parity points came from.

### Counts

- **Skips removed: 71** - 63 across 18 files for the five tags (`anchors` 30, `word-flag` 10,
  `line-boundaries` 9, `grapheme` 8, `keep-marker` 6), then 8 more for `needs:atomic` after the
  review. **Skips added back: 44**, every one a test whose boundary opcode now works and whose
  remaining blocker is a later slice's public surface: `find-all` 20, `splitting` 12,
  `substitution` 6, `lookaround` 5, `right-to-left` 1. Net: 5542 tests, **4343 passing** (was 4239),
  1199 skipped, 0 failing.
- **Parity 34.0% to 39.0%** (767 of 1966). `Boundaries` 0% to 58.5%, `Various` 54.6% to 65.8%,
  `Anchors` 0% to 41.7%, `Grapheme` 0% to 40%, `Atomic` 0% to 100%, `Captures` 75% to 87.5%,
  `Regressions` 16.2% to 17.7%. `anchors`, `line-boundaries`, `word-flag`, `keep-marker`, `grapheme`
  and `atomic` have all left the "waiting on a capability" table.

### Oracle

New `boundaries` generator in `tools/record-oracle.py`, added to the default list in
`tools/run-oracle.ps1`. Four pattern shapes (`affix`, `infix`, `keep`, `grapheme`) over six flag
prefixes (`""`, `(?a)`, `(?w)`, `(?V1)`, `(?V1w)`, `(?aw)`) and four subject bands chosen for their
word-break and grapheme classes, with line breaks and whole clusters inserted.

Final run: **`agree 10500  unsupported 0  diverge 0`** over seven generators at 1500 rows each, seed
726405461. Zero `unsupported` is the load-bearing number here - it is what says every `\X` row
actually ran.

### Negative controls

All four against the same wave: **generator `boundaries`, 600 rows, seed 7**
(`python tools/record-oracle.py --generator boundaries --count 600 --seed 7 --output
TestResults/oracle/wave.jsonl`, then `pwsh -File tools/run-oracle.ps1 -SkipRecord`).

> Control A, `ri-count`: in `Matcher.cs`, `CountRegionalIndicatorsLeft`, change `return count;` to
> `return leftPos - pos;`. Result: **599 agree, 1 diverge.**

> Control B, `gb9-extend`: in `Matcher.cs`, `AtGraphemeBoundary` rule GB9, change
> `if (rightProp is UnicodeTables.GbreakExtend or UnicodeTables.GbreakZwj)` to
> `if (rightProp is UnicodeTables.GbreakZwj)`. Result: **598 agree, 2 diverge.**

> Control C, `wb5-letters`: in `Matcher.cs`, `AtDefaultBoundary` rule WB5, change
> `if (IsAhLetter(leftProp) && IsAhLetter(rightProp))` to
> `if (IsAhLetter(leftProp) || IsAhLetter(rightProp))`. Result: **598 agree, 2 diverge.**

> Control D, `keep-restore`: in `Matcher.cs`, the `Opcode.Keep` **backtrack** case, change
> `state.MatchPos = (int)keepMatchPos;` to `_ = keepMatchPos;`. Result: **597 agree, 3 diverge.**

**Three of those four did not fire on the generator as first written, and the generator was changed
until they did.** That is the point of the exercise and it is worth the next slice's attention:

- **C fired 0 of 600** at first. WB1/WB2 answer "there is a boundary here" for the two ends of the
  subject before any other rule runs, and an affix-shaped pattern searched forwards mostly matches
  at position 0, so 48 rows holding `(?w)` and `\b` never reached a rule past WB2. Fixed by the
  `infix` shape - an assertion *between* two characters the subject holds side by side.
- **A fired 0 of 600** after that change. Regional indicators have to be adjacent for the rule to
  decide anything and a per-character draw rarely produces a pair. Fixed by `BOUNDARY_CLUSTERS`,
  which inserts whole clusters (a flag pair, an emoji ZWJ sequence, a base plus combining mark, a
  Devanagari conjunct, `can't`, `3.2`).
- **D fired 0 of 600** after that. None of the first five `\K` shapes can *fail* after the marker,
  so the restore was never exercised. Fixed by three shapes that put the `\K` inside an alternative
  or an optional group.
- Even now A, B and C fire on 1-3 rows of 600. Thin, which is why the four behaviours they cover
  also have dedicated assertions in `tests/FuzzyRegex.Tests/Gaps/Engine/BoundaryTests.cs`.

### Review

One blind pass over the whole diff plus the untracked gap-test file, briefed per
`docs/VERIFICATION.md`. **Three findings raised, three reproduced, three fixed. No engine defect was
found**; every finding was in the skip and status accounting.

1. Three tests re-labelled `needs:find-all` actually need `lookaround` - their patterns hold
   `(?<=\G.*)` and `(?<=[^\n])`, so `FuzzyRegex.Matches` alone would not unskip them. Reproduced
   with `pwsh -File tools/run-oracle.ps1 -Rows .scratch/lookaround.jsonl`:
   `agree 1  unsupported 3  diverge 0  of 4 rows`, the one agreeing row being `\G\w{2}`, which
   really does only need `Matches`. Retagged `needs:lookaround`.
2. Six tests were still skipped `needs:atomic`, which this slice made false. Reproduced by removing
   all eight `needs:atomic` skips and running the suite: six passed, two failed on
   `needs:lookaround`. Those two are now tagged for lookaround.
3. `Seam.For` still routed `Atomic`, `EndAtomic`, the six word-boundary opcodes, `GraphemeBoundary`
   and `Keep` to tags this slice delivers, against the convention documented in the same method and
   in DECISIONS 2026-08-31. Reproduced by grep: every one of them has a real case in the dispatch
   switch, so the arms are unreachable. Removed, with a comment saying which slice delivered them.

**No second pass was run.** The fixes were the reviewer's own three findings applied verbatim - five
skip attributes retagged, eight removed, four dead `Seam.For` arms deleted - so there is no delta
the reviewer did not see, and no public API or tooling changed.

### For the next slice

- **`(?w)` works, `FuzzyRegexOptions.Word` still does not exist.** Every word-flag test that passes
  reaches the flag through an inline `(?w)`. Adding the enum member is a public-API decision the
  owner has not been asked, so it stayed out.
- **`find-all` is now the biggest single win at 163 tests**, ahead of `ignore-case` at 154 - S25's
  `Matches` unblocks a large slice of what S20 had to retag.
- **`lookaround` is 40 tests and blocks more than the table shows.** Three of the retags in this
  slice, and two of the atomic tests, are waiting on it rather than on the capability their old
  prose named.
- **Write a slice file with the grep in hand** - S18, S19 and now S20 have each hit a scope surprise
  the header comments hid. S20's was the opposite of the previous two: the slice under-scoped rather
  than over-scoped, because `\X` compiles through a construct the slice file never mentions.
