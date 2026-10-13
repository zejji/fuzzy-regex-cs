# Overnight triage, 2026-10-12

Two investigations from the overnight brief (Job C): a flaky timing test, and the scheduled
differential oracle, which has been red on every run since 2026-09-21. Branch
`fix/overnight-triage` from main f1ae913d.

## 1. The flaky named-list test is the efficiency cores, not D82

`OptimiserTrapsTests.A_long_named_list_tries_only_the_members_that_can_start_here` gave its search
a 500 ms match timeout over a megabyte, and failed four times on 2026-10-11, once at 542 ms on an
idle main, in Debug.

**Measured.** A console harness that repeats the test's operation exactly (same members, subject,
warm-up and a 5 s timeout), built against each commit, one fresh process per run, under the test
lock, 2026-10-12. Times in ms; "first" is the call the test makes, "warm" the fastest of four more.

| Commit | Build | Cores | First call, 5 runs (min-max) | Warm, min |
|---|---|---|---|---|
| a1191e65 (before D82 part 1) | Debug | performance (0xFFF8) | 275-308 | 247 |
| 85989b79 (D82 part 1) | Debug | performance | 293-367 | 253 |
| f1ae913d (main) | Debug | performance | 271-302 | 246 |
| a1191e65 | Debug | efficiency (0xFFF0000) | 521-573 | 475 |
| f1ae913d | Debug | efficiency | 532-564 | 477 |
| a1191e65 | Debug | unpinned, idle machine | 286-300 | 250 |
| f1ae913d | Debug | unpinned, idle machine | 281-306 | 250 |
| a1191e65 | Release | performance | 157-173 | 36 |
| 85989b79 | Release | performance | 192-206 | 33 |
| f1ae913d | Release | performance | 194-223 | 32 |

The test alone through the test runner (Debug, unpinned, idle): 406-413 ms over three runs, all
passing; the runner's own start-up work shares the process.

**Verdict.** Not a regression. Debug is the same on all three commits within noise. The search
takes about 280 ms on a performance core and 520-570 ms on an efficiency core (this machine's
i7-13850HX has 16 performance threads, 0-15, and 12 efficiency cores, 16-27, per
`GetSystemCpuSetInformation`), so the 542 ms failure is the search landing on an efficiency core,
which a loaded machine or a long suite run makes likely. GC is not involved (no gen-0 collection
during the call). In Release, D82 part 1 moved the first call from about 165 to about 200 ms (the
loop now starts in tier 0 and is promoted with profile data) and made warm calls about 10% faster;
neither touches the Debug run the ratchet makes.

**Fix (on this branch).** The test now counts engine steps instead of timing the search, the
method D13 introduced for the same kind of flake (`EngineWork.ShouldTakeAtMostSteps`, Debug-only,
skipped in Release after the answers are checked). Measured in Debug: 881,790 steps with the
member index, 214,918,731 (244 times as many) with the index switched off (`BranchChain.MinAlternatives` raised past the
list, a temporary edit). The bound, 2,000,000, sits between them, so the test still fails if the
index stops being used, and it no longer depends on which core the search lands on. The
`[NotInParallel]` attribute existed only for the timing and is gone.

**Not changed, same family.** `An_alternation_of_literals_searches_for_all_of_them_at_once` in the
same class still times a 4 MB search against 25 ms. It has not failed, but it has the same exposure;
it should move to a count the same way when it next needs touching (the vectorised search would be
`WorkCounter.CharactersSearched` territory, which today counts only the fuzzy literal filter).

## 2. The scheduled oracle

### Run 38237349305 (main a9ea2c95, 2026-10-12), seed 7

Reproduced locally on f1ae913d with `tools/run-oracle.ps1 -Seeds 7 -Count 2000 -Configuration
Release` (the workflow's own command): diverge 2, undefined 3, the same two rows.

**Row 24857** (`interactions`, search, partial):
`(?b)(?e)(?:[[:digit:]](?:([^\p{L}]+)){1<=e<=2}){e<=2,s<=1}\K\b` over U+1D7EE U+1F3FB U+1D518
U+1F600. Upstream P(1,4) counts (0,1,0) listing a substitution at 1; CI's port (a9ea2c95) P(0,4)
counts (2,0,0); main's port P(2,4) counts (1,0,0).

Known: ledger 81 (D81). The outer `{e<=2,s<=1}` allows substitutions only (upstream itself:
`(?:0(?:-+){1<=e<=2}){e<=2,s<=1}` does not match 'x-a-', and with `,i<=1` added it does). Starts 0
and 1 both need a second error inside the inner section, which the outer forbids, so main's P(2,4)
with one substitution is the leftmost legal partial. CI's answer broke the outer limit (two
substitutions), which is exactly what D81 fixed on 2026-10-11 (df75b6cc), after a9ea2c95. Upstream's
answer breaks it too (an insertion), and its change list contradicts its counts (ledger 11).
Judged as row 7/24857, cause `ledger 81 + ledger 11`.

**Row 35060** (`partial-sliced`, prefilter-free, search, IGNORECASE|FULLCASE, slice [0, 1)):
`(\p{L}*?)(?P<g2>[\U0001F3FB])\2\K$` over U+200D. Upstream P(0,1), groups unset; port P(1,1),
group 1 (1,1). Red on this schedule on 2026-09-28, 2026-10-05 and 2026-10-12, never judged.

New upstream defect: ledger 82. Minimised to `(?if)a*?b` over '-': upstream P(0,1), but neither `a`
nor `b` matches '-', so no completion can start at 0 (README's own definition of a partial).
`(?i)` alone, no flags, and upstream one character later ('-c': None) all agree with the port;
PCRE2 10.47 gives no partial. Cause: the LAZY_REPEAT_ONE arm for a STRING_FLD tail returns a partial
as soon as its look-ahead runs out of text, before checking the repeat can take the characters in
between (`_regex.c:16764-16767` against `:16776-16786`). The port does not port that arm and is
right. Pinned by `RepeatTests.A_lazy_repeat_before_a_full_folded_literal_reports_a_partial_only_where_one_can_start`
and judged row 7/35060; draft report `upstream-reports/entry-82-...`. The group-1 difference is
entry 74's (a partial sets the groups it completed).

**The three "undefined" rows** (38151, 38183, 39863, `verbs`, finditer-overlapped) are ledger 5:
upstream crashes or reads uninitialised memory from `new_position` in `basic_match`, attributed by
the MSan screen. They do not fail the run. The `IndexError: list index out of range` and "group 'gN'
can call itself" lines in the CI log belong to EXPECTED rows (`reverse-grapheme-takes-the-whole-
cluster`, `verb-unwinds-through-unfinished-groups`, `left-recursion-is-refused-at-compile-time`),
which the failure message prints in full.

### Why the schedule has been red since 2026-09-21

Every run of the workflow since 2026-09-21 failed. The first two were infrastructure; from
2026-09-28 on, every run drew rows nobody had judged. To see which are still unaccounted for, the
66 DIVERGE and FAULT rows of runs 3-7 were cut out of their recorded waves, re-recorded with
`run-oracle.ps1 -Rows` and compared with main f1ae913d plus this branch's two judged rows:
56 are EXPECTED now (47 judged rows, 6 `fuzzy-exact-item-offered-as-a-deletion`, 2
`fuzzy-empty-iteration-needed-rule`, 1 `bestmatch-loses-a-candidate`), 9 diverge and 1 is a fault.

| Run | Date, head | Job | Failing rows then | Now on main |
|---|---|---|---|---|
| 35579883763 | 09-21, 229a2dd6 | oracle | none: the compile-parity fixture was stale | infrastructure, fixed since |
| 35972437798 | 09-24, 966c2fb6 | sweep | none: sweep-seeds.ps1's `^agree \d` match was stale | infrastructure, fixed since |
| 36404722626 | 09-28, 2373e273 | oracle | seed 7: 35060; 4242: 34781; 20260928: 24171, 24641, 28167, 33663, 42027; reuse test: 31964 | 35060 judged here (ledger 82); 34781, 42027 accounted; **24171, 24641, 28167, 33663 not**; 31964 see below |
| 36843747887 | 10-01, 9672d557 | sweep, 6 seeds | 25 rows | all accounted |
| 37295584525 | 10-05, 9672d557 | oracle | 7: 35060; 4242: 34781; 20261005: 25346, 25732, 40458 | all accounted (35060 judged here) |
| 37758544541 | 10-08, 9672d557 | sweep, 6 seeds | 21 rows | all accounted |
| 38237349305 | 10-12, a9ea2c95 | oracle | 7: 24857, 35060; 20261012: 24184, 24238, 24272, 33186, 42649, FAULT 38391; reuse test: 36404 | 24857, 35060 judged here; **24184, 24238, 24272, 33186, 42649 and FAULT 38391 not**; 36404 see below |

**Not accounted for, and not judged tonight** (each needs the research and blind review that
judging takes; recorded here so the next session starts from the list):

- 20260928/24171 `interactions` fullmatch: `(?b)(?e)(?P<g1>(?:\W(?:ß){d<=1}){s<=1,i<=1,d<=1}(?P>g1)?)...`,
  upstream no match, port (0,9) through a recursive fuzzy group.
- 20260928/24641 `interactions` search, IGNORECASE: `(?b)(?:a\s{1}(\p{L})){1<=e<=2:[^a-z]}(\w{1})`
  over 'aa\ra', upstream None, port (1,3) with a deletion where the text character is outside the
  error test `[^a-z]`. A question of what a deletion's test reads.
- 20260928/28167 `conditionals` finditer-overlapped: `(?(?<!(?:İ|\p{Lu})+)(A))$`, upstream
  3 matches, port 2 (misses (4,1)).
- 20260928/33663 `partial` search, IGNORECASE: a `(*SKIP)` then a conditional call, upstream
  as (UTF-16 index, length) P(3,4), port P(2,5).
- 20261012/24184 and 24272 `interactions` split: `(*SKIP)` inside a reversed fuzzy pattern;
  upstream splits into 4, port does not split.
- 20261012/24238 `interactions` finditer: `(?e)\b\K(?<=(?:[^a][a]([[:digit:]]*)){1<=e<=2})`,
  same spans, group 1 and fuzzy counts differ from the second match on.
- 20261012/33186 `partial` search: `^([a\d]+)\1(?:\p{L}?(*SKIP)[^a]|[\w\s])` over 'AAﬃ',
  upstream P(3,3), port P(0,3).
- 20261012/42649 `fuzzy-anchored` search: as (UTF-16 index, length), upstream P(1,7) counts (1,2,0), main
  P(5,3) (1,0,0), CI's port P(1,7) (2,1,0); D81 moved it, so ledger 81 is the likely cause, unconfirmed.
- 20261012/38391 `verbs` finditer-overlapped, FAULT: upstream crashes; the MSan screen finds
  uninitialised memory from `data_r2368` in `basic_match` as well as `new_position`, "NO KNOWN
  DEFECT". A possible new ledger entry.

**The reuse test's two failures were the harness, now fixed.** On 09-28 (row 31964) and 10-12
(row 36404) `A_pattern_that_has_answered_before_answers_every_row_as_a_fresh_one_does` failed on a
substitution whose template refers to a group the pattern does not have. Fresh and reused patterns
both raised `FuzzyRegexParseException: invalid group reference` while matching, the same answer;
the comparison also read the exception's stack trace, whose top frame was `GetSubReplacement` the
first time and `Subx` the second (the JIT had inlined the helper by then). Upstream behaves the
same way for the observable part (`regex.compile('a').sub(r'\1', 'zzz')` returns 'zzz', and raises
only when a match needs the template). The comparison, in the reuse and the lazy-walk tests, now
reads the one-line answer only (`OracleWave.SameAnswer`), pinned by
`OracleWaveTests.Two_errors_that_differ_only_in_their_stack_trace_are_the_same_answer`.
