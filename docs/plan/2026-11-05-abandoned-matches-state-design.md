# Reusing the state of an abandoned `Matches` walk (b') (2026-11-05)

Design note for phase 11 item 13. Note only: nothing here is built. The checklist lives in
`STATE.md`.

## The problem

`FuzzyRegex.Matches` returns a lazy `MatchCollection`. Its walk (`Engine.Iteration.Enumerate`)
rents one `MatchState` from the pattern's one-slot cache (`MatchStateCache`) when it starts and
gives it back only when the walk ends: the last match found, an exception, or the enumerator
disposed. A caller that reads part of the collection and drops it never ends the walk:

```csharp
var first = regex.Matches(text)[0];          // walk paused after one match
var two = regex.Matches(text).Take(2).ToList(); // Take disposes its own enumerator, not the walk
```

The state stays with the collection until the garbage collector takes both, so the next call on
the same pattern finds the slot empty and builds a new `MatchState`. Measured on main 2026-11-04
(`2026-11-04-phase11-profile.md`, finding 4): `Matches(s)[1]` 653 ns and 3,032 B per call; the
same two matches through `EnumerateMatches` with a `break`, which returns its state, 301 ns and
416 B. `MatchState..ctor` was 25% of the call. D138 (merged into the integration branch) made a
fresh state cheaper but did not change who pays for one.

## How .NET does it

`System.Text.RegularExpressions.MatchCollection` holds no runner between matches (dotnet/runtime
main, `MatchCollection.cs` and `Regex.cs`, read 2026-11-05). Its fields are the regex, the input,
the matches found, `_startat`, `_prevlen` and `_done`. Each `GetMatch` calls
`Regex.RunSingleMatch(FullMatchRequired, _prevlen, _input, 0, _input.Length, _startat)`, which
takes the runner with `Interlocked.Exchange(ref _runner, null) ?? CreateRunner()` and puts it back
in a `finally`, after `runner.runtext = null` "to avoid keeping it alive in a cache". So an
abandoned collection costs .NET nothing, and every match pays one exchange and one store.

## What a paused walk of ours carries

Between two matches the walk carries, besides the fixed call arguments (bounds, overlapped,
partial, direction, limits):

- **The resume point:** `TextPos` and `MustAdvance`, set by `MatchState.AdvancePastMatch` from
  `MatchPos`, `AttemptStart` and `Overlapped`. Everything else an attempt reads is reset at its
  start (`init_match`'s port: stacks, groups, guards through `ResetGuards`, fuzzy counts,
  `SearchAnchor = TextPos`).
- **Per-text caches, built lazily and kept for the whole walk:** `_characterIndex` (the
  codepoint-to-UTF-16 map for text with astral characters, O(n) to build), the scan facts
  `(_cleanStart, _cleanEnd, _highSurrogateFound)`, `LastSubstitutablePos`, `CaptureTexts`, and the
  prefilters' own per-text knowledge. Losing them never changes an answer; rebuilding them per
  match can change the cost class: an O(n) index rebuilt for each of m matches is O(n m).

## Options

**(a) Keep today's behaviour.** No risk. An abandoned walk costs one fresh state on the next call
(about 350 ns and 2.6 KB measured before D138; to re-measure on the integration branch).

**(b) The .NET model: rent and return per match.** The collection keeps the resume point and the
per-text caches in its own fields; each match rents a state, attaches them, matches, detaches,
returns. Pros: abandonment costs nothing, no string pinned. Cons: one `Init` per match (it clears
about fifty fields and collections) plus two interlocked operations, on every match of every
`Count` and `foreach`. `\w+` Count over 100 KB of English-like text takes 1.38 ms (measured
tonight in `.scratch/d141r/harness`), tens of nanoseconds a word, so a per-match `Init` is a cost
the common path would feel; it would have to be measured, but the trade runs the wrong way: it
taxes every full walk to help the abandoned one. Moving the caches out of `MatchState` is a wide change to
a state object that `MatchStateCacheTests` guards field by field.

**(b') Park the state when a read pauses, reclaim it when reading resumes.** The walk keeps its
state while it is being driven, exactly as now. When a read returns with the walk still live, the
collection advances the state past the match it just found (`AdvancePastMatch`, so the resume point
is final), puts it in the pattern's slot and keeps a reference. The next read that needs more
matches first tries `Interlocked.CompareExchange(ref slot, null, parked)`:

- the same state is still there (nobody else called the pattern): resume with nothing rebuilt;
- another call took it: rent any state, `Init` it for the walk's text and bounds, and restore the
  resume point (`TextPos`, `MustAdvance`). The per-text caches are rebuilt lazily, once per such
  steal, not once per match.

Where it parks matters for cost. Parking on every `FindUpTo` would put two interlocked operations
on every `MoveNext` of a `foreach`. Instead park only (1) when the indexer returns with the walk
live and (2) when an enumerator is disposed with the walk live; a `foreach` drives the walk
through one enumerator and parks once, at its end. `Count` runs the walk to its end and never parks.
LINQ's `First`, `Take`, `ElementAt` dispose the enumerator they used, so they park.

Risks, each with the check that would settle it:

1. **Exactness of the steal path.** A field the next attempt reads that neither `Init` nor the
   restore sets would make a stolen walk answer differently from an unbroken one. Check: a test
   hook that forces a steal at every park, run as a differential over the oracle corpora and the
   ported suite (every walk broken at every match must give the same matches as an unbroken walk),
   plus `MatchStateCacheTests`' field audit extended to name each field "reset by Init",
   "restored on steal" or "per-attempt".
2. **String pinning.** A parked state references the walk's text, so the pattern pins the input
   until its next call. .NET drops the text for exactly this reason. A pattern kept in a static
   field and called once with a 100 MB string would hold it for good. Choices: drop `Text` and
   `_characterIndex` when parking and re-attach the text (the collection holds the string anyway)
   on reclaim, paying the index rebuild once per paused read on astral text; or park only below a
   text-length bound. The first keeps memory behaviour identical to today's after a walk ends.
3. **Quadratic resumption.** `for (int i = 0; ; i++) coll[i]` with no `Count` parks after every
   match. With the caches dropped at park (risk 2) an astral text then rebuilds its O(n) index per
   match. Check: the D90-style cost grid (time against n and m, Release) on astral text before
   choosing what parking drops.
4. **Threads.** The collection's lock already serialises advancing; the parked reference is
   read and written under it. A steal by another call is the ordinary cache race the slot already
   handles (`Interlocked.Exchange`).

Expected gain, to measure before building: `Matches(s)[1]` and `First()` on a reused pattern from
the fresh-state cost to the cached-state cost (653 to about 301 ns on main before D138), nothing
on `Count` or a whole `foreach`, and two interlocked operations per paused read.

## Recommendation

Not now. (b') is the right shape if partial reads of `Matches` on a reused pattern turn out to be
common in real use: it keeps the full-walk paths as they are and moves the cost only for a pause.
But its gain is one fresh state per abandoned read, D138 has already cut that state's cost, and
the exactness proof (risk 1) is the expensive part. Order: re-measure `Matches(s)[1]` on the
integration branch after D138; if the gap to the reused-state path is still above about 150 ns or
1 KB, prototype (b') behind the forced-steal differential and the cost grid; otherwise close the
item with (a). Meanwhile `EnumerateMatches`, which returns its state when disposed, is the route
`docs/GUIDE.md` already points to for a walk that needs neither `Count` nor random access.
