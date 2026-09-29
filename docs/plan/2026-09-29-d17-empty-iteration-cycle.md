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
- the fuzzy counts of the open section, and the match's error and cost totals;
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
- Every part of the key only rises along a path, or is fixed for the run, so a path that goes
  round for ever must revisit a state, and the set is finite for a finite subject.

With a bounded repeat the count keeps every state on a path distinct, so a bounded repeat ends
exactly where upstream's does; only sibling repeats are skipped.

## The mechanism

- `RepeatData.RunId`: a number drawn from a per-match counter when the repeat is entered
  (`GREEDY_REPEAT`, `LAZY_REPEAT`). It is saved and restored wherever upstream saves the repeat's
  count and start: the repeat's own backtrack entry, and `push_repeats`/`pop_repeats` around
  lookarounds, conditionals and group calls. Without it, a state recorded by one run would be
  read by another run of the same repeat with a different continuation (a nested repeat, or a
  repeat re-entered by recursion).
- `MatchState.EmptyIterationStates`: one set per match attempt, keyed by run id and the state
  above. It is emptied when the guards are reset for a new start position.
- `END_GREEDY_REPEAT` and `END_LAZY_REPEAT` consult it only when the iteration read no text and
  the group half of `capture_change` moved, and set `changed = false` on a hit.

Existing memos considered:

- The repeat guards (`GuardList`) record positions only, and are off for a body that tests a
  group (`Optimiser`, Ref status), which is exactly where cycles happen.
- The failure memo (`RepeatInfo.FailureMemo`) is off in any pattern with a backreference or a
  conditional.
- The fuzzy repeat memo (`RepeatData.Memo`, `Matcher.RepeatMemoHit`) is on
  `maint/fuzzy-exact-deletion-tidy`, not main, and keys at most two tested groups and no run. When
  it merges, the two should share one key type: its key is this key without the run id, and its
  hit drops the path where this one only stops the loop.

## Cost

- A pattern that tests no group: the counter's group half never moves (`Matcher.cs`, START_GROUP
  and END_GROUP bump it only for a referenced group), so the lookup is never reached. The run id is
  pushed only when `PatternObject.TestedGroups` is not empty: one field test per repeat entry and
  per `push_repeats`.
- An iteration that read text: one extra comparison (`TextPos == Start`) on a path already taken.
- An empty iteration that changed a tested group: one allocation and one set lookup. This is the
  only path that pays, and it is the one that could not end before.

## Tests

`Gaps/Engine/EmptyIterationCycleTests`: the repro, a three-state cycle, cycles seen by a
backreference and by a conditional (in the body and in the tail), a nested repeat, lazy and `{3,}`
forms, a fuzzy section around the loop and after it, `(?r)` forms with lookbehinds and lookaheads,
and the other alternatives still tried after a cycle. All 14 loop for ever without the check.

Each part of the key has a witness, a test whose answer changes when that part is left out
(checked by zeroing the part in a scratch build, 2026-09-29): the position, the count, the run and
the open section's fuzzy counts. Every witness's expected answer is upstream's, since upstream
ends on each of them.

The error and cost totals have no witness. They change only when a fuzzy section closes, which
also changes the enclosing section's counts, and they matter only to the ranking modes; three
random searches over generated fuzzy and BESTMATCH patterns, about 20 minutes in all, found no
answer that depends on them. They
are kept because an extra part can only make the check cut less, never cut wrongly.
