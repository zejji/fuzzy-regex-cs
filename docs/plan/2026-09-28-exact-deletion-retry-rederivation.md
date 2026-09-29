# The exact-deletion retry and recursion that returns: a re-derivation

Date: 2026-09-28. Status: design only, nothing in `src/` changed. Branch `maint/fuzzy-exact-deletion`
at b96b7be. Step 1c of finding F-A. Follows `2026-09-27-recursion-failure-memo-design.md` (C1, the
failed-call memo), whose "What remains exponential" section this note picks up.

Measurements come from the owner's laptop, one run per cell, first run in a fresh process (JIT
included), so read them as orders of magnitude. "Production" means the tree at b96b7be. The
prototypes were built in a scratch copy (`git archive HEAD src`), never in this worktree, so the
probe code below is described rather than committed; section 7 says how to rebuild it.

**Incomplete, marked where it applies:** the C2 prototype has not been through a differential grid
(section 4, "Correctness"), and the TotalErrors leak has no witness yet (section 5).

## Summary

- **The retry cannot be reformulated to stop the multiplication.** It already sits in the ordinary
  search order: it is the same backtracking entry `fuzzy_match_item` pushes, taken after everything
  that follows the exact match has failed. Any formulation that tries the same paths in the same
  order does the same work. Work goes down only if paths are proved redundant, or if equal states
  share their work. Both were measured.
- **Sharing work inside one call does not help.** One call's exits are almost all different from
  each other: 4.77 million distinct among 6.62 million, even with a coarse key. Pruning repeated
  exits takes shape 3 over `abab` from 11.5 s to 5.8 s at best.
- **Call summaries (C2) help a great deal, but shape 3 stays exponential.** A prototype takes shape
  3 over `abab` from 10.8 s to 0.59 s. It takes shape 1 over `ababa` from over 10 s to 216 ms, and
  shape 2 over `abab` from over 10 s to 135 ms. But on shape 3 the number of distinct call *entries*
  grows about 3 times per character (6,838, 20,032, 172,139, 526,026 at lengths 4 to 7), so C2 still
  takes 18.5 s at length 7.
- **Shape 3 is exponential with the retry off too.** Production takes 17.9 s over `ababab` with the
  retry off. The retry moves the point where it gets slow from about length 6 to length 4. It does
  not create the blow-up.
- **The C1 exclusions switch every call-level memo off here, C2 included.** Each residual shape has a
  call inside a lookaround. What forces the exclusion is a leak: captures made through a call inside
  a lookaround survive a later failure. On shape 3 over `abab`, group 1's capture list holds 639,476
  entries with the retry on and 6,963 with it off. The same leak makes C2 store those entries in
  every exit it records, and it runs out of memory at length 6.
- **Recommendation: (c) now, the leak fix next, C2 only after that and only on measured need.**
  Keep the retry as it is. Keep `MatchTimeout` as the documented bound, and pin the three shapes as
  timeout witnesses. Then fix the capture-list leak as its own ledger entry: it is a bug in its own
  right, and it is also what blocks every call-level memo. After it, lift the exclusions that
  existed only for the leak, and decide on C2 with the numbers in section 4.
- **`(?:a?){d<=1}` over `''` answers (0, 0) with no errors on F-A.** This comes from ledger 44's
  "needed" rule and is what that rule intends. Ledger 42 plays no part. It does leave `a?` and
  `(?:a|)` answering differently. See section 6.

## 1. What the retry exists to guarantee

Ledger 42 (`docs/plan/upstream-reports/LEDGER.md`, entry 42): a deletion is "a pattern item absent
from the text" (`upstream/README.rst:538-566`). So `(?:a){d<=1}` has the paths of `(?:a|)`: first
the `a`, then nothing. Upstream tries an item's errors only when the item fails to match
(`fuzzy_match_item`, `upstream/src/_regex.c:10185-10258`). An item that matches pushes no
backtracking entry, so once the rest of the pattern fails, deleting that item is never tried.
The retry (`Matcher.PushExactItemDeletion` and its three twins, `Matcher.cs:5156-5315`) pushes that
entry at every exact match that fits the budget. The guarantee is therefore:

> The search is complete and in order. Deleting an exactly matched item is tried after everything
> that follows its exact match has failed, and before any earlier choice.

Here is what that changes:

1. **None becomes a match.** `match('(?:a){d<=1}a', 'a')`: upstream None, port (0, 1) with one
   deletion.
2. **A search finds an earlier start.** `search('(?:ab){d<=1}b', 'abxabb')`: (3, 6) becomes (0, 2).
3. **An earlier branch wins.** `(?:(?:a){d<=1}ab|a)` over `ab`: (0, 1) becomes (0, 2).
4. **A verb is reached through a deletion.** `(?:(?:a){d<=1}ab(*SKIP)(*FAIL)|b)` over `ab`: (1, 2)
   becomes None.
5. **A match becomes None through a negative lookaround or a conditional.** The lookaround's body
   now finds the matches it used to miss, so the lookaround fails. Residual shape 1 is this case:
   with the retry on it answers None over `ab` to `ababa`, and with the retry off (0, 1) with one
   substitution (production, Release, measured 2026-09-28). Upstream cannot grade this: it raises
   MemoryError on all three shapes at every length from 2 to 6 (regex 2026.9.10, about 0.55 s each).
6. **The counts change on the same span.** `match('(?:ab){e<=2}b', 'bb')`: (0, 0, 1) becomes
   (1, 0, 1).

On shape 3 the retry changed no span, count or edit at lengths 2 to 7. It did change the capture
lists, through the leak.

## 2. Why a reformulation cannot remove the multiplication

The residual work is the case "a call returns, and then its caller fails". Picture the matcher at
nesting depth d. Each call returns k times, once per exit. For each exit, the caller runs the rest
of its own body, which makes more calls, and those return k times each. So the work is about k to
the power d. The retry raises k, because every exact match inside a called group now has a second
exit. Measured on shape 3 over `abab`:

| | Calls | Exits | Continuations that failed |
|---|---|---|---|
| retry off | 22,137 | 31,440 | 24,478 |
| retry on | 2,338,879 | 6,623,506 | 5,984,031 |

The candidates in option (a) either keep the same set of paths or drop some:

- **Folding the retry into the normal order.** It is already there. `PushExactItemDeletion` pushes
  the frame `fuzzy_match_item` pushes (`Matcher.cs:5184-5190`), and it is taken in ordinary
  backtracking order. A compile-time rewrite of `X` to `(?:X|<delete>)` explores the same paths.
  The only other ordering, "all deletions after every other path at this start", was rejected in
  ledger 42 (branch `spike/fuzzy-deletion-retry`): it answers `(?:(?:a){d<=1}ab|a)` over `ab` with
  (0, 1) and ignores a `(*SKIP)` reached through a deletion. So reordering is not available. The
  order is the guarantee.
- **A tighter bound on the retry.** The narrowing (`ExactDeletionMayMatch`, `Matcher.cs:4967-5013`)
  and the twin prune (`DeletionRepeatsAnEarlierAlternative`, `Matcher.cs:5136-5147`) already drop
  every choice that provably holds no new match. Shape 3's retried items are `b` and `.` in
  `(?:b.)+` under `{1<=e<=2}`. With a fresh budget of 2 at each level, the run argument cannot rule
  out deleting both, so the choice stays. Any further bound would need the state after the deletion
  to have been seen before. That is a memo, which is the next point.
- **A memoised form inside one call: skip an exit already seen.** If two exits of the same call
  leave equal states, the caller's continuation behaves the same from both. So once the first has
  failed, the second can be skipped. Prototype: at `GROUP_RETURN`, hash the exit, and remember
  hashes whose continuation failed (seen in the `GROUP_RETURN` backtrack arm). Shape 3, `abab`,
  retry on:

  | Exit key | Distinct exits, summed over calls | Time without, then with the skip |
  |---|---|---|
  | position, counts, section, totals, frame, last edit, all group spans, edit count | 5,917,124 of 6,623,506 | 11,488 ms to 9,496 ms |
  | the same without the edit count | 5,457,040 | 9,531 ms to 6,047 ms |
  | position, counts, section, totals, frame | 4,768,936 | 9,722 ms to 5,779 ms |

  The exits of one call are nearly all different, so this form does not help. (The larger keys
  hold more than the continuation needs. They are shown so the ceiling is visible: even the
  smallest key keeps 72% of the exits.)

**Conclusion for (a):** the repeats are across calls, not inside one. A call with the same entry
is made again and again from different callers, and each time it produces its exits all over
again. On `abab` there are 2.34 million calls but only 10,992 distinct entries (C1's
`FailedCallKey`). That is C2's target.

## 3. The exclusions, and the leak behind them

`PatternObject.UseCallMemo` (`PatternObject.cs:780-786`) is off whenever
`WritesInDiscardingConstruct` holds: a capture group, call or fuzzy section inside an atomic
group, possessive repeat, lookaround or conditional test. All three residual shapes have a call
inside a lookaround (`(?!(?R)(?1))` in shapes 1 and 2, `(?=(?1))` in shape 3). So in production
C1 never runs on them, and the "2.9 s with C1" figure in the brief came from the prototype, which
had no exclusions. Re-measured with C1 forced on (`UseCallMemo` and `EagerCallMemo` set by
reflection; that is unsound here, and shown only as a ceiling):

| Shape, subject | Production (memo off) | C1 forced on | Retry off | Retry off, C1 forced |
|---|---|---|---|---|
| 1, `abab` | 1,564 ms, None | 3.8 ms | 36 ms, (0,1) 1s | 1.8 ms |
| 1, `ababa` | over 10 s | 10.0 ms | 317 ms | 5.8 ms |
| 2, `abab` | over 10 s | 1,020 ms | 4,824 ms | 31 ms |
| 2, `ababa` | over 10 s | over 10 s | over 10 s | 128 ms |
| 3, `abab` | 7,313 ms, (0,4) 1i 3d | 1,499 ms | 37 ms | 22 ms |
| 3, `ababa` | over 10 s | over 10 s | 82 ms | 78 ms |
| 3, `ababab` | over 20 s | over 20 s | 17,944 ms, (0,6) 1i 3d | 3,635 ms |
| 3, `abababa` | over 20 s | over 20 s | 13,796 ms, (0,7) 1s 1i 2d | 9,715 ms |

Release. Debug, same probe, 10 s cap: shape 1 `abab` 8,084 ms (4.5 to 16 ms with C1 forced),
shape 2 `aba` 6,412 ms and `abab` over 10 s, shape 3 `aba` 296 ms and `abab` over 10 s (retry off
459 ms). Debug is 3 to 10 times slower, as the earlier note found.

**Why the exclusion exists.** The 2026-09-27 grid found two ways a failed path leaves a trace:

1. **Capture lists.** When a lookaround body succeeds, the undo entries of captures made inside it
   through a group call are thrown away, so `captures()` keeps entries from paths that later
   failed. Upstream shows the same, and disagrees with itself: `(a)(?:(?!.(?1))|.)+?b` over `aaab`
   keeps `[0,1][2,1]`, while `(a)(?:(?!.(a))|.)+?b` keeps only `[0,1]` (earlier note, section 2b).
   On shape 3 the leak dominates the answer's capture lists:

   | Shape 3, retry on | `abab` | `ababa` |
   |---|---|---|
   | group 1 capture list, production | 639,476 entries | not measured (timeout) |
   | group 1 capture list, retry off | 6,963 | 10,214 |
   | group 1 capture list, C2 prototype | 783,950 | 1,567,129 |

   A match reporting 639,476 captures for one group, most from failed paths, is a bug, whatever the
   memo question. Per the owner's rule (no known bugs before 1.0) it needs its own ledger entry.
   That entry needs the research and blind review an upstream-bug claim requires.
2. **Error totals.** A fuzzy section that ends inside a discarding construct writes `TotalErrors` at
   `END_FUZZY`, and the only entry that restores it is one the construct throws away. The grid's
   witness only goes wrong *with* a memo. **Incomplete:** nobody has yet looked for a witness
   without a memo, in the style of ledger 32.

So the exclusion does not come from the key being wrong. It comes from failed paths not being
side-effect free. Every call-level memo, whether C1, C2 or anything else that skips work, changes
what those leaks leave behind. **No memo can help the residual shapes until the leaks are fixed.**

## 4. Option (b): C2, call summaries

**Design as prototyped.** C1 is the special case of C2 in which a call has no exits. Generalise its
record point:

- At `GROUP_CALL`, build the entry key (`FailedCallKey`, unchanged) and start a record: the change
  count, each group's capture count and the totals at entry.
- At each `GROUP_RETURN` of that call, append an exit to the record: position, section counts,
  totals if they changed, the edits added since entry, and the capture entries added since entry.
  Drop an exit equal to one already in the list: from any caller the continuation behaves the same
  from both, so if the first fails the second fails too, and if the first succeeds the second is
  never reached.
- In the `GROUP_CALL` backtrack arm, which runs only once every choice inside the call has been
  tried, store key to (ordered exits, residue). The residue is whatever the call leaves behind
  after it is exhausted. A cut (atomic or lookaround success, a verb) discards the record, because
  the arm never runs.
- A later call whose key is stored, and whose (group, position) is not open, does not run the
  group. It applies exit 0, pushes a replay frame and jumps to the return node. When the matcher
  backtracks into the replay frame, it undoes that exit and applies the next one. Once none are
  left, it applies the residue and goes on backtracking.

This is sound in the same way C1 is (earlier note, section 2a to d), plus one condition: *the
callee's exits, in order, depend only on the entry key.* That condition fails if a caller's failed
continuation can leave a trace that the callee reads later, or that an exit records. That is the
leak again: a failed continuation's leaked captures get recorded into the next exit's capture
entries.

**Measured** (C2 on, production C1 off, Release):

| Shape, subject | Retry on, no memo | C2 | C2 ignoring capture lists | Retry off, no memo |
|---|---|---|---|---|
| 1, `abab` | 1,673 ms, 1,494,856 calls | 6.2 ms, 667 calls | | 24.7 ms |
| 1, `ababa` | over 10 s | 216 ms | | 460 ms |
| 2, `abab` | over 10 s | 135 ms | | 7,997 ms |
| 2, `ababa` | over 10 s | 1,111 ms | | over 10 s |
| 3, `ab` | 97 ms | 12.3 ms | | 6.7 ms |
| 3, `aba` | 295 ms | 31.3 ms | | 6.5 ms |
| 3, `abab` | 10,830 ms | 587 ms, 46,813 stored exits | 615 ms | 96 ms |
| 3, `ababa` | over 10 s | 1,750 ms | 509 ms | 131 ms |
| 3, `ababab` | over 20 s | out of memory | 5,925 ms, 2.4 M stored exits | over 20 s |
| 3, `abababa` | over 20 s | | 18,514 ms, 7.7 M stored exits | over 20 s |

On every row C2 kept the same span, counts and edit positions. Only the capture lists differed,
through the leak.

**Why shape 3 stays exponential under C2.** The number of distinct entries grows with the length:
6,838, 20,032, 172,139 and 526,026 recorded calls at lengths 4 to 7. The entry key has to hold the
open calls ahead of the position (the re-entry guard reads them), and each level has a fresh
section budget. Deep recursion under `(?r)` makes those combinations grow exponentially. Dropping
repeated exits within an entry with a coarse key (position, counts, edit counts by kind, last edit)
kept 34,662 of 46,471 stored exits, so the exits are distinct too. C2 turns the continuation work
into polynomial work on shapes 1 and 2 at these lengths, but not on shape 3.

**Correctness. Incomplete:** the prototype has not been through the memo grid. It was checked only
on the rows above. A driver (`c2grid.cs`) is written, adapted from `memo-grid.cs`, but was not run
before the deadline.

**Cost and risk.** The replay frame is a new backtracking opcode. The recorded writes must cover
every field a return leaves changed: position, section counts, `TotalErrors` and `TotalCost`, the
edit list, and the capture lists. The capture lists are restored by count, and the `Current`
indices by the saved stack (`Matcher.cs:2877-2902`, `9456-9492`). The memory cost is one exit per
distinct (entry, exit), which was 7.7 million on shape 3 at length 7 even without the capture
lists. It needs a cap like C1's 2^20.

## 5. Option (c): a documented `MatchTimeout` bound

This is already the policy after C1 (`docs/DIVERGENCES.md`, `FuzzyRegex.MatchTimeout`). A timed-out
match raises `RegexMatchTimeoutException`. It never returns a wrong answer. Upstream raises
MemoryError on all three shapes at every length measured, in about 0.55 s, so the port is no worse.
PCRE2 sets the same precedent with `match_limit` (earlier note, section 3).

## Recommendation

1. **Keep the retry exactly as it is.** No formulation that keeps the ordered, complete search does
   less work (section 2). The only other ordering on record changes answers.
2. **(c) now.** Pin the three shapes as timeout witnesses, and add a note to the `MatchTimeout`
   documentation. Shape 3 is exponential with the retry off too, so this bound belongs to recursion
   plus fuzzy matching, not to ledger 42.
3. **Next, fix the capture-list leak** through a call inside a lookaround, as its own ledger entry
   (upstream-bug research, isolating probe, blind review first). Then look for a no-memo witness of
   the `TotalErrors` leak, and fix it if one is found. Only then lift the parts of
   `WritesInDiscardingConstruct` that existed for these leaks, and rerun the memo grid on the
   "unsafe" variant until it shows zero changes. Expected gain, from the C1-forced column in
   section 3: shape 1 from over 10 s to about 10 ms over `ababa`. Shapes 2 and 3 stay slow under C1.
4. **Then decide on C2 from the numbers in section 4.** Its measured gains are on shapes 1 and 2,
   and a factor of 18 on shape 3 at length 4. It does not make shape 3 polynomial. Recommend
   building it only if recursion plus fuzzy matching turns up in real use, or if the owner's
   "optimise beyond upstream" rule is judged to cover a large, high-risk change for generated
   shapes.

## Build plan

**Step 1, (c), a small change.**
- Witness tests (`Gaps/Engine/FailedCallMemoTests.cs` or a new `RecursionBoundTests.cs`): each shape
  with a 500 ms timeout must raise `RegexMatchTimeoutException`, never answer wrongly. Shape 1 over
  `ababa`, shape 2 over `abab`, shape 3 over `ababab`. They must be red if a change makes them fast
  with a *different* answer. So assert either a timeout or the retry-on answers recorded here: shape
  1 None; shape 3 over `abab` (0, 4) with 0s 1i 3d and deletions at 4, 1, 2 and an insertion at 3.
- Docs: in `docs/DIVERGENCES.md` and the `MatchTimeout` documentation, one sentence saying the
  bound covers recursion inside fuzzy sections whether the retry is on or off.

**Step 2, the capture-list leak (its own slice and ledger entry).**
- Failing tests first: `(a)(?:(?=.(?1))x|.)+?b` and `(a)(?:(?!.(?1))|.)+?b` over `aaab` must keep
  the same capture list as the direct-capture forms. Shape 3 over `abab` must give a group 1 capture
  list of bounded size (retry off gives 6,963; with the fix, measure it and pin it).
- Assumptions to list, cite and `Debug.Assert` (VERIFICATION rule 10):
  - A1: after a lookaround's body succeeds, every capture count is back to its value at the
    lookaround's entry plus the captures the lookaround keeps on purpose. Assert this at
    `END_LOOKAROUND` against a count saved at `LOOKAROUND`.
  - A2: `GROUP_RETURN` restores each group's `Current` from the saved stack
    (`Matcher.cs:9483-9492`), so the only trace a called group leaves is in `Count`. Assert that
    `Current` is below `Count` after the restore.
  - A3: backtracking past a `GROUP_CALL` leaves every `Count` where it was at the call. Assert this
    in the `GROUP_CALL` backtrack arm against a count saved at the call, once the fix is in.
- Grid constructs (rule 11, each reported as "present AND fired"): a call inside a positive and a
  negative lookahead and lookbehind, an atomic group, a possessive repeat and a conditional test; a
  capture group directly inside the same; `(?&name)` and `(?0)`; `captures()` compared in full.
  The oracle suite must stay unchanged, apart from rows the new entry claims.

**Step 3, lift the exclusions (after step 2).**
- Delete the capture clause of `WritesInDiscardingConstruct` and rerun `memo-grid.cs ... unsafe` at
  three seeds. It must show zero changes, capture lists included. Keep the fuzzy-section clause
  until the `TotalErrors` question is settled (find a no-memo witness, or write the argument, with
  `file:line`, that none can exist).
- Witness: the lookaround capture-list row `(?r)(((?R)?R(?!.(?)(?R))(.))){2<=e<3}` over `aa` now
  gives the same answer with the memo on and off. It becomes a test of equality, not of the switch.

**Step 4, C2 (only if decided).**
- Assumptions to assert:
  - B1: a call's exits, in order, depend only on its entry key. Assert this in Debug by recording
    the exits of every second invocation of a stored key and comparing them with the table (a
    Debug-only shadow run).
  - B2: at `GROUP_RETURN`, repeats, `Current`, `CaptureChange`, `SectionFrame` and the open calls
    equal their values at the call. Assert this in the forward arm.
  - B3: the replay frame's undo gives back exactly the state before the apply. Assert this on
    position, counts, totals, edit count and capture counts.
- Witnesses: one per exit field (position, counts, totals, edits, captures). Each is a row where
  leaving that field out of the replay changes the answer.
- Grid: memo-grid on C2 versus no memo, all four modes, capture lists included, plus the shapes of
  section 4 as growth tests, with a cap on stored exits.

## 6. `(?:a?){d<=1}` over `''`: verdict

Probe: a file-based program over the F-A tree (b96b7be) and over `git archive main`, Release,
2026-09-28, with upstream regex 2026.9.10 run from Python.

| Pattern | Subject | F-A | F-A, retry off | F-A, `UpstreamEmptyIterations` | main | upstream |
|---|---|---|---|---|---|---|
| `(?:a?){d<=1}` | `''` | (0,0) (0,0,0) | (0,0) (0,0,0) | (0,0) (0,0,1) | (0,0) (0,0,1) | (0,0) (0,0,1) |
| `(?:a?){e<=1}` | `''` | (0,0) (0,0,0) | (0,0) (0,0,0) | (0,0) (0,0,1) | (0,0) (0,0,1) | (0,0) (0,0,1) |
| `(?:a?){d<=1}b` | `b` | (0,1) (0,0,0) | (0,1) (0,0,0) | (0,1) (0,0,1) | (0,1) (0,0,1) | (0,1) (0,0,1) |
| `(?:a\|){d<=1}` | `''` | (0,0) (0,0,1) | (0,0) (0,0,1) | (0,0) (0,0,1) | (0,0) (0,0,1) | (0,0) (0,0,1) |

Search and fullmatch agree on every row.

- **Cause: ledger 44, not 42.** The answer is the same with `SkipExactDeletionRetry` set. Setting
  `UpstreamEmptyIterations` gives back main's and upstream's (0, 0, 1). So the "needed" rule causes
  it (`Matcher.EmptyIterationAdmitted`, `Matcher.cs:4716`).
- **Intended by 44.** `a?` is a repeat with a minimum of 0. The deleting iteration consumes no text
  and spends one error. The repeat is not below its minimum, no open section has a minimum, and no
  tested group changes. So the rule fails the iteration, and the repeat's tail matches with no
  errors. This is the same move as the entry's own first row, `(?:[0-9]+){d<=2}` over `42kg`. It is
  not F-D (no minimum is involved), and it is not a regression against the rule as decided.
- **But it raises a consistency question for the owner.** On F-A, `(?:a?){d<=1}` and `(?:a|){d<=1}`
  now differ over `''`: 0 errors against 1. Main and upstream give 1 for both. Ledger 42's own
  argument treats `(?:a){d<=1}` as having the paths of `(?:a|)`. Ledger 44 applies only to repeats,
  so the alternation spelling keeps its pointless deletion. I recommend asking the owner whether
  the "needed" rule should also cover an empty branch that holds only deletions. Until then, pin
  both answers so neither can move silently.
- **Why neither the oracle nor the ratchet flagged it.** The oracle claims it on purpose. The
  `fuzzy-empty-iteration-needed-rule` entry (`tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs:3222-3251`)
  accepts any row where `OracleComparer.RunWithUpstreamEmptyIterations`
  (`OracleComparer.cs:660`), which also switches the retry off, reproduces upstream's answer, and
  this row does. The ratchet pins the port's own answers: there is no test of `a?` under a deletion
  budget over the empty string, and none comparing the `?` form with the `(?:x|)` form. Neither
  instrument is built to catch an intended divergence. They would catch it only if 44 had not
  meant it.

## 7. Probes, and how to rebuild them

All probes are file-based programs (`dotnet run -c Release <file>.cs`). They were kept in the
session scratchpad, not committed, per the brief.

- **Production timing** (`res.cs`): the three shapes with `SkipExactDeletionRetry` on and off, and
  with the memo off (`SkipCallMemo`) or forced (`UseCallMemo` plus `EagerCallMemo`), set by
  reflection as `tools/probes/recursion-failure-memo/residual.cs` does, but without the
  instrumentation patch. Run it with `-c Debug` for the Debug column.
- **Exit counting and the skip inside one call** (`exits.cs` over a scratch tree): the `OpenCalls`
  tuple gains an invocation id. `GROUP_RETURN` forward hashes the exit and pushes the id and the
  hash onto the backtracking stack. Its backtrack arm marks the hash as failed for that invocation.
  Forward `GROUP_RETURN` re-pushes the return node and backtracks on a failed hash.
- **C2** (`c2.cs`, `C2Probe.cs` over a scratch tree): as described in section 4, with the replay
  frame as backtracking opcode 250.
- **Empty-iteration verdict** (`empty.cs`): four patterns, subjects `''`, `a`, `b` and `aa`, search
  and fullmatch, with the retry and `UpstreamEmptyIterations` switches.
- **Upstream**: one subprocess per cell with a 30 s wall clock, as `upstream.py` does.
