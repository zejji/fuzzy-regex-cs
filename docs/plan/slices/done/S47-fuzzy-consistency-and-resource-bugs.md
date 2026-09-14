---
slice: S47
phase: 6
title: Inherited fuzzy consistency and resource bugs - ledger entries 11 and 14
delivers: []
---

# S47 - Change positions that contradict counts, and the recursion that exhausts memory

Two inherited bugs with no proposed fix yet. Both need a decision about the right answer before
code, so research is the first half of the slice.

## Scope

- **Entry 11: a fuzzy match reports change positions that contradict its own change counts.** S38
  pinned two rows where the port reproduces upstream; S40a measured a one-line fix and reverted it
  because those rows pin the contradiction. Decide the invariant from the documentation
  (`fuzzy_changes` "gives a tuple of the positions of the substitutions, insertions and deletions")
  and from the definition that counts and positions describe one edit script: the length of each
  positions list equals the corresponding count. Find the mechanism (S38: `match_fuzzy_changes`
  walking counts rather than the list, and the nested-section interaction), fix so the invariant
  holds, and pin the invariant as a property over the whole `fuzzy` wave rather than as individual
  answers. Re-judge the two S38 rows against it.
- **Entry 14: a self-recursive call round a fuzzy section that can match empty exhausts memory.**
  Reproduce under `timeout` and a memory cap and characterise: unbounded depth, unbounded
  branching, or both (S43 measured that a progress guard bounds depth, not branching). Research how
  PCRE2 bounds this (`match_limit`, `depth_limit`, `PCRE2_ERROR_RECURSIONLIMIT`) and what
  upstream's repeat guards do in the non-fuzzy case. Decide: a correctness fix if the empty-matching
  call is re-entered where the guards should stop it, or a resource bound surfaced as a clear
  exception if the pattern is genuinely unbounded. The port must not exhaust memory either way; pin
  with a test that runs under `MatchTimeout` and asserts the outcome. Then lift the `interactions`
  exclusion for a self-recursive call round a fuzzy section and re-run the wave.
- Ledger entries 11 and 14 gain the decision and the fix; nothing filed.

## Verification

- Invariant property test over the `fuzzy` wave at three seeds; the recursion test bounded in time
  and memory; both exclusions lifted; default wave GREEN at three seeds.

## Done when

- [x] Entry 11's invariant decided from the definition, fixed, pinned as a property. **Decided and
      pinned; fixed on two of its four mechanisms** - see sitting 1's note. **The oracle now accounts
      for both of the fixed ones and the default wave is GREEN at three seeds** - sitting 2.
- [x] Entry 14 characterised, bounded or fixed, pinned; exclusion lifted. **FIXED in sitting 3** -
      PCRE2's positional guard, failing the path rather than the match; the bound stays as the
      backstop with a test of its own; the exclusion is lifted and paid for itself on its first run.
- [x] Ratchet GREEN, blind review (hunt: the invariant met by trimming the list rather than
      recording correctly; a bound that turns a finite pattern into an exception), commit.
      **All three sittings did all three. Sitting 3's pass found a real defect in the guard and it
      is fixed; a SECOND pass over that fix is owed and was not run - see the closing notes.**

## Checkpoint, sitting 1 (2026-09-14)

**The invariant is decided and pinned, and half of entry 11 is fixed. Entry 14 is untouched.**

*Decided.* `fuzzy_counts` and `fuzzy_changes` are two views of one edit script, so each list holds
exactly as many positions as its own count. Pinned two ways: as
`Gaps.Engine.FuzzyMatchingTests.The_reported_changes_agree_with_the_counts_on_every_shape_that_used_to_contradict_them`
over the seven minimised rows, and as
`OracleTests.OracleWaveTests.Our_own_change_positions_always_agree_with_our_own_counts`, a property
of this port's answers alone over every fuzzy row of a whole wave.

*Fixed.* Mechanism A - `start_match` clears the change list beside the counts, so an attempt that a
verb or an atomic group abandoned cannot displace the winning attempt's changes. Mechanism B -
`Match.FuzzyCounts` is tallied from the change list on a PARTIAL match, where the state's counter
provably holds the innermost open section's errors alone. Suite green at 5,948, ratchet GREEN.

*Not fixed, and why.* The entry turned out to be one defect class with four mechanisms rather than
one bug with two doors: the counts are saved and restored as a block and the changes are unwound one
item at a time, and nothing keeps them in step. Mechanisms C (POSIX/BESTMATCH candidates) and D (a
lookaround under `(?e)`) are in the ledger with their reproductions; both engines agree on both, so
only the new wave property can see them - it found D at seed 4242 on its first run. Fixing them
means pairing the change list with the counts at all nineteen
`PushFuzzyCounts`/`PopFuzzyCounts` sites, each needing a "restore or merge" judgement. That is a
slice of its own.

*The open problem for sitting 2, before entry 14.* Fixing A and B makes the default wave diverge on
39 rows (B) plus 19 rows (A) per 18,000, because upstream still leaks. There is no narrow predicate
for the 19: upstream's leaked positions look exactly like a port that got a position wrong. The
design that works is a second recorded question - ask upstream the same row again ANCHORED at the
span it reported, where no earlier attempt exists to leak from, and account for a divergence when
this port's answer equals upstream's own leak-free answer. That is a recorder change, an
`OracleWave` field and one `ExpectedDivergences` entry. **Do that first**, then entry 14.

*Review.* Two blind passes. **Pass 1** over the whole diff raised six findings; **all six reproduced
and all six were fixed**, which is far above this repo's usual one-in-five and is the shape you get
when a slice renames tests and rewrites the comments that named them. The substantive one was the
strictness alarm: `Every_expected_divergence_still_diverges` went red because the `start_match`
clear moved this port's answer on `bestmatch-loses-a-partial`'s own example row 4 from a
substitution at 0 - a leftover of an attempt a `(*SKIP)` abandoned, outside the (5, 1) span - to one
at 5, and the recorded judged string had not been updated. The alarm did exactly what it exists for.
The other five were a comment in `Matcher.cs` asserting the opposite of the line beneath it and
citing two names this slice had removed; the same in `FuzzyTestConstraintTests`, whose "strengthen
these when Phase 6 fixes the inherited bug" note was now spent (so its first two rows now assert
positions, measured against upstream); the new wave property silently dropping every scan row, which
is about a fifth of a default wave (widened to check every match of a `MatchesOutcome`, and the POSIX
limit written down honestly); two ledger reproductions quoted without the flag bits they need; and
an overclaim that upstream's change list "is `[ins@1, sub@3]`", which cannot be read past
`sum(counts)` entries from Python.

**Pass 2** over the delta pass 1 never saw - the scan widening, the changed judged string, the two
new position assertions - raised two, both reproduced and both fixed: the property test was putting
`timeout` and `resource` rows to the engine that `RunWave` deliberately never asks, which cost 28.7
of its 29.0 seconds on a 12,000-row wave and ran a row upstream exhausted its heap on with nothing
here to bound it; and `:12377` was the wrong upstream line for `FUZZY`'s sstack push, which is
`:13137` with the `memset` at `:13143` (`:12377` is inside `CONDITIONAL`'s backtrack arm). Pass 2
also verified the changed judged string and both new positions against the live engine and against
regex 2026.9.10.

*Controls.* None run: this sitting changed no oracle generator, and the property test IS the
instrument - it went red on mechanism D at seed 4242 on its first run, on a row both engines answer
identically, which is a live demonstration that it can fail rather than a mutation of the engine.

## Checkpoint, sitting 2 (2026-09-14)

**The oracle now accounts for the two mechanisms sitting 1 fixed, and entry 14 is characterised with
its fix named by a second engine but not made. A third sitting closes the slice.**

*The oracle, which sitting 1 left as the open problem.* Fixing mechanisms A and B made this port stop
reproducing upstream's leak, so every row where upstream leaks became a divergence: measured on the
6000-row three-seed gate, **19 rows for A and 39 for B** (35 carrying positions, 4 POSIX). They are
accounted for separately because only one of them needed a new question put to upstream.

- **B needed none, because upstream's own answer is the evidence.** Its counts are componentwise no
  larger than this port's and its reported positions are a PREFIX of this port's, per kind and in
  record order - which is what a change stack truncated to a wrong total looks like and is not what
  an engine computing different positions looks like. The prefix relation holds on all 35 rows that
  carry positions, with none failing. Entry `fuzzy-counts-of-a-partial-are-the-innermost-sections`.
- **A did.** The recorder asks upstream the same row again ANCHORED at the span it reported,
  `match(pos=start, endpos=end)`, so the winning attempt is upstream's FIRST attempt and no earlier
  one exists to leak from, and records that fuzzy half per match as `leakFreeFuzzy`
  (`_leak_free_fuzzy`). On all 15 diverging matches upstream will answer, its leak-free answer is
  this port's answer exactly. Entry `fuzzy-changes-leaked-from-an-abandoned-attempt`.

*The part of sitting 1's design that did not survive contact, which is the finding here.* The
handover said the anchored re-ask was the design for the whole problem. It is not: it is the design
for A alone - the re-ask of a PARTIAL is the identical question and returns the identical wrong
counts - and it cannot always be asked even for A. Anchoring destroys the question on three shapes:
a fuzzy section inside a LOOKAHEAD, which must read past `endpos`; a `\K`, whose reported start is
not where the attempt began; and a scan's second match at a position an earlier match already used.
**Eight of the 23 diverging matches, in four rows, are one of those**, and the entry's second arm
accepts this port's positions there with nothing to hold them to. That arm is the widest thing in
`ExpectedDivergences.cs` and its own `Reason` says so. Four asking strategies were scored before one
was chosen (`.scratch/anchored2.py`): `match(pos,endpos)` answers 20 of 38 matches,
`search(pos)`/reversed `search(endpos)` 25, and the union of all four leaves 13 unanswered, so trying
several buys ONE row of 19 - hence one strategy, the simplest.

*Result.* Default wave **GREEN at three seeds**. The 6000-row three-seed gate went from 24 / 23 / 25
diverging rows to **3 / 2 / 9**, and the 14 that remain are exactly the untriaged rows that were
already red at HEAD; not one is a fuzzy-reporting divergence. Suite 5,948, ratchet GREEN.

*Entry 14, and why it is not finished.* The characterisation the slice asked for was already settled
by S43 and the ledger - BOTH unbounded depth and unbounded branching, a progress guard bounding only
the first - and the port already bounds it: `InvalidOperationException: the regular expression
engine's backtracking stack exceeded its 1GB limit`, re-measured at 0.25s to 1.52s across all ten
pinned shapes. What sitting 2 added is the second engine amendment 16 asks for. PCRE2 cannot be shown
the pattern (it has no fuzzy matching and reads `{e<=2}` as literal text), but it can be shown the
same mechanism with the fuzzy section replaced by an optional atom, and it answers with a **named
match-time guard**: `nested recursion at the same subject position`, `PCRE2_ERROR_RECURSELOOP`, in
microseconds, while still answering (0, 4) to the recursion that does progress
(`tools/probes/pcre2-bounds-an-unbounded-recursion.py`, pcre2 0.7.1 over libpcre2 10.47). So the
answer to "correctness fix or resource bound" is **both, in that order**: the bound is right and is
already here, and there is a correctness fix, and it is PCRE2's positional guard. Porting it is not a
transcription - PCRE2's guard is positional rather than a progress proof, so a shape re-entering one
position where a different branch would still have succeeded would change answer - and it has to be
measured against a wave. **That, and then lifting the `interactions` exclusion, is sitting 3.**

*What sitting 2 did finish on entry 14.* The three blowup assertions in `FuzzyRecursionTests` now
name the exception TYPE and its message instead of accepting any exception at all. A bare
`Throw<Exception>` is also satisfied by the `RegexMatchTimeoutException` from the tests' own
30-second budget, so the file could not tell its bound from its clock and a regression that merely
made the engine slow would have passed. The blind review mutated all three messages to a sentinel and
got exactly three failures, so none of them is vacuous. The stale claim in `record-oracle.py` that
the shape is excluded because a `MemoryError` aborts the recorder was also corrected: S43 made it a
recorded `resource` outcome, and what keeps the shape out now is the branching plus the cost of a row
this port spends a second and a gigabyte on.

*Review.* **One blind pass over the whole diff, and it raised NO findings** - the first in this
slice's two sittings. It is recorded as a pass rather than waved through because of what it ran: 17
hand-built wave rows corrupting this port's answer in every way the two predicates could plausibly
swallow (leak-free deletion moved, leak-free counts raised, leak-free substitution relocated,
`leakFreeFuzzy` absent, upstream span widened, a group missing, a group shifted, `lastIndex` changed,
upstream insertions off the prefix, upstream counts larger than ours, upstream not partial) - **every
one REPORTED, not classified**, with only the two unmodified controls and the two documented-wide
arms classifying. It also re-asked `match(start, end)` independently for every fuzzy match of a
126,000-row wave and found 2,506 checked with 0 mismatches against the recorded field, confirmed the
POSIX guard over 5,722 POSIX rows with 0 leaks, measured the recorder's cost at 1.60s against 1.55s
without the change, diffed a full 6,301-row wave old against new (0 rows differing apart from the new
key, 114 gaining it), and re-recorded all four committed `Example` rows to identical output. **No
second pass was needed: nothing changed after the review.**

*Controls.* None run, and the same reason as sitting 1 applies with one addition: no oracle generator
changed, and the instrument for the two new entries is the reviewer's 17-row corruption batch, which
is a negative control by another name - it demonstrates the predicates failing on wrong answers
rather than asserting that they would.

## Closing notes, sitting 3 (2026-09-14)

**Entry 14 is fixed, the exclusion is lifted, and the slice closes.** PCRE2's positional recursion
guard is ported: a call that would re-enter call-ref index `i` at text position `p` while a call of
`i` at `p` is already open fails that path. `MatchState.ActiveCalls` is the membership set,
`MatchState.OpenCalls` the same calls as a stack with the sstack depth each frame ends at, and
`Matcher.CloseCallsAbove` is called after each of the six sites that restore a saved
`state.Sstack.Count`.

*The one deliberate difference from PCRE2, which is the whole design judgement.* PCRE2 fails the
MATCH (`PCRE2_ERROR_RECURSELOOP`); this fails the PATH. Refusing one infinite path cannot cost an
answer another path reaches, and it demonstrably keeps answers PCRE2 throws away:
`(?P<g1>(?:ab)?(?&g1)?)` over `'abab'` is an error there and (0, 4) here, and `(?P<g>(?&g)a|b)` over
`'ba'` gets left recursion's one-step unrolling, (0, 2), rather than nothing. Every shape the ledger
table predicted a blowup for now answers what its non-vanishing sibling answers - (0, 4) at two atoms,
(0, 6) at three - and the degenerate `(?R)` rows answer `no match`, which is right: a pattern whose
only content is a call to itself has an empty language.

*The 1GB bound stays and gained its own test.* The guard bounds DEPTH, not BRANCHING, so
`ByteStack.Grow` is still the backstop - reachable by `(?P<g1>\p{L}*)+?(?:ab){e<=1}` over `'bb.a\r.'`,
which holds no group call at all. Without that test nothing in the suite would reach that path any
more, which is a coverage loss the removal of the three blowup assertions would otherwise have caused.

*The exclusion.* `INTERACTION_FUZZY_WRAPPERS` gained `call`, so the generator draws a self-recursive
call round a fuzzy section again: 14 rows of 600 at seed 1, three of them `resource`. It paid for
itself at once - row 72179 of `interactions` at seed 20260914 is a drawn self-recursive call this port
used to exhaust its stack on, and upstream's `no match` there is its REQUIRED-STRING PREFILTER
refusing the subject rather than an answer from its engine (put the required character in and upstream
raises MemoryError). Pinned as
`FuzzyRecursionTests.A_drawn_wave_row_upstream_only_escapes_through_its_prefilter`. All the generator's
measured figures were re-taken, including both ablations, as its docstring requires.

*Proof the guard costs nothing upstream can answer.* The three saved 6000-row gate waves (126,000 rows
a seed) were consumed again by the PRE-GUARD engine, from a stash, and the result is identical
row-for-row at seeds 7 and 4242 and one row WORSE without the guard at seed 20260914. The 19 rows the
gate still reddens on are therefore HEAD's own - the 14 STATE already named plus 5 more the RNG shift
exposed, none holding a group call. The gate now takes about 6.5 minutes rather than about one,
because upstream spends ~1s on each `resource` row.

*Review.* **One blind pass over the whole diff, and it raised two findings, both reproduced and both
fixed - the first a real defect in the guard.** `(*PRUNE)` and `(*SKIP)` truncate the backtracking
stack and leave the saved stack alone, so an open call's `GROUP_CALL` entry is discarded while the
call is still open; an enclosing atomic group, lookaround or conditional then restores
`Sstack.Count` and throws the orphan frame away, so NEITHER backtrack arm ever runs and the key stayed
in the set for the rest of the attempt, refusing the next legitimate call of that group at that
position. Reproduced with
`regex.search(r'(?>(?&g))?(?=(?P<cap>a))(?&g)(?(DEFINE)(?P<g>a(*PRUNE)(?P=cap)))', 'aa')` - upstream
(0, 2) in 0.00s, this port `no match` - and it is not a shape upstream blows up on, so it was a real
answer lost. That is what `OpenCalls`, the recorded sstack depth and `CloseCallsAbove` exist for; the
first draft's claim that the `start_match` clear covered the verb cuts was the second finding, and it
was simply false. Pinned by
`GroupCallTests.A_verb_that_cuts_the_backtracking_inside_a_call_does_not_leave_the_call_open`, whose
first row is the no-wrapper control. The reviewer also verified the two stacks' push and pop orders
mirror, that the key cannot collide for a reachable position, and that no new test is unfalsifiable.

**A SECOND BLIND PASS OVER THE FIX IS OWED AND WAS NOT RUN** - the orchestrator's shutdown landed
about fifteen minutes after the fix went green. `OpenCalls`, `PopOpenCall`, `CloseCallsAbove`, the six
call sites, the reverted sstack slot and the new test are unreviewed code. Control C below is the only
instrument that has been pointed at them. This is the first thing the next session should do.

*Controls.* All three re-run against the code being committed, at two seeds each.

> **Control A, the guard made positionless**: in `Matcher.cs`, `GROUP_CALL` forward, change
> `long groupCallKey = ActiveCallKey(groupCallIndex, state.TextPos);` to
> `long groupCallKey = ActiveCallKey(groupCallIndex, 0);`.
> Wave: `pwsh -File tools/run-oracle.ps1 -Generator recursion,interactions,fuzzy -Seeds 7,31337 -Count 6000`.
> Result: 8 diverging of 18,000 at seed 7 and 8 at seed 31337, against a baseline of 4 and 3 - so
> +4 and +5, every one of them a row holding a self-recursive call. Suite: 8 failures of 5,953.

> **Control B, the guard removed**: in the same place, replace
> `if (!state.ActiveCalls.Add(groupCallKey))\n{\n    goto backtrack;\n}` with
> `state.ActiveCalls.Add(groupCallKey);`.
> Same wave: 4 and 3, IDENTICAL to the baseline - **the wave cannot see this mutation at these
> seeds**, and that is the honest reading. What sees it is the suite (5 failures of 5,953) and the
> 6000-row three-seed gate, where the pre-guard engine diverges on one row more at seed 20260914
> (row 72179) and on exactly the same rows at seeds 7 and 4242.

> **Control C, the depth bookkeeping made useless** - the control for the blind review's finding: in
> `Matcher.cs`, `GROUP_CALL` forward, change
> `state.OpenCalls.Add((groupCallKey, state.Sstack.Count));` to
> `state.OpenCalls.Add((groupCallKey, 0));`, so `CloseCallsAbove` never pops.
> Wave: `pwsh -File tools/run-oracle.ps1 -Generator recursion,interactions,fuzzy,verbs -Seeds 7,31337 -Count 6000`.
> Result: 4 and 4 of 24,000, identical to the baseline - **the wave cannot see this one either**.
> Suite: exactly one failure,
> `A_verb_that_cuts_the_backtracking_inside_a_call_does_not_leave_the_call_open`.

**Two of the three controls are invisible to the wave, and that is a finding about the generator
rather than a tick.** No generator draws a backtracking verb inside a CALLED group inside an atomic
group, a lookaround or a conditional, which is the shape the leak needs; `interactions` composes
verbs and calls but never nests them that way. S52 (oracle hardening) should widen `called-group` or
`verb-alt` to reach it. Until then the suite is the only instrument that can see a regression in the
guard's bookkeeping.

*Suite 5,953, ratchet GREEN, default wave GREEN at three seeds.*
