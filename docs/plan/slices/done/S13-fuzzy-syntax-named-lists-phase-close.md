---
slice: S13
phase: 2
title: Fuzzy constraints, named lists, grapheme, and closing the parser
delivers: [parse-errors]
---

# S13 - Fuzzy constraints, named lists, grapheme, and closing the parser

The last Phase 2 slice. After it, every construct upstream's parser accepts is parsed and
compiled here, and every one of the S06 corpus rows passes.

## Scope

`src/FuzzyRegex/Parsing/`, mirroring `upstream/regex/_regex_core.py`.

- **Fuzzy constraints**: `parse_fuzzy`, `parse_fuzzy_item`, `parse_cost_constraint`,
  `parse_cost_limit`, `parse_constraint`, `parse_fuzzy_compare`, `parse_cost_equation`,
  `parse_cost_term`, `parse_fuzzy_test`, `is_actually_fuzzy`, `apply_constraint`
  (`:548-560`, `:590-604`, `:655-846`); `Fuzzy` (`:2786-2919`) including
  `_constraints_to_string` and the `FUZZY`/`FUZZY_EXT` compile paths; the `fuzzy` argument
  threaded through every node's `_compile(reverse, fuzzy)`, which earlier slices carried but
  never set.
- **Named lists**: `StringSet` (`:4069-4110`), `make_string_set` (`:441`), `parse_string_set`
  (`:1427`), `Info.named_lists_used`, and the pipeline's `named_lists`, `named_list_indexes`,
  `args_needed`, `complain_unused_args` and `missing named list` handling
  (`_main.py:482-492`, `:606-621`). Upstream's `ValueError` for an unused keyword argument maps
  to `ArgumentException`; its `error` for a missing list stays `FuzzyRegexParseException`.
  `FuzzyRegex.NamedLists` returns the sets upstream returns as `frozenset`s.
- **Grapheme**: `Grapheme`, `GraphemeBoundary` (`:2919-2938`) and `\X` in `parse_escape`.
- `PrecompiledCode` (`:3303`) is used only by `Scanner`, which is not ported (PORTMAP); record
  it as not ported rather than porting dead code.

## Closing the phase

- **Un-skip `needs:parse-errors`** in full (72 tests across 7 files), reading each skip's prose
  first: a few name a missing position (`multiple-repeat detection does not report a
  position`, `an empty \p{} property name does not report a position`), which the faithful
  port now reports. Template rows S12 could not un-skip stay skipped for Phase 3 with their
  prose updated to say why.
- **Every `_regex_core.py` symbol accounted for.** List every `class` and `def` in the file
  (`grep -n "^class \|^def \|^    def "`) and check each appears in `docs/PORTMAP.md` as ported
  or as deliberately not ported with a reason (`Scanner`, `_shrink_cache`, `PrecompiledCode`,
  the `dump` methods if not ported, bytes-only branches). S05 did this for the test methods;
  do it for the parser.
- **All corpus rows pass**: 1534 compiles, 46 errors, 62 templates, zero skipped. If any row
  still skips, the phase is not complete; if a row needs a construct the corpus never exercised,
  that is a gap for the Phase 3 oracle to cover and is written into the closing notes.
- **Phase 3 handover notes**, for whoever authors the Phase 3 slices: which opcodes the compiler
  emits (all of them, by the end), the seam's shape, where the `re_compile` port
  (`_regex.c:25863-26121`, which turns the code list into the node graph) should start, and the
  span convention warning from DECISIONS 2026-08-30 (`(start, end)` versus `(Index, Length)`).
- Roadmap and budget: revise the Phase 3 session estimate against Phase 2's measured rate, as
  the roadmap asks, and record what `docs/plan/slice-log.jsonl` now holds.

## Done when

- [x] Corpus: every row passes, none skipped, none failing.
- [x] `parse-errors` delivered; `docs/STATUS.md` shows the tag gone from the waiting table.
- [x] PORTMAP complete for `_regex_core.py` and `_main.py`; "Deliberately not ported" and
      "Where we diverge" tables current.
- [x] Ratchet GREEN, baseline updated, blind review (hunt: a cost equation parsed with integer
      division where upstream uses `/`, `UNLIMITED` as a fuzzy limit, `frozenset` versus list
      semantics in a named list with duplicate entries), commit.
- [x] STATE.md says Phase 2 is complete and the driver stops at the boundary.

---

## Closing notes (2026-08-31)

**What landed.** The parser is finished. The fuzzy-constraint grammar (`parse_fuzzy` and its eight
helpers, `is_actually_fuzzy`, `apply_constraint`), the `Fuzzy` node with its `FuzzyConstraints`
state, named lists (`StringSet` as a `Branch` subclass, `make_string_set`, `parse_string_set`,
`_fold_case` and the named-list build in `_main._compile`), and `fuzzy = isinstance(parsed, _Fuzzy)`
in **both** places upstream asks it. `Grapheme` and `\X` were already ported in S11, so this slice
had nothing to do there. Ratchet GREEN: 3926 tests, 2046 passing (baseline was 1812), 0 failing,
overall parity 1.6% -> 4.5%. **All 1659 corpus rows pass and none skips** - the first slice at which
that is true. `parse-errors` and `fuzzy-syntax` have both left the status board.

**Surprises.**

- **`_check_group_features` asks `isinstance(parsed, Fuzzy)` too** (`_regex_core.py:4436`), not just
  `_main.py:577`. STATE said "hard-coded twice in `PatternCompiler.Compile`"; the second one is in
  `ParseFunctions.CheckGroupFeatures`. Wiring only the first appends a spurious `CALL_REF` copy of
  the whole pattern for `(?:a(?0)?){e<=1}`. Found by the blind review.
- **A fuzzy cost is the one number upstream range-checks nowhere.** `is_above_limit` guards repeat
  counts; nothing guards a cost. So *any* ceiling on the parse-time value is observable as a pattern
  this port rejects and upstream compiles. Two ceilings were tried and both were wrong (details in
  PORTMAP "Where we diverge"); the values are now `BigInteger` and the only clamp is
  `Fuzzy.CodeWord`, at the emitted `uint`.
- **`StringSet._key` is dead upstream.** It sets `_key = (class, name, case_flags)` and then
  inherits `Branch.__eq__`, which compares *branches*, so two differently-named lists holding the
  same strings are equal. Ours matches by not overriding `Equals` at all.
- **A backreference inside a fuzzy test is rejected by upstream** - `Fuzzy.fix_groups` never
  descends into `constraints["test"]`, so `RefGroup.group` stays a string and `_regex.compile` says
  `RuntimeError: invalid RE code`. Our `RefGroup.GroupNumber` defaults to 0, a *valid* code word, so
  the port quietly compiled a reference to group 0 until a guard was added.
- **The named-list ordering leak is in the recorder, not the port.** `record-compile-corpus.py`'s
  `_canonical_kwargs` sorts each list before handing it to upstream, so any wave harness must sort
  too. Fifty-eight false divergences came from not doing that.

**Differential wave.** 1,055 generated patterns (fuzzy grammar, cost equations, the `{...:test}`
form, what a constraint attaches to, brace forms that must stay literal, named lists with
duplicate/equal-length/astral/empty members) - `.scratch/s13_record.py`, `.scratch/wave/`,
`.scratch/s13_compare.py`, and `.scratch/s13_classify.py`, which buckets every divergence so none is
merely counted. 51 divergences remain and **every one is documented**: 18 code words above
`UNLIMITED`, 7 `a{1i<=}`-shaped `ValueError`s, 12 (6 patterns, counted twice by the recorder) where
we reject a backreference in a fuzzy test as upstream's compiler does, and **14 that are Phase 3's**
- see below. Negative control: the harness reported 58 divergences before the kwargs-sort fix and 4
after, so it discriminates.

**For Phase 3.** `{e<=1:\X}`, `{e<=1:\b}`, `{e<=1:\A}`, `{e<=1:\Z}` and `{e<=1:\L<a>}` compile here
and produce upstream's exact bytecode, but upstream's *C compiler* rejects all five with
`RuntimeError: invalid RE code`. That validator is `re_compile` (`_regex.c:25863-26121`), so **the
Phase 3 slice that ports it must reject these**, and `.scratch/s13_record.py` is the ready-made
check. Also worth knowing: `_main._compile` asks `isinstance(parsed, _Fuzzy)` of the *unoptimised*
tree and `_check_group_features` of the *optimised* one, and upstream allows the two answers to
differ - do not "fix" that.

**Analyzer findings.** Three rules fired on new code and each was decided on its merits rather than
suppressed. MA0008 (add `StructLayoutAttribute`) fired on a `readonly record struct Limit`; the
type was **deleted** in favour of the named-tuple shape `Info.NamedListsUsed` already uses, so the
rule had nothing to fire on. SS008 (`GetHashCode` refers to a mutable member) fired on
`FuzzyConstraints.GetHashCode`; the rule is **right** - the constructor fills those fields in after
construction - so the hash became a constant, which is the precedent `Branch` already set. IDE0270
(null check can be simplified) fired three times on `bool? x; if (x is null) throw`; the code was
changed to `bool x = ... ?? throw`, which is shorter and clearer. **Nothing was disapplied and
`.editorconfig` is unchanged.**

**Review.** Two blind passes, both dispatched with `docs/VERIFICATION.md`'s brief and both reporting
reproductions rather than prose. The first raised **4 findings, all 4 reproduced, all 4 acted on**:
the `_check_group_features` `fuz` placeholder (fixed), the `UNLIMITED` saturation being observable
in accept/reject and off by one (fixed by moving the clamp to the code word), the `a{1i<=}`
`ValueError` (kept on the merits, documented and tested), and the unresolved backreference in a
fuzzy test (fixed with a guard). Because those fixes were substantial new code the first reviewer
never saw, **a second pass ran over that delta only**. It raised **3 findings, 2 reproduced and
fixed** - the `long.MaxValue` ceiling still overflowed `min_cost += 1` and still collapsed two
distinct values, on patterns upstream accepts - **and 1 doc correction, applied**. No third pass:
the second pass's fix is a mechanical widening to `BigInteger` of code that pass had already read,
it is covered by tests proved to fail without it, and the 1,055-row wave classifies every remaining
divergence. Both fixes were verified by reverting them and watching the new tests fail.

**Six duplicate test ids.** `Update-Baseline` records 2046 passing tests as 2040 ids, because
`VariousParseErrorTests` faithfully ports six patterns that upstream's own table lists twice
(`a[]b`, `a[`, `a\`, `abc)`, `(abc`, `)(`). Harmless - identical input, so one recorded id catches
both - but it explains the gap between the two numbers.
