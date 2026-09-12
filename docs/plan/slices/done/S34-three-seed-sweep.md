---
slice: S34
phase: 4
title: Three-seed sweep - judge the residual divergences and make the default wave reliably green
delivers: []
---

# S34 - Three-seed sweep

Added 2026-09-12 after S33's blind review found that **a default oracle wave is not reliably
green and never was**: every slice ran one seed, and that seed happened to be clean. Three
divergence families surfaced at other seeds, none caused by S33. The owner's rule applies: each
gets a verdict backed by an isolating probe, a second engine where one exists, and a blind
review; a port bug is fixed test-first; a port-right divergence is pinned permanently and
classified in `tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs`. The phase cannot close on a
red default wave, so this slice precedes S35.

## Scope

1. **Forward `(*SKIP)` inside a bounded repeat under an overlapped scan.**
   `regex.finditer(r'(?:[^\d](*SKIP)){2,3}', '\r\naabb ', regex.M, overlapped=True)` gives upstream
   `(0,3)(1,4)(2,4)(3,4)(4,7)(5,7)`; the port has `(2,5)` and `(3,6)` in the middle. Probed at the
   checkpoint (`tools/probes/`, add the script): upstream's **own** `match` and `search` at every
   start position 0..6 agree with the port exactly - `(2,(2,5))`, `(3,(3,6))` - so only its
   overlapped scanner disagrees, and the hypothesis is S29's mechanism: the scanner keeps one
   `RE_State` across matches, a `(*SKIP)` that moved `slice_start`/`slice_end` in a failed attempt
   persists into the next scan, and `scanner_search_or_match` (`:20874`) does not reset the slice
   between overlapped scans. Prove or refute by reading that function and by emulating the carry-over
   in a probe. If upstream's scanner is the inconsistent party, the port is right: pin it, classify
   the family, and add it to the upstream report draft. If the port's `Iteration.Scan` differs from
   upstream's scanner in a way *both* of upstream's doors would reject, fix it.
2. **A reversed capture whose end precedes its start.**
   `regex.compile(r'(?r)(?<g>[ab]+)(?=(?&g))b').search('>abbaa\r<').spans('g')` is `[(3, 2), (1, 3)]`
   upstream; the port records `(3, 3)`. A span with end before start is not a valid span, so
   upstream is wrong on its face - but find *why* (a group call inside a lookahead under `(?r)`,
   with `build_GROUP` direction propagation - issue 614, fixed upstream 2026-08-30 - the first
   suspect), state what the correct span is and why the port's `(3,3)` is it, blind-review the
   verdict, pin, classify, draft.
3. **The bounded-lazy-repeat partial family** (`ba??x` on `baa`, judged port-right in the research
   document, PCRE2 concurring). It reaches the `partial` wave at seed 314159, two rows. S33 found no
   classifier predicate that does not also swallow a genuine missed partial. Write the
   `ExpectedDivergences` entry on the *minimised rows* rather than on a predicate, so the staleness
   alarm still fires, and widen the entry only with rows a probe has individually judged.
4. **Three seeds are the new floor.** `run-oracle.ps1` gains a `-Seeds` list (default three, one
   of them the run's date) and reports per seed; `docs/VERIFICATION.md` and the `port-slice` skill
   say a wave counts only at three seeds. **Then `verbs` goes on the default list** and the whole
   default list runs green at all three seeds, with every classified row printed as `EXPECTED`.
5. **The upstream report draft** gains the items above that turn out to be upstream's, in the
   same shape as the existing four. Still not filed.

## Verification

- Every generator on the default list, three seeds, zero unexpected divergences; the command and
  its output quoted.
- `Every_expected_divergence_still_diverges` covers every new entry; each entry names its permanent
  test and its minimised row.
- Controls: `python tools/run-controls.py --slices S29,S31,S33` against the committed code; a new
  control for any engine change this slice makes.

## Done when

- [x] Items 1 and 2 have a written verdict each, a blind review's report, and either a fix or a
      permanent test plus an `ExpectedDivergences` entry.
- [x] Item 3 classified on minimised rows; the staleness test covers it.
- [x] Three-seed runs in `run-oracle.ps1`, documented in VERIFICATION.md and the skill.
- [x] `verbs` on the default list; the whole default list green at three seeds.
- [x] Report draft extended, not filed.
- [x] Ratchet GREEN, baseline updated, blind review (hunt: a classifier entry wide enough to
      swallow a genuine divergence - mutate the engine and confirm the entry does not absorb it),
      commit.

---

## Closing notes, 2026-09-12

**What landed.** All five items, no engine change: `src/` is byte-identical to where the slice found
it, which is the shape the scope demanded - two verdicts, three classifier entries, two permanent
tests, the three-seed runner, and two more drafted upstream reports.

**Item 1, forward `(*SKIP)` under an overlapped scan: PORT RIGHT, and upstream is wrong on three
counts rather than one.** Its own `match` and `search` at every start position agree with this port
(`tools/probes/upstream-overlapped-skip-scan.py`). Its scanner returns a match *narrower than the
pattern's minimum width* - `regex.compile(r'(?:[^\d](*SKIP)){2}').finditer('abcde',
overlapped=True)` yields `(2, 3)`, one character for a pattern needing two, where its own
`match('abcde', 2)` is `(2, 4)`. And its answer is not a function of its inputs: a `gc.collect()`
between iterations of the `{2,3}` form changes six matches into four, and an `open()` into two
(`tools/probes/upstream-overlapped-skip-instability.py`, reproduced in `BacktrackingVerbTests`). The mechanism is read, not
guessed: `RE_OP_SKIP` (`:14553`) moves `slice_start` and nothing restores it - `init_match`
(`:3404`), `do_match` (`:18121`) and `scanner_search_or_match` (`:20874`) all leave the slice alone,
`state_init` (`:18438`) writes it once per scanner - so an overlapped scan resuming at
`match_pos + 1` (`:20903`) can start *below* `slice_start`, which no other forward path produces.

**Item 2, the reversed group call: PORT RIGHT, and the bug is issue 614, ALREADY FIXED UPSTREAM.**
Fetching upstream's history (rather than reading the pinned submodule) showed five releases past our
pin. `build_GROUP()` not propagating the match direction was fixed on 2026-08-30 by commit
`9398a6d`, one line, `subargs.forward = forward;`, released in 2026.8.30. Upstream's own inline copy
of the called body already gives this port's answer: `(?r)(?<g>[ab]+)(?=([ab]+))b` records `(2, 5)`
where `(?r)(?<g>[ab]+)(?=(?&g))b` records `(2, 1)`, an end before its start that upstream renders as
the empty string - which `[ab]+` cannot match. A fixed-count body through the same call is right,
which places the fault in the variable repeat.

**Item 1 has an upstream fix too, and it is not proven to cover this row.** Issue 613 (`b77694a`,
same release) clamps `GREEDY_REPEAT_ONE`'s backtrack limit down to the current position, because a
stale slice could raise the limit above it and make the equality-only stop unreachable. Same class,
same cause. Whether it fixes this exact row is UNTESTED: it needs the 2026.9.10 wheel, installing it
was not permitted in this session, and this project builds no C locally. The drafted report says so
and is marked HOLD.

**THIS PORT STILL CARRIES THE PRE-613 CODE** at `Matcher.cs`'s `GreedyRepeatOne` backtrack arm,
verbatim from the pin. No wave row reaches it - every overlapped `(*SKIP)` answer here matches
upstream's own per-position `match` - so it was not fixed in S34: porting an upstream fix test-first
is the sync slice's job, and inventing the test here would guess at the trigger. DECISIONS records
it so the sync ports it deliberately rather than diffing past it.

**Item 3, the bounded-lazy-repeat partial: classified on TWO judged rows, not on a predicate.**
`^([A-Z]??)__$` over `'__aA '` and `(?r)A(.??)` over `'_ﬃ'[0:2]`. Making the quantifier greedy
removes the divergence from both, which is what says the lazy repeat is the cause
(`tools/probes/upstream-partial-prefilter-free.py`). The second row needs its `prefilter-free` recording to diverge at all -
plain upstream answers `(0, 1)` exactly as this port does, and only with `locate_required_string`
switched off does upstream stretch the partial to `(0, 2)`. The entry keys on the row AND on this
port's judged answer to it, so an engine change that alters either un-classifies the row.

**Item 4, three seeds and `verbs`.** `run-oracle.ps1` takes `-Seeds` (comma-separated, because
`pwsh -File` hands every argument over as a string and a real `[int[]]` binds the second seed to the
next parameter), defaults to 7, 4242 and the run's date, runs each in turn, and keeps a red seed's
wave and report under its own name. Every generator is now on the default list.

**Item 5, the draft.** Two new reports appended - the overlapped `(*SKIP)` scan (HOLD, see above)
and a crash both engines share (below) - plus a section saying which two need no report at all,
with the upstream commits that fixed them.

**The discriminator, and why it is narrower than it first looked.** A wave row now carries
`anchoredScan`: upstream's own answer to the same scan, taken one match at a time from a fresh
`search`, groups and captures included. The blind review found two cases where it is not upstream's
own door and both are now excluded by the recorder rather than papered over - a non-overlapped step
needs `must_advance`, which no Python call carries (`regex.compile('a??').finditer('aa')` is five
matches and a `p+1` walk gives three), and a reversed step needs `endpos`, which truncates the
subject and changes every end-of-subject assertion. So it is recorded only for an **overlapped,
forward** `finditer` with `(*SKIP)`. `search-start-skip-slice` is therefore still undiscriminated -
every row it covers is reversed - and its own note now records the measurement that says so, with
the three-way ordering the attempt turned up.

**What this slice found and did not fix, for the next slice.** At `-Count 2000` the default list at
three seeds turns up one more row, twice, and it is **a crash both engines share**:

    regex.compile('(?r)^İﬁ', regex.I | regex.F)
    # IndexError: tuple index out of range, from String.get_firstset (_regex_core.py:4036)

An empty `String` node reaches the first-set walk. This port raises `IndexOutOfRangeException` from
the ported `String.GetFirstset` (`Nodes.cs:2099`) for the same reason. New, inherited, and not in
S34's scope - there is no upstream answer to port - so it is drafted as upstream report 6 and handed
to Phase 6's issue sweep. **Read the green below as green at the count and seeds it ran.**

**One thing the slice could not do.** Item 4 asks for the three-seed rule in the `port-slice` skill
as well as in VERIFICATION.md. `docs/VERIFICATION.md` has it, as rule 7a with its evidence row;
editing `.claude/skills/port-slice/SKILL.md` was refused by the harness's permission gate in this
session, so **that edit is outstanding and needs the owner or a session with write access to
`.claude/`**. The rule itself is not lost - VERIFICATION.md is what the skill points at.

### Verification, quoted

    pwsh -File tools/run-oracle.ps1
    ===== seed 7 =====        agree 5999  unsupported 0  expected  1  diverge 0  of 6000 rows
    ===== seed 4242 =====     agree 5999  unsupported 0  expected  1  diverge 0  of 6000 rows
    ===== seed 20260912 ===== agree 5997  unsupported 0  expected  3  diverge 0  of 6000 rows
    Oracle: GREEN - no row diverged from upstream, at all 3 seeds.

    pwsh -File tools/run-oracle.ps1 -Generator verbs,recursion,partial,partial-sliced -Count 2000 \
        -Seeds 7,31,4242,314159,20260912,20260913
    seed 7        agree 7990  expected 10  diverge 0  of 8000 rows
    seed 31       agree 7989  expected 11  diverge 0  of 8000 rows
    seed 4242     agree 7989  expected 11  diverge 0  of 8000 rows
    seed 314159   agree 7985  expected 15  diverge 0  of 8000 rows
    seed 20260912 agree 7987  expected 13  diverge 0  of 8000 rows
    seed 20260913 agree 7985  expected 15  diverge 0  of 8000 rows
    Oracle: GREEN - no row diverged from upstream, at all 6 seeds.

    pwsh -File tools/run-oracle.ps1 -Count 2000
    seed 7        agree 39989  expected 10  diverge 1  of 40000 rows   <- the shared crash, above
    seed 4242     agree 39989  expected 11  diverge 0  of 40000 rows
    seed 20260912 agree 39986  expected 13  diverge 1  of 40000 rows   <- the same shared crash

    pwsh -File tools/check-ratchet.ps1
    Tests: 5763  passing: 5578 (5470 distinct ids)  baseline: 5470
    Ratchet: GREEN

### Controls

`python tools/run-controls.py --slices S29,S31,S33`, run against the code being committed, with
`.scratch/control-waves/` deleted first because the recorder changed. `diverge` is what the control
fires with; `expected` is what the accounted-for list absorbed.

| Control | seeds | agree | expected | diverge | of |
|---|---|---:|---:|---:|---:|
| S29-A `SKIP widens the slice instead of moving it to text_pos` | 7 / 20260913 / 4242 / 314159 | 600 / 599 / 599 / 599 | 0 / 1 / 0 / 1 | 0 / 0 / 1 / 0 | 600 |
| S29-B `SKIP moves the wrong end under (?r)` | 7 / 20260913 / 4242 / 314159 | 552 / 560 / 564 / 556 | 12 / 17 / 12 / 18 | 36 / 23 / 24 / 26 | 600 |
| S29-C `a verb prunes to the bottom of the bstack, not the top of the pstack` | 7 / 20260913 / 4242 / 314159 | 375 / 359 / 379 / 365 | 2 / 2 / 0 / 1 | 223 / 239 / 220 / 234 | 600 |
| S29-D `the scanner carries findall's slice_start guard` | 7 / 20260913 / 4242 / 314159 | 598 / 596 / 600 / 599 | 0 / 4 / 0 / 0 | 2 / 0 / 0 / 1 | 600 |
| S31-A `the do_match fallback is skipped` | 31 / 4242 | 584 / 570 | 0 / 0 | 16 / 30 | 600 |
| S31-B `text_pos is not restored between the two do_match runs` | 31 / 4242 | 575 / 576 | 0 / 0 | 25 / 24 | 600 |
| S31-C `every match is reported as a partial match` | 31 / 4242 | 523 / 504 | 0 / 0 | 77 / 96 | 600 |
| S31-D `try_match no longer reports a partial` | 31 / 4242 / 7 | 2396 / 2395 / 2395 | 1 / 2 / 1 | 3 / 3 / 4 | 2400 |
| S31-E `a lazy repeat that cannot extend no longer answers partial` | 7 / 31 / 4242 | 2398 / 2399 / 2398 | 1 / 1 / 2 | 1 / 0 / 0 | 2400 |
| S33-A `the reversed string test bounds itself by text_start` | 31 / 4242 | 7976 / 7963 | 23 / 36 | 1 / 1 | 8000 |
| S33-B `try_match never consults a string test node at all` | 31 / 4242 / 7 | 1990 / 1987 / 1990 | 8 / 5 / 7 | 2 / 8 / 3 | 2000 |

**S34 made no engine change, so it has no control of its own**, and that is the honest reason rather
than an omission: a control mutates the code under test, and this slice's code is a recorder, a
classifier and a PowerShell loop.

**Two findings in that table.** `S29-B`'s `diverge + expected` totals are 48 / 40 / 36 / 44, exactly
the figures S29 recorded before an accounted-for list existed - so no row was lost, but a quarter of
them are now absorbed rather than reported. And **`S29-D` is absorbed WHOLE at seed 20260913**:
honest `verbs` at 600 rows is `3 expected, 0 diverge` and the mutated run is `4 expected, 0 diverge`,
the mutation's own row landing in `search-start-skip-slice`. It still fires at seeds 7 and 314159.
That pair of numbers is written into the entry as the test of whether anyone has narrowed it.

**S33-A and S33-B reproduce their recorded figures exactly**, and S31-A through S31-E match S31's.

### Review

**Two blind passes, both dispatched inside this session's turn and both waited for.**

The first covered the whole diff. **Four findings raised, four reproduced, four fixed** - a rate
well above this repo's usual one in five, because the change is a discriminator whose fidelity is
exactly what a reviewer can test. (1) The `overlapped-skip-stale-slice` entry classified a genuine
port defect, demonstrated by mutating `MatchState.AdvancePastMatch` to adopt the walk's own stepping
rule. (2) The root cause: the walk's non-overlapped step is `end + 1` where upstream re-attempts at
the same position with `must_advance`. (3) The reversed step truncates the subject through `endpos`,
so the walk records matches upstream never produces. (4) A comment named an oracle entry id that had
been renamed. The fix for 1-3 is one change: the recorder now records a walk only for an overlapped,
forward row, and the two exclusions are documented with their measurements.

The second pass covered the delta the first reviewer never saw - the restriction, the rewritten
docstring, the entry whose condition was added and then removed, and the re-recorded examples.
**Six findings raised, six reproduced, six fixed.** The one that mattered: `$runs = if (...) {
@(-1) }` unwraps to an `Int32`, so `$runs.Count` threw under `Set-StrictMode` and **every
single-seed, `-Rows` and `-SkipRecord` run crashed after comparing the wave, exiting 1 with no
verdict line** - which is how the three-seed default runs stayed green while the documented
single-seed examples did not work at all. The `@()` now goes outside the `if`. The others: a
docstring paragraph naming `^` and `\A` as slice-sensitive when both are bound by `text_start`
(corrected against `try_match_START_OF_STRING`, `:7373`); a stale `<summary>` on
`ReadAnchoredScan`; a line citation pointing at a blank line; a CSharpier violation that would have
failed CI; and a pre-existing `Example` row that had drifted from what the recorder produces, which
is precisely the rot the new class remarks tell the sync to prevent, so it was re-recorded.
