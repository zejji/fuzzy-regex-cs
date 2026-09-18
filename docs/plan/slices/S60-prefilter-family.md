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

10. **A reject-only prefilter for fuzzy sections** (added 2026-09-18 from the research sweep,
   `docs/plan/2026-09-18-optimisation-research.md` §1). Items 1-4 are unusable under a fuzzy
   section because an error can delete the required character. Navarro's pattern partitioning
   (ACM CSUR 2001, §8.1: a single edit "cannot alter both halves of the pattern") gives a filter
   that survives it: for a section with total budget `k`, split its literal part into `k+1` pieces;
   a window containing none of them (vectorised `IndexOf`) cannot hold a match and is skipped; the
   backtracker verifies everything else unchanged. Build beside the required-string analysis in
   `PatternObject.cs` (around `:253-276`, `GetRequiredChars` `:393`); consult at the search-start
   sites (`Matcher.cs:4725`, `:4793`); honour the slice-narrowing site (`Matcher.cs:10049`) so a
   `(*SKIP)`-moved position is never re-searched (item 5's rule). Not upstream code: it carries a
   `sync-divergence:` marker and a SYNC-DIVERGENCE.md row. Gate on `k` small relative to the literal
   length, by measurement (Navarro: filters are "very sensitive to the error level"). A Myers
   bit-vector second stage (reject a window whose minimum Levenshtein distance exceeds the budget)
   is optional and only built if stage one leaves too many candidates. Measure on a fuzzy no-match
   large-subject workload, which S58 must include for this reason.

11. **Fixed-distance sets at non-zero offsets, ranked and capped at three** (added 2026-09-18,
   `docs/plan/2026-09-18-optimisation-research.md` §2, .NET `RegexFindOptimizations`). mrab has one
   fixed-offset required string (`PatternObject.cs:179`); build the set-at-offset list from
   `Parsing/Nodes.cs:154-177` and use it only to reject start positions at `Matcher.cs:4718`/`:10046`,
   taking the slice from the same state as the slow path (item 5).
12. **Per-position minimum-length pruning and end-anchor fixed-length jump** (same source). `MinWidth`
   (`NodeCompiler.cs:156`, `MatchState.cs:580`) is checked once per attempt at `Matcher.cs:9238`,
   `:9395`, `:9739`; check it per candidate start too, and when the pattern ends in an anchor and has
   a fixed length, jump straight to `length - MinWidth`. Keep the `MaxErrors == 0` condition.
13. **Literal after loop, non-fuzzy only** (same source: "The loop doesn't overlap with the literal,
   so we can start from after the last place the literal matched."). Lands at `Matcher.cs:4718`/
   `:4772` with loop-node data from `NodeCompiler.cs`. Guard: `MaxErrors == 0`, greedy loop, no
   verbs; under fuzzy costing the loop's set can eat the literal by substitution.
14. **Multi-string leading search for literal top-level alternations** (same source), the same
   `SearchValues<string>` machinery as item 9 (`Parsing/Nodes.cs:1099-1116`); it must return the
   earliest position and leave branch choice to the engine.
15. **First-unit versus required-unit clearing** (PCRE2 `pcre2_study.c`: "Patterns such as /a*a/
   don't work if both the start unit and required unit are the same."). A correctness trap for item
   1: write the `a*a` test red before the locator ships.
16. **Start-code bitmap as the cheap form of item 4** (PCRE2 `set_start_bits`; .NET uses plain
   `IndexOfAny` up to five characters and `SearchValues` above). A 256-bit bitmap with an escape bit
   for values above 255, with PCRE2's caseless-pair collapse for `[Ww]ord`; measure against
   `SearchValues<char>` for wide sets and keep the faster.
17. **Leading `.*` auto-anchoring** (PCRE2 `pcre2perform`), small; `Optimiser.cs:21`; guard as PCRE2
   does (all top-level branches anchorable, DOTALL, not multiline, no `(*PRUNE)`/`(*SKIP)`) plus
   `MaxErrors == 0`.

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
- [ ] Items 8 to 17 landed, or each deferred with a row and a comment; item 15's `a*a` test is
  not deferrable.
- [ ] Ratchet and AOT green; blind review (hunt: a prefilter that searches below a position a verb
      committed past; a `SearchValues` built per call instead of per pattern; a reverse or
      case-folded arm that skips the fold; the underflow site clamped rather than handled; a
      partial match whose run-out position moved), commit.
