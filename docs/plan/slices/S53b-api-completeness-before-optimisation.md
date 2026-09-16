---
slice: S53b
phase: 6
title: API completeness before optimisation - lazy enumeration, the three inline-only flags, the dictionary view of groups, and slicing on Replace
delivers: []
---

# S53b - API completeness before optimisation

Owner decision 2026-09-16 after the Fable review of `docs/DIVERGENCES.md`'s "Upstream members with
no port equivalent" (`docs/plan/2026-09-16-unported-members-review.md`). Four additive public-API
changes that are free before 1.0, awkward after, and that S54's baselines must measure in their
final shape. **No engine change.** Every item is verifiable with instruments that already exist:
the oracle (`finditer`, `splititer`, `sub` with `pos`/`endpos` rows), the compile-parity corpus
(flag bytecode identity), and S52b's concurrency tests.

Same-result-cheapest-route applies: each item is small, so batch the blind review and the verifier
over the whole slice, not per item.

## Scope

1. **Lazy enumeration.** `IEnumerable<Match> EnumerateMatches(string input, int beginning = 0,
   int length = -1, bool overlapped = false, bool partial = false, TimeSpan? timeout = null,
   CancellationToken cancellationToken = default)` and `IEnumerable<string?> EnumerateSplits(string
   input, int maxSplits = -1, int beginning = 0, int length = -1, ...)` on `FuzzyRegex`, plus the
   static conveniences with `(input, pattern, options)` like the others. `EnumerateMatches` is a
   `yield` loop over `Match`/`NextMatch()` (which creates and disposes one `MatchState` per step,
   `Engine/Iteration.Next`), so nothing is rented across a suspended iterator. `EnumerateSplits`
   yields exactly the pieces `Split` returns, nulls included, in the same order; the reversed case
   yields in the order `Split` returns them (this port's `Split` already settles that). `Matches`
   and `Split` stay eager; `Count` stays. Red-first tests: sequence equality with `Matches`/`Split`
   over the default wave's `finditer` and `split` rows; early exit after two matches on a 1 MB
   subject performs at most three engine steps (count them through a test hook or by timing
   against the eager call, whichever is cheaper and deterministic); timeout and cancellation
   propagate from the step they fire on. Record the per-step-state cost as a `ponytail:` note
   naming the Phase 7 lift (one state reused across the walk) in `docs/plan/OPTIMISATION-NOTES.md`.
2. **`Ascii`, `Unicode`, `Word` on `FuzzyRegexOptions`**, with upstream's bit values
   (`RegexFlags`); `Locale` and `Debug` stay out. `FuzzyRegex.Options` stops stripping them:
   `_unexposedFlags` shrinks accordingly and a pattern compiled as `(?w)\bx\b` reports `Word`.
   Proof, not assertion: extend the compile-parity corpus so that `flags=regex.WORD` (and `A`, `U`)
   and the leading inline form compile to identical bytecode, as Phase 2 proved for 28 `A`/`U`
   sites; add oracle rows with the flag passed as a flag. Check the ASCII/LOCALE/UNICODE
   incompatibility error still fires when two are combined as options. Decision D (2026-08-30) is
   superseded for these three members only; record that in DECISIONS.
3. **`GroupCollection : IReadOnlyDictionary<string, Group>`**, as `System.Text.RegularExpressions.
   GroupCollection` has been since .NET 5: `Keys` (named groups only, in number order, as .NET
   does), `Values`, `TryGetValue`, `ContainsKey`, and the `KeyValuePair` enumerator alongside the
   existing list enumerator (the class must pick one `IEnumerable<T>` for `foreach`; keep the
   `Group` one and expose the pairs through the dictionary interface explicitly, which is what
   .NET does). Tests: every `groupdict`/`capturesdict` assertion upstream has (fifteen lines,
   already ported through `Groups`) re-expressed through the dictionary view as well.
4. **`beginning`/`length` on `Replace` and `ReplaceFormat`** (all overloads, instance and static),
   with the same semantics as on `Matches`: replacements happen only inside the slice, and the
   text outside it is copied through unchanged, which is what upstream's `sub(pos, endpos)` does
   (`_regex.c:22157` onward; verify by running it). Oracle rows: `sub`/`subf` with `pos`/`endpos`
   drawn by the default generators. Closes the API-shape row "an unclosed gap".
5. **Records.** `docs/DIVERGENCES.md`: `splititer` row becomes `EnumerateSplits` SHIPPED; the
   `findall` note points at `EnumerateMatches` for lazy projection; `ASCII`/`UNICODE`/`WORD` row
   removed from the not-ported table (LOCALE and DEBUG rows stay); `groupindex`/`groupdict` rows
   name the dictionary view; the `Replace` slicing row is deleted from API shape; `purge`/
   `cache_all` row becomes PLANNED (Phase 7) with `CacheSize` as the port equivalent (decision
   only, no cache in this slice). PORTMAP rows for each. DECISIONS entry. `docs/PORTMAP.md`'s
   deliberately-not-ported table updated. S54's workload list gains `EnumerateMatches` on the 1 MB
   subject beside `Matches`.

## Verification

- Ratchet GREEN; oracle GREEN at three seeds with the new rows; compile-parity corpus GREEN with
  the flag-as-option cases.
- One blind review over the whole diff (hunt: an enumerator that holds engine state across a
  `yield`; a `Keys` order that differs from .NET's; `Replace` slicing that drops text outside the
  slice; an `Options` value that now exposes an internal bit), then one verifier pass re-running
  the parity corpus and the oracle summary.

## Done when

- [ ] `EnumerateMatches`, `EnumerateSplits` shipped with sequence-equality and early-exit tests.
- [ ] `Ascii`, `Unicode`, `Word` options; `Options` reports them; bytecode identity proven.
- [ ] `GroupCollection` implements `IReadOnlyDictionary<string, Group>`.
- [ ] `Replace`/`ReplaceFormat` take `beginning`/`length`, oracle-proven.
- [ ] DIVERGENCES, PORTMAP, DECISIONS, OPTIMISATION-NOTES, S54 workload list updated.
- [ ] Ratchet GREEN, blind review, verifier, commit.
