---
slice: S40
phase: 5
title: The fuzzy test constraint, and delivering the six plain fuzzy tags
delivers: [fuzzy-matching, fuzzy-budget, fuzzy-counts, fuzzy-changes, fuzzy-insertion, fuzzy-deletion, fuzzy-substitution]
---

# S40 - The `{...:test}` constraint, and the six plain fuzzy tags

The last piece of plain (non-`(?e)`, non-`(?b)`) fuzzy matching, and the slice where every test
on the seven tags above must be green - whichever earlier slice's probe already un-skipped some.
Depends on S38 and S39.

All line references are `upstream/src/_regex.c` unless marked.

## Scope

- **The test constraint** `{e<=1:[a-z]}`: `FUZZY_EXT`'s compilation is already ported
  (`NodeCompiler.cs:444`); the engine half is the switch in `fuzzy_ext_match` (`:9938-10032`) and
  `fuzzy_ext_match_group_fld` (`:10033-10115`), whose arms are `CHARACTER*`, `PROPERTY*`, `RANGE*`,
  `SET_*` forward and reversed, `_IGN` and not. Upstream's changelog notes it did not support sets
  until issue 371 (`upstream/changelog.txt:543-545`): the generator must include a set test.
- **The five inputs that must keep being rejected**: `{e<=1:\X}`, `{e<=1:\b}`, `{e<=1:\A}`,
  `{e<=1:\Z}`, `{e<=1:\L<a>}` and a backreference test `(a)(?:abc){e<=1:\1}`. Upstream's parser
  accepts each, emits exactly this port's bytecode, and its C engine answers
  `RuntimeError: invalid RE code`; S15 rejects them with `NotSupportedException("invalid RE code")`,
  pinned by `Gaps/Engine/NodeGraphTests.cs:49` and `:102`. Do not "fix" them into working: a test
  that upstream refuses at runtime is not a feature this port can verify, and the ledger is the
  place to record that upstream should reject them at compile time (ledger entry, no filing).
- **The seven tags.** After the constraint lands, remove every remaining `needs:` skip on the seven
  tags in the frontmatter and make every test green. A red test is analysed to its upstream line,
  not retagged; if the ported test itself is wrong, prove it against upstream (run
  `.venvs/regex-2026.9.10` or the pinned oracle interpreter and quote the output) before touching
  it, per the skill's rule. The nine tests on `fuzzy-insertion`, `fuzzy-deletion` and
  `fuzzy-substitution` were tagged from measured `fuzzy_counts` (DECISIONS 2026-08-30), so their
  counts are the ground truth to compare against.
- **`{e<=0}` is a no-op constraint upstream does not elide** (issue 596, a 210x slowdown). Port
  faithfully; a gap test pins that it matches exactly like the plain pattern. Elision is Phase 7's
  question and the issue is on Phase 6's list.

## Verification

- Gap tests first: each `fuzzy_ext_match` arm with a positive and a negative test character,
  forward and `(?r)`, case-insensitive and full-fold; the six rejections still rejected.
- **Widen the `fuzzy` generator** with test constraints (`:[a-z]`, `:\d`, `:.`, `:(?i)x`, `:[^\s]`)
  on every body shape S38 and S39 generate. GREEN at three seeds, 2000 rows; then the default wave
  GREEN at three seeds, 6000 rows.
- All 183 minus the `fuzzy-bestmatch` (18) and `fuzzy-enhancematch` (6) tests un-skipped and green;
  the counts in the closing notes per tag and per file.
- Negative controls: `fuzzy_ext_match` returning `TRUE` for a set arm; a test consulted at the
  wrong position (`new_text_pos` versus `text_pos`); a `_REV` test reading past `slice_start`.

## Done when

- [x] The seven tags delivered **as amended** - see the closing notes: three were already skip-free,
      the constraint and every plain row of the other four landed, and 31 ranking-mode test cases
      moved to `fuzzy-enhancematch`/`fuzzy-bestmatch`. Zero `needs:` skips remain on any of the
      seven. The six rejections are still pinned, and the backreference one gained a test.
- [x] `fuzzy` generator green at three seeds with test constraints. **Default wave at 6000 not
      green, and not runnable: upstream hangs.** See "The two things this slice could not do".
- [x] PORTMAP: every row of the fuzzy bucket except the `do_best`/`do_enhanced` machinery marked
      ported.
- [x] Ratchet GREEN, baseline updated, blind review, commit.

---

# Closing notes

**Parity 91.1% -> 97.2%** (1911 of 1966 ported upstream tests). `Fuzzy` 14.3% -> 88.4%,
`CaseFolding` and `Regressions` both up, 29 -> 30 areas at 100%. Suite 5821 tests, 5766 passing,
**55 skipped where there were 174**, and every one of the 55 is `fuzzy-bestmatch` (25) or
`fuzzy-enhancematch` (30). Ratchet GREEN, baseline 5524 -> 5658 distinct passing ids.

## What landed

**The switch, and it was smaller than the seam suggested.** `fuzzy_ext_match` (`:9938`),
`folded_char_at` (`:10014`) and `fuzzy_ext_match_group_fld` (`:10033`) replace the two
`Seam.For(Opcode.FuzzyExt)` throws. Upstream writes one `case` per opcode, each the same two lines
with a different `matches_*` call; this port already has that choice factored out as
`Matcher.MatchesOne`, so the arms group by **direction** instead - forward reads `CharAt(pos)`
against `pos < SliceEnd`, reversed reads `CharBefore(pos)` against `pos > SliceStart`. The opcode
lists are upstream's exactly, arm for arm, and the blind review checked them that way.

**Both switches are asymmetric, and both asymmetries are upstream's.** `fuzzy_ext_match` has no
`SET_*_REV` or `SET_*_IGN_REV` arm; `fuzzy_ext_match_group_fld` has no `SET_*_IGN` arm either. An
opcode the switch does not list falls off the end to `return TRUE`, so a reversed set test - and,
inside a case folding, any set test - constrains nothing where the same test forward would bite.
Measured on regex 2026.7.19, not inferred: `(?<=(?:[ab][cd]){e<=1:[0-9x]})$` against `'azc'`
substitutes, where `{e<=1:x}` has to delete instead. `.` is a no-op everywhere for the same reason,
ANY having no arm at all. DECISIONS has both; whether upstream should hear about it is Phase 6's.

**Two ports that can never run, kept because upstream writes them.** The fuzzy-test grammar accepts
only a character set - `{e<=1:(?-i:x)}` and `{e<=1:(?i)x}` are both
`error: expected character set` - so a test compiled under `(?fi)` is always an `_IGN` opcode and
`fuzzy_ext_match_group_fld`'s non-`_IGN` arms are unreachable. The same grammar makes `SET_DIFF`,
`SET_INTER` and `SET_SYM_DIFF` reachable **only under `(?V1)`**, which the oracle generator does not
emit, so `FuzzyTestConstraintTests.The_other_three_set_opcodes_reach_the_same_arm` covers those
three directly.

**The tags, and the amendment they forced.** With the constraint in, 31 test cases still tagged
`fuzzy-matching`, `fuzzy-budget`, `fuzzy-counts` or `fuzzy-changes` were red - and **every one of
the 31 failed at the engine's own ENHANCEMATCH or BESTMATCH seam, not on an assertion.** Their
patterns carry `(?e)`, `(?b)`, `(?be)` or `FuzzyRegexOptions.BestMatch`. They are now tagged for the
capability that gates them, and eight upstream methods that mixed plain and ranking rows in one
`[Arguments]` fan-out were split so a plain row is not skipped to keep a `(?e)` sibling company.
**Design spec amendment 18** and the ROADMAP's Phase 5 paragraph record it: no slice moved, no scope
changed, only tags. The 55 skips left are exactly S41's and S42's scope, which is the number those
slices need.

**Gap tests**: `Gaps/Engine/FuzzyTestConstraintTests.cs`, 15 tests, every value measured by
`tools/probes/upstream-fuzzy-ext-test.py` against regex 2026.7.19 on 2026-09-13 and quoted beside
its assertion. One arm family per test, forward and reversed, plain and `_IGN`, plus the two no-op
asymmetries, the `{e<=0}` non-elision (upstream issue 596), and the backreference rejection
`(a)(?:abc){e<=1:\1}` that the slice file named and no test held.

**The generator** draws a `{...:test}` on about a third of constraints, inner section as well as
outer, from a pool with one entry per arm family plus `.` for the no-arm case.

## The two things this slice could not do, neither of them its own

**1. The default wave at 6000 rows will not run, because upstream hangs.** The slice file asked for
it; no slice has ever run the default wave at 6000 (S37's 6000 was `interactions` alone, S39's
default wave was 600 a generator). At seed 4242 the recorder stops dead in the `verbs` generator and
never returns. Bisected to one row and then minimised by hand:

> `regex.search('.?x(?>a(*SKIP)z)', 'xzxa')` **never returns** on regex 2026.7.19.

It needs all three of an optional leading item, an atomic group, and `(*SKIP)` inside it:
`(*PRUNE)` instead returns `None`, a non-atomic group returns `None`, and dropping the leading `.?`
returns `None`. The original row was `verbs` row 5944 at seed 4242, count 6000, pattern
`[^a]?\U0001f600(?>[a\d]{1,3}(*SKIP)\p{Ll})` on `'\U00010400\r_\U0001f600\xdf\U0001f600aA'`.
Reproduce with `.scratch`-style scripts; the minimisation ladder is in the closing notes above.
This is a new upstream infinite loop, the same family as issues 551 and 554 but a different shape,
and it belongs in the ledger.

**2. At seed 7, where the 6000-row wave does complete, four rows diverge - and all four are
HEAD's, not S40's.** Rows 97927 and 98956 (`partial`), 103926 (`partial-sliced`) and 117679
(`verbs`). **Proven, not argued:** the wave was saved, a worktree was checked out at HEAD (26e2a20),
and its engine consumed the identical wave with `-SkipRecord`. HEAD reports *the same four rows*.
It also reports 1432 `unsupported` where this tree reports 0, which is both S40's gain and the proof
that the two runs really did use different engines.

    working tree  agree 125959  unsupported    0  expected 37  diverge 4  of 126000 rows
    HEAD 26e2a20  agree 124527  unsupported 1432  expected 37  diverge 4  of 126000 rows

## A third finding, from the blind review, also pre-existing

**A fuzzy section inside a lookbehind reports change positions that contradict its own counts.**

| pattern | subject | upstream | port |
|---|---|---|---|
| `(?<=(?:[ab][cd]){e<=1})$` | `axc` | `counts=(1,0,0) changes=([2],[],[])` | `counts=(1,0,0)` but **`dels=[1]`** |
| `(?<=(?:abc){e<=2})$` | `ac` | `counts=(1,0,1) changes=([1],[],[0])` | `counts=(1,0,1)` but **`dels=[1,2]`** |

The port says one substitution in `FuzzyCounts` and then lists a *deletion* in `FuzzyChanges`. It is
not `(?r)` in general - `(?r)(?:[ab][cd]){e<=1}` against `'axc'` reports `subs=[2]`, correctly - and
it is not the constraint: these patterns have no test node, so `FuzzyExtMatch` returns at its first
branch exactly as HEAD's did. S38/S39 territory, `record_fuzzy` under a lookbehind.
`The_REV_arms_test_the_character_before_the_position` therefore asserts `FuzzyCounts` only, and says
in a comment why, so nobody "strengthens" it into a red.

## Review

**One blind pass over the whole diff, Opus, briefed for reproductions only** (no explanations, no
proposed corrections), hunting the failure modes the slice file named plus six more: a missing or
extra opcode in either arm list, the wrong bound or the wrong character in the forward and reversed
arms, `FoldedCharAt` indexing out of range at either end, a call site passing `text_pos` where
upstream passes `new_text_pos`, a retagged test whose pattern does not actually need a ranking mode,
a split whose halves lose an upstream assertion, and a generator change that moves another
generator's row stream.

**Findings raised: 1. Reproduced: 1. Fixed: 0 - because the one finding is a pre-existing defect the
diff does not touch**, reproduced independently here against upstream and against the port (the
table above), and sharpened in the process: the reviewer reported it as a lookbehind-or-`(?r)`
problem, and `(?r)` is in fact correct. It is recorded above and in STATE.md rather than fixed,
on the S33/S34/S35 precedent. **Zero defects were found in the diff itself**, and the reviewer
verified the opcode lists arm by arm against `_regex.c`, re-read `test_fuzzy#73-78` and
`test_hg_bugs#89-103` in `upstream/regex/tests/test_regex.py` to confirm each split covers every
upstream assertion, confirmed all 378 constraint x test combinations parse upstream, and confirmed
`FUZZY_TESTS`, `FUZZY_SPLIT_TESTS`, `_fuzzy_constraint` and `FUZZY_BACKREF_FOLD_*` are referenced
only from `_generate_fuzzy`.

**No second pass was needed.** The only edit after the review is a comment in
`FuzzyTestConstraintTests.cs` recording why those assertions stop at `FuzzyCounts`. No public API,
no tooling and no behaviour changed after the reviewer saw it.

## Negative controls

Five, in `tools/controls.json` as `S40-A` .. `S40-E`. **Every figure below was re-run against the
exact code and generator this slice commits**, at the two recorded seeds and at **555, a seed this
slice had not used** - and that mattered: an earlier run of A read 16 and 11, and the generator has
been widened twice since.

Wave: `fuzzy`, **2000 rows**. Re-run any of them with
`python tools/run-controls.py --ids S40-A --seeds 3`.

| id | what it breaks | seed 7 | seed 20260913 | seed 555 |
|---|---|---:|---:|---:|
| A | `set-test-constrains-nothing` | 6 | 7 | 3 |
| B | `test-consulted-at-the-wrong-position` | 63 | 45 | 46 |
| C | `reversed-test-reads-past-slice-start` | 6 | 9 | 8 |
| D | `test-ignores-node-match-negation` | 53 | 38 | 49 |
| E | `folded-test-asks-the-unfolded-character` | 1 | 0 | 0 |

The mutations, as the files actually read:

**A**, in `Matcher.cs`, `FuzzyExtMatch`: delete `            or Opcode.SetUnionIgn` from the forward
arm's opcode list, so the four `SET_*_IGN` opcodes fall through to `_ => true`:

> `            or Opcode.SetSymDiffIgn` <br>
> `            or Opcode.SetUnionIgn => pos < state.SliceEnd`

becomes

> `            or Opcode.SetSymDiffIgn => pos < state.SliceEnd`

**B**, in `Matcher.cs`, `NextFuzzyMatchItem`'s `case FuzzyValue.Sub:` arm, change

> `                    if (!FuzzyExtMatch(state, state.FuzzyNode, data.NewTextPos))`

to

> `                    if (!FuzzyExtMatch(state, state.FuzzyNode, newPos))`

**C**, in `Matcher.cs`, `FuzzyExtMatch`'s reversed arm, change

> `            or Opcode.RangeIgnRev => pos > state.SliceStart` <br>
> `                && MatchesOne(state.Encoding, testNode, state.CharBefore(pos)) == testNode.Match,`

to

> `            or Opcode.RangeIgnRev => pos < state.SliceEnd` <br>
> `                && MatchesOne(state.Encoding, testNode, state.CharAt(pos)) == testNode.Match,`

**D**, in `Matcher.cs`, `FuzzyExtMatch`'s forward arm, drop `== testNode.Match`:

> `            or Opcode.SetUnionIgn => pos < state.SliceEnd` <br>
> `                && MatchesOne(state.Encoding, testNode, state.CharAt(pos)) == testNode.Match,`

becomes

> `            or Opcode.SetUnionIgn => pos < state.SliceEnd` <br>
> `                && MatchesOne(state.Encoding, testNode, state.CharAt(pos)),`

**E**, in `Matcher.cs`, `FuzzyExtMatchGroupFld`'s forward arm, replace the fold index with `0`:

> `                && MatchesOne(state.Encoding, testNode, FoldedCharAt(state, state.TextPos, foldedPos))`

becomes

> `                && MatchesOne(state.Encoding, testNode, FoldedCharAt(state, state.TextPos, 0))`

### Control E is effectively blind, and the reason is worth more than the number

E read **0 of 2000 at every seed, three times running**: with the group side ASCII, with it widened
to fold (`FUZZY_BACKREF_FOLD_ATOMS`), and with folding-splitting tests merely added to the pool. The
code path was live the whole time - a throwaway control that *inverted* the same arm's verdict
diverged 1 and 10 rows - so this is a **third** way for a control to read zero, alongside "the
generator never draws the shape" (S38's E) and "the cached wave is the old generator" (S39's):

> **no test in the pool can tell the two answers apart.** `folded_char_at(pos, folded_pos)` differs
> from `folded_char_at(pos, 0)` only when a subject character folds to more than one character *and*
> those characters differ. The generator's foldings are `ß`->"ss" and `ﬀ`->"ff" - two identical
> characters - and every one of the 15 tests answered the same for both halves of every folding.

`FUZZY_SPLIT_TESTS` (`[a-s]` splits "st", `[a-f]` splits "fi") and a 0.6 bias towards them on
fold-mode rows with a backreference took E from 0 to 1, which is still too thin to be evidence. **So
the real coverage of the fold index is a gap test, not the wave**:
`FuzzyTestConstraintTests.The_folded_test_asks_the_character_at_the_fold_position_not_the_first_one`.
`ß` folds to "ss" and `ﬆ` folds to "st", so against each other the comparison agrees on the first
folded character and differs on the second; the error is tried at `folded_pos` 1, where the
character is `t`. Measured: `(?fi)(ß)(?:\1){e<=1:t}` on `'ßﬆ'` matches with
`counts=(1,0,0) changes=([1],[],[])` and `{e<=1:s}` does not match at all. Six assertions, and they
fail the moment anyone writes `0` for the index.

## What the next slice should know

1. **S41 and S42 own all 55 remaining skips**, 30 `fuzzy-enhancematch` and 25 `fuzzy-bestmatch`, and
   the tags are now accurate, so `docs/STATUS.md`'s two rows are the real scope.
2. **Three defects are recorded and none is fixed**: upstream's `(*SKIP)`-in-an-atomic-group
   infinite loop, the four HEAD divergences at 6000 rows, and the lookbehind `FuzzyChanges` bug.
   The owner's rule (amendment 16) says every one of them is fixed or filed before 1.0. A slice for
   them is the honest next step, not an S41 detour.
3. **The recorder has no per-row timeout**, which is why one hanging upstream row kills a whole
   wave. `regex` takes a `timeout=` keyword - `pat.search(s, timeout=5)` raises `TimeoutError` - so
   the fix is small, and it is what makes a 6000-row default wave possible at all.
4. **S35's `_fix_full_casefold` divergence is still unentered** and the `fuzzy` generator still
   draws at most one expanding fold per section to stay off it. Phase 6 decides.
