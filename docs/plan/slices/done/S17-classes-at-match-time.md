---
slice: S17
phase: 3
title: Character classes, ranges, sets and properties at match time
delivers: [character-classes, escapes, unicode-properties, set-operations, ascii-flag]
---

# S17 - Character classes, ranges, sets and properties at match time

Needs S16. Membership tests only - single-position opcodes over the S09/S10 tables the parser
already consults - so it is the cheapest big win after the spine: `escapes` (134), `character-
classes` (114), `unicode-properties` (95) all sit behind it. Forward, case-sensitive variants
only; `_IGN`/`_FLD` are S22 and `_REV` is S23.

## Scope

All line references are `upstream/src/_regex.c`.

- **Predicates**: `matches_PROPERTY` (`:2924`), `matches_RANGE` (`:3003`), `in_range` (`:2816`),
  `matches_member` (`:3025`), `in_set_diff` / `in_set_inter` / `in_set_sym_diff` / `in_set_union`
  (`:3155`, `:3201`, `:3236`, `:3278`), `matches_SET` (`:3313`).
- **Property lookup by encoding**: `unicode_has_property` (`:1362`) over the transliterated
  tables (`src/FuzzyRegex/Unicode/UnicodeProperties.g.cs` etc., landed S09), and
  `ascii_has_property` (`:822`) - the ASCII flag's whole matching story is that the encoding
  table swaps, which is why `ascii-flag` is deliverable here. The locale encoding stays not
  ported (PORTMAP).
- **Main-switch cases**: `PROPERTY` (`:13804`), `RANGE` (`:13914`), `SET_DIFF` / `SET_INTER` /
  `SET_SYM_DIFF` / `SET_UNION` (`:14446-14449`); their entries in the shared one-character
  backtrack block (`:15210-15243`).
- The `match_many_*` bulk variants for these ops (`:4045`, `:4485`, `:4737`) only if S16's spine
  already routes single steps through a `match_many`-shaped helper; otherwise they arrive with
  the repeat opcodes in S19, which is their only caller upstream.

## Verification

- **Un-skip** `needs:character-classes`, `escapes`, `unicode-properties`, `set-operations` and
  `ascii-flag`, reading each skip's prose first; tests that also need quantifiers, groups or
  case-insensitivity move to those tags with prose saying so.
- **Oracle wave**: generated classes - single chars, ranges, negations, nested sets with all four
  operators under V1, POSIX classes, `\p{...}` properties (reuse S10's property-name blocks),
  `\d\D\w\W\s\S` under plain and ASCII flags - over subjects spanning ASCII, Latin-1, BMP and
  astral. This is where a wrong table *lookup* first becomes observable (S10 verified the tables'
  content; this verifies the engine reads them at the right codepoint). Zero divergences; quote
  counts; negative control.
- A corpus-style caution from S10 applies: if a set behaves wrongly, check the S09 fixture tests
  are still green before suspecting the engine.

## Done when

- [x] The five tags delivered or their stragglers retagged with prose; counts in the closing
      notes.
- [x] Oracle wave green, counts quoted, any divergence minimised into a permanent test.
- [x] `docs/PORTMAP.md` updated with every symbol ported here.
- [x] Ratchet GREEN, baseline updated, blind review (hunt: a property lookup by `char` instead of
      by codepoint on an astral subject, a set operator evaluated left-to-right where upstream
      nests, a negated set that forgets the newline/ANY interaction), commit.

## Closing notes (2026-08-31)

**What landed.** `Matcher` gained the membership predicates - `InRange`, `MatchesRange`,
`MatchesProperty`, `MatchesMember`, `MatchesSet` and the four `InSet*` walkers - and the dispatch
switch gained real `PROPERTY`, `RANGE` and `SET_DIFF`/`SET_INTER`/`SET_SYM_DIFF`/`SET_UNION` cases.
Upstream's seven separate one-character cases have identical eleven-line bodies, so they are one
case group here with the predicate chosen by a switch, exactly as S16 did with the nine zero-width
cases; `CHARACTER` was folded into that group rather than left as a copy. `Encodings.HasProperty`
gained the `ascii_has_property` overload. The backtrack switch is unchanged: upstream's shared
one-character block (`:15210-15243`) is nothing but `retry_fuzzy_match_item`, so it is still a seam.

**Parity: 6.5% to 18.8%** (128 to 369 of 1,966 ported upstream tests). 5,516 tests, 3,917 passing,
1,599 skipped, nothing failing. `Escapes` went 5.8% to **100%**, `UnicodeProperties` 0% to 70%,
`Various` 14.3% to 25.8%, `Regressions` 3.4% to 9.7%. All five delivered tags are gone from the
"waiting on a capability" table entirely.

**`ENCODING_KIND` is real, and reasoning nearly got it wrong.** The macro (`_regex.c:167`) reads
bits 16-17 of `node->status`, which are also `RE_STATUS_HAS_GROUPS` and `RE_STATUS_HAS_REPEATS`,
and *nothing in `_regex.c` ever writes those bits* - grep finds only the `#define` and the macro.
The obvious conclusion, that the switch is dead and always takes its `default` arm, is **wrong**:
the bits arrive from the Python side, where `Property._compile` ORs `self.encoding <<
ENCODING_OP_SHIFT` (5) into the code word's flags and `create_node` then shifts the whole flags word
by `RE_STATUS_SHIFT` (11), landing it on 16-17. Proved by intercepting `_regex.compile`:
`(?a:\d)` emits flags `33` (0x21) and `(?u:\d)` emits `65` (0x41) where plain `\d` emits `1`. This
**is** the scoped ASCII flag's whole matching story, and without it `(?a:\d)` matched U+FF19
FULLWIDTH DIGIT NINE. Caught by a ported test, not by reading. Pinned by
`Gaps/Engine/ScopedEncodingTests`, eleven rows recorded from upstream covering both directions of
scoping, a scoped node inside a set (so `matches_member`'s own copy of the switch is exercised) and
a POSIX class.

**Tag accounting.** 84 skip attributes carrying the five tags were removed; 300 tests then failed,
all for the expected reason. After the engine landed, 156 still failed - none on an assertion, every
one at a seam for another slice - and were retagged: **132 `find-all`** (`Matches`/`Count` are S25,
the class itself matches), **57 `groups`** (S18), **~40 `quantifiers`** (S19) and **~9
`ignore-case`** (S22). Six fan-out methods had to be split because only some of their rows were
blocked: `test_various`'s 60-row and 17-row tables in `VariousCharacterClassTests` (into 27 + 15 +
18 and 15 + 1 + 1), `VariousEscapeTests` (9 + 3 + 1), `SetTests`'s first table (2 + 2) and
`CyrillicPropertyFormsTests` (10 + 1). Every `[Arguments]` row was checked against
`git show HEAD:<path>` for altered data; four rows moved into single-case methods and nothing else
changed.

**Analyzer decision: S4144 disapplied for `tests/FuzzyRegex.Tests/Ported/**` only.** Splitting a
ported table test by capability necessarily leaves several methods with identical bodies - upstream
drives its whole 524-row table through one loop and one assertion, and what distinguishes two
methods here is their `[Arguments]` data and their `[Skip]` tag. Merging them back to satisfy the
rule would delete the per-capability gap information `docs/STATUS.md` is generated from, and
extracting the body into a helper does not help either: the callers are then identical one-liners.
Scoped to `Ported/` rather than all of `tests/`, because that reason is only true there.

**Oracle.** New `classes` generator: 39 class atoms (single chars, ranges, negations, POSIX
classes, `\d\D\w\W\s\S`, `\p{...}`) plus 10 V1 set-operator atoms nested one level, one to three
per pattern weighted towards one, over four subject bands - ASCII, Latin-1, BMP and astral - with
the ASCII flag on half the rows. Final wave **`agree 6000  unsupported 0  diverge 0`** over
`literals`, `literal-dot`, `anchors` and `classes` at 1,500 rows each (seed 126115823). Two negative
controls, both on the seed the clean run had just passed: inverting `InSetSymDiff`'s toggle gave
**6 divergences of 1,200**, and swapping the ASCII property table for the Unicode one gave **133 of
1,200**. `tools/run-oracle.ps1`'s default generator list now includes `classes`, so CI picks it up.

**Review.** One blind pass, reproduction-gated. It raised **1 finding, reproduced and fixed**:
`Seam.Tag` still routed `PropertyIgn`, `RangeIgn` and the four `Set*Ign` opcodes to
`needs:unicode-properties` and `needs:character-classes` - capabilities this slice delivers - so
`(?i)\p{Cyrillic}` threw `needs:unicode-properties` while the matching test was tagged
`needs:ignore-case`, which would have put a delivered capability on an oracle `unsupported` row and
in a stack trace. Reproduced with a scratch console app against the real library before touching
anything; all seven remaining `*_IGN` opcodes now name `ignore-case`. The reviewer independently
ran ~36,000 hand-built oracle rows (including 5,402 astral) plus 4,000 generated ones and found no
divergence, and diffed every `[Arguments]` multiset against `HEAD`. **No second pass**: the fix is
one arm of a switch in `Matcher.cs`, inside the file the reviewer had just read and reported on, and
it added no public API and changed no tooling.

**Worth knowing next.** The `*_IGN` predicates (`in_range_ign`, `matches_member_ign`,
`matches_SET_IGN`, `in_set_*_ign`, `:2821`-`:3356`) are deliberately left for S22 and are a
mechanical repeat of what landed here, with `encoding->all_cases` on the front. The `match_many_*`
bulk variants (`:4045`, `:4485`, `:4737`) are S19's: their only callers upstream are the repeat
opcodes, and this spine still steps one character at a time. `NodeStatus.EncodingKind` reads the
same two bits as `HasGroups`/`HasRepeats`, which is safe only because no node carries both meanings
- a slice that starts setting `HAS_GROUPS` on a `PROPERTY` node would break the ASCII flag silently.
