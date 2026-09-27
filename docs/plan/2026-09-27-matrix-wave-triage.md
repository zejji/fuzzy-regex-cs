# Matrix wave triage, 2026-09-27

The first three waves of the `matrix` generator (`tools/record-oracle.py`, `_generate_matrix`), 3000
rows each at seeds 7, 4242 and 20260927, recorded on branch `maint/interaction-matrix` against
`regex` 2026.9.10. Nothing was re-recorded. Inputs: `.scratch/wave-seed{7,4242,20260927}.jsonl`,
`.scratch/report-seed*.txt` and `.scratch/matrix-wave*.log`.

| Seed | agree | expected | resource | diverge | upstream rows that contradict themselves |
|---|---|---|---|---|---|
| 7 | 2961 | 7 | 28 | 4 | 1 |
| 4242 | 2953 | 4 | 39 | 4 | 2 |
| 20260927 | 2954 | 3 | 40 | 3 | 4 |

This port's own self-consistency test (`Our_own_answers_never_contradict_themselves`) passed at all
three seeds. The EXPECTED rows fall under existing entries and were not re-triaged.

## Method

Every DIVERGE row and every self-contradicting row was answered by three engines: upstream (`regex`,
one process per row), this port on `main` (`tools/probes/port-probe.cs`) and this port on
`maint/fuzzy-exact-deletion` (the F-A fix, worktree `fuzzy-a2`, the same probe pointed at that
tree's project). Each row was then cut down to the shortest pattern and subject that still shows the
difference. Where a regex principle alone did not settle the right answer, PCRE2 10.47 (Python
binding `pcre2` 0.7.1) was asked the same question without the fuzzy parts.

## Root causes

"F-A" is the port on `maint/fuzzy-exact-deletion`. Spans are UTF-16 offsets; counts are
(substitutions, insertions, deletions).

| Root cause | Rows (seed:row) | Minimal repro | Upstream | Port main | Port F-A | Verdict | Fix location |
|---|---|---|---|---|---|---|---|
| Q2, `\G` inside a fuzzy section (known) | 7:52, 7:1367, 7:1447, 4242:491, 4242:1316, 20260927:230, 20260927:2643 | `(?:a\G){i<=1}`, `match` over `'ab'` | RuntimeError: invalid RE code | NotImplementedException `needs:basic-matching` | same as main | No match: `\G` holds only at 0, and an insertion only moves further from it. Upstream has no answer | `src/FuzzyRegex/Engine/Matcher.cs:11625` (no backtrack arm for a retried `SearchAnchor`) |
| Same-position call guard refuses a finite left recursion (NEW) | 4242:1719, and 7:2439's POSIX-free twin | `(?:\|(?R)a)`, `fullmatch` over `'aa'`; reversed: `(?r)(?:\|a(?R))` | (0,2) | None | None | Upstream right. See below | `src/FuzzyRegex/Engine/Matcher.cs:8441`, rationale at `src/FuzzyRegex/Engine/MatchState.cs:238-256` |
| POSIX fuzzy answer charges an error its flagless twin does not, ledger 9 (known) | 4242:1352, 20260927:197 | `(?e)(?p)(?:(a?)\1){e<=2}`, `match` over `''` | (0,0) with (0,0,1) | (0,0) with no errors | same as main | Port right: upstream's own POSIX-free answer spends no error. The entry is keyed on exact rows, so new shapes show as DIVERGE | `tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs:5271` (add the rows) |
| BESTMATCH ranks by cost, answered at a different span (known, plus F-A) | 7:2677 | `(?b)(?:ab){1<=e<=2,2i+1d+1s<=3}`, `search` over `'aba'` | (0,3) with (0,1,0), cost 2 | (1,2) with (0,0,1), cost 1 | (0,1) with (0,0,1), cost 1 | F-A right. See below | `tools/record-oracle.py:6762` (the generator should not draw `(?b)`/`(?e)` on a weighted equation) |
| Upstream runs away in a recursion that can match empty, ledger 14 (known) | self-contradictions 4242:2682, 20260927:544, plus 105 of the 107 resource rows | `(?p)(?:(?:\|[ab](?R))){2i+1d+1s<=2}`, `fullmatch` over `'a'` | MemoryError; its POSIX-free twin answers (0,1) with (0,1,0) | (0,1) with (0,1,0) | same as main | Port right where sampled. See "Resource rows" | none in the port |
| Fuzzy changes leaked from an abandoned attempt, ledger 11 (known, EXPECTED) | 20260927:559, 20260927:1672 | as recorded | counts and change list disagree | agrees with upstream's counts | 559 as main; 1672 (0,1) with (1,0,1) | Already classified | none |

### The same-position call guard

Ledger entry 14's guard fails any call of a group at a text position where a call of the same group
is already open. Its justification (`MatchState.cs:238-256`, and the comment in
`GroupCallTests.A_call_re_entered_at_the_position_it_is_already_at_fails_instead_of_recursing_for_ever`)
is that such a call "cannot consume anything before it arrives back where it started, so that path
recurses for ever". That is not true of a left recursion whose innermost call takes the empty branch.
With `G -> '' | G 'a'`, matching `'aa'` goes G(G(G(''))'a')'a': three calls of G open at position 0
at once, and the innermost one returns at once. The path is finite, and the guard cuts it.

Measured:

- `(?P<g>|(?&g)a)$` over `'aa'`: upstream (0,2), PCRE2 (0,2), port (1,2).
- `^(?<g>|(?&g)a)$` over `'aa'`, `'aaa'`, `'aaaa'`: PCRE2 (0,2), (0,3), (0,4); upstream the same.
- `(?r)((?:ab|a(?R)))` fullmatch `'aaab'`: upstream (0,4), port None. This is what row 7:2439 hits:
  its POSIX-free twin answers (0,6) upstream, and the port answers None with and without `(?p)`.

PCRE2's own check is narrower than the port's. As remembered from `pcre2_match.c` (`OP_RECURSE`,
not re-read for this note), it raises "nested recursion at the same subject position" only when the
position is the same AND no further text has been inspected since the outer call was entered
(`last_used_ptr`). The measurements fit that: PCRE2 lets the deeper calls through above, where each
earlier failed attempt had looked further ahead, and refuses `^(?<g>|(?&g)a)$` over `'b'`, where
nothing new is ever inspected. The existing tests' shapes that need the guard (for example
`(?P<g1>(?:a?)(?&g1)?)` over `'aaaa'`, and `(?:(?R))`) must keep answering; the fix has to find a
progress test at least as permissive as PCRE2's rather than drop the guard.

Proof that the guard is the cause: the new red test (below) fails on all three rows (None, None,
(1,2)); with line 8441's `goto backtrack` disabled it passes on all three; the line was then
restored and `git status` shows `src/` unchanged.

### Row 7:2677 and the cost term

The port ranks `(?b)` and `(?e)` answers by cost first, then by error count
(`IsBetterFuzzyMatch`, `Matcher.cs:11789`; owner decision 2026-09-12, upstream issue 470). A cost
equation with unequal coefficients is therefore never neutral, even when every candidate is within
its limit: it reprices the candidates. Under `2i+1d+1s` the one-insertion answer costs 2 and the
one-deletion answers cost 1, so the port prefers a deletion; upstream counts errors only and keeps
the first one-error match it finds, the trailing insertion. Controls on the port: `{1<=e<=2}` and
`{1<=e<=2,1i+1d+1s<=3}` (unit costs) give (0,3) with one insertion again, as upstream does. Upstream's
own engine confirms the cheaper match exists: `(?b)(?:ab){1<=e<=2,2i+1d+1s<=1}` gives (1,2) with one
deletion.

Of the two cost-1 answers, (0,1) (match `a`, delete `b`) starts first, so it is the right one. `main`
misses it because of F-A (the exactly matched `b` is never retried as a deletion) and finds (1,2);
the F-A tree finds (0,1). The `bestmatch-ranks-by-cost` entry deliberately refuses a divergence at a
different span, and the other generators keep weighted equations away from `(?b)` and `(?e)` with
`_has_weighted_cost` (`tools/record-oracle.py:4231`, `:6206`, `:6417`). The matrix generator does not
(`:6762`), which is the harness gap that produced this row.

### Rows 4242:1352 and 20260927:197

Both are `(?e)(?p)` over an empty slice. Upstream charges one deletion; its recorded POSIX-free
answer, and this port, charge none. So upstream contradicts itself (`posix-chooses-among-flagless-answers`)
and the port is right. Reading `fuzzy_changes` on either upstream match is an access violation, which
is ledger entry 9's known crash and why the rows say "changes unavailable upstream".

A side observation, not a wave row: without `(?p)` or `(?e)`, `(?:a?){d<=1}` over `''` costs one
deletion on upstream and on `main`, and none on the F-A tree. The F-A branch's own oracle run should
account for that change.

## Resource rows

107 rows, every one upstream `MemoryError`. By shape: 105 contain a group call or `(?R)` (ledger
14, the runaway recursion), and 2 have no recursion: 4242:1928 `(?fi)(?:(?:b){d<=1})++` over
`'bbaaab'`, which is ledger 33's deletion-only repeat (the port answers (0,2) with one deletion), and
20260927:1893 `(?:(?:a){d<=1})+?` fullmatched over `'aaaaba'`, which is open-defect queue item 4 (the
port exhausts its 1 GB backtracking stack in 3.6 s; the right answer is no match).

A random sample of 15 (Python `random.seed(27)`) was answered by the port and checked by hand
against the grammar: all 15 answers are right, every one reached through a path that does not loop.
The 10 that PCRE2 can express without `(?r)`, `(?p)` or fuzzy sections all get no answer from PCRE2:
5 "nested recursion at the same subject position", 4 compile-time "length of lookbehind assertion
is not limited", 1 "JIT stack limit reached". So PCRE2 neither agrees nor disagrees; it refuses, as
upstream does.

## The failing lazy-walks test

`The_lazy_walks_answer_exactly_what_the_eager_ones_do` fails because the matrix generator draws only
`search`, `match` and `fullmatch`, so the wave has no `finditer` or `split` row and the test's floor
("a wave with no iteration row in it discriminates nothing") trips. That is expected, not a harness
gap: `tools/run-oracle.ps1` already documents that a single-generator wave cannot be green for the
same reason, and the floor will be met once `matrix` runs inside the default mixed wave.

## Not settled

- Upstream can crash with an access violation on a heavy row that follows a `MemoryError` in the
  same process (row 4242:2682 after row 7:2439); alone, the same row raises `MemoryError` three times
  out of three. The triage probe runs one process per row because of it. Not investigated further.
