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
