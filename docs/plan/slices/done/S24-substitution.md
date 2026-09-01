---
slice: S24
phase: 3
title: Substitution - Replace, ReplaceFormat, Match.Result, and the template application
delivers: [substitution, format]
---

# S24 - Substitution: Replace, ReplaceFormat, Match.Result, and template application

Needs S18 (group values feed templates). S12 ported the *parsing* of replacement templates; this
slice applies them to real matches - the half DECISIONS 2026-08-31 recorded as S12's one
undelivered scope item ("`Match.Result` is not wired to the template compiler").

## Scope

Line references are `upstream/src/_regex.c` unless marked `_main.py`.

- **The sub loop**: `pattern_subx` (`:21726`) - iterate matches, apply the replacement, join. It
  contains its own scan-advance including the empty-match policy (advance by one after a
  zero-width match, with the V0/V1 difference), which S25's iterators share - port it here, reuse
  there. `get_sub_replacement` (`:21667`), `check_replacement_string` (`:19865`),
  `get_match_replacement` (`:19635`), `_compile_replacement_helper` (`_main.py:687` - already
  mostly ported in S12; wire, don't re-port). The join-list machinery (`:19687-19864`) collapses
  to a `StringBuilder`; record it in PORTMAP as such.
- **Expansion on a match**: `match_expand` (`:19902`) behind `Match.Result`; `match_expandf`
  (`:20045`) with `make_capture_dict` (`:19983`) behind `Match.ResultFormat` and
  `ReplaceFormat` - upstream's `subf`/`expandf` format-string path, where `{1}`, `{name}` and
  format specs apply to group values.
- **Public**: all four `Replace` overloads (string replacement, `MatchEvaluator`, each with and
  without `out int replacements` - upstream `sub`/`subn`), `ReplaceFormat` x2 (`subf`/`subfn`),
  `Match.Result`, `Match.ResultFormat`. `count`/`replacements` semantics per upstream (`count=0`
  upstream means unlimited; our `count: -1` default maps to it - S01's surface decided this,
  follow what the ported tests assert).
- **The deferred error rows**: `SubTemplateNumericEscapeTests`' 12 invalid-group-reference rows
  (DECISIONS 2026-08-31: a group-*number* check happens during expansion and needed a match) -
  un-skip them now, plus the unknown-group `ArgumentException` mapping DECISIONS records.

## Verification

- **Un-skip** `needs:substitution` (98) and `needs:format` (11), reading each skip's prose
  first - the fuzzy-substitution tests stay on their fuzzy tags; stragglers retag with prose.
- **Oracle wave**: generated (pattern, subject, template) triples - group references in all
  forms (`\1`, `\g<1>`, `\g<name>`, `{1}`, `{name}` on the format side), unmatched groups in
  templates, zero-width matches (the empty-match advance policy is where sub bugs live - probe
  V0 and V1 both), `count` limits, and literal-only templates. Compare the returned string and
  the replacement count. Zero divergences; negative control.

## Done when

- [x] Both tags delivered or stragglers retagged; the 12 deferred error rows un-skipped; counts
      in closing notes.
- [x] Oracle wave green including zero-width V0/V1 probes; counts quoted.
- [x] `docs/PORTMAP.md` updated.
- [x] Ratchet GREEN, baseline updated, blind review (hunt: the empty-match advance applied
      before the replacement instead of after, an unmatched group expanding to "None" or
      throwing where upstream yields empty, `count` counting scans instead of replacements),
      commit.

## Closing notes (2026-09-01)

**What landed.** Every substitution entry point runs. New `Engine/Substitution.cs` holds
`Subx` (`pattern_subx`, `:21726`) with `AddReplacement` for its four-way loop body,
`IsLiteralTemplate` (`check_replacement_string`, `:19865`), `GetSubReplacement` (`:21667`),
`GetMatchReplacement` (`:19635`), `ExpandFormat`/`ExpandField` - a port of CPython's `str.format`
grammar, which is what `match_expandf` delegates to - and two small helpers, `CodepointCount` and
`TryParseSubscript`. `Match.Result` and `Match.ResultFormat` are wired; all four `Replace`
overloads, both `ReplaceFormat` overloads and the two static ones are live. `FuzzyRegex.Subx` is
the argument shuffling only; `ValidateReplacement` became `CompileReplacement` and now returns the
compiled template instead of discarding it. `PORTMAP.md` gained a Substitution section.

**The loop lives in `Engine/`, not on `FuzzyRegex`.** It is a port of a 200-line C function and
tripped MA0051 at 117 lines. The rule is right where it fired: `FuzzyRegex.cs` is the public
surface file and is deliberately outside the `{Parsing,Engine,Unicode}` relaxation that exists for
exactly this. So the loop moved to where `_regex.c` ports belong rather than earning a suppression,
at the cost of three `internal` members (`PatternObject`, `GroupCount`, `TimeoutTicks`) which are
upstream's `self->pattern`, `self->public_group_count` and `self->timeout`. The other two analyzer
findings were fixed on the merits, not disapplied: S3358 by unnesting a ternary, S3267 by replacing
a hand-rolled digit loop with `text.All(char.IsAsciiDigit)`.

**Counts.** 84 skip attributes removed, fanning out to 116 failing tests, then 3 retagged
`needs:lookaround` from evidence (the two `Bug10328` rows and `test_hg_bugs#88` need `(?<=...)` and
`(?!...)`, which no slice has landed). `needs:substitution` and `needs:format` are both off the
board. Suite **5691 total, 4890 passing, 801 skipped, 0 failing**; parity **59.3%** (was 53.5%).
`Substitution` 8.0% to **95.5%**, `Format` 0% to **100%**, `Anchors` 41.7% to **91.7%**,
`Regressions` 24.6% to **27.8%**. The 12 deferred invalid-group-reference rows are un-skipped and
pass. `ApiSurfaceTests` lost `Replace` from its still-a-stub list and gained a real test over all
eight substitution entry points, as that test's own comment asks the delivering slice to do.

**Five things the ported suite does not assert, all measured against regex 2026.7.19 on
2026-09-01 and pinned in the new `Gaps/Substitution/SubstitutionRulesTests.cs`.**

*The `count` convention is upstream's inverted, at both ends.* Upstream's 0 is "no limit" and its
negative is "no replacements"; this surface's -1 and 0. `regex.subn('a','b','aaaaa',count=-1)` is
`('aaaaa', 0)`.

*The too-short-subject shortcut runs before the template is compiled*, so the same malformed
template is rejected or not depending on the subject's length:
`regex.sub('xx', r'\g<bad', 'z')` is `'z'` and `regex.sub('x', r'\g<bad', 'z')` raises.

*The same out-of-range reference raises two different exceptions*, because upstream checks it in
two places: `regex.error` from `sub` (`RE_ERROR_INVALID_GROUP_REF`) and `IndexError` from
`expand` (`RE_ERROR_NO_SUCH_GROUP`).

*`sub` and `expand` short-circuit a literal template; `expandf` does not.* So
`regex.subf(r'(\w+)', '}', 'ab')` is `'}'` while `.expandf('}')` raises `ValueError`.

*A format spec and the `!r`/`!a` conversions are rejected, because upstream rejects them too* -
the object being formatted is a `_regex.Capture`, not a `str`, so `{1:>10}` raises `TypeError` and
`{1!r}` yields a CPython object address. `NotSupportedException` here.

**The V0/V1 empty-match difference the slice was told to probe does not exist in this release.**
Modern `pattern_subx` sets `must_advance` from the match alone, with no version test anywhere in
it; `(?V0)x*`, `(?V1)x*` and `x*` all give `('-a-b--d-', 5)` on `'abxd'`. Pinned, so S25 does not
go looking for it either.

**Oracle.** New `substitution` generator: (pattern, subject, template, count) quadruples over
patterns built with their group inventory in hand, so a template can reference the groups the
pattern actually has and an out-of-range reference is made deliberately rather than by accident.
Every other row is `(?r)`; both `sub` and `subf` templates; counts in upstream's convention
including a negative one, so both ends of the translation are exercised. One row in twelve is a
"narrow" row aimed at the shortcut's cell (see control F). It is in `run-oracle.ps1`'s default
list, now eleven generators. The row shape and the consumer grew with it: a `sub` outcome carrying
text and count, a `template`/`count` pair on the row, and - the rule change - a recorded
`whileMatching` flag, because upstream rejects an out-of-range template reference *while
substituting*, so the comparer's old assumption that every recorded rejection came from
`regex.compile` made every such row a false divergence. Final wave: **agree 16500, unsupported 0,
diverge 0** at 1500 rows per generator, seed 606; the same at seeds 923 and 4242 earlier.

**Negative controls.** Six, all against the committed code and the committed generator, wave
recorded fresh at each seed, 600 rows. All six fire.

> Control A, `last-pos`: in `Engine/Substitution.cs`, `Subx`'s loop, change
> `lastPos = state.TextPos;` to `lastPos = state.MatchPos;`. Wave: `substitution`, 600 rows,
> seed 7. Result: 437 agree, **163 diverge**. Re-run at seed 55: **141 diverge**.

> Control B, `unmatched-group`: in `GetSubReplacement`, the block
> ```
>             GroupData group = state.Groups[index - 1];
>             if (group.Current < 0)
>             {
>                 return "";
>             }
> ```
> with `return "None";` for `return "";`. Seed 7: 563 agree, **37 diverge**. Seed 55: **32**.

> Control C, `count-limit`: change
> `        int maxSub = count < 0 ? int.MaxValue : count;` to `... : count + 1;`.
> Seed 7: 506 agree, **94 diverge**. Seed 55: **83**.

> Control D, `reverse-join`: change
> ```
>         if (state.Reverse)
>         {
>             joined.Reverse();
>         }
> ```
> to `if (!state.Reverse)`. Seed 7: 470 agree, **130 diverge**. Seed 55: **125**.

> Control E, `format-negative-index`: in `ExpandField`, change
> ```
>         if (index < 0)
>         {
>             index += captures.Count;
>         }
> ```
> to `index += captures.Count - 1;`. Seed 7: 584 agree, **16 diverge**. Seed 55: **14**.

> Control F, `min-width-code-units`: in `CodepointCount`, change
> ```
>         int count = 0;
>         foreach (Rune _ in text.AsSpan(start, end - start).EnumerateRunes())
>         {
>             count++;
>         }
> ```
> to `foreach (char _ in text.AsSpan(start, end - start))`, which makes it count code units. The
> call site is left alone deliberately: removing the call orphans the method and the build fails
> on the unused-member analyzer before the wave runs. Seed 7: 579 agree, **21 diverge**. Seed 55:
> **21**.

The driver that applies each patch, rebuilds, re-runs the consumer against the same wave and
restores the file is `.scratch/controls.py`, which is gitignored; it is 120 lines and the six
snippets above are its whole content that matters.

**Two of those controls only fire because the generator was widened while the mutant was still
planted, and both numbers are the evidence.** Control E fired on 11 of 600 when no pattern shape
could make a group capture more than once, so `[-1]` and `[0]` agreed by accident; the `plus`
shape `(atom)+` and a heavier weight on the negative subscripts took it to 16 and 14. Control F
fired on **0** of 600 - the wave could not reach the cell at all - because seeing it needs an
astral subject, a pattern whose codepoint width falls strictly between the subject's codepoint and
code-unit counts, *and* a template the compiler rejects, all at once. The `_narrow_row` arm forces
all three and takes it to 21 and 21.

**Review.** One blind pass over the whole diff, then a second over the delta it never saw.

The first pass raised **3 findings, all 3 reproduced against upstream, all 3 fixed**:

1. *The too-short shortcut compared codepoints against code units.* `min_width` is a codepoint
   count and this port's positions are UTF-16 code units, so on an astral subject the shortcut was
   skipped where upstream takes it, and the port then compiled and rejected a template upstream
   never looks at. `regex.subn('..', r'\g<bad', '\U0001F600')` is `('\U0001f600', 0)`; the port
   raised. Fixed with `CodepointCount` over `Rune` enumeration. This is *not* the same comparison
   as `do_exact_match`'s width check, which may safely use code units because it only ever fails
   early - the comment at the site says so.
2. *The `{n[i]}` subscript was parsed by an invented rule.* Upstream reaches the value two ways:
   CPython converts an all-digit key to an `int` itself, and anything else goes through
   `index_to_integer` (`:21311`), which is `int(text, 0)`. `int.TryParse` with `AllowLeadingSign`
   is neither, and it accepted `{1[-01]}` - index -1 - where upstream raises `TypeError`, because
   a *signed* key is a base-0 literal and base 0 forbids a redundant leading zero. Fixed by
   `TryParseSubscript`, which applies that rule and refuses everything else `int(text, 0)` would
   take (`{1[ 0 ]}`, `{1[0x1]}`, a non-ASCII decimal digit). That refusal is a **marked ceiling**,
   named at the method and in PORTMAP: it fails by refusing, never by returning a different
   capture.
3. *The oracle translated only one end of the `count` convention.* `row.Count == 0 ? -1 : row.Count`
   left a negative count meaning "no replacements" upstream and "no limit" here, so a hand-written
   `-Rows` row with `count: -1` reported a correct port as RED. Fixed to a three-way switch, pinned
   in `OracleWaveTests`, and `SUB_COUNTS` gained a `-1` so the generator exercises it too.

The second pass, over `CodepointCount`, `TryParseSubscript`, the count switch, `SUB_COUNTS` and
the four tests pinning them, raised **1 finding, reproduced, fixed**: adding `-1` to `SUB_COUNTS`
shifts the generator's RNG stream, so the measured figures in `_generate_substitution`'s docstring
were the pre-change ones and four of the five were wrong. Re-measured. That is the inherited duty
"re-take every measurement you quote, after the last change to the thing measured" failing on its
first outing, and it is why every figure above was taken after the last generator change rather
than as each one landed.
