# D99: reusing failed calls whose continuations are equivalent

Design note, 2026-11-17. Checklist item 15 (D99). Background: the D99 row of
`docs/KNOWN-DEFECTS.md` and `.scratch/s1016/perf-research/D93.md` (options, the D93 proof, and the
failed-return memo that was tried and reverted).

## The problem

`(?:(?P<g>a(?&g)b|c)){d<=6}` over `'a' * 12 + 'c'`: 2,092 steps before D93, 76,446 on main (Debug
`WorkCounter.Steps`, re-measured 2026-11-15), and about 100x the bytes. D93 made the call guard key on
the fuzzy counts, so a level may delete its `a` and call `g` again at the same position with one more
deletion. That was a correctness fix. The cost is the search it opens: at each of the 13 positions the
match may re-enter up to the remaining deletion budget, and each chain fails the same way.

## Why the obvious memo misses

The failed-call memo (`FailedCallKey`) keys a call on (call, position, counts, the groups the call
reads, the enclosing sections' rooms). It records a call that ran out of choices without returning.
Here almost every call returns, and the failure comes later, in the caller's continuation, so nothing
is recorded. The failed-return memo tried in October keyed a return on (call, position, counts, reach,
section, frame) and also missed: 7,077 returns gave only 275 distinct keys once frames were
approximated, but with real frame ids no two returns ever met the same frame. Every re-entry is a new
call with a deeper stack, so a key that names the frame never repeats.

## What is actually equal

Two calls of `g` at the same position with the same counts fail the same way when everything that
runs after them is the same. That is the body of `g`, which the call index fixes, and the stack of
pending returns: what each enclosing caller does after its call returns. In the witness every pending
frame is the same thing, "match `b`, then return", so two calls whose stacks hold the same number of
such frames, in the same section state, have the same future. The call itself is not the unit of
reuse; the pair (call, continuation) is.

So the key needs a value that is equal for equal continuations and cheap to keep:

- **A chained continuation hash.** When a call opens, its frame gets
  `h = combine(h(parent frame), return node, the caller state the continuation reads)`; the outermost
  call's parent hash is a constant. Opening a call costs O(1); no stack walk.
- **The caller state the continuation reads.** Everything a return restores and the code after it can
  read: the captures the caller's continuation reads (as `CallReadGroups` already selects for the
  failed-call key), the repeat counts of repeats open in the caller (the continuation's repeat ends
  read them), the parked group starts on the saved stack, the section frame. Not the reach (see the
  open gap below). With D68, a frame already records which groups and repeats a call can change; the
  continuation needs the complement plus what the caller's frames hold.

The memo entry is then (call index, position, counts, sections' rooms, continuation hash), recorded
when backtracking unwinds past the call's entry with every choice tried, whether or not it returned.
A hit fails the call at once, as the failed-call memo does.

## Cost on paper

Per call: one combine at entry (O(fields read)), one key build and lookup (O(key length)), one write
on unwinding. On the witness the distinct keys are about positions x deletion counts x stack depth,
13 x 7 x 19 at most, against tens of thousands of calls, so the memo bounds the search by the number
of distinct (position, counts, continuation) states. Adversarial input: a pattern whose continuations
are all distinct (each pending frame differs: different return nodes or captures) gets no hits and
pays the entry combine and one lookup per call; that overhead must be measured on the D68 rows and
the call benchmarks before anything merges.

## What must be proved first

1. **Equal key, equal future.** If two call entries share the key, a match through one exists exactly
   when a match through the other does. The argument is the one the failed-call memo already rests on
   (`FailedCallKey`'s remarks), extended across returns: a return restores only what its frame saved
   (D51, D68), and the continuation hash covers everything the code after each return reads. Every
   reader of state after a return has to be listed and shown to be in the hash or in the key.
2. **Hash collisions.** A collision would fail a call that could match: a wrong answer, not a slow
   one. Either the entry stores the full continuation (a list of frames, O(depth) memory per entry),
   or a collision check compares the frame lists on a hit. The cheaper design is a hash with a full
   compare on hit, using a persistent frame list (each frame points at its parent), so the compare
   stops at the first shared frame.
3. **The reach gap.** The D93 blind review found that `ReachedLow`/`ReachedHigh` grow over a whole
   attempt and are not restored on backtracking, and the call guard and the failed-call memo key read
   the reach. A continuation memo must either not read the reach or prove the reach cannot change the
   outcome. Needs its own witness search first.
4. **Where it is off.** As the failed-call memo: SKIP, PRUNE, `\K`, partial passes (D101's memo covers
   those), and patterns with more than one fuzzy section until the section chain is in the key.

## Recommendation

Do not build this yet. First measure how often the situation arises outside the D99 witness: count,
on the oracle's call rows and the ported call tests, how many call entries share (call, position,
counts) with an earlier entry whose continuation is equal. If the answer is "almost only on the
witness", the memo is a lot of proof for one pathological row, and D99 should be closed as accepted
with the witness kept as a step bound. If it is common in fuzzy patterns with recursion (the hot path
the owner cares about), build it test-first with the proof above reviewed blind before merging.

## Measured: how often equal continuations recur (2026-11-17)

A scratch build (never merged; `.scratch/d99-instrument.py`, `.scratch/d99/measure.cs`) counted, at
every call entry, whether an earlier entry in the same match had the same call, position, fuzzy counts,
partial side and stack of pending return nodes. That ignores captures and repeat state, so it is an
upper bound on what the memo could reuse:

| Corpus | Rows that make a real call | Call entries | Repeated |
|---|---|---|---|
| The D99 witness | 1 | 3,806 | 3,746 (98%) |
| D93's 30,000 targeted call-in-one-fuzzy-section rows (`d93/calls.jsonl`) | 20,609 | 2,762,546 | 2,561,164 (92.7%) |
| the battery's call rows (`d93/battery.jsonl`), all | 2,555 | 104,922 | 95,008 (90.6%) |
| the same, fuzzy rows only | 866 | 98,594 | 94,164 (95.5%) |
| oracle wave seed 20261116 | 10 | 64 | 0 |

Calls the inliner removes do not appear (most of the wave's call rows), and no non-fuzzy row repeats
much. Fuzzy recursion repeats almost every call: the worst targeted row makes 607,307 calls over a
14-character text, 606,859 of them repeats. So the condition the recommendation set is met: equal
continuations are the common case in fuzzy patterns with recursion, not only on the witness.

**Updated recommendation: build it**, test-first, in its own worktree, with the four proof
obligations above written before the code and reviewed blind before merging. First step: refine the
measurement key with the caller state the continuation reads (captures read after a return, open
repeats' counts) to see how much of the 92.7% survives; that sets the gain the build can claim.

## Refined: with the caller state and the reach (2026-11-17 evening)

The same probe (`.scratch/d99-instrument2.py` plus a reach column, `.scratch/d99/measure2.cs`, on
main b41fd1ca) added two stricter keys. "Strict" adds every group's count, current capture and its
span, and every repeat's count and start: more than the continuation reads, so what repeats under it
is a lower bound on what the memo can reuse, short only of the parked group starts on the saved stack.
"+ reach" also adds `ReachedLow`, `ReachedHigh` and the section frame, which the failed-call key
already holds (obligation 3 asks whether the memo can drop the reach; this column is the cost of
keeping it).

| Corpus | Call entries | Pending returns only | Strict | Strict + reach |
|---|---|---|---|---|
| The D99 witness | 3,806 | 3,746 (98.4%) | 3,746 (98.4%) | 3,740 (98.3%) |
| D93's targeted rows (`calls.jsonl`) | 2,439,157 | 91.7% | 89.4% | 2,109,522 (86.5%) |
| The battery's call rows, all | 104,922 | 90.6% | 87.7% | 89,657 (85.5%) |
| The same, fuzzy rows only | 98,594 | 95.5% | 93.1% | not split |

The entry totals move by about 1% between runs of the same build: rows that reach the 5-second
timeout stop at different points. Almost all of the recurrence survives the stricter keys, so the
captures and repeats do not eat the gain, and keeping the reach in the key costs about 3 points.
The next section qualifies this: the off rules the proof needs remove most of it on the targeted rows.

## Measured with the draft's key and off rules (2026-11-17, 21:50)

The proof draft below makes the key wider and turns the memo off in places, so the probe was rerun
with those rules (`.scratch/d99/probe-full.diff` on main b41fd1ca, `.scratch/d99/measure2.cs`).
Each column is the "strict + reach" key plus one change; a repeat counts only if the memo would be on:

| Corpus | Strict + reach | A: off when a repeat starts at p | B: A + the guard ancestors (at or after p, reach equal to now, as a set) | C: + the raw saved stack |
|---|---|---|---|---|
| The D99 witness (3,806 entries) | 98.3% | 3,740 (98.3%) | 3,665 (96.3%) | 0 |
| Battery call rows (104,922) | 85.5% | 70.3% | 70.3% | 19.7% |
| D93's targeted rows (2,052,229) | 84.0% | 5.4% | 4.7% | 29.1% |

- **The off rule is what matters.** D93's targeted rows put the call inside a repeat,
  `(?P<g>[ab](?&g)*c|[ab])`, so almost every call is made where an iteration of `(?&g)*` has just
  started, and the rule switches the memo off for them. The witness has no repeat around its call and
  keeps 96%. The probe tests `Repeats[r].Start == TextPos` for every repeat, open or not, so it
  switches off more often than the rule would; column A is a lower bound on the targeted rows.
- **The guard ancestors cost little** once filtered to those that can refuse (B against A).
- **Column C is not a measurement of the design.** The raw saved stack holds each frame's saved
  `CaptureChange`, a counter that differs on almost every call, which the draft's key leaves out
  (read only under Start == p). It says only that keying the stack bytes as they are would destroy
  the gain; the key must pick out the fields obligation 1 lists.

So the gain the build can claim depends on calls made where a repeat iteration starts. Turning the
memo off there keeps the witness's shape (recursion not wrapped in a repeat at the call) and loses most
of the fuzzy `(?&g)*` shapes. Keying that state exactly instead (the repeat's `CaptureChange`,
`ChangesAtStart` and the D17 empty-iteration state) is the next design question; first measure how
many of column A's switch-offs come from repeats that are actually open at the call.

## The four proof obligations (draft 2026-11-17, not yet reviewed)

Written by an Opus subagent from the code alone; every claim is a hypothesis until the audit mode
below reproduces it (VERIFICATION.md).

Scope: `docs/plan/2026-11-17-d99-continuation-keyed-call-memo.md`, "What must be proved first".
Line numbers: main 31b526fb, `src/FuzzyRegex/Engine/Matcher.cs` (M), `MatchState.cs` (S),
`PatternObject.cs` (P). Nothing was run for this draft; every claim cites code. Closest precedents:
`FailedCallKey` remarks (M:5872-5933), D94's failed-state memo and its proof
(`.scratch/s1016/perf-research/D94.md` "Proof of the key", M:10836-11000) and D104/D122's
lookaround continuation memo (S:3881-3960). Both precedents are "failed with everything after it"
memos; D94 excludes calls and D104 excludes calls and fuzzy, so this is the first one across returns.

### Mechanism under proof

- E = a GROUP_CALL entry (M:20339). Key K_E built at entry; on a hit return `_armBacktrack` (as M:20378).
- Record K_E in `BacktrackGroupCall` (M:21935) when the GROUP_CALL bstack byte is popped, whether
  or not the call returned.
- Carrying the key across a return: `AdvanceGroupReturn` drops the OpenCalls entry (M:20569) and
  `BacktrackGroupReturn` re-adds it with `MemoKey = null` (M:22095-22104). The build must push the
  continuation key (or its index) with the return's bstack record (M:20557-20567) and restore it at
  M:22100. Cost: one push/pop per return.
- Keep the existing failed-call memo too: a call that never returned fails under every continuation,
  so its key without the continuation part is the stronger fact.

### Why "the arm ran" means "every path from E failed"

The GROUP_CALL byte leaves the bstack only by (i) `BacktrackGroupCall`, after every entry above it
was popped, or (ii) a cut: END_ATOMIC / END_LOOKAROUND / possessive / conditional test end
(`CutBstack` M:6470), `(*PRUNE)`/`(*SKIP)`, or (iii) the pass returning (Success, Partial,
Cancelled, fork budget M:17820-17825). (ii) and (iii) record nothing, so they are safe by
construction. Remaining risk is (ii) firing *above* E for a construct opened before E and closing in
E's continuation: impossible while `UseCallMemo` holds, because `WritesInDiscardingConstruct`
(P:1666-1672, set by `BuildGroupCall` NodeCompiler:1300 and the fuzzy builder NodeCompiler:563)
turns the memo off for any call inside a discarding construct. A construct opened inside the
continuation cuts only entries above its own start, which are its body's, deterministically in E1
and E2 alike. PRUNE/SKIP are excluded (P:1670-1671). **Assert:** at `BacktrackGroupCall`, no open
atomic/lookaround/conditional frame lies below the call (Pstack depth at entry == at pop).

Success: within a pass SUCCESS returns at once except under POSIX (M:17666-17761; POSIX `CheckPosixMatch`
then backtracks, M:17755-17761). Every `BasicMatch` call is preceded by `InitMatch` (M:26621,
26910, 27467, 27731, 27866, 27911), which empties the memo (S:2800-2840). So a "failure" of E is
never a success the driver refused inside the same pass. POSIX is already off (P:1672).

Prunes: any mechanism that cuts only paths that fail anyway ("sound prune") cannot make E1 and E2
differ in outcome, so its state need not be keyed (D94 items 4-5). Any mechanism that may cut a path
that would succeed ("semantic prune": the call re-entry guard, D17's empty-iteration states) must be
keyed exactly or shown to cut at least as much in E2 as in E1.

### Obligation 1: equal key, equal future

Classes: (a) already in `FailedCallKey`; (b) restored by GROUP_RETURN from the frame; (c) fixed for
the memo's life / not read / sound prune; **(d) NEW: must be keyed**; (off) handled by turning the
memo off.

#### Live MatchState fields

| State | Read after the return by | Class |
|---|---|---|
| `TextPos` at entry | everything | (a) M:5970 |
| `FuzzyCounts`, `FuzzyNode` (innermost section) | fuzzy items, END_FUZZY | (a) M:5971-5974 |
| Read groups' current value (`CallReadGroups`, mode from `CallReadOf` P:591, P:1691) | backrefs, conditionals in the continuation | (a) M:5976-5979 for the live value at entry; a group not in E's `FrameGroups` is unchanged at the return (D68 assert M:5020-5030), one in it is restored to the same at-entry value |
| Groups not read: current, count, capture lists | only output; a failed path is never reported | (c) |
| `CaptureChange` | equality tests at repeat ends / optional passes / keyed tails, only under `TextPos == Start` (D94 item 1; `OptionalPassAdmitted` M:11380-11390, `FuzzyIterationStands` M:6313) | (b) restored M:20551; reader-relevance handled by the Start rule below |
| **Repeats, all of them** (`Repeats[r].Count`, capped at the minimum for an unbounded repeat as D94) | the continuation's repeat ends (min/max) | **(d)**. FailedCallKey leaves repeats out because the callee starts its own (M:5914-5915); the continuation reads the callers'. FrameRepeats ones come back from the frame (M:20539-20545), others are untouched (D68 M:5032-5041): both equal the value at E's entry, so key all at entry |
| **Repeats' `Start`, `ChangesAtStart`, `CaptureChange`, D17 `EmptyIterationStates`/RunId** | readers fire only when `TextPos == Start` (D94 item 1 list; `RevisitsEmptyIterationState` M:4560, `EmptyIterationAdmitted` M:11225). In the continuation TextPos >= p (forward; mirror for reverse) and every pending Start <= p | **(d) or (off)**: memo off (no lookup, no record) when any repeat, live or saved in a pending frame, has Start == p. D17 is a semantic prune, so it cannot be dropped otherwise |
| **`OptionalPasses[]` (ledger 44), saved with the repeats (M:5281-5283)** | `OptionalPassAdmitted` only when `TextPos == pass.TextPos` (M:11384) | **(d) or (off)**: off when any pass's TextPos == p, as D94 item 2 |
| **Last fuzzy change** (`FuzzyChanges[^1]`) | `DeletionRepeatsAnEarlierAlternative` M:11976-11986: insertion at TextPos - step | (c) by M:5929-5931 (sound prune), but **(d) recommended**: one bit "last change is an insertion just behind p", as D94 keyed it (M:10990-10993). Cheap and removes the argument |
| `FuzzyChanges.Count` vs saved counts | under Start == p / pass.TextPos == p only | covered by the off rule |
| `DeletionsPastReturns` walk (M:11765, D93 perf) | return nodes and sstack layout of pending frames, room | determined by the continuation id; sound prune ("certainly holds no new match") |
| `ReachedLow`/`ReachedHigh` | re-entry guard at every later GROUP_CALL (M:20366-20386), `FailedCallKey` M:6063-6068 | (a). See obligation 3 |
| **Open calls (re-entry guard, `ActiveCalls`/`ActiveCallCounts`, `OpenCallSpans`, `OpenCallSections`)** | guard at every GROUP_CALL in the continuation (M:20383-20390, `TryOpenCall` M:5660) | **(d), wider than (a).** FailedCallKey keys only ancestors passing `CouldRefuseInside` (M:6192-6218). Its two tests rest on "read groups set never shrink inside an open call" and "spans captured inside C start at or after p": both false in the continuation, because returning from a caller K restores K's entry snapshot (`ReturnToCallerCaptures` M:20530), which can have fewer set groups, or spans starting before p. Concretely: O opened at p with group 1 unset, K opened at p with group 1 unset, group 1 set empty, E opened at p. O is filtered out of E's key, yet after E and K return, O's body calls again at p with group 1 unset and O refuses it. Key: every open call with pos == p (forward; <= for reverse is moot, lookbehind calls are off) and Reach == reach now, with its key, read values and section bit, **without** the `CouldRefuseInside` filter. `SectionCouldRefuseInside` (M:6158) rests on "counts only grow along a path within an instance": still true across returns (counts are not in call frames), but re-check after END_FUZZY/re-entry in the continuation |
| `SectionFrame` chain: outer sections' entry counts, nodes (sstack, M:18623-18630) | END_FUZZY in the continuation, `RaisesUnmetMinimum`, `OneMoreErrorFitsTheOpenSections` | **(d)**. FailedCallKey keys outer sections' rooms *clamped to the inner section's room* (M:6001-6012, 6044-6047): valid only for errors made inside the call. After END_FUZZY the continuation spends outer room unclamped. Covered if the continuation id holds the sstack section frames (below) plus the innermost live counts (a); else memo off when more than one section frame is open |
| `TotalErrors`, `TotalCost` | END_FUZZY overwrites before its limit check (M:5925-5928, M:17700ff); drivers after the pass | (c) |
| `MaxErrors`, `MaxCost`, `RefineStart/End`, `RefinesInANarrowedSlice`, `SliceStart/End`, `MatchAll`, `MustAdvance`, `SearchAnchor`, `OneAttempt`, `FewestErrors`, `MinWidth` | SUCCESS (M:17668, 17676 `EndsOutsideTheRefinement`, 17725-17737), insertion ban (M:12336 etc.), `\G` | (c): set only between passes (`InitMatch` precedes every pass, list above); slice moves only in lookarounds/conditional tests, which hold no call here |
| `BoundlessBodies` (D102/D114) | M:16700, SUCCESS assert M:17687 | (c): counts open lifting bodies; none is open around a call while `WritesInDiscardingConstruct` is false. **Assert** 0 at E |
| `AttemptStart`, `MatchPos` | result only; no matcher reader (D116 list M:27142-27154) | (c). Cross-attempt reuse: rules tied to the attempt only remove choices later (D94 item 7) |
| `PartialSide`, `AttemptTouchedTheTextToCome`, `HasUnknownCaptures`, `WMarks`, `InsideW`, `LookaroundSuccesses`, `CallTracks`/`CallOutcomes`, `NegativeOutcomes`, tail memo, fork budget | partial second pass | (off): memo off when `PartialSide` set (S:2817). Assert as M:20451 does |
| `IsRepeatGuarded` guard lists, `KeyedTail` | non-fuzzy patterns only: `IsRepeatGuarded` returns false when `IsFuzzy` (S:4193-4198) | (c) for fuzzy patterns. Non-fuzzy: GROUP_CALL clears every repeat's guards (M:20466-20471) but restores only `FrameRepeats` (M:20406, 20535-20545), so E1 and E2 can hold different guard contents; sound only on upstream's guard premise. **Recommend: memo off for non-fuzzy patterns** (the measurement found no non-fuzzy row that repeats much) |
| Per-run repeat memo, `FailedCalls`, D94 `FailedRepeatStates` (off with calls, M:10856), D116 `BestWalkFailures` (whole-search facts only, M:27163-27230) | prunes | (c) sound prunes |
| `RetriedAWholeFoldEdit` | set M:14463, read M:19017/19285 inside the same folded item | (c) written before read |
| `Iterations`, timeout, cancellation | effort only | (c) |

#### Saved stack below E (the continuation proper)

In an eligible pattern the save stack between two call frames holds only (pushes listed at
M:16866/18210 parked group ends, M:18564-18568/18623-18630/19801-19805 section frames,
M:21707 CALL_REF NULL for a group matched inline, M:20406-20410 call frames; lookaround state
M:20639 cannot sit around a call here). Its layout is fixed by the call site's static nesting
(groups, sections, inline group bodies around the GROUP_CALL node), as `DeletionsPastReturns`
already relies on (M:11765 ff).

| Content | Read by | Class |
|---|---|---|
| Each pending frame's return node | GROUP_RETURN (M:20506-20514) | **(d)** the chain |
| Each frame's saved read-group values (`FrameGroups` ∩ `CallReadGroups`, as `ReadValue`) | restored at its return (M:20530), then backrefs/conditionals | **(d)** |
| Each frame's saved repeats (Count capped; Start for the off rule) and optional passes | restored M:20539-20545 | **(d)** |
| Each frame's saved `CaptureChange` | as above, only under Start == p | covered by the off rule |
| **Parked starts of read groups open around a call site** | END_GROUP pops it to form the span (M:16829-16836), then backrefs/conditionals | **(d)**. D122's witness proves this class matters for a continuation memo: `a*(a*(?=(?:a\|b)*)b)\1` over 'aabaa' answered P(3,5) for P(0,5) (S:3919-3925). Parked starts of unread groups: output only, (c) |
| Section frames (entry counts, node; not the link offset) | END_FUZZY | **(d)** (see above) |
| CALL_REF NULL markers | GROUP_RETURN takes the not-called branch | structural, fixed by the call site: include the call site node id |
| Repeat guard lists inside saved repeat data (M:20461-20463) | non-fuzzy only | off for non-fuzzy |

#### Bstack and pstack below E

The past only: alternatives tried after E's failure. Read forward only by cuts, which cannot reach
below E here (see "Why the arm ran"). (c).

#### Summary: the continuation key

`K_E = FailedCallKey(E)` (reach included) ++ live: every repeat's capped Count, last-change bit,
the guard ancestors (pos == p, Reach == now, unfiltered by read groups) ++ `ContId`, where
`ContId = intern(ContId(innermost open call), E's call site node, E's saved frame values, read-group
parked starts and section frames in the sstack segment above that call's frame)`; outermost parent is
a constant. Off when any repeat Start or optional pass TextPos equals p (live or saved), when
`!IsFuzzy`, and in every case of obligation 4.

### Obligation 2: collisions

Recommended: **hash-consing (interning) of continuation frames**, exact by construction.

- A per-pass `Dictionary<FrameTuple, int>` (tuple = parent id, call site node index, the frame's keyed
  values as a `long[]`, compared by content like `FailedCallKeyComparer`). Equal ids iff equal
  continuations, by induction on depth, so the memo key is a plain `long[]` in the existing
  `HashSet<long[]>` with the existing comparer. No second compare, no hash trust.
- Lazy: an open call gets its id only when a keyed call (past `CallMemoThreshold`, M:20377) needs it;
  computing walks down to the first open call that has one and interns upward, so each frame is
  interned at most once while open. Store the id in the `OpenCalls` tuple and carry it across
  return/backtrack like the key. Unkeyed calls pay one field init.
- Per keyed call: one intern lookup O(frame fields) + the key build (FailedCallKey already walks all
  open calls, M:6073, so O(depth) is today's cost) + O(R) repeats. Per return: one extra bstack push.
  Memory: one tuple per distinct frame per pass; cap it with the memo (`_failedCallCap` M:6244,
  SHORTCUT already there) and stop recording beyond it.
- Rejected: persistent parent-pointer frame lists with a full compare on hash hit. Same exactness,
  but one allocation per call and an O(depth) compare per hit; interning gives O(1) compare and
  allocates only per distinct frame. Storing the full frame list per memo entry: O(depth) memory per
  entry, no benefit.

### Obligation 3: the reach

Readers (complete; `grep ReachedLow|ReachedHigh|ReachedWidth` over src): `NoteReached` at every
backtrack (M:17805-17809, only with calls, M:15777) and at GROUP_CALL (M:20366); the guard's reach
(M:20367, 20385-20386, `TryOpenCall` M:5660, `ReopenCall` M:5698); `FailedCallKey` M:6063-6068 and
its ancestor filter M:6079. Reset per attempt (M:16071-16072). Nothing else, so the reach changes an
outcome only through guard refusals, a semantic prune.

Can it be dropped? Sketch: if E2 runs in the same attempt after E1's subtree was exhausted, every
position E1 noted is inside E2's reach, so E2's reach never grows and E2 refuses every call that E1
refused *inside its own run*. It fails for refusals by ancestors opened before E: E1's ancestor O
refuses I only if O.Reach == reach now, which E2's ancestor need not satisfy. Across attempts the
reach is reset, so even the inner half fails. So: **keep the reach in the key** (measured cost about
3 points: 89.4% to 86.5% on `calls.jsonl`, design note table). A drop would need per-ancestor
"Reach == now" bits and a same-attempt restriction, and a witness search first.

Witness search (small, under 5 min each, on the scratch build in `.claude/worktrees/d99m`):
1. Audit mode: build the memo but never take a hit. On a hit, flag E2's GROUP_CALL entry; keep a
   count of flagged entries on the bstack (++ at push, -- in `BacktrackGroupCall`). SUCCESS with
   count > 0 is a counterexample: print pattern, subject, E's key. Run with (i) the full key,
   (ii) the key minus the reach, (iii) minus each (d) field in turn. (ii)/(iii) finding zero on a
   corpus that had the field present AND the memo active counts (VERIFICATION rule 11); report both.
2. Corpus: D99 witness; `d93/calls.jsonl` (first 2,000 rows); battery call rows; plus a targeted
   generator for guard-relevant shapes: zero-width re-entry `(?:|a)(?R)`, `(?:(?R)|a)*b`,
   `(?P<g>(?:x|)(?&g)?y)`, under `{d<=k}`, `{e<=k}`, `{i<=k}`, with and without a read group
   (`(a)?...\1`, `(?(1)..)`), subjects of 2-10 characters over {a,b,x,y}.
3. Extra hand witnesses for the guard-ancestor filter (obligation 1): `(?:()|)(?:x?(?R))?(?(1)z|y)`
   style, where an ancestor at p with fewer set groups can refuse after a return.

### Obligation 4: where the memo is off

Inherited (the existing memo's): no call site (`GroupCallSites > 0`), `WritesInDiscardingConstruct`
(call, fuzzy section or `\K` in an atomic group, possessive repeat, lookaround or conditional test;
also covers calls in lookbehinds), `KeepInSubmatch` (`\K`), `(*PRUNE)`, `(*SKIP)`, POSIX
(P:1666-1672, rationale P:600-637); partial second pass and `SkipCallMemo` (S:2817-2820); before the
threshold (S:2823, M:20377).

New for the continuation memo:
- **Non-fuzzy patterns** (`!IsFuzzy`): repeat guards are read there (S:4193) and are not restored by
  a call for repeats outside `FrameRepeats` (M:20466 vs M:20406); the measured gain is fuzzy-only.
- **A repeat (live or saved in a pending frame) with Start == p, or an optional pass with
  TextPos == p**: D94 items 1-2; D17's empty-iteration states are a semantic prune.
- **More than one open section frame at E**, unless the sstack section frames are in `ContId`
  (they are in the design above; keep the runtime test as an assert while proving).
- BESTMATCH/ENHANCEMATCH: may stay on (each refinement pass re-inits; SUCCESS returns at once), but
  D94 excluded them; gate on a (?b)/(?e) call grid in audit mode before enabling.
- Oracle-only ablation switches (`CallGuardComparesAcrossSections`, `CallMemoKeyOmitsPosition`):
  off, or key what they change.

### Reasons it could be unsound or not worth it

1. **Unsound if built on `FailedCallKey`'s ancestor filter as is** (obligation 1, open calls row):
   `CouldRefuseInside`'s premises do not hold across returns. Fix: unfiltered (pos == p, reach == now)
   ancestors. Costs reuse wherever read groups exist.
2. **Clamped outer section rooms** in `FailedCallKey` are too weak past END_FUZZY; must come from the
   section frames.
3. **Parked starts** of read groups are a proven class of bug for this kind of memo (D122).
4. **Gain may shrink**: the 86.5% measurement keyed live groups and repeats but not parked starts,
   section frames, the unfiltered ancestors, the last-change bit, and did not switch the memo off at
   Start == p. Patterns like `(?:...(?R)...)+` open a repeat iteration and call at once, so Start == p
   may switch it off for much of the hot path. **Re-run `measure2.cs` with this exact key and the
   off rules before building**; it is cheap and sets the claim.
5. Cost on non-repeating rows: lazy interning means only keyed calls pay; the D68 rows and call
   benchmarks must still be measured (design note "Cost on paper").

Confidence: high on the reader inventory for the advance arms named here and on items 1-3 above;
medium on completeness (the "only these kinds on the sstack between frames" claim needs a Debug
assert; D17/D101/D121 state was classified by reading, not run).
