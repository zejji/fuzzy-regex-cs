---
slice: S40d
phase: 5
title: The last two reversed-(*SKIP) shapes, and the 6000-row three-seed gate S40a could not reach
delivers: []
---

# S40d - the gate

**S40a's exit gate, held back until S40b and S40c have cleared their eleven rows.** Running it
before then measures nothing: a wave red on three mechanisms cannot tell a new divergence from an
old one, which is the same argument S37 made for clearing `interactions` before S43 widened it.

## Scope

### 1. The two reversed-`(*SKIP)` shapes S40a measured and deliberately left red

Both are the carried slice of ledger entry 5, and both are refuted - but not by anything the ROW
carries, which is why neither was classified. `python tools/probes/upstream-reversed-skip-scan-shapes.py`
re-runs all three rows of the family, including the one that is classified, so the difference
between them is visible in one output.

**Seed 20260913 row 116388, a substitution.** `(?r)(?:\d*?(*SKIP)𝔘|a)$` over `'aa𝔘𝔘'`: upstream
replaces 3 times and this port once. A `SubOutcome` carries a string and a count and no match
positions, so the `$` tell has nothing to read. Asked separately, upstream replaces at (3,4), (2,3)
and (1,2) where `$` is true at the end alone, and `(*PRUNE)` gives this port's single replacement.
The honest options are a tell that works on a substitution row, or the recorder recording the spans
a substitution replaced at.

**Seed 4242 row 117071, an overlapped scan with no `$` and no groups.**
`(?r)\w{1,3}?(*SKIP).(?:\p{L}(*SKIP)){2,3}` over `'_ ___𐐀𐐀𐐀'`: upstream reports (3,8), (3,7), (3,6)
in codepoints and this port the first alone. Neither tell exists. What refutes it is upstream's own
reversed search over the truncated subject - `search(S, 0, 7)` is None for a match it reports as
ending at 7 - and that is legitimate HERE because this pattern holds no end-sensitive item.

**So the real work is the reversed `anchoredScan` the recorder refuses** (`_anchored_scan`,
tools/record-oracle.py). It refuses because moving `endpos` truncates the subject and changes what
`$`, `\Z`, `\b` and `\B` mean - but that is a property of the PATTERN, not of reversal, and a
pattern holding none of them can be walked safely. Narrow the refusal to patterns that carry an
end-sensitive item, record the walk for the rest, and the row itself then carries its refutation.
Judge the widening by what it would classify, not only by what it unblocks.

### 2. The gate

`pwsh -File tools/run-oracle.ps1 -Count 6000` - three seeds, 126,000 rows a seed. S40a's last
measurement, with the recorder timeout in and the per-match slice reset in:

| seed | agree | expected | timeout | diverge |
|---|---:|---:|---:|---:|
| 7 | 125,960 | 37 | 0 | 3 |
| 4242 | 125,944 | 50 | 1 | 5 |
| 20260913 | 125,942 | 51 | 0 | 7 |

S40a session 2 classified two of the fifteen and then **re-ran the whole gate on the committed
code**: 3 + 5 + 5 = **thirteen**, with rows 93133 and 116766 now `EXPECTED`. Four are S40b's, seven
are S40c's, two are this slice's, and the per-seed rows are named in those files.

**The run itself is cheap and the slice should not budget for it.** Measured 2026-09-13: 126,000
rows records in 17 seconds and consumes in 6, so all three seeds together are about a minute. The
forty minutes S40 lost was the hanging row, which the recorder's timeout now records instead. What
this slice costs is judging two rows, not running a wave.

## Done when

- [x] Rows 116388 and 117071 are classified or fixed, with the reason, and the recorder change (if
      that is the route) has a control that fires.
- [x] The default wave is GREEN at three seeds at 6000 rows a generator.
- [x] Ratchet GREEN, baseline updated, blind review, commit.

---

# Closing notes (2026-09-13)

**The gate is GREEN at all three seeds at 6000 rows a generator - 126,000 rows a seed, 378,000 in
all**, which is S40a's exit gate and the thing three slices have been clearing rows for:

| seed | agree | expected | timeout | diverge |
|---|---:|---:|---:|---:|
| 7 | 125,962 | 38 | 0 | **0** |
| 4242 | 125,948 | 51 | 1 | **0** |
| 20260913 | 125,942 | 58 | 0 | **0** |

The slice file's own table above was stale when this session opened: it listed S40a's fifteen rows,
and S40b and S40c had taken it down to two. The two were this slice's, exactly as STATE.md said.

**Neither row needed a widened tell. Each needed a fact the row did not carry.**

**Row 117071 - the walk.** `(?r)\w{1,3}?(*SKIP).(?:\p{L}(*SKIP)){2,3}` over `'_ ___𐐀𐐀𐐀'`, where
upstream reports codepoint (3, 8), (3, 7) and (3, 6) and this port the first alone. No `$`, no
groups, so neither existing tell exists. `tools/record-oracle.py`'s `_anchored_scan` records
upstream's own answer to a scan asked one match at a time from a fresh state, and it refused every
REVERSED row because stepping one means moving `endpos`, which truncates the subject. **That refusal
was never about reversal - it is about the pattern**, so it now reads the pattern:
`_reads_the_end_of_the_subject` refuses only where the pattern holds `$`, `\Z`, `\z`, `\b`, `\B`,
`\m`, `\M`, `\K`, `\G`, `\X` or a lookahead. The step is upstream's own (`:20903`, `match_pos - 1`
when reversed), measured on seven shapes including a zero-width one, and on four end-reading shapes
that DO differ: `python tools/probes/upstream-reversed-walk-step.py`. The row now carries
`anchoredScan: [(3, 8)]` - this port's answer, one match against upstream's three.

**Row 116388 - the substitution.** `(?r)(?:\d*?(*SKIP)𝔘|a)$` over `'aa𝔘𝔘'`, upstream replacing 3
times and this port once. A `SubOutcome` is a string and a count, so the row carried no position for
the `$` tell to read. The recorder now records `subMatches` on a `sub`/`subf` row whose pattern holds
a `(*SKIP)`: where upstream replaced, asked as a separate `finditer`, since `subn` walks the same
scanner. Upstream's three spans are codepoint (3, 4), (2, 3) and (1, 2), and the two beyond this
port's need `$` at 3 and at 2, where the subject has a character.

**A THIRD row turned up, at a seed no slice had used, and it is a new symptom of the same upstream
bug.** Running a control at seed 20260914 reddened `verbs`: row 3679,
`(?r)\p{Lu}*(*SKIP)B(?P<g1>(?:\D{2,4}(*SKIP)a|.))` over `'B_\ra'`, where upstream's scan reports ONE
match and this port two. It is ledger entry 5 again with the moved `slice_end` too far LEFT rather
than too far right, so upstream's scanner loses a match instead of inventing one - and the walk this
slice had just unlocked refutes it outright: upstream's own `search(S, 0, 4)` is (0, 4) and
`search(S, 0, 3)` is (0, 2), both of which this port finds. New entry
`overlapped-skip-missing-match-reversed`, new pinned test, ledger entry 5 gains a fourth symptom.
**It is not caused by anything this slice changed** - the engine is untouched, and a row's outcome is
recorded before any second question is asked. The three reversed entries are now disjoint by count:
spans moved with the counts equal, upstream longer, upstream shorter.

**What did NOT change: the engine.** `git diff src/` is empty. This slice is recorder, classifier and
tests.

## Controls

All four wave figures re-run against the committed code and generator, at the recorded seed and at a
seed the slice had not otherwise used. Baseline both seeds: `agree 5994  expected 6  diverge 0`.

> **Control A, the reversed overlapped step.** In `src/FuzzyRegex/Engine/MatchState.cs`,
> `AdvancePastMatch`, change
> `TextPos = Reverse ? PrevPos(MatchPos) : NextPos(MatchPos);` to
> `TextPos = Reverse ? PrevPos(PrevPos(MatchPos)) : NextPos(MatchPos);`.
> Wave: `pwsh -File tools/run-oracle.ps1 -Generator verbs -Count 6000 -Seed 7`.
> Result: **24 diverge, expected still 6**. At seed 20260914: **20 diverge, expected still 6**.

> **Control B, the reversed substitution.** In `src/FuzzyRegex/Engine/Substitution.cs`, in the
> `while (subCount < maxSub)` loop, after `state.AdvancePastMatch();` insert
> `if (state.Reverse && subCount == 1) { break; }` (CSharpier spreads it over five lines).
> Wave: `pwsh -File tools/run-oracle.ps1 -Generator verbs -Count 6000 -Seed 7`.
> Result: **23 diverge, expected still 6**. At seed 20260914: **25 diverge, expected still 6**.

> **Control C, the new pinned test's alarm.** In `src/FuzzyRegex/Engine/Matcher.cs`, delete
> `state.SliceEnd = state.InitialSliceEnd;` from the per-match slice reset (the line after
> `state.SliceStart = state.InitialSliceStart;`). Run
> `dotnet run --project tests/FuzzyRegex.Tests -- --treenode-filter "/*/*/BacktrackingVerbTests/An_overlapped_reversed_scan_of_a_skip_keeps_the_match_upstreams_own_stepwise_door_still_finds"`.
> Result: **total 1, failed 1**. Restored: total 1, failed 0.

In every control the `expected` tally is unchanged, which is the thing being measured: the widened
entries absorbed none of the mutations. Control A and B fire at both seeds; the margin is 20-25 rows,
not one or two, so neither is a coincidence of a single draw.

## Review

Two blind passes, plus a third over the delta the second produced.

**Pass one, over the whole diff. One finding raised, one reproduced, one fixed.**
`tools/record-oracle.py` truncated `subMatches` with `replaced_at[: recorded["count"]]`, and
upstream's count convention is not a slice: 0 means NO LIMIT and a NEGATIVE count replaces nothing.
`SUB_COUNTS` draws -1, so a row upstream never touched was recorded with every match but the last.
Reproduced independently before touching anything - `regex.subn('a(*SKIP)', 'X', 'aaa', count=-1)` is
`('aaa', 0)` where `count=0` is `('XXX', 3)`, and the recorder wrote two spans for it. Fixed, and
verified on real output: over a 6000-row seed-7 `verbs` wave, 936 `sub`+`(*SKIP)` rows of which 129
have a negative count, and **0 whose recorded list length differs from the recorded replacement
count** (8 before the fix). The reviewer also stress-tested the walk - 529 pattern bodies x 7 flag
sets x 8 subjects with no `(*SKIP)`, and all 109 reversed rows of a real wave with the verb neutered
to `(*PRUNE)` - and found **0** where the walk is not the scan.

**Pass two, over the fix.** Clean, with ~69,000 rows of evidence across `SUB_COUNTS`, out-of-range
limits, `subn` against `subfn`, and callable against string templates.

**Pass three, over the new entry, its test and the probe section - none of which pass one saw.** One
finding: the new test passes with its `(*SKIP)` verbs deleted. Reproduced, and judged NOT a defect -
that is the point rather than a weakness, because this port's correct answer IS the verb-free answer
once a moved slice stops carrying into the next match. What settles it is control C above, which the
reviewer also measured: the test fails the moment the per-match reset goes. Recorded in the test's
own comment so nobody re-raises it. The reviewer also found one dead-but-harmless branch - the new
entry accepted `finditer` where only `finditer-overlapped` can ever carry a walk - which was narrowed.

No critique loop: three passes over three disjoint bodies of code, and one round of fixes.

## For the next slice

- **S41 (`ENHANCEMATCH`) and S42 (`BESTMATCH`) are next, and the 55 remaining skips are exactly
  their scope.** Nothing here touches them.
- **Seed 31 is still unjudged** and is not this slice's: three divergences on
  `partial,partial-sliced,interactions` at 6000 rows (rows 1075, 6943, 16545), present before S40a
  and after S40d.
- **The `$` tell still has its hole**, and S40d narrowed rather than closed it: a port defect that
  ended a reversed scan one match early is out of reach of the walk on exactly the rows whose pattern
  ends in `$`, because that is the shape the recorder still refuses a walk for. The substitution arm
  compares counts and not text for the same kind of reason. Both are stated in
  `ExpectedDivergences.cs` beside the code.
- **A seed outside the gate's three found a real new symptom in under a minute.** That is the second
  time (S33 was the first). Whatever Phase 6's oracle hardening does, rotating seeds is cheap and
  keeps paying.
