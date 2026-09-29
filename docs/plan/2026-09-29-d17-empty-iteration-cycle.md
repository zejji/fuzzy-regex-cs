# D17: an empty iteration that cycles a tested group

## The defect

A repeat goes round again after an iteration that read no text only if the iteration changed the
span of a group the pattern tests with a backreference or a conditional. That is upstream's rule,
kept by D12 (`EmptyIterationGroupProgressTests`). Nothing stops a body that flips such a group
between spans at one position:

```
^(?:(?=(?P=g)b)(?=(?P<g>ab))|(?=(?P<g>a)))*$   over 'ab'
```

Pass 1 sets `g` to (0, 1), pass 2 to (0, 2), pass 3 to (0, 1) again, and so on. Every pass is a
change, so the loop never ends. Upstream raises MemoryError; the port exhausts its backtrack stack.
No pass reads text and `$` cannot hold at 0, so the answer is no match (PCRE2 and Perl, D12 survey).

## The rule

An empty iteration is progress only if the state it leads to has not been reached before in the
same run of the repeat. A run is one entry into the repeat, with its fixed continuation. The state
is everything the rest of the match can depend on:

- the text position;
- the count, clipped to the minimum when the repeat has no maximum (counts past the minimum are
  alike then);
- the error counts, clipped where they stop mattering (below);
- the spans of every group the pattern tests (`GroupInfo.Referenced`), unset told apart from empty.

A revisited state is treated exactly as upstream treats an empty iteration that changed nothing:
the iteration stands, the body is not tried again from it, and the tail is. So the loop above
stops at pass 3 and the tail fails, and every alternative left on the backtrack stack is still
tried: `^(?:(?=(?P=g)b)(?=(?P<g>ab))|(?=(?P<g>a))|a|b)*$` over `ab` matches (0, 2).

Why this is exact where upstream terminates, and terminates where it does not:

- The state has not been reached before in this run on a path that ends: then nothing changes.
  Every D12 case is of this kind, so its 27 tests keep their answers.
- It was reached earlier on the current path (a cycle): the body from here can only produce the
  states it produced from there, which are already on the backtrack stack. Upstream loops for
  ever here, so no upstream answer changes.
- It was reached on an earlier path that has been backtracked (a sibling): that path's whole
  future, body and tail, was explored and found no match, or the search would have returned it.
  The same state has the same future, so skipping the body loses nothing.
- Every part of the key takes finitely many values within one run: the position is inside the
  subject, the count is clipped or bounded by the maximum, spans lie inside the subject, and the
  error counts are clipped (below). So a run of consecutive empty iterations at one position can
  reach only finitely many states, and a path that would go round for ever must revisit one.

With a bounded repeat the count keeps every state on a path distinct, so a bounded repeat ends
exactly where upstream's does; only sibling repeats are skipped.

## The error counts

A body with a fuzzy section in it charges errors on every pass, even when the groups cycle:
`^(?:(?=(?P=g)b)(?=(?P<g>ab))|(?=(?P<g>a))(?:x){d<=1})*$` deletes the `x` each time round.
END_FUZZY merges the inner section's counts into the open ones without checking the open section's
limits, so the counts rise without end and, keyed exactly, no state would ever repeat. The first
version of this fix keyed them exactly, and the error and cost totals too, and so still looped on
that pattern (review, 2026-09-29).

The rest of the match reads the counts only by comparing them, alone, summed or weighted into a
cost, with limits: the open section's minimums, maximums and cost limit (its END_FUZZY and each
edit's permission check); every enclosing section's, because each END_FUZZY adds the counts into
the section around it, which checks the sums at its own END_FUZZY; and the whole match's error and
cost limits, which only the ranking modes lower. Two counts at or above one more than the largest
finite limit among those give the same answer to every comparison, now and after any further
edits, because counts only rise, a sum containing either is at or above that too, and no cost
weight is negative. So the key clips each count there (`Matcher.ErrorCountCap`), taking the
largest limit of any section in the pattern (`PatternObject.LargestFuzzyLimit`, found once at
compile time) with the match's two. The first version of the cap read only the open section, so
inside `(?:(?:LOOP){d}...){d<=1}` it was 0 while the outer limit still told the states apart
(review round 2, 2026-09-29); no answer was found that it changed. That is exact, not a dominance cut: below the cap every
count is kept, as a minimum such as `{1<=e}` needs, since more errors can then be what makes a
match. With no finite bound anywhere, the cap is 0 and the counts drop out, which is exact because
nothing reads them.

The totals are not in the key. END_FUZZY recomputes them from the counts each time it runs, so
nothing still to come reads the old values.

## The mechanism

- `RepeatData.RunId`: a number drawn from a per-match counter when the repeat is entered
  (`GREEDY_REPEAT`, `LAZY_REPEAT`). It is saved and restored wherever upstream saves the repeat's
  count and start: the repeat's own backtrack entry, and `push_repeats`/`pop_repeats` around
  lookarounds, conditionals and group calls. Without it, a state recorded by one run would be
  read by another run of the same repeat with a different continuation (a nested repeat, or a
  repeat re-entered by recursion).
- `MatchState.EmptyIterationStates`: one set per match attempt, keyed by run id and the state
  above. It is emptied when the guards are reset for a new start position, and at the end of a
  call a set with room for more than 1,024 states is dropped rather than kept on the cached
  state. It is judged by its capacity, since the reset at each start position empties it without
  shrinking it.
- `END_GREEDY_REPEAT` and `END_LAZY_REPEAT` consult it only when the iteration read no text and
  the group half of `capture_change` moved, and set `changed = false` on a hit. A lookup fills the
  state's scratch array and allocates only when the state is new.

Existing memos considered:

- The repeat guards (`GuardList`) record positions only, and are off for a body that tests a
  group (`Optimiser`, Ref status), which is exactly where cycles happen.
- The failure memo (`RepeatInfo.FailureMemo`) is off in any pattern with a backreference or a
  conditional.
- The fuzzy repeat memo (`RepeatData.Memo`, `Matcher.RepeatMemoHit`, from F-A) is on main now. It
  runs only for an iteration that made a fuzzy edit, keys the position, the count, the raw counts
  and at most two tested groups, and is scoped to a run by being emptied on each entry; a hit drops
  the iteration. This record runs after it, and only for an empty iteration that changed a tested
  group. The two should later share one key (this one's, with the counts clipped and every tested
  group) and one per-repeat store, which would retire `RunId`; the two actions stay separate.

## Cost

- A pattern that tests no group: the counter's group half never moves (`Matcher.cs`, START_GROUP
  and END_GROUP bump it only for a referenced group), so the lookup is never reached. The run id is
  pushed only when `PatternObject.TestedGroups` is not empty: one field test per repeat entry and
  per `push_repeats`.
- An iteration that read text: one extra comparison (`TextPos == Start`) on a path already taken.
- An empty iteration that changed a tested group: one set lookup, and one small array when the
  state is new. `AllocationTests` pins both: a warm `(a)(?:\1|b)*c` allocates nothing, and
  `^(?:(?=(a))|a)*\1?$` over 2,000 characters allocates under 400 bytes per character (6.6 GB
  over 10,000 before the scratch key).

Measured 2026-09-29, Release, main (171a1a7c, the same code as 7cecdb35) against this branch, run
in turns on a busy machine, per call:

| Workload | main | this branch |
| --- | --- | --- |
| `(a)(?:\1\|b)*c` failing over 402 characters (`TestedGroupRepeat`) | 51-68 ms, best 50.9 | 50-65 ms, best 49.9 |
| `(\w+) \1` (control) | 39-55 µs, best 38.3 | 38-49 µs, best 37.5 |
| `(\w+\s?)+$` (control) | 4.1-5.4 ms, best 4.0 | 3.9-4.9 ms, best 3.9 |
| `^(?:(?=(a))\|a)*\1?$` over 16 `a`s and `bc` | 229 ms | 0.40 ms |
| the same over 20 | 3.5-4.4 s | 0.58 ms |
| the same over 200 (`EmptyIterationRecord`) | over 150 s, not finished | 35-39 ms |

The first three are within the machine's noise. The last is the sibling rule at work: on main every
path through the `(?=(a))` and `a` choices is explored, doubling the time with each character; here a
state reached before on a failed path is not explored again. Both answer no match.

## Tests

`Gaps/Engine/EmptyIterationCycleTests`: the repro, a three-state cycle, cycles seen by a
backreference and by a conditional (in the body and in the tail), a nested repeat, lazy and `{3,}`
forms, a fuzzy section around the loop and after it, `(?r)` forms with lookbehinds and lookaheads,
the other alternatives still tried after a cycle, and six loops whose body also charges an error
on every pass. All 20 loop for ever without the check.

Each part of the key has a witness, a test whose answer changes when that part is left out
(checked by zeroing the part in a scratch build, 2026-09-29): the position, the count, the run and
the error counts. Every witness's expected answer is upstream's, since upstream
ends on each of them.

A `{1<=e<=1}` row pins that the counts matter under a minimum too.
