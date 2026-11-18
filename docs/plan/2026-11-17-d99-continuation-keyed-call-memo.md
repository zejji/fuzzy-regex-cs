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

## Keying the repeat-start state instead of switching off

A second subagent read the code for the question above (`.scratch/d99/repeat-start-design.md`, 129
lines; no probe ran, the D82 benchmark had the machine). Its findings, unverified until measured:

- **No run-time "open" flag.** `RepeatData` keeps stale values after a repeat exits. An exact test can
  be computed at compile time (the repeat's body holds the call site and lies in that level's group
  body). On the targeted rows the `*`'s body is the call itself, so the switch-offs there are real:
  counting only open repeats will not recover much.
- **Every Start == p reader compares a counter with its value at the iteration's start**, so relative
  values suffice: the `CaptureChange` difference (the frame's saved one for pending levels), the
  `FuzzyChanges.Count` minus `ChangesAtStart`, the edits of each kind since the start, the capped count.
- **Blocker: D17's empty-iteration states.** The set is per attempt, never undone on backtracking, and
  keyed by a run id that body restarts keep, so it is read for every open repeat whatever its Start.
  A hit makes `tryTail` true below the repeat's minimum (Matcher.cs ~24384, ~24625), opening a path
  E1 never tried, so "E1 failed" would not imply "E2 fails". Options: memo off when the pattern tests
  a group (`TestedGroups.Length > 0`), which costs the witness and the targeted rows nothing; or key
  the number of D17 entries each open run holds. No witness built yet.

Measured 2026-11-18 02:00 (`.scratch/d99/probe-k.diff`, logs `battery7.log`, `calls7.log`; one run
each). A' switches off only for repeats open at the call (a compile-time lexical test, not checked at
run time); K keys the relative values instead of switching off, with the memo off when the pattern tests
a group; K0 is K without that rule:

| Corpus | Entries | Strict + reach | A (any repeat) | A' (open only) | A' + guard ancestors | K | K0 |
|---|---|---|---|---|---|---|---|
| The D99 witness | 3,806 | 98.3% | 98.3% | 98.3% | 96.3% | 96.3% | 96.3% |
| Battery call rows | 104,922 | 85.5% | 70.3% | 85.2% | 85.2% | 85.1% | 85.4% |
| D93's targeted rows | 1,958,390 | 83.2% | 5.7% | 15.6% | 14.4% | 63.5% | 65.0% |

- Most of the battery's loss under A came from stale repeats; on the targeted rows 70.6% of entries
  have a genuinely open repeat starting at the call, which only keying recovers.
- The D17 rule (off when the pattern tests a group) costs 0.3 points on the battery and 1.5 on the
  targeted rows.
- K is a lower bound: the probe switches off any level it cannot resolve and keys every optional-pass
  slot at p, open or stale.

**Recommendation: build K**, with the memo off when the pattern tests a group, the key of obligation 1
plus the relative repeat-start values, and the audit mode of obligation 3 as the first step. A blind
review of the proof comes before the build is merged.

## Blind review of the design (2026-11-18)

An Opus reviewer was given the mechanism and the key but not this note's argument
(`.scratch/d99/blind-review.md`). Its findings, with what the build must do about each. The two
witnesses' expected answers were re-checked in Python regex 2026.9.29 (pinned venv): (0, 3) with fuzzy
counts (1, 0, 0), and (0, 2).

1. **High, witnessed: clamped section rooms.** `FailedCallKey` clamps each outer section's room, and
   the pass rooms, to the open section's own room (Matcher.cs ~6005-6058). Exact inside the call, not
   after it returns: once the inner section closes, later errors are charged against the outer
   section's real count. `(?:.*\d)?(?:(?:q|a)(?:(?1)){e<=0}c|(b(?1)?)){s<=1}` over `abd`: the calls at
   position 1 after a substituted `q` and after an exact `a` build identical keys; skipping the second
   loses the match (0, 3). The chain alone does not cover it (no pending frame). **Build:** the
   continuation part keys every open section frame's node and exact counts (entry counts on the saved
   stack plus the live ones), unclamped, including those outside the outermost call.
2. **Blocking, witnessed: the call site.** The key names the called group, not the site.
   `(?:..z|)(?:(?1)x|(?1)y|(a(?1)?)){s<=0}` over `ay`: both calls at 0 build the same key; skipping the
   second site gives (0, 1) for (0, 2). **Build:** E's own return node is the first link of the chain
   (the draft's summary had it; it must not be lost).
3. **Medium, mechanism only: ancestors in other sections.** After a return a later call can be made in
   an ancestor's own section, or a new one; the guard then compares the ancestor's exact counts
   (`TryOpenCall`). The key holds only a same-section flag, and the D93 filter drops ancestors outright.
   **Build:** drop that filter too, and key each ancestor's section and counts.
4. **Low-medium, mechanism only: the reach across attempts.** The memo lives for a pass, the reach for an
   attempt; skipping a continuation leaves `ReachedHigh` lower than main would, so a later call in that
   attempt can be refused by the re-entry guard where main lets it through. **Build:** empty the new
   memo at every attempt start. The existing failed-call memo has the same exposure: a witness search
   for it goes in the audit mode, and it is not a defect until one is found.
5. **Medium, mechanism only: repeats outside the frames.** A repeat around an outer call site whose body
   the called group cannot write is not in the call frame (D68), yet its live Start, `CaptureChange` and
   `ChangesAtStart` are read by `EmptyIterationAdmitted` after the returns. **Build:** collect the open
   repeats from the call sites of every level, reading live values for those not in a frame.

Checked sound by the reviewer: the record point (SUCCESS means no match was possible), capture lists,
discarding constructs, repeat guards (off for fuzzy), the other memos, timeouts and partial passes.


## Build (2026-11-18, branch `perf/d99-continuation-memo`)

The memo is `src/FuzzyRegex/Engine/CallContinuationMemo.cs`; its remarks list every key part and
the reader it serves. Each pending level is described by a snapshot taken when its call was made
(what the return gives back), of the repeats and optional passes whose body holds that level's call
site only (a compile-time table per return node), so a call pays for the repeats around it and not
for every repeat in the pattern (D68's lesson). The five blind-review fixes are in, and one more:

6. **A hit clears the open calls' failed-call keys.** Found by the differential, not the audit: a
   hit skips the call's continuation, which runs through every pending return, so an open call can
   be backtracked past without returning although it could have. The failed-call memo then
   recorded it as a call that cannot return and failed a later call with the same entry key and a
   different continuation. `(?:(?P<g>[ab](?&g)a+|ba)(?&g)){1<=d<=2}` over 'aabbaa' answered group 1
   (0, 1) for (0, 2).

Audit and differential (scratch: `.scratch/d99b/audit.cs`; Debug; "eager" switches both memos on
from the first call). A counterexample is a match found inside a call the audit flagged as a hit;
"diffs" compares span, every group's captures and the fuzzy counts with the memo off and on. Every
row also compared the failed-call memo on against both memos off (blind review item 4).

| Corpus | Rows run | Rows keyed | Keyed calls | Would-be hits | Counterexamples | Diffs on/off | Failed-call memo diffs |
|---|---|---|---|---|---|---|---|
| The D99 witness | 1 | 1 | 3,792 | 3,665 | 0 | 0 | 0 |
| Battery rows with a call (`d93/battery.jsonl`) | 10,597 | 66 | 1,039 | 91 | 0 | 0 | 0 |
| The same, eager | 10,597 | 603 | 2,851 | 96 | 0 | 0 | 0 |
| `d93/calls.jsonl`, first 5,000 | 3,994 | 1,063 | 197,131 | 166,767 | 0 | 0 | 0 |
| The same, eager | 3,994 | 2,868 | 213,044 | 167,727 | 0 | 0 | 0 |
| Generated grid (17 shapes x 9 budgets x plain/(?b)/(?e)), eager | 10,206 | 5,943 | 57,171 | 8,453 | 0 | 0 | 0 |
| Guard grid (zero-width re-entry across returns), eager | 23,040 | 8,594 | 33,301 | 12 | 0 | 0 | 0 |
| Repeat and pass starts grid (fuzzy minimums), eager | 12,960 | 3,317 | 60,520 | 23,685 | 0 | 0 | 0 |
| The whole test suite, eager (10,393 tests) | - | - | 66,363 | 1,338 | 0 | - | - |

Against main itself (a worktree build of 56fcabfb, memo on in the branch): 0 differing answers in
121,598 row runs, every corpus above with and without the eager switch.

Most battery call rows are outside the memo: the battery is built from `(*SKIP)`, tested groups and
non-fuzzy shapes, where the failed-call memo, and so this one, is off.

Mutations (each part removed in turn, then the eager grids and the call rows run again):

| Part removed | Found | Test |
|---|---|---|
| Section frames' exact counts (F1) | witness F1 | `CallContinuationMemoTests.The_key_holds_the_exact_counts_of_the_sections_outside_the_call` |
| The call's own return node (F2) | witness F2 | `The_key_holds_where_the_call_returns_to` |
| The pending calls' return nodes | `(?:(?:x\|(?&g))*?(?P<g>a(?&g)?b?)x?){1<=d<=2}`, fullmatch 'abaa': one deletion for two | `The_key_holds_where_each_pending_call_returns_to` |
| Clearing the open calls' failed-call keys (fix 6) | the row above | `A_hit_keeps_the_open_calls_out_of_the_failed_call_memo` |
| Pending calls at this position, their section and counts (F3) | nothing | open: kept for the guard (`Matcher.TryOpenCall`, `AnOpenCallSawThisReadState`) |
| Iteration and pass starts at this position | nothing | open: kept for `FuzzyIterationStands`, `EmptyIterationAdmitted`, `OptionalPassAdmitted` |
| The live level's repeats; repeat counts | nothing | open: kept for the repeat ends |
| Insertion just behind | nothing | open: kept for `DeletionRepeatsAnEarlierAlternative` |
| Emptying the set per attempt (F4) | nothing | open: kept for the guard's reach |

Each witness test fails with its part removed and passes on main. The open parts are each read by
the code named, so they stay in the key; removing them is a later question, which needs a witness
search per part first.

Cost (Debug `WorkCounter.Steps` and warm bytes; Release times are indicative: best of 15, pinned to
the performance cores, no quiet window):

| Row | Steps off / on | Bytes off / on (Release) | Release us off / on |
|---|---|---|---|
| D99 witness `{d<=6}` over a12c | 76,446 / 3,497 | 395,000 / 78,512 | 4,354 / 293 |
| `{d<=2}` over a12c | 1,884 / 629 | 11,392 / 12,520 | 100 / 43 |
| `(?:(?P<g>[ab](?&g)*c\|[ab])){e<=2}` over 'abababcbab' | 294,043 / 20,340 | 987,640 / 189,936 | 20,164 / 1,881 |
| D68 rows (depth 30, flat 30, fuzzy) at K = 0, 25, 400 | the same | the same | within 5% either way |

"Off" is the branch with the memo switched off, which runs main's code: every new line is behind
`MatchState.ContinuationMemoOn`.

Gates: ratchet GREEN (10,393 tests); oracle seeds 7 and 4242 clean; seed 20261118 diverges on two
rows with no group call, where the memo never switches on, and main gives the same two answers:
row 2850 `(?r)(?w)\X*` (match) and row 3889 `(?b)(?r)\M(?>(?:🏻[a\d]+?){e<=2})(\p{Lu}*?)` (partial
search). Both are new oracle rows to judge, not D99's.

### After the build's blind review (2026-11-18, later)

The review (`.scratch/d99r/review.md`) found that each keyed call walked every open call and copied
a key of length O(depth), so a search that recursed n deep at each start went from n^2 to n^3:
`(?P<g>a(?&g)?c)(?:x){s<=1}` over 'a' * 500 took 591 ms and 872 MB against 51 ms and 19 MB. The
test `CallContinuationMemoTests.A_deep_recursion_costs_about_what_it_costs_without_the_memo` pins
it (Debug, depth 200: 5,077,840 B off against 61,595,856 B on before the fix). Changes:

7. **The continuation is interned.** A pending call made before the position now is keyed by one
   id (`CallContinuationMemo.LinkOf`): its parent's id, the called group, the return node and a
   tail (the capped counts of the repeats around the site and the section frames its caller opened
   above its own frame), interned per attempt. Equal ids mean equal continuations, by induction on
   depth. Each open call is interned once while it stays open, so a key costs the same at any
   depth. Only the pending calls made at this position are keyed in full (their starts, the guard's
   section and counts), and positions only grow along the open calls, so they are a run at the top.
8. **Only calls the failed-call memo does not record are recorded.** A call that never returned
   is in the failed-call memo, whose key is a prefix of this one and is asked first. Keys of open
   calls are kept in one flat list, so opening a call allocates nothing.
9. **A trial per pass** (`CallContinuationMemo.Pays`): after 16,384 keys the memo stops building
   them unless one in 64 hit. Sound by construction: `AlreadyFailed` returns false before any key is
   built (`if (!keyed || ... || !Pays(memo)) return false;`), so the call is neither failed nor
   recorded, which only loses pruning. Measured (Release, indicative, best of 3-25, machine busy):

   | Row | Off | On, with the trial | On, without |
   |---|---|---|---|
   | `(?P<g>a(?&g)?c)(?:x){s<=1}`, a x 1000 | 423 ms | 455 ms, 77.9 MB as off | 497 ms |
   | `(?:(?P<g>a(?&g)?c)x){s<=1}`, a x 1000 | 577 ms, 52.1 MB | 598 ms, 55.6 MB | 654 ms, 159.7 MB |
   | D99 witness | 4.05 ms | 0.347 ms | 0.330 ms |
   | `{d<=2}` witness | 0.107 ms | 0.065 ms | 0.063 ms |
   | `(?:(?P<g>[ab](?&g)*c\|[ab])){e<=2}` | 13.3 ms | 1.40 ms | 1.35 ms |
   | D68 rows, K = 0, 25, 400 | | within 8% of off | within 8% of off |
   | `d93/calls.jsonl`, first 500 rows | 139 ms | 28.6 ms | 25.8 ms |

   It earns 8-14 points on the deep rows and costs nothing measurable elsewhere, so it stays.
10. **No key while a level holds a late start** (`CallContinuationMemo.LateStart`). A link keys a
    level's starts as before the position, which needs every start at or before the call's own
    position, and a slot can hold one after it: `(?:(?:(?&g)c?|){2,3}$(?P<g>a(?&g)?)){e<=1}` over
    'abyb' made a call at 1 with its optional pass's slot at 4 (the counts grid's assert). Sound
    by construction: `memo.LateLevels > 0` returns false in the same line as the trial. It is not a
    performance choice and cannot be dropped without keying such starts in the link.
11. **No key when a pending call's section has closed.** The guard compares section frames by
    their place on the saved stack, and a call made at this position whose frame is no longer on
    the chain of open frames (seen with `(?R)` under a fuzzy section, e.g.
    `(?:[ab](?R)?b?|ab){d<=3}` over 'abbababaabaaac', 10 calls in 600 rows) cannot be keyed exactly:
    `FrameIndex` returns -2 and the call gets no key. The first build mapped such a frame to -1, as
    to "no section"; no counterexample was found, but the mapping was not exact.
12. **A re-opened call's level takes the call's own position and section**, from the record on the
    backtracking stack, not the state the return left (found by the assert in `LinkOf`).

Growth after the fix (Release, indicative, best of 3; answer None throughout):

| Row, n | Off | On |
|---|---|---|
| `(?P<g>a(?&g)?c)(?:x){s<=1}`, 500 | 73 ms, 19.0 MB | 79 ms, 19.0 MB |
| the same, 1000 | 327 ms, 77.9 MB | 350 ms, 77.9 MB |
| the same, 2000 | 1,806 ms, 163.0 MB | 1,850 ms, 163.0 MB |
| `(?:(?P<g>a(?&g)?c)x){s<=1}`, 500 | 103 ms, 13.0 MB | 107 ms, 16.6 MB |
| the same, 1000 | 453 ms, 52.1 MB | 476 ms, 55.6 MB |
| the same, 2000 | 2,404 ms, 208.1 MB | 2,437 ms, 211.6 MB |

The D99 witness now takes 4,367 Debug steps (3,497 before the fix, 76,446 on main): calls that
never returned are left to the failed-call memo, which is emptied per pass rather than per attempt.

Gates after the fix: ratchet GREEN (10,395 tests); oracle seeds 7 and 4242 clean, seed 20261118 the
same two call-free rows as before (2850, 3889).

Audit after the fix (Debug; default threshold / eager; every row also run on a main 56fcabfb
worktree build): 0 counterexamples, 0 on/off diffs, 0 failed-call memo diffs, and 0 answers that
differ from main in 315,134 row runs.

| Corpus | Rows | Keyed calls (default / eager) | Would-be hits (default / eager) |
|---|---|---|---|
| Deep rows (n = 200) | 2 | 32,768 / 32,768 | 0 / 0 |
| Battery call rows | 10,597 | 1,039 / 2,851 | 77 / 82 |
| `calls.jsonl` first 5,000 | 3,994 | 197,044 / 212,956 | 166,090 / 166,782 |
| Generated grid | 10,206 | 22,226 / 56,888 | 8,334 / 8,423 |
| Guard grid | 23,040 | 10,143 / 33,301 | 2 / 2 |
| Starts grid | 12,960 | 40,144 / 60,519 | 22,772 / 23,685 |
| Counts grid (repeats with minimums around call sites) | 96,768 | 117,471 / 281,498 | 13,533 / 20,560 |

#### Key parts with no witness test

- **The count cap at a repeat's minimum** (where its maximum is unbounded) is exact. Every reader of
  a repeat's count compares it with the minimum only: `EmptyIterationAdmitted` (Matcher.cs ~11233,
  `Count <= min` after the increment), the repeat ends (~24312-24372, `Count >= min`, and the
  maximum test, which is always true for an unbounded maximum), the body and tail starts (~23483,
  ~23560); the repeat memo caps the same way (~11632). The continuation can only change a count by
  an increment followed by these tests, and a decrement that undoes its own increment, so counts
  at or above the minimum answer every test alike.
- **The inserted-behind bit** is read only by `DeletionRepeatsAnEarlierAlternative` (~11982), a
  prune that drops a deletion only when an earlier alternative reaches the same state
  (`FailedCallKey`'s remarks, ~5928-5931), so it cannot change whether the continuation matches: no
  witness can exist. It stays because it costs one value and matches D94's key (~10990).
- **The repeats and passes around each site** (`AddLevel`, and the counts in each link) and **the
  guard's section and counts for pending calls at this position** have readers (the empty-iteration
  and optional-pass rules, ~6313, ~11225, ~11380; the guard, `TryOpenCall` ~5660), so they stay, but
  no witness was found: removing each in turn gave no counterexample and no answer change on the
  generated, guard, starts and counts grids (eager) after the fix. They are open items under
  VERIFICATION rule 12, not proved redundant.

#### After the fix round's blind review (2026-11-18, `.scratch/d99r2/review.md`)

No wrong answer found. Changes:

- **The O(1) key is pinned.** `WorkCounter.CallContinuationLevelsInterned` counts levels interned;
  `CallContinuationMemoTests.Each_open_call_is_interned_once_however_deep_the_recursion` bounds it
  by twice the keys built on the deep row. Debug: 16,384 keys and 16,269 levels at depth 200, 16,341
  at 400; with the cache in `LinkOf` dropped (the review's mutant), 1,216,115 and 3,075,705, and the
  test fails.
- **The key encodes exactly.** Every segment whose length varies is now preceded by its length (the
  failed-call key, each section list, the run of levels made at the position); every other segment
  has a length fixed by a value written before it (a return node fixes the repeats and passes
  around its site, `AddStart`'s first value fixes its own length, the pattern fixes a guard
  entry's section part). So a key decodes one way. Before, the counts trailed their segments.
- **Item 11, the section frame off the chain, is reachable.** The review read it as unreachable
  (END_FUZZY pops only the top frame). It is reached: `(?:[ab](?R)?a+|a+){i<=1,d<=2}` over
  'ababbbcbaaab' gives 4 such keys, with or without the eager switch. No mechanism is named yet, so
  it stays a switch-off, not an assert (an assert would fail on that row), and the comment at
  `CallContinuationMemo.FrameIndex` now says so. No witness of a wrong answer: mapping it to "no
  section", as the first build did, changed no answer on any grid.
- **The late-start guard (item 10) has no witness, and none can exist.** A start after the
  position its call was made at is stale: matching moves forward here (the memo is off in reverse),
  so every iteration or pass on the path began at or before the position, and a start the path
  did not write is from an abandoned branch. Such a start is never read before it is written
  again: a repeat writes `Start` when the path enters the iteration (Matcher.cs ~24539, ~24793,
  ~24866, ~25101) and its end reads only that; an optional pass's slot is written by its branch
  and read only at its end (`OpenOptionalPass` remarks, Matcher.cs ~11275). So keying such a level
  as `LinkOf` does, every start as "before the position", is already exact. The guard
  (`memo.LateLevels > 0` in `AlreadyFailed`, CallContinuationMemo.cs:353) only switches the memo
  off and keeps the assert in `AddStaticLevel` (~:840) literal; it was found by that assert, not by
  an answer. The review's mutant without it: 0 diffs, with 92,684 hits taken while a late level
  was open.
- **The re-opened call's position and section (item 12)** are correct by construction: both come
  from the call's own record on the backtracking stack (Matcher.cs ~22078), and `BuildKey` asserts
  on every key that a level's section is the guard's own record of it (CallContinuationMemo.cs:709);
  before the fix that assert fired on `calls.jsonl` rows. The position feeds only the late-start
  switch-off above, so it cannot change an answer. No answer witness was found (review mutant 4,
  two seeds, 0 diffs); the assert is the check, and the Debug grids run it on every key (281,498
  keys on the counts grid alone).

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
