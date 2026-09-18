---
slice: S60
phase: 7
title: The start optimisations - locate_required_string and the search_start family, on SearchValues and vectorised IndexOf
delivers: []
---

# S60 - The prefilter family, without importing upstream's answers

The research's first-ranked target and the only algorithmic one: a prefilter skips positions rather
than making the inner loop faster, six arms are already ported and waiting behind "unreachable
until Phase 7" comments (DECISIONS 2026-08-31), and `SearchValues<T>` with vectorised `IndexOf` is
its natural implementation - the swap .NET's own `RegexFindOptimizations` made. `src/` has **zero**
`SearchValues` uses today. S19 measured what this is worth: upstream's apparent instant answer on
`(a|a)*b` was `locate_required_string` rejecting the subject before the engine ran.

**The constraint is the whole slice.** ROADMAP, owner rule 2026-09-12: upstream's prefilters change
what upstream answers on `(*SKIP)` patterns and some partial matches, and those answers are wrong -
PCRE2 agrees with this port with its own optimiser on or off. `Gaps/Engine/BacktrackingVerbTests.cs`,
`PartialMatchingTests.cs` and `ReverseMatchingTests.cs` are **permanent**: a red one means an
upstream bug was ported, and the fix is to make the prefilter honour the slice a verb moved, the way
upstream's own slow path does, never to invert the test.

## Scope

1. **`locate_required_string` and the required-string search** (`Matcher.cs:197`, `:4786`,
   `:7149-7583`): the `string_search`, `_fld`, `_ign`, `_rev` variants whose arms are ported and
   unreachable, plus `string_search_rev` (`:8715`). Wire the required-string case flags
   `PatternObject.cs:176` already computes, and the search offsets `MatchState.cs:641` and
   `Node.cs:56` carry but nothing reads.
2. **`search_start` / `do_search_start`** (`Matcher.cs:388`, `:4718`, `:4772`, `:10046`, `:10157`),
   including the underflow site the notes mark at that last pair - handle it explicitly and pin it
   with a test, do not leave it to a clamp. `Optimiser.cs:508`: the match loop starts consulting the
   markings the optimiser already makes, which is why the graph was given upstream's shape.
3. **`try_match`'s test-node fast arm** (`Matcher.cs:5404`), reachable once the locator exists.
4. **Implemented with the library primitives, not by hand.** `SearchValues<char>` built once per
   compiled pattern (built once, searched many times - exactly the shape `SearchValues` wants) and
   `IndexOf`/`IndexOfAny`/`IndexOfAnyInRange` wherever upstream scans a character at a time. No
   hand-written SIMD: `MatchState.cs:501` already shows the house pattern. `AllowUnsafeBlocks`
   stays unset.
5. **The verb constraint, implemented not asserted.** The prefilter takes the current slice from
   the same state the slow path does, so a position a `(*SKIP)`/`(*PRUNE)` moved past is never
   re-searched below. Write the pinning test **before** the change and prove it fails without it -
   the fixture is the three permanent files plus one new case per verb the prefilter can reach.
6. **Encoding arms** (`Unicode/Encodings.cs:166`, `:177`): `same_char_ign_turkic` inside
   `string_search_fld`, and the `*_has_property_ign` table slot whose only other caller is
   `search_start`. They come with the family or they stay deferred with a row; they do not get
   half-wired.
7. **Anything not landed is deferred explicitly**, with a `ponytail:`/`Phase 7` comment at the line
   and an OPTIMISATION-NOTES row; rows for what did land are deleted with their comments in the
   same commit. If a sub-part is dropped for size, say which and why in the closing notes - this
   family is large and a partial landing is acceptable, a silent one is not.

8. **Rarity gate on the skip character** (added 2026-09-18 from the fuzzy-regex-rs review,
   `docs/plan/2026-09-18-fuzzy-regex-rs-techniques.md` #3). When the prefilter has a choice of
   which required character to vectorise on, prefer the rarest by a small precomputed frequency
   table, and skip the prefilter entirely when the subject is shorter than a measured break-even.
   A skip-choice cannot change an answer; measure it on the no-match large-subject workload, which
   is the slow path both libraries share, and on short subjects, where S58's floor decides whether
   the gate itself costs more than it saves.
9. **Named-list membership above a threshold** (same review, #7). `\L<name>` lists are tested per
   character through `Matcher.InSetUnion` (`Matcher.cs:536`); a list larger than a threshold gets a
   per-list `SearchValues<string>` or trie built once at compile time (`PatternObject.cs:103`,
   `:109`). Answer-identical by construction, but it touches match selection code, so it gets its
   own oracle wave over list-heavy patterns and a threshold recorded in the commit message. If it
   does not fit the slice's budget it is deferred with a row, not squeezed in.

## Verification

- Before and after, same machine, same session, driver idle:
  `dotnet run -c Release --project bench/FuzzyRegex.Benchmarks -- --filter '*' --job medium
  --exporters json --artifacts artifacts/bench/<date>-S60-<before|after>`, compared with
  `pwsh -File tools/compare-benchmarks.ps1` against the committed baseline at S58's noise floor.
  Per workload, time **and** allocated bytes/op. The literal-heavy, class-heavy, scan and
  backtracking workloads are where the win must show; a workload that regresses beyond the floor is
  triaged before the slice closes, not after.
- `pwsh -File tools/check-ratchet.ps1` GREEN.
- `pwsh -File tools/run-oracle.ps1` GREEN at its three default seeds, `ExpectedDivergences` strict.
  This is the slice where an optimisation is likeliest to change an answer, so the oracle is the
  gate, not a formality.
- The three permanent files run explicitly and named in the commit message, one command each:
  `dotnet run --project tests/FuzzyRegex.Tests -- --treenode-filter "/*/*/<Class>/*"` for
  `BacktrackingVerbTests`, `PartialMatchingTests` and `ReverseMatchingTests`.
- `pwsh -File tools/run-aot-tests.ps1` and `pwsh -File tools/run-aot-smoke.ps1` GREEN, binary size
  against 6,972,928 bytes; `SearchValues` is a plain sealed BCL class, so a size or warning change
  is a finding.
- No public surface change expected; if one appears, `pwsh -File tools/update-public-api.ps1` and
  justify it, because the API was frozen at S53b.

## Done when

- [ ] Required-string locator and the `string_search` arms live, with the case flags and search
      offsets actually read.
- [ ] `search_start`/`do_search_start` live, the underflow site handled and pinned.
- [ ] `SearchValues<char>` built per compiled pattern; vectorised `IndexOf` replaces the scalar
      scans; no hand-written SIMD, no `unsafe`.
- [ ] The verb-slice test written red first and green after; the three permanent files green
      unchanged.
- [ ] Oracle GREEN at three seeds; measured before and after per workload in the commit message.
- [ ] Every sub-part not landed carries a comment and an OPTIMISATION-NOTES row; landed rows and
      their comments deleted.
- [ ] Any structural divergence recorded in `docs/plan/SYNC-DIVERGENCE.md` with a
      `sync-divergence:` marker, `tools/check-sync-divergence.ps1` green.
- [ ] Items 8 and 9 landed, or each deferred with a row and a comment.
- [ ] Ratchet and AOT green; blind review (hunt: a prefilter that searches below a position a verb
      committed past; a `SearchValues` built per call instead of per pattern; a reverse or
      case-folded arm that skips the fold; the underflow site clamped rather than handled; a
      partial match whose run-out position moved), commit.
