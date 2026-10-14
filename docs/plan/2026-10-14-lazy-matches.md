# Lazy `Matches()` (2026-10-14)

Owner decision: DECISIONS 2026-10-14, "`Matches()` becomes lazy, as .NET's MatchCollection is".

## What .NET does

`System.Text.RegularExpressions.MatchCollection` (dotnet/runtime `src/libraries/System.Text.RegularExpressions/src/System/Text/RegularExpressions/MatchCollection.cs`,
read at commit 4e7eb8c7, 2026-04-17) keeps a `List<Match>` of the matches found so far, the next
start (`_startat`), the previous length (`_prevlen`) and a `_done` flag. `GetMatch(i)` returns
`_matches[i]` when it is already there, and otherwise calls `RunSingleMatch` once per missing match
until it has `i + 1` or the scan fails. `Count` calls `GetMatch(int.MaxValue)`, so it runs to the
end. Each `RunSingleMatch` is timed on its own. The type is not thread safe: the docs say result
objects "should be used on a single thread ... callers must serialize access to them"
(learn.microsoft.com, "Thread safety in regular expressions", read 2026-10-14).

## What this port does

The collection holds:

- the matches found so far, in a `MatchList` (chunks of 1,024, as now);
- the walk that finds the rest: the enumerator `Iteration.Enumerate` returns, which owns one
  `MatchState` for the whole walk, exactly as `Scan` did, so every rule the scan carried from one
  match to the next (the must-advance rule, the overlapped step, a `(*SKIP)` that moved the slice
  start, the failure memo, what the state learnt about surrogates) carries over unchanged;
- a lock, taken to find more matches and by `Count`;
- the exception that stopped the walk, if one did.

Reading a match already found by index or `foreach` takes no lock. `Count`, and finding more, take the lock and runs the walk one
match at a time until the caller has what it asked for. When the walk ends it is disposed, which
gives the state back to the pattern's cache. `Count` runs to the end.

Why not .NET's shape of a fresh engine state per match, carrying only the next start? Because
this engine's state carries more than a position between matches: `(*SKIP)` moves `SliceStart` for
the rest of the scan (`Iteration.cs` class remarks, measured against upstream 2026-09-11), and
`Match.NextMatch`, which does build a state per step, would have to carry every such field. Holding
the walk's own state is exact by construction: it is the loop the oracle already proves equal to the
eager scan (`OracleWaveTests.The_lazy_walks_answer_exactly_what_the_eager_ones_do`).

## Thread use

The README already promises that a `MatchCollection` can be read from several threads at once
(each with its own `foreach`). A lazy collection keeps that promise, which is stronger than .NET's:

- a match already found is read without a lock. The list publishes its count with
  `Volatile.Write` after the slot is written, and a reader reads the count with `Volatile.Read`
  first. A new chunk array or a grown first chunk is published by an object assignment, which the
  .NET memory model makes a release for the array's elements (`docs/design/specs/Memory-model.md`,
  "Object assignment"), so a reader that sees the new array sees its copied elements;
- finding more is serialised by the lock, so two threads never drive one engine state at once.
  Without it, two threads could each dispose the same state and hand it to two later callers of
  the same pattern, which would corrupt answers far from the collection.

## Timeouts and cancellation

The accepted price: `Matches()` does no matching, so it can only throw for bad arguments or a token
already cancelled. A timeout or cancellation surfaces from `Count`, the indexer or enumeration,
whichever needs the match the walk was looking for. The budget bounds each match (the walk restarts
its clock per step, as `EnumerateMatches` does and as .NET's `RunSingleMatch` does), not the whole
scan.

After a walk has thrown, its state is disposed and the exception is kept. Matches found before it
stay readable; any read that needs a later match throws the same exception again, as `Lazy<T>`
does with a cached exception. Retrying is not offered: the state that threw is gone, and a fresh
state cannot be put back exactly where the old one was (see above).

## Worst-case cost on paper

- `Matches()`: O(1). Argument checks and one small object; no state is rented.
- Reading match `i` already found: O(1), no lock, no allocation.
- Reading match `i` not yet found: one engine step per missing match, each bounded by the budget.
  The total is what the eager scan spent on the same prefix, plus a lock per call and an iterator
  step and a clock restart per match (the clock read happens only when a timeout is set).
- `Count`, first call: the eager scan's cost plus those per-match constants. Later calls: O(1),
  one uncontended lock.
- Memory: the matches found, as before. A collection read only part way holds its state (and the
  buffers the state rented) until the collection is collected. The buffers then go to the collector
  rather than back to `ArrayPool<byte>.Shared`, which the pool's contract allows ("Failure to return
  a rented buffer is not a fatal error", `ArrayPool<T>.Return` docs); the pattern's cache simply
  builds a new state for its next call.

## Adversarial inputs

- **Two easy matches, then an exponential cliff** (`LazyEnumerationTests`'s `(a|a)*\1?(?:b|\b\B)`
  over `abab` + 26 `a`): the eager collection timed out on construction. Now `[0]` and `[1]` answer
  in a few dozen engine steps and `Count` times out.
- **A caller that catches the timeout and asks again in a loop**: each read rethrows the kept
  exception without matching, so a retry loop costs nothing per turn.
- **Many threads reading one fresh collection**: readers of found matches never wait; readers past
  them queue on the lock, and each match is still found exactly once.
- **A collection read part way and abandoned, per call, in a hot loop** (the `MatchesFirstTwo`
  row): each call builds a fresh state because the abandoned one never returns to the cache. This
  is the cost to measure; it is bounded by one `MatchState.Create` per call.

## Engine assumptions (VERIFICATION rule 10)

1. `Iteration.Enumerate` yields the same matches in the same order as the eager `Scan` loop, with
   the clock restarted per step: same loop body, `Iteration.cs` `Enumerate` against `Scan`; witnessed
   by the oracle's lazy-equals-eager test and the main-versus-branch differential.
2. A `Match` copies everything it reports out of the state, so the state can move on after it is
   built: `FindAll` relied on this already for every match but the last.
3. Only one thread drives the walk at a time: asserted with `Debug.Assert(_gate.IsHeldByCurrentThread)`
   where the walk is advanced.
4. A match below the published count is in place before the count is published: asserted in
   `MatchList.Add` order and witnessed by the many-threads test.

## STATE (2026-10-14, 13:00)

Built, tests added, every gate run on the uncommitted tree that the WIP commit records. Logs are
under `.scratch/lazy-matches/` in the main checkout.

- Red before the fix: 6 of 20 new tests failed on main (`red-new.log`). The thread test failed 3 of
  3 runs with the lock removed (`m1-run1.log`..`m1-run3.log`); the per-step clock test, `Matches`
  case included, failed with `RestartClock` removed (`m2-run.log`).
- Ratchet GREEN with the baseline updated (`ratchet2.log`; final run before the commit in
  `ratchet3.log`).
- Doc examples: 124 ok (`docs.log`).
- Oracle GREEN at seeds 7, 4242, 20260927 (`oracle.log`).
- AOT tests GREEN, AOT smoke GREEN, binary 7,611,392 bytes (`aot-tests.log`, `aot-smoke.log`).
- Differential against main `fc5b190c`: 260,000 rows (s62 rows-c, rows-d, and rows-e, which is
  rows-c as `finditer`/`finditer-overlapped`), identical by `cmp`, no collection-read
  disagreements; the probe reads each collection by enumeration, by index or by `Count` first in
  turn. 153,571 finditer rows, 60,060 with matches, 18,252 with several, 30,115 overlapped,
  41,378 partial, 770 `(*SKIP)` with several matches, 6,260 reverse, 4,041 fuzzy (`gates.status`,
  `out-*.txt`).
- BDN pinned A/B: see OPTIMISATION-NOTES, "Measured by the lazy MatchCollection"
  (`bdn-cmp-M.log`, `bdn-cmp-Ref.log`).

Left: owner review and merge. Not done: the AOT binary size against main (only the branch was
measured), and a DECISIONS line recording that the collection is safe to read from several threads
and rethrows a kept exception (both choices are in this note and the XML docs).

