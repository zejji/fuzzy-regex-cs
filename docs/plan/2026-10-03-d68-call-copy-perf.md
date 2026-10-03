# D68: the D50 branch's slower group calls

Measured on 2026-10-03 against branch `maint/d50-call-copy-optimised` (d976b464) and main (4fb5c251),
Release builds, average CPU load 0-3% before and after each run. Harness: `tools/probes/d68-call-perf/bench.cs`,
200 warm-up matches, then the median of 15 samples of about 20 ms each. It runs with
`DOTNET_TieredCompilation=0`, so every method is fully compiled from the first call. Two runs per
build agreed to within 3%. Step counts come from a Debug build (`WorkCounter.Steps`) and are exact (`posn.cs` beside it counts every start position too).

## The earlier numbers were tier-0 JIT

The 2-7x figures in the D50 report came from a harness that timed each row within about 300 ms of
startup, while the matcher was still running unoptimised tier-0 code. Measured that way, row 0's
call took 920 us against 122 us for the call written out. Run in a loop for 2 s, the same row
takes 74 us against 45 us. The table below uses fully compiled code throughout.

## Table (us per search, median)

| row | pattern (subject) | main, call | branch, call | branch, written out | main, written out |
|---|---|---|---|---|---|
| 0 | `(?fi)(?:(?P<g1>(?P>g3)(?:(?:ab\|ac))*)){e<=1}(?&g2)(?P<g2>(?(?<!(?P>g3))ss\|(?P<g3>.*ff)))` ('ﬀbﬀft') | 70.2 | 164.5 | 92.9 | 93.6 |
| 1 | `(?fi)(?:(?P<g1>(?P>g2)(?(?<!(?P>g2))ff\|a{1,2}))){i<=1}(?P<g2>(?:ff){1,2})` ('ﬀSﬀﬀS') | 4.7 | 37.0 | 18.5 | 18.7 |
| 2 | `(?fi)(?:(?P<g1>(?&g2).)){e<=1}(?P>g2)(?P<g2>(?:ss\|st))(?:.)*` ('ﬆxﬆts') | 10.8 | 34.6 | 14.5 | 14.3 |
| 3 | `(?i)(?:(?P<g1>(?&g2))){e<=1}(?=(?P>g2)(?>(?:(?:ab\|ac).*){d<=1}))(?P<g2>ss)` ('sßsﬆbb') | 19.2 | 47.5 | 29.0 | 27.4 |
| 4 | `(?i)(?:(?P<g1>(?&g2)(?:SS){e<=1})){e<=1}(?P<g2>(?:ss\|st))` ('ﬆfﬀßxfx') | 8.2 | 23.1 | 14.9 | 14.8 |
| 5 | `(?fi)(?:(?P<g1>(?P>g2)(?<!(?P>g2)st))){s<=1}(?P<g2>(?:(?:a\|b)){e<=1}(?:ss\|st))` ('fﬀsatﬀﬆ') | 27.7 | 38.0 | 33.2 | 31.9 |
| 6 | `(?fi)(?:(?&g2)){i<=1}(?P<g2>(?:ff){1,2})` ('ﬀSﬀﬀS'), main None | 3.6 | 1.8 | 1.3 | 1.3 |

All rows from row 0 to row 5 answer None on both builds.

## What the numbers say

1. **The written-out form costs the same on both builds** (columns 5 and 6). Patterns with no call
   compile to the same bytecode, as the D50 code comparison already showed.
2. **The branch's call is not 2-7x slower than the written-out form. With compiled code it is 1.1-2.4x
   slower** (column 4 against column 5). The step counts give the same ratios: row 0 takes 1,554
   steps against 1,084, and row 2 takes 473 against 233.
3. **Main's call was fast because its copy failed early.** Main's copy of `ss` or `ff` is two
   separate characters, which can never match the full fold of 'ß', 'ﬀ' or 'ﬆ', so every attempt
   failed at the first character (row 1: 58 steps on main, 280 on the branch, 180 written out). On
   rows 0-5 that early failure still gives the right answer, None, as written out (corrected after
   the blind review). Main's wrong answers among these rows are rows 6 and 7 (None where the
   written-out form gives (0, 4) and (0, 3)) and row 1 of `ascii.jsonl` (None where (0, 7) is
   written out), which involves no full fold. The branch answers all three as written out.
4. **The call overhead is not new to the branch.** With the folds removed (each fold character
   replaced by its ASCII spelling, `tools/probes/d68-call-perf/ascii.jsonl`), main's call takes about the same
   extra steps over the written-out form as the branch's call does. Row 0 takes 2,426 steps on main
   and 2,572 on the branch, against 1,764 written out. Row 1 takes 374 on main and 93 on the branch,
   against 61 written out.
   Anchored attempts at every start position show the same extra cost, so the cost is in each call,
   not in how the search chooses where to start.

## Cause

The extra cost belongs to the call machinery, and it is paid on every entry to a call. A fuzzy
section enters the call many times, once for each error it tries. It is not caused by packed
full-fold fuzzy matching: with packing, the written-out form costs the same on both builds. Each
`GROUP_CALL` (`src/FuzzyRegex/Engine/Matcher.cs:11495-11540`) does the following:

- adds an entry to the `ActiveCalls` hash set;
- may probe the failed-call memo;
- runs `PushRepeats`, which saves the state of every repeat;
- runs `PushCaptures` (`:3182`), which saves the count and current span of every group (D51);
- pushes the return node.

`GROUP_RETURN` (`:11621`) and its backtrack arm undo all of it (`ReturnToCallerCaptures`, `:3051`).
Rows 0-5 each have three to five groups and one or two repeats, so each entry copies all of them,
and the call's extra steps are also costlier per step (row 0: 0.106 us per step against 0.086 us).
Where a call sits in a fuzzy section, a lookaround or a condition, known defect D33 also switches
off the failed-call memo and C2 for the whole pattern.

## Is there a cheap, safe fix?

Not inside D50. The candidates:

- **Save only what the called body can change.** Save the groups the body can write, and the repeats
  it contains, rather than all of them. This is medium-sized work: the compiler would compute a
  per-call-reference write set, and `PushCaptures` and `PushRepeats` would take a range. It needs
  its own witness tests for nested and recursive calls, because D51 and D62 depend on exactly what
  is restored. Expected gain: most of the 1.1-2.4x gap on rows like these, and nothing on patterns
  with one group.
- **D33's memo restriction.** This is already registered and planned.

The site carries a `SHORTCUT (D68)` comment at `Matcher.cs:11536`.

## Review finding F1: calls into fuzzy sections, plain letters

The blind review found calls that are slower than main with the same answer and no full fold.
`ascii.jsonl` row 3 is `(?i)(?:(?P<g1>(?&g2))){e<=1}(?=(?P>g2)(?>(?:(?:ab|ac).*){d<=1}))(?P<g2>qq)`
over 'qqqqqtbb', answer None on both. Measured as above (Release, tiering off, load 1%, median of
15, three interleaved runs per build):

| | main | branch |
|---|---|---|
| call | 30.7-31.7 us | 41.0-41.4 us |
| written out | 22.2-23.3 us | 22.5-23.9 us |

The branch's call is slower than main's call and than the written-out form. Opcode counts for one
search (each build copied and instrumented with a counter at the matcher's two dispatch switches; forward `F` and backtrack `B`) show
where the time goes:

- **The branch's call runs exactly the written-out form's body.** Atomic, Branch, Fuzzy, EndFuzzy,
  Lookaround, StringIgn, FuzzyInsert and Failure counts are identical to the written-out form's.
  The only extra opcodes are the call's own: GroupCall and GroupReturn (25 and 26 each way), and
  the copy's group capture (StartGroup 30 against 5, EndGroup 41 against 15). So nothing is specific
  to the copy. The time over the written-out form is the per-call save and restore described above.
- **Main's call explored a smaller search, not the written-out one.** Its copy of `qq` was two
  `CharacterIgn` items. The packed string `qq` pushes a `FUZZY_INSERT` frame after it matches
  (`Matcher.FuzzyInsert`, `Matcher.cs:6918`, upstream `fuzzy_insert`, `_regex.c:10346`), so one more
  trailing insertion is tried, and the per-character copy never pushes that frame. Main's run had 0
  FuzzyInsert, 14 Fuzzy and 15 Lookaround, against 10, 16 and 20 for the branch and for the
  written-out form. Fewer paths also meant fewer calls (20 against 25), each paying the save and
  restore.

So the slowdown against main comes from the copy now searching what the written-out group searches,
which is the D50 rule, multiplied by the per-call overhead. The string's insertion frame is
upstream's own fuzzy string semantics. Removing it would change which fuzzy match is found first,
so it is not a performance fix. There is no cheap, safe fix inside D50. The per-call save and restore
above is the place to recover the time, in the performance phase.

Recommendation: keep D50 as built. On these rows its cost is the cost of answering correctly, and
the written-out form pays the same. Leave D68 for the performance phase as "per-call save and
restore copies every group and repeat", with the write-set idea above as the starting design.
