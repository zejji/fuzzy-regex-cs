# D71: where a partial second pass's fork time goes

Design note, 2026-11-17. Checklist item 15 (D71). What the fork budget is and why it exists:
D11 (M4) and the D71 row of `docs/KNOWN-DEFECTS.md`.

## The witness, measured

X1, `(?:(?=(.*)z|())a)*b\1\2` over `'a' * n + 'c'`, partial search. Release library (checked: the
built `FuzzyRegex.dll` is optimised), tiered compilation off, indicative (no temperature monitor),
median of the runs in 2 s, `.scratch/d71/x1loop.cs`:

| n | answer | ms per search |
|---|---|---|
| 100 | P(101, 101) | 14.2 |
| 200 | P(201, 201) | 57.9 |
| 300 | P(301, 301) | 137.1 |
| 400 | P(0, 401) | 338.6 |

Quadratic up to the budget. At n = 400 the per-attempt fork budget fires and the answer becomes the
C5 phantom P(0, n + 1), upstream's answer at every n.

## The profile

WPR CPU sampling (the owner's scheduled tasks), n = 300, 10 s, 10,524 samples in the process.
Inclusive shares of the matcher's time:

| Frame | Share |
|---|---|
| `EndALookaroundInTheSecondPass` (the end of each lookahead body in the second pass) | 36.6% |
| `KeyedTailGuards.Find` (continuation and tail-guard lookups) | 23.3% |
| of which `TailKeyValues..ctor`, hashing the key array | 14.1% |
| of which `Dictionary.FindValue` | 5.9% |
| `MatchState.PopContinuationMark` (recording a failed continuation) | 18.9% |
| `AdvanceGreedyRepeatOne` / `CountOne` (the body's `.*`) | 17.9% |
| `KeyedTailGuards.ForWrite` | 16.5% |
| `MatchState.ContinuationFailed` | 16.2% |
| `MatchState.ContinuationKeyOf` | 8.9% |
| `MatchState.CutBodyKeyOf` | 7.5% |

The `.*` scan is inherent: the lookahead rescans to the end of the text from every position, so
every backtracking engine is quadratic on this pattern. The memo work around it is not inherent,
and it is about 60% of the time: every fork builds a continuation key, hashes it to look it up
(`ContinuationFailed`), and when the continuation fails pops the key off the backtracking stack and
hashes it again to record it (`PopContinuationMark` into `ForWrite`). The same key is hashed twice,
and the hash walks the whole array with `HashCode.Add`.

## Options

**A. Hash a key once.** `ContinuationKeyOf` computes the hash as it fills the key; the mark carries
it on the backtracking stack (8 bytes) beside the key; `Find` and `ForWrite` take a
`TailKeyValues` built with that hash. Removes one of the two hashes per fork and the hash at the
pop. Expected: most of the 14% hashing share, measured not assumed.

**B. A cheaper hash for `long[]` keys.** One multiply-rotate pass over the span instead of
`HashCode.Add` per element. Smaller on its own; combinable with A. Needs a collision check on the
key shapes the memos see (positions and spans, small integers).

**C. Stop memoising where it does not pay.** If the continuation memo never hits on shapes like X1,
its whole cost (about 60%) is overhead there. A per-pass switch that turns the continuation memo off
after K writes with no hit would recover it, but it reintroduces the exponential cases D104 exists
for whenever the first K writes happen not to hit: needs the D104 witnesses and a grid before it is
safe. Measure the hit rate first.

**D. Change the budget.** Raising the budget moves the point where the answer flips to the phantom;
lowering it makes the phantom more common. Neither is a performance fix, and the flip itself is
D11's accepted fallback, so this is out of scope here.

## Recommendation

Measure C's premise first (the continuation memo's hit rate on X1 and on the D104 witnesses, a Debug
counter). Build A regardless: it is local, changes no answer, and removes work the profile shows.
Take B only if A's re-profile still shows the hash on top. Every change gets the usual gates
(ratchet, oracle 3 seeds, differential against main, allocation check) and a before/after profile.

## After option A (2026-11-17, main ed333ada)

Option A merged: each continuation key is hashed once (`TailKeyValues.HashOf`, the hash carried on
the continuation mark). Hashes per X1 search halved, X1 5-10% faster. A second WPR profile of the
same run (n = 300, 10 s): the hash is now 7.0% inclusive, the dictionary lookups 9.3%, and copying
each looked-up key into the one-entry cache (`KeyedTailGuards.Remember`) 3.9%. The `.*` rescans are
still about 18%. What is left of the memo's cost is one hash and one dictionary lookup per key,
which the memo needs. Option B (a cheaper hash) and skipping the one-entry cache would each save a
few percent at most; neither is worth a change on its own. A larger gain needs fewer keys, not
cheaper ones: building no key where a lookup cannot hit, which is research, not a local change.
D71 stays open on that.
