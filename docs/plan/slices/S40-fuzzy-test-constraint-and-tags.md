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

- [ ] The seven tags fully delivered, zero `needs:` skips on any of them; the six rejections still
      pinned.
- [ ] `fuzzy` generator green at three seeds with test constraints; default wave green at 6000.
- [ ] PORTMAP: every row of the fuzzy bucket except the `do_best`/`do_enhanced` machinery marked
      ported.
- [ ] Ratchet GREEN, baseline updated, blind review (hunt: a test-constraint arm that ignores
      `test_node->match` negation; a `SET_*` test arm missing), commit.
