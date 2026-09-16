---
slice: S53b
phase: 6
title: API completeness before optimisation - lazy enumeration, the three inline-only flags, the dictionary view of groups, and slicing on Replace
delivers: []
---

# S53b - API completeness before optimisation

**Per-sitting record: `docs/plan/slices/notes/S53b-sittings.md`.** Sitting 1 landed all five scope
items green and left only the independent verifier; read that file before re-reading this spec.

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

- [x] `EnumerateMatches`, `EnumerateSplits` shipped with sequence-equality and early-exit tests.
- [x] `Ascii`, `Unicode`, `Word` options; `Options` reports them; bytecode identity proven.
- [x] `GroupCollection` implements `IReadOnlyDictionary<string, Group>`.
- [x] `Replace`/`ReplaceFormat` take `beginning`/`length`, oracle-proven.
- [x] DIVERGENCES, PORTMAP, DECISIONS, OPTIMISATION-NOTES, S54 workload list updated.
- [x] Ratchet GREEN, blind review, verifier, commit.

## Closing notes (sitting 2, 2026-09-16)

Two sittings. Sitting 1 landed all five scope items green and left one claim unverified; sitting 2
ran only that claim and closed the slice. Per-sitting detail is in
`docs/plan/slices/notes/S53b-sittings.md`; this is the durable record.

### What landed

1. **Lazy enumeration.** `FuzzyRegex.EnumerateMatches(input, beginning, length, overlapped, partial,
   timeout, cancellationToken)` and `EnumerateSplits(input, maxSplits, timeout, cancellationToken)`,
   instance and static. `Engine/Iteration.Enumerate` is a `yield` loop over a new `Iteration.Step`,
   which is `Next`'s body generalised - so `Match.NextMatch` and the lazy walk are one method and
   `Next` is a four-line wrapper. **One engine state per STEP, not per walk, for disposal rather
   than fidelity**: a state owns rented buffers, so one held across a `yield return` is one an
   abandoned `foreach` never returns. Both consequences are recorded rather than hidden - the
   per-match pass over the subject (`OPTIMISATION-NOTES.md`, two rows, Phase 7 lift) and the
   per-step rather than per-walk `timeout` (`DIVERGENCES.md`, API shape). Arguments are validated
   eagerly in the non-iterator body.
2. **`Ascii` (0x80), `Unicode` (0x20), `Word` (0x800) on `FuzzyRegexOptions`.** `_unexposedFlags` is
   derived from the enum and shrank by itself; `LOCALE`, `DEBUG`, `TEMPLATE` stay hidden. Ten
   assertions across five files moved and every one moved TOWARDS upstream, because `Options` now
   reports the `UNICODE` that `_main._compile` ORs into every text pattern.
3. **`GroupCollection : IReadOnlyDictionary<string, Group>`.** A source break, taken deliberately
   before 1.0; `Groups.Values` is the documented fix at a call site.
4. **`beginning`/`length` on `Replace` and `ReplaceFormat`**, all overloads plus the statics, with
   the text outside the slice copied through unchanged.
5. **Records**: DIVERGENCES, PORTMAP, DECISIONS, OPTIMISATION-NOTES and S54's workload list.

### Three slice-file claims were wrong and are corrected in DECISIONS

.NET's `GroupCollection.Keys` is TOTAL, not named-only (`[0, 1, a, c]` for `(?<a>a)(b)(?<c>c)?`,
measured on .NET 10.0.10); `EnumerateSplits` can take no slice, because upstream's `pattern_split`
takes none (`_regex.c:22235`); upstream's inverted `pos>endpos` is unspellable through
`beginning`/`length`, and `(3, 0)` gives upstream's answer for the reason a caller expects.

### Negative controls - how to re-run them

Both were re-run against the code and generators committed at the sitting-1 checkpoint, after the
last engine change. Sitting 2 changed no code, so the numbers stand.

> **Control A, `sub-keeps-only-the-slice`**: in `src/FuzzyRegex/Engine/Substitution.cs`, the block
>
> ```csharp
>         // The segment following the last match.
>         int endPos = state.Reverse ? 0 : input.Length;
>         if (lastPos != endPos)
>         {
>             joined.Add(state.Reverse ? input[..lastPos] : input[lastPos..]);
>         }
> ```
>
> becomes
>
> ```csharp
>         // The segment following the last match.
>         int endPos = state.Reverse ? start : end;
>         if (lastPos != endPos)
>         {
>             joined.Add(state.Reverse ? input[start..lastPos] : input[lastPos..end]);
>         }
> ```
>
> Wave: `pwsh -File tools/run-oracle.ps1 -Generator substitution -Count 600 -Seeds 7`.
> Result: 563 agree, **37 diverge** of 600. Re-run at seed 55: **23 diverge** of 600.
> Unbroken, the same waves are 0 of 600 at both seeds.

> **Control B, `lazy-walk-forgets-overlapped`**: in `src/FuzzyRegex/Engine/Iteration.cs`, inside
> `Enumerate`'s `while` loop, the `Step(...)` call's fifth argument `overlapped` becomes the literal
> `false`.
> Wave: `pwsh -File tools/run-oracle.ps1 -Generator iteration -Count 600 -Seeds 7`.
> Result: the ordinary comparison stays **0 diverge of 600** - the eager answer is untouched - and
> `The_lazy_walks_answer_exactly_what_the_eager_ones_do` fails with **28 of 600** iteration rows
> disagreeing. Re-run at seed 55: **36 of 600**. Unbroken, it passes at both.

**There is deliberately no control for the `boundaries` flags-integer widening.** The flags integer
and the inline prefix reach the same compiler through `(int)options` with no second code path, so a
break would fire on both spellings and the control would measure nothing. That item's evidence is
bytecode identity (`Gaps/Api/EncodingAndWordOptionTests`) plus the measured upstream answers.

### Review

**Blind pass over the whole diff: 0 findings.** The reviewer ran 20,850 lazy-vs-eager comparisons,
69,168 `sub`-with-a-slice rows against upstream, an `IReadOnlyDictionary` contract sweep over 13
patterns including branch reset and duplicate names, and both spellings of the six `boundaries`
prefixes on both sides, and reported no reproducible finding. **No finding was raised, so none was
reproduced and none was fixed; no second pass was needed, because nothing went unreviewed.**

Two things it surfaced that are not findings: the C comment at `_regex.c:22091` is wrong (it says
`text_length` "is truncated to `slice_end`", while `state_init` sets it to the whole subject's
length, `:18439` - this port follows the code), and an `Options` disagreement on inline `(?V0)`
predates this slice. Both carried in STATE.md.

### Verifier

Eleven claims, over two sittings, all CONFIRMED. Sitting 1's verifier confirmed ten - the three
probes' outputs, the four upstream flag/`findall` measurements, the recorder's seven row statistics,
the ratchet, and both negative controls at both seeds - and ran out of deadline before the full
oracle. Sitting 2 re-dispatched a fresh verifier against the committed tree for that one claim:
`pwsh -File tools/run-oracle.ps1` with every default (seeds 7, 4242, 20260916; 22 generators; 300
rows; Release), **0 diverge at each seed** - agree 6342/6347/6354 of 6380, expected 32/28/21,
timeout 2/0/0, resource 4/5/5 - and "Oracle: GREEN - no row diverged from upstream, at all 3 seeds."
Ratchet re-run on the closing tree: GREEN, 6226/6226, baseline 6118.

### For the next slice

- The API is frozen from here (`PublicApiAnalyzers`, commit `f3c1135`), so S54's baselines measure
  the shape this slice settled. S54's workload list already names `EnumerateMatches` beside
  `Matches` on the 1 MB subject.
- `EnumerateMatches`' per-step state is the first Phase 7 lift in `OPTIMISATION-NOTES.md`, and the
  owner's ground rule is that the pooled-state-versus-`ref struct` decision waits for S58's numbers.
