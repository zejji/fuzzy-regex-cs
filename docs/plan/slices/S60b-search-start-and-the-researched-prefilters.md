---
slice: S60b
phase: 7
title: The search_start family and the ten researched prefilter refinements S60 could not hold
delivers: []
---

# S60b - what S60 proved it could not land in one slice

S60 landed the required-string half of the prefilter family: `locate_required_string`'s forward
`Opcode.String` arm, `string_search`/`simple_string_search`, the per-pattern needle, the `(*SKIP)`
constraint implemented rather than asserted, and 19 gap tests (S60 sittings 2 and 3). Its scope
items **2, 3, 6, 8-14, 16 and 17** did not land, and sitting 3's arithmetic is why they are a slice
rather than a paragraph of prose: item 2 alone is upstream's `search_start`
(`upstream/src/_regex.c:8385`), the dispatcher that its `do_search_start` flag (`:588`) turns on,
over roughly thirty `search_start_*` functions (`:7859-8385`, one per boundary opcode plus a `_rev`
twin for each), and items 8-14, 16 and 17 are nine research-derived optimisations, each wanting its
own measurement against the noise floor.

**Everything here is already deferred in the open, not rediscovered.** Each carries a
`ponytail:`/`Phase 7` comment at its line and a row in `docs/plan/OPTIMISATION-NOTES.md` (S60 scope
item 7); this file is the slice that consumes those rows, and each one is deleted with its comment
in the commit that implements it.

**The constraint S60 was written under is unchanged and is the whole slice.** ROADMAP, owner rule
2026-09-12: upstream's start optimisations change what upstream answers on `(*SKIP)` patterns and
some partial matches, and those answers are wrong. `Gaps/Engine/BacktrackingVerbTests.cs`,
`PartialMatchingTests.cs` and `ReverseMatchingTests.cs` are permanent; a red one means an upstream
bug was ported, and the fix is to make the prefilter honour the slice a verb moved, never to invert
the test. S60's `PatternObject.HasSkipVerb` already withholds the start-position jump from any
pattern that can run a `(*SKIP)` (DECISIONS 2026-09-20), and every item below inherits that rule.

## Scope

Numbering keeps S60's, so a `ponytail:` comment that names "S60 item 11" still resolves.

Every line reference below was re-resolved against the tree on 2026-09-20, by symbol, because the
rows this slice inherited from S60 had drifted by hundreds of lines. Re-resolve them again at the
start of the sitting: the symbol names are the durable part, the numbers are not.

2. **`search_start` / `do_search_start`** (`Matcher.cs:394`, `:5095`, `:5149`, `:5184`), and the
   slice-narrowing site at `:10436-10441`, which carries upstream's issue-612 underflow - handle it
   explicitly and pin it with a test, do not leave it to a clamp. `Optimiser.cs:508`: the match loop
   starts consulting the markings the optimiser already makes, which is why the graph was given
   upstream's shape.
3. **`try_match`'s test-node fast arm** (`Matcher.cs:5794`, the arm's own "unreachable until Phase
   7" comment; upstream `_regex.c:7671`), reachable now the locator exists.
6. **Encoding arms** (`Unicode/Encodings.cs:189`, `:197`): `same_char_ign_turkic` inside
   `string_search_fld`, and the `*_has_property_ign` table slot whose only other caller is
   `search_start`. They come with the family or they stay deferred with a row; they do not get
   half-wired.
8. **Rarity gate on the skip character** (`docs/plan/2026-09-18-fuzzy-regex-rs-techniques.md` #3).
   When the prefilter has a choice of which required character to vectorise on, prefer the rarest by
   a small precomputed frequency table, and skip the prefilter entirely when the subject is shorter
   than a measured break-even. A skip-choice cannot change an answer; measure it on the no-match
   large-subject workload and on short subjects, where S58's floor decides whether the gate itself
   costs more than it saves.
9. **Named-list membership above a threshold** (same review, #7). `\L<name>` lists are tested per
   character through `Matcher.InSetUnion` (`Matcher.cs:542`); a list larger than a threshold gets a
   per-list `SearchValues<string>` or trie built once at compile time (`PatternObject.cs:103`,
   `:109`). Answer-identical by construction, but it touches match selection code, so it gets its
   own oracle wave over list-heavy patterns and a threshold recorded in the commit message.
10. **A reject-only prefilter for fuzzy sections** (`docs/plan/2026-09-18-optimisation-research.md`
    §1). Items 1-4 are unusable under a fuzzy section because an error can delete the required
    character. Navarro's pattern partitioning (ACM CSUR 2001, §8.1) gives a filter that survives it:
    for a section with total budget `k`, split its literal part into `k+1` pieces; a window holding
    none of them (vectorised `IndexOf`) cannot hold a match. Build beside the required-string
    analysis in `PatternObject.cs` (the fields at `:179-197` - `ReqOffset`, `ReqFlags`,
    `RequiredChars`, `ReqString` - and `GetRequiredChars` `:478`, called from
    `:313`); consult where the locator is consulted (`LocateRequiredString` `Matcher.cs:4927`, its
    one caller `:5173`); honour the slice-narrowing site (`Matcher.cs:10436`). Not upstream code:
    `sync-divergence:` marker and a SYNC-DIVERGENCE.md row.
    Gate on `k` small relative to the literal length, by measurement. A Myers bit-vector second
    stage is optional and only built if stage one leaves too many candidates.
11. **Fixed-distance sets at non-zero offsets, ranked and capped at three** (same source, §2, .NET
    `RegexFindOptimizations`). mrab has one fixed-offset required string (`PatternObject.cs:179`);
    build the set-at-offset list from the first-set walk (`Parsing/Nodes.cs:154-155`) and use it only
    to reject start positions at `Matcher.cs:5173` and `:10436`, taking the slice from the same state
    as the slow path.
12. **Per-position minimum-length pruning and end-anchor fixed-length jump** (same source).
    `MinWidth` (`NodeCompiler.cs:156`, `MatchState.cs:580`) is checked once per attempt at
    `Matcher.cs:9626`, `:9783`, `:10127`; check it per candidate start too, and when the pattern ends
    in an anchor and has a fixed length, jump straight to `length - MinWidth`. Keep the
    `MaxErrors == 0` condition.
13. **Literal after loop, non-fuzzy only** (same source). Lands in the string-search helpers
    (`SimpleStringSearch` `Matcher.cs:4686`, `StringSearch` `:4819`) with loop-node data from
    `NodeCompiler.cs`. Guard: `MaxErrors == 0`, greedy loop, no verbs; under
    fuzzy costing the loop's set can eat the literal by substitution.
14. **Multi-string leading search for literal top-level alternations** (same source), the same
    `SearchValues<string>` machinery as item 9 (`Parsing/Nodes.cs:1099-1116`); it must return the
    earliest position and leave branch choice to the engine.
16. **Start-code bitmap as the cheap form of item 4** (PCRE2 `set_start_bits`; .NET uses plain
    `IndexOfAny` up to five characters and `SearchValues` above). A 256-bit bitmap with an escape bit
    for values above 255, with PCRE2's caseless-pair collapse for `[Ww]ord`; measure against
    `SearchValues<char>` for wide sets and keep the faster.
17. **Leading `.*` auto-anchoring** (PCRE2 `pcre2perform`), small; `Optimiser.cs:21`; guard as PCRE2
    does (all top-level branches anchorable, DOTALL, not multiline, no `(*PRUNE)`/`(*SKIP)`) plus
    `MaxErrors == 0`.

**Sittings are expected, and the split is by item, not by file.** Item 2 is one sitting on its own.
Items 8-14, 16 and 17 are independent of each other and of item 2: each is landed, measured and
committed on its own, and any one of them may end in "measured, not worth it, reverted, recorded" -
that is a result, not a failure, and it is recorded in `docs/plan/OPTIMISATION-NOTES.md` so no later
slice re-derives it.

## Verification

Identical to S60's, because the risk is identical - this is the family where an optimisation is
likeliest to change an answer.

- Before and after, same machine, same session, driver idle:
  `pwsh -File tools/compare-benchmarks.ps1 -Job medium -ArtifactsPath artifacts/bench/<date>-S60b-after`
  against the committed baseline at S58's noise floor. Per workload, time **and** allocated
  bytes/op. A workload that regresses beyond the floor is triaged before the sitting commits.
- `pwsh -File tools/check-ratchet.ps1` GREEN.
- `pwsh -File tools/run-oracle.ps1` GREEN at its three default seeds, `ExpectedDivergences` strict.
- The three permanent files run explicitly and named in the commit message, one command each:
  `dotnet run --project tests/FuzzyRegex.Tests -- --treenode-filter "/*/*/<Class>/*"` for
  `BacktrackingVerbTests`, `PartialMatchingTests` and `ReverseMatchingTests`.
- `pwsh -File tools/run-aot-tests.ps1` and `pwsh -File tools/run-aot-smoke.ps1` GREEN, binary size
  against the size S60 recorded.
- No public surface change expected; if one appears, `pwsh -File tools/update-public-api.ps1` and
  justify it, because the API was frozen at S53b.

## Done when

- [x] `search_start`/`do_search_start` live, the underflow site handled explicitly and pinned.
- [ ] `try_match`'s test-node arm live, or deferred with a row and a comment.
- [ ] The encoding arms wired whole, or deferred whole - never half.
- [ ] Each of items 8-14, 16 and 17 either landed with its before/after numbers in the commit
      message, or recorded as measured-and-rejected with the numbers that rejected it.
- [ ] Every landed item's `ponytail:` comment and `OPTIMISATION-NOTES.md` row deleted in the same
      commit; every still-deferred item still carries both.
- [ ] Oracle GREEN at three seeds and the three permanent files green, unchanged, on every sitting
      that touches the engine.
- [ ] Any structural divergence recorded in `docs/plan/SYNC-DIVERGENCE.md` with a
      `sync-divergence:` marker, `tools/check-sync-divergence.ps1` green.
- [ ] Ratchet and AOT green; blind review (hunt: a prefilter that searches below a position a verb
      committed past; a `SearchValues` built per call instead of per pattern; a reverse or
      case-folded arm that skips the fold; the underflow site clamped rather than handled; a partial
      match whose run-out position moved), independent verifier, commit.
