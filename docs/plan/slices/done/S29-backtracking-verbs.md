---
slice: S29
phase: 4
title: Backtracking verbs - (*PRUNE) and (*SKIP)
delivers: [backtracking-verbs]
---

# S29 - Backtracking verbs: `(*PRUNE)` and `(*SKIP)`

Needs S27 and S28: half the verb tests place the verb inside a lookbehind, and the pruning stack
they cut to is the one `push_repeats` maintains. `(*FAIL)` is already the `FAILURE` opcode and its
tests passed on 2026-09-11; this slice is the other two verbs.

## Scope

- **`PRUNE`** (`upstream/src/_regex.c:13894`): `top_bstack(state)` (`:2811`, which is
  `top_size` over `pstack`) then continue. **`SKIP`** (`:14544`): move `slice_start` (or
  `slice_end` when reversed) to `text_pos`, then the same prune. Neither has a backtrack arm -
  they never push, so backtracking never reaches them.
- **The pruning stack `pstack`** already exists as `MatchState.Pstack`, and one push site is
  ported (`Matcher.cs:2418`). Account for every upstream `pstack` site: pushes at `:2591`
  (`push_repeats`), pops at `:2765`, drops at `:2801`, resets at `:3408` and `:15750`. A verb
  cuts to whatever the most recent push recorded, so a missing push makes a verb cut too far and
  a missing pop makes it cut too little - and both pass a test that has no repeat in it.
- **Upstream issue 613**: `(*SKIP)` inside an atomic group leaves a stale backtrack limit, which
  in C reads past the buffer and here would surface as an `IndexOutOfRangeException` or a wrong
  answer. Port faithfully; add the pattern from the issue as a gap test pinning *current* upstream
  behaviour, so Phase 6 has a failing test to fix rather than a report to reproduce.

## Verification

- **Un-skip** `needs:backtracking-verbs` (32 tests, `RegressionsBacktrackingVerbTests.cs`).
- **Oracle generator `verbs`**: `(*PRUNE)` and `(*SKIP)` after a greedy run, after a lazy run,
  inside an alternative, inside an atomic group, inside a positive and a negative lookaround,
  under `(?r)`; compared through search and findall, where `SKIP` moving the slice start is
  observable as the *next* match's start. Zero divergences. Negative controls: `SKIP` not moving
  the slice; `PRUNE` cutting to the bottom of `bstack` instead of the top of `pstack`.
- **Add `verbs` to the default oracle list.**

## Done when

- [x] Tag delivered or stragglers retagged.
- [x] **Oracle wave green**; controls recorded in full. *Controls recorded in full, with baselines,
      in "Controls, re-run at the close" below. The wave is **not** green and cannot be made green by
      this slice: four rows in 1200 at seed 20260913 are upstream's `search_start` optimisation,
      which this port defers to Phase 7 - not a verb defect. Named, minimised, pinned as gap tests
      and recorded in PORTMAP against Phase 7, which is the same treatment the owner approved for
      `locate_required_string` on the same day. The `verbs` generator therefore stays off
      `run-oracle.ps1`'s default list until Phase 7 lands `search_start`. See "The real root cause"
      below; this box is ticked as **superseded**, not as satisfied.*
- [x] PORTMAP: `PRUNE`, `SKIP`, `top_bstack`, and every `pstack` site accounted for.
- [x] Ratchet GREEN, baseline updated, blind review (hunt: a verb reached by backtracking; `SKIP`
      under `(?r)` moving the wrong end), commit.

---

# PARKED, 2026-09-11 - not moved to `done/`

`(*PRUNE)` is delivered and believed correct. **`(*SKIP)` is not faithful**, the differential oracle
found it, and this slice stopped rather than guess at the fix. Everything below is committed and
green except the one criterion above.

## What landed

- **`PRUNE` (`:13894`) and `SKIP` (`:14544`)** as two forward cases in `Matcher.BasicMatch`, placed
  where upstream places them. Neither has a backtrack arm, because neither pushes anything.
- **`top_bstack` (`:2811`)** as `Matcher.TopBstack`, over a new `ByteStack.TopSize` (`top_size`,
  `:2805`). Reading the top of the pruning stack into `Bstack.Count` truncates the backtracking
  stack, and that one line is the whole of what a verb does.
- **Every `pstack` site accounted for.** The slice file said one push was ported; that was stale -
  S27 and S28 had taken all ten sites. Nothing was missing. PORTMAP now lists them by line.
- **32 ported tests un-skipped**, all passing; `needs:backtracking-verbs` is gone from the board.
- **A root-cause fix in `Engine/Iteration.cs`**, described below - unrelated to the verbs themselves
  but only reachable through one.
- **Oracle generator `verbs`**, and four negative controls.
- **Gap tests** in `tests/FuzzyRegex.Tests/Gaps/Engine/BacktrackingVerbTests.cs`.

Ratchet GREEN: 5729 tests, 5394 passing, baseline updated. Parity 81.3% -> the board is regenerated.

## The blocker: `(*SKIP)` does not commit the search position

Reproduced against `regex` 2026.7.19 on 2026-09-11, deterministic and stable across repeats:

```python
import regex
p = regex.compile(r"(?:..(*SKIP)x|q)x")
p.search("ab cd xx")      # None   - twice running, and findall gives []
p.match("ab cd xx", 4)    # (4, 8) - the match our port returns from search
```

Our port returns `(4, 8)` from `search`. Upstream finds that match only when the search *starts* at
4; started at 0, a failed attempt in which a `(*SKIP)` fired commits the search past it, and
position 4 is never tried. Our port re-tries it. This is forward-direction and plain `search` - it
needs no `(?r)` and no multi-match operation - so it is the core semantics of the verb, not an edge.

The same root cause is very likely behind the four `(?r)` multi-match divergences the wave also
found, e.g. at 1200 rows, seed 20260913:

```
tools/run-oracle.ps1 -Generator verbs -Count 1200 -Seed 20260913   # agree 1196, diverge 4
```

rows 502 (`finditer`), 504 (`split`), 519 and 863 (`finditer-overlapped`), all `(?r)`, all carrying
a `(*SKIP)`. Checked and **not** upstream instability: each is stable when the caller holds the
previous `MatchObject` and when it does not.

**Why this was not fixed here.** The mechanism is not understood - a hand trace of upstream's
`FAILURE` advance (`:15695`-`:15738`, whose `text_pos < slice_start` clamp *is* ported, at
`Matcher.cs:5334`) predicts upstream should match at 4, and it does not. Guessing at a fix in
`basic_match`'s search-advance path risks breaking the 5394 passing tests for a cell no ported test
covers. S30 or a follow-up slice should start by instrumenting upstream's advance on the
reproduction above.

**`verbs` is deliberately NOT in `run-oracle.ps1`'s default generator list.** Leaving it there would
turn every later slice's oracle run red for S29's reason and hide that slice's own result. The
parameter's doc comment says so and says to put it back the moment this is fixed.

## A real bug this slice did fix: `findall` and `finditer` are not the same loop

S25 recorded that `pattern_findall` and the scanner were "measurably the same function - verified
2026-09-01 ... on nine subject/pattern pairs". They are not. Only `pattern_findall` carries a
`slice_start <= text_pos <= slice_end` loop guard (`:22415`); `scanner_search_or_match` (`:20903`),
`pattern_subx` (`:21859`) and `pattern_split` (`:22285`) leave it to `do_match`'s own test
(`:18128`), which compares `text_pos` against `slice_end` going forward and never against
`slice_start`. The two agree on every pattern that cannot move `slice_start` mid-scan, which in S25
was every pattern there was - so the nine pairs could not have separated them. Measured:

```python
regex.findall(r"[A-Z]*(*SKIP)_", "__BB__B", overlapped=True)          # 3 matches
[m.span() for m in regex.finditer(r"[A-Z]*(*SKIP)_", "__BB__B", overlapped=True)]
# [(0,1), (1,2), (2,5), (3,5), (4,5), (5,6)] - 6 matches
```

The six are correct: each equals `p.match("__BB__B", k)` for k = 0..5, so the scanner agrees with a
fresh state at every position and `findall`'s guard is what cuts its own scan short. `Iteration.Scan`
and `Iteration.Next` had that guard; this surface has no `findall` (`Matches` returns `Match`
objects), so the guard is removed and `MatchState.IsInSlice` is deleted. Verified independently of
the oracle, against per-position ground truth.

## Controls

Run with `python tools/run-controls.py --slices S29`. Figures are from the committed code and the
committed generator, re-run after the last change to either. Generator `verbs`, 600 rows, seeds 7 /
20260913 / 4242.

> **Control S29-A, `SKIP widens the slice instead of moving it to text_pos`**: in `Matcher.cs`, the
> `case Opcode.Skip` body, change `state.SliceEnd = state.TextPos;` to `state.SliceEnd =
> state.TextEnd;` and `state.SliceStart = state.TextPos;` to `state.SliceStart = state.TextStart;`.
> Result: **0 / 1 / 1** divergences of 600.

> **Control S29-B, `SKIP moves the wrong end under (?r)`**: in the same body, swap the two arms so
> the reverse branch assigns `SliceStart` and the forward branch `SliceEnd`. Result:
> **48 / 40 / 36** of 600.

> **Control S29-C, `a verb prunes to the bottom of the bstack, not the top of the pstack`**: in
> `Matcher.TopBstack`, replace the whole `if (state.Pstack.TopSize(out long bstackCount)) { ... }`
> with `state.Bstack.Count = 0;`. Result: **225 / 241 / 220** of 600 (seed 4242 also reports 1
> unsupported).

> **Control S29-D, `the scanner carries findall's slice_start guard`**: in `Iteration.Scan`, change
> `while (true)` back to `while (state.SliceStart <= state.TextPos && state.TextPos <=
> state.SliceEnd)`. Result: **2 / 4 / 0** of 600.

The exact before/after text of each is in `tools/controls.json`, which is what the harness applies,
so these are reproducible from the repository rather than from this prose.

**A and D are thin, and that is a finding about the generator, not a tick.** Both probe the same
cell - a moved slice start only changes where a *later* match in the same scan may begin - and D is
blind at seed 4242. A widening was tried and **reverted**; the measurement is worth keeping so
nobody repeats it. Weighting the piece count towards one, raising the doubled-subject probability,
dropping most anchoring affixes, broadening the run atoms and drawing more atoms from the subject,
measured at 4800 rows, seed 7, before and after: overlapped rows carrying a `(*SKIP)` 384 -> 319,
rows matching more than once 23 -> 49, rows where the moved slice start changes the answer
**13 -> 12**. It doubled the coverage it aimed at and did not move the cell at all. What limits the
cell is not "does the row match twice" but "does a `(*SKIP)` land two or more characters past the
match start and the pattern still match one position on". It was reverted because it also drove the
wave into the unresolved region above.

C is the strong one, and it is the control that matters: it is the mutation that makes a verb cut to
the wrong place, and a fifth to two fifths of the wave sees it.

## Review

One blind pass over the whole diff, briefed for reproductions only. **One finding raised, one
reproduced, zero fixed** - the finding is the `(*SKIP)` search-commit blocker above, which is why
this slice is parked rather than done. The reviewer also cleared the other three hunts with evidence:
no wave or test reached a verb by backtracking (neither opcode pushes to `Bstack`); the `(?r)`
slice-end branch is on the correct side (inverting it is control S29-B, 2 -> 145 divergences at seed
424242 on its own run); and `TopBstack`'s narrowing and empty-pstack cases produced no failing case.
It also confirmed the `IsInSlice` removal causes no hang and no regression - 12000 rows across the
fifteen other generators at seed 5150 agree, and the full suite is 5729 tests, 0 failed. No second
pass was needed, because nothing was changed in response.

## Root cause, found at the owner checkpoint (2026-09-11, after the park)

Not a verb defect and not ours: **upstream's required-string prefilter.** `locate_required_string`
(`_regex.c:11082`) caches where the pattern's required literal was last found (`req_pos`, `req_end`)
and reuses the cache while `text_pos <= req_pos`. When the required literal has a variable offset
(`req_offset < 0`, as `x` does in `(?:..(*SKIP)x|q)x`) and a `(*SKIP)` has moved `slice_start`,
the cached position and the moved slice disagree and the attempt fails before `basic_match` runs.
Three probes isolate it (`regex` 2026.7.19, `.scratch/skip-probe3.py`, re-creatable from this list):

| Pattern | Subject | Upstream | Why it discriminates |
|---|---|---|---|
| `(?:..(*SKIP)x\|q)x` | `ab cd xx` | `None` | the parked reproduction; `.search(s, 4)` is `(4, 8)` |
| `(?:..(*SKIP)x\|q)[xy]` | `ab cd xx` | `(4, 8)` | same verb, no required literal |
| `(?:..(*SKIP)x\|q)x` | `abxcd xx` | `(4, 8)` | an early `x` moves the cache |
| `..(*SKIP)xx` | `cd xxx` | `(1, 5)` | fixed `req_offset`: locator jumps to 1, the verb never fires |

Our port has no prefilter until Phase 7 and returns `(4, 8)`, matching PCRE and upstream-from-4.
The four `(?r)` wave divergences (seed 20260913, rows 502, 504, 519, 863) have the same shape
reversed. This is the mechanism of upstream #612 in the Phase 6 triage. What to do about the
`verbs` generator is the owner's call - see STATE.md.

## Verdict and finishing scope, set at the owner checkpoint (2026-09-11)

**Refinement of the root cause above, and the verdict.** Upstream's own compile call carries
`req_offset=3, req_chars="x"` for `(?:..(*SKIP)x|q)x` (intercept `regex._regex.compile`), so
`locate_required_string` (`_regex.c:11082`) moves the *first attempt* to `found_pos - 3`: `x` at 6
puts it at 3, and from 3 the verb legitimately steps 3 -> 5 and position 4 is never tried. That is
the fixed-offset jump, not the cache. Perl's `use re "debug"` shows the same: `Found floating substr
"x" at offset 6 (rx_origin now 3)`, attempts at 3 and 5 only, and Perl agrees with upstream on 20+
probes bit for bit. PCRE2 documents the class ("Optimizations that affect backtracking verbs",
pcre2pattern). With upstream's prefilter neutralised from Python (`req_offset=-1, req_chars=None`),
every divergent case gives the port's answer: `ab cd xx` -> (4, 8); `abcdxxx` -> (2, 6) not (3, 7);
`(?:aa(*SKIP)x|M)x` on `aaaaxx` -> (2, 6). 450 oracle rows of verb patterns with **no literal
anywhere** (so no required string exists) agree with the port completely. A blind Opus review tried
to falsify this and its counter-cases all flipped the same way under the neutralised prefilter.

**So: not an upstream bug to report, and not a port bug to fix.** The port has no prefilter until
Phase 7 and matches upstream-without-its-prefilter exactly. Phase 7 porting `locate_required_string`
faithfully will make the real upstream and the port agree.

**Finish the slice as follows** (this is the pending queue's work; the code committed in `8e80b21`
stays):

1. **Record the `verbs` generator against a prefilter-free upstream.** In `tools/record-oracle.py`,
   for rows of this generator only, wrap `regex._regex.compile` so that argument 7 (`req_offset`)
   is `-1` and argument 8 (`req_chars`) is `None`, and tag each such row (for example
   `"oracle": "prefilter-free"`) so the wave file says what it was recorded against. Quote the
   reproduction above in the docstring, and say that **Phase 7 must remove this wrapper the moment
   `locate_required_string` is ported** - at which point the plain upstream and the port agree and
   the tag becomes a lie. That is the strictness the ROADMAP asks of an intentional divergence.
2. **Put `verbs` back on `run-oracle.ps1`'s default list.** Re-run `-Generator verbs -Count 1200
   -Seed 20260913`: the four `(?r)` rows must now agree; zero divergences overall. Re-run every
   S29 control against the committed generator, at the recorded seed and one fresh seed, and
   re-record the figures (they will change: the wave's ground truth changed).
3. **A gap test** pinning `(?:..(*SKIP)x|q)x` on `ab cd xx` -> (4, 8) and `(?:aa(*SKIP)x|M)x` on
   `aaaaxx` -> (2, 6), whose comment says both become `None` when Phase 7 lands the prefilter and
   that the test must then be inverted, not deleted.
4. **PORTMAP**: extend the deferred `locate_required_string` row with one sentence naming this
   interaction, so Phase 7 knows the verbs wave will go red on purpose.
5. Closing notes' Review paragraph, `git mv` the slice file to `done/` - the step the first
   session missed - STATE.md, DECISIONS, commit.

---

# CLOSED, 2026-09-11 (second session)

**The verdict above is right about `locate_required_string` and wrong about what the wave found.**
Step 2 of the finishing scope - "the four `(?r)` rows must now agree" - is false, and it was tested
before it was believed: recording the generator against a prefilter-free upstream changes **0 rows
of 3600** (seeds 7, 20260913, 4242, 1, 20260911; `.scratch/wrapper-effect.py`, which compares each
row recorded both ways). The four wave divergences survive the prefilter-free recording unchanged.
The parked reproduction and the wave were two different defects filed as one.

## The real root cause of the four wave rows: upstream's `search_start`

Minimised from row 502 to one line, against `regex` 2026.7.19 on 2026-09-11:

```python
regex.finditer(r"(?r)(?:a*(*SKIP)b|[^a-f])$", "\nb", regex.M)   # upstream: one match, (1, 2)
```

This port finds `(1,1)` and then `(0,1)`.

`basic_match` takes the fast `search_start` path (`:11819`) whenever the start test has a
`search_start_*` twin, and the two halves disagree about the slice:
**`search_start_END_OF_LINE_rev` (`:8055`) bounds itself with `text_end`, while
`try_match_END_OF_LINE` (`:7108`) - the predicate `basic_match` itself consults - bounds itself with
`slice_end`.** Nothing but a `(*SKIP)` moves the slice inside an attempt, so the two agree on every
pattern there is; once one does, upstream's fast path walks straight past a start position its own
slow path would accept. This port has only the slow path (`Matcher.cs`, the `next_match_2` block -
`search_start` is a Phase 7 deferral), so it tries that position.

**Confirmed by construction, not by reading.** Emulating `search_start_END_OF_LINE_rev` in front of
each attempt (a scratch console project against the internals, `.scratch/probe/`) makes the port
reproduce upstream exactly on all five cases tried, including `b\nb`, where upstream has two matches
and the emulation must not lose the second. The isolating ladder that got there is in the gap test's
comment: the divergence needs a `(*SKIP)` (a `(*PRUNE)` agrees), needs `(?r)`, needs a multi-match
operation (plain `search` agrees), and needs `$` **with** MULTILINE - `\Z` and `$` without MULTILINE
both agree, because `END_OF_STRING_LINE`'s `try_match` (`:7127`) and its reversed `search_start`
twin (`:8113`) both bound themselves with `text_end` and cannot disagree.

The other two shapes (rows 519 and 863) have `startTest=SetUnionRev`, so they are the same mechanism
through a different twin in the same family.

**This is the same class as the required-string finding, and gets the same treatment**: it is an
upstream start-position optimisation that `(*SKIP)` makes observable, it is not a bug on either
side, and porting it is Phase 7's job. PORTMAP's prefilter row and its `search_start_*` deferral row
both said "semantically transparent ... answers the same"; both are corrected, because that is true
only of patterns that cannot move the slice mid-attempt.

## What landed in this session

- **`tools/record-oracle.py`**: `PREFILTER_FREE_GENERATORS` and `_compile_upstream`, which intercept
  `regex._regex.compile` and force `req_offset=-1, req_chars=None` for `verbs` rows only, tagging
  each such row `"oracle": "prefilter-free"`. `cache_pattern=False` keeps upstream's own pattern
  cache out of it in both directions. **It changes 0 rows of 3600** - it is insurance against the
  shape the parked reproduction names, not a fix for anything the generator currently emits, and the
  closing notes say so rather than letting a future reader assume it earned its keep.
- **Two gap tests** in `Gaps/Engine/BacktrackingVerbTests.cs`, both marked
  `PHASE 7: INVERT THIS TEST, DO NOT DELETE IT`: the required-string pair ((4, 8) and (2, 6)) and the
  `search_start` pair, the latter carrying its own two-case control that the port must not simply
  find fewer matches.
- **`docs/PORTMAP.md`**: both deferral rows corrected, with the mechanism, the measurement date and
  the Phase 7 instruction.
- **`tools/run-oracle.ps1`**: the `verbs` doc comment rewritten to the real reason, with the
  minimised reproduction and the three shapes the four rows take.
- **`tools/controls.json`**: a fourth seed (314159) on all four S29 controls.

## Controls, re-run at the close

Run with `python tools/run-controls.py --slices S29`, against the code and the generator being
committed, at seeds 7 / 20260913 / 4242 / **314159** (the fourth is new, and is the seed this slice
had not used). `.scratch/control-waves/` was deleted first, because the recorder changed.

**Read these as before-and-after, not as totals.** The honest engine already diverges on these waves
(the `search_start` rows above), so a raw mutated count is not the control's signal. Baseline, from
`.scratch/baseline-waves.py` - the same cached waves through the unmutated consumer:

| wave | seed 7 | seed 20260913 | seed 4242 | seed 314159 |
|---|---:|---:|---:|---:|
| honest engine | **0** | **3** | **0** | **1** |

| Control | mutated | delta against baseline |
|---|---|---|
| S29-A `SKIP widens the slice instead of moving it to text_pos` | 0 / 1 / 1 / 1 | 0 / **-2** / +1 / 0 |
| S29-B `SKIP moves the wrong end under (?r)` | 48 / 40 / 36 / 44 | +48 / +37 / +36 / +43 |
| S29-C `a verb prunes to the bottom of the bstack, not the top of the pstack` | 225 / 241 / 220 / 235 | +225 / +238 / +220 / +234 |
| S29-D `the scanner carries findall's slice_start guard` | 2 / 4 / 0 / 1 | +2 / +1 / 0 / 0 |

The exact before/after text of each is in `tools/controls.json`, which is what the harness applies,
so these are reproducible from the repository rather than from this prose.

**Three findings in that table, and none of them is a tick.**

**S29-A is not a control at all as recorded, and at one seed it runs backwards.** Its mutation
widens the slice back to the whole text - which is precisely what neutralises the `search_start`
asymmetry - so at seed 20260913 it *removes* two of the three divergences the honest engine has and
adds one, for a net **-2**. It fires on exactly one row, at one of four seeds. The first session
recorded it as "0 / 1 / 1" and read that as thin; it is worse than thin, and the reason is now
understood. Whoever ports `search_start` should rewrite or retire it.

**S29-D is blind at two of four seeds** (+2 / +1 / 0 / 0). Unchanged from the first session's
reading, and the widening it tried and reverted is documented in the generator itself; the fourth
seed adds a third data point and does not change the conclusion.

**S29-C is the strong one** and is what says a verb cuts to the right place: a third to two fifths of
every wave sees the mutation, at every seed.

## Review

One blind pass over the whole diff, briefed for reproductions only and dispatched inside the
session's own turn. **One finding raised, one reproduced, one fixed:** `run-oracle.ps1`'s new doc
comment said the four wave rows are ones "where this port answers with an extra match", and the
reviewer reproduced the wave to show that only row 502 is that - 519 and 863 keep the match count and
move one span, and 504 is a `split` giving three parts against upstream's one. The comment now says
all three shapes. The reviewer cleared everything else with evidence rather than with an opinion:
the argument indices 7 and 8 are right against `_main.py:660` and the interception really prints
`req_offset, req_chars = (3, (120,))` for the parked pattern; the patch cannot leak in either
direction (a `--rows` file alternating `literals` and `verbs` rows on the same two patterns records
`nomatch` and `[4,8]` respectively in both orders, and `cache_pattern=False` skips upstream's cache
lookup *and* its store); the restore is in a `finally`; all 7 tests in the file pass as asserted and
every upstream value quoted in their comments reproduces; every upstream line number in the diff is
correct, including the `:8055` / `:7108` `text_end`-versus-`slice_end` claim the whole root cause
rests on; `run-oracle.ps1`'s `-Generator` default is byte-identical to `HEAD`; and `--self-check` and
`--verify-determinism` both pass. **A second pass was not run**: the only change made in response
was one doc comment inside a file the reviewer had already read in full, and it added no public API,
no tooling behaviour and no code.

## For the owner - the one judgement call in this close

The slice file's own "Done when" asked for a green `verbs` wave, and this slice closes without one.
The call made here is that the residue is **not this slice's capability**: `(*PRUNE)` and `(*SKIP)`
are delivered, all 32 ported tests pass, and what is left is a named, minimised, test-pinned Phase 7
optimisation, handled exactly as `locate_required_string` was handled a few hours earlier on the
owner's own decision. The alternative was to park S29 a second time, which would leave the pending
queue blocked on work no Phase 4 slice can do. If you would rather S29 stayed open until
`search_start` lands, move this file back out of `done/` - nothing else in the commit depends on
which directory it sits in.
