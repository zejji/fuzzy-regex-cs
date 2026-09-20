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
      *Partly: the forward case-sensitive arm is live and reads `ReqOffset`. The reverse and
      case-folded arms are deferred with comments and OPTIMISATION-NOTES rows - S60b item 1.*
- [ ] `search_start`/`do_search_start` live, the underflow site handled and pinned.
      *Not landed - S60b item 2. The underflow site (`Matcher.cs:10436-10441`) is live and
      annotated, because S60 made `ReqPos` live, but it is not pinned by a test yet.*
- [ ] `SearchValues<char>` built per compiled pattern; vectorised `IndexOf` replaces the scalar
      scans; no hand-written SIMD, no `unsafe`.
      *Not landed - S60b. The sweep uses `MemoryExtensions.IndexOf`, which is vectorised; the
      per-pattern `SearchValues<char>` belongs with items 4 and 16.*
- [x] The verb-slice test written red first and green after; the three permanent files green
      unchanged.
- [x] Oracle GREEN at three seeds; measured before and after per workload in the commit message.
- [x] Every sub-part not landed carries a comment and an OPTIMISATION-NOTES row; landed rows and
      their comments deleted.
- [x] Any structural divergence recorded in `docs/plan/SYNC-DIVERGENCE.md` with a
      `sync-divergence:` marker, `tools/check-sync-divergence.ps1` green.
- [ ] Items 8 to 17 landed, or each deferred with a row and a comment; item 15's `a*a` test is
  not deferrable.
      *Item 15 landed with its `a*a` test. Items 8-14, 16 and 17 are deferred with rows and
      comments and move to S60b (spec amendment 30).*
- [x] Ratchet and AOT green; blind review (hunt: a prefilter that searches below a position a verb
      committed past; a `SearchValues` built per call instead of per pattern; a reverse or
      case-folded arm that skips the fold; the underflow site clamped rather than handled; a
      partial match whose run-out position moved), commit.

## Closing notes (2026-09-20, four sittings)

**What landed.** Upstream's `locate_required_string` forward case-sensitive arm, with
`string_search`/`simple_string_search` under it, the per-pattern needle, and the `(*SKIP)`
constraint implemented rather than asserted: a pattern that can run a `(*SKIP)` may have its
subject refused by the prefilter but never its first attempt position chosen for it. Item 5 and
item 15 (with its `a*a` test) landed. 19 gap tests, and three oracle rows judged and pinned -
`end-of-line-reads-a-skip-moved-slice` gained a sixth row, row 3633 of seed 31337, the entry's
first PARTIAL SEARCH. The per-sitting record is `docs/plan/slices/notes/S60-sittings.md`; this
file is the spec and stays short.

**What did not land, and where it went.** Items 2, 3, 6, 8-14, 16 and 17 move to
`docs/plan/slices/S60b-search-start-and-the-researched-prefilters.md`, keeping S60's item numbers
so that every `ponytail:` comment and OPTIMISATION-NOTES row already in the tree still resolves.
Spec amendment 30 and a ROADMAP paragraph record the phase-plan change in this commit.

**Surprises the next slice should know.**

1. `ReferenceBenchmarks.BacktrackingPort` no longer measures backtracking. Its `(a|a)*b` over a
   subject with no `b` is refused by the prefilter before the matcher runs: 87,169,133 ns to
   126.6 ns. S62 needs a workload that reaches the matcher, or it is measuring the locator.
2. The prefilter's cancellation poll was deleted rather than pinned. `MatchState.InitMatch` zeroes
   `Iterations` on every attempt, so `basic_match`'s opening cancel check is an open gate and a
   spent budget is always caught before the sweep starts; the poll could only fire mid-sweep,
   which is a race with the machine. `IndexOf` over 100,000,000 code units takes 14.1 ms, so the
   longest string .NET can hold sweeps in about 150 ms.
3. A benchmark on this machine must pause Stryker AND
   `.claude/worktrees/stryker/.scratch/after-parsing.ps1`, whose watcher relaunched the queue
   mid-run at 08:54 and contaminated the tail of a 42-minute suite.
4. Three workloads reading 1.15x-1.32x against the committed baseline were settled by an A/B, not
   an argument: the same three benchmarks in a `14aad0a~1` worktree and in this tree, back to back
   (DefaultJob, which auto-sizes each run - 12 to 62 result measurements a workload, counted from
   the artifacts - both sides under the same ~20% background load), give
   1.026x, 1.030x and 1.040x. The slice costs them nothing measurable; the baseline is four days
   old and was taken on a machine that was not quiet, and re-taking it is S63's job.

## The negative controls, re-run against this commit

All four are suite controls, not oracle waves: the fixture is the committed test files, so there is
no generator or seed, and the count below is the whole suite. Each is one edit to
`src/FuzzyRegex/Engine/Matcher.cs`, built and run with
`dotnet run -c Debug -p:UseSharedCompilation=false --project tests/FuzzyRegex.Tests`, plus
`-- --treenode-filter "<filter>"` where one is shown. Re-run 10:21-10:24 on 2026-09-20 against the
tree in this commit, after the last code change; the baseline with no edit is `failed: 0
succeeded: 6459`. The script that drove the edit-run-restore cycle was `.scratch/controls-final.ps1`
(scratch, not committed); every edit below is a plain string replace that can be redone by hand.

> **Control A, the verb guard.** In `LocateRequiredString`, change
> `bool useOffset = pattern.ReqOffset >= 0 && !pattern.HasSkipVerb;` to
> `bool useOffset = pattern.ReqOffset >= 0;`. Whole suite. Result: **2 failed of 6459** -
> `BacktrackingVerbTests.Skip_past_a_required_string_tries_a_start_position_upstreams_prefilter_skips`
> and `RequiredStringPrefilterTests.A_skip_verb_keeps_the_prefilter_from_choosing_where_the_first_attempt_starts`.

> **Control B, the astral limit walk.** In the same method, replace the walk
>
> ```csharp
>                     limit = state.SliceStart;
>                     for (long i = 0; i < pattern.ReqOffset + reqString.Values.Count && limit < state.SliceEnd; ++i)
>                     {
>                         limit = state.NextPos(limit);
>                     }
> ```
>
> with the transliterated addition
> `limit = (int)long.Min(state.SliceStart + pattern.ReqOffset + reqString.Values.Count, state.SliceEnd);`.
> Whole suite. Result: **2 failed of 6459** -
> `BackrefAndConditionalTests.A_backreference_spanning_an_astral_character_and_a_bmp_one_matches_the_whole_span`
> and `RequiredStringPrefilterTests.An_astral_required_string_is_not_cut_short_by_the_offset_limit`.

> **Control E, the sweep's base.** In `StringSearch`, change `return textPos + found;` to
> `return found;`. Filter `/*/*/RequiredStringPrefilterTests/*`. Result: **1 failed of 19** -
> `The_prefilter_searches_the_slice_and_not_the_subject`.

> **Control F, the newly live `req_pos` fast path.** In `basic_match`'s `Opcode.String` case,
> anchored on the comment above it because the assignment is not unique:
>
> ```csharp
>                         // the prefilter has already compared is not compared a second time.
>                         state.TextPos = state.ReqEnd;
> ```
>
> change that assignment to `state.TextPos = state.ReqEnd + 1;`. Whole suite. Result:
> **139 failed of 6459**. The number is the point: that arm was dead code before S60.

Control C did not survive - the chunked sweep it tested was deleted, for the reason given in the
sittings notes - and Control D is retired with the chunk boundary it broke. Neither is claimed as
evidence here.

## Review

Two blind passes, both reproduced before anything was changed.

**Pass 1, over sitting 3's diff (`1f55858`) and the working tree.** Six findings raised, six
reproduced, six fixed: row 3633 is the entry's sixth row and not its fifth (four places said
"row 5"); seed 31337 is an extra seed and not one of the gate's three defaults
(`run-oracle.ps1:256` reads 7, 4242 and the date), which four places got wrong; the S60 paragraph
sat ahead of paragraphs about earlier rows; the grid probe's `$` sweep inherited the REVERSE bit,
which anchors `match` at `endpos` so every position answers (proved by a two-run control, `[2, 5]`
against `[0, 1, 2, 3, 4, 5]`); the `(?w)` control runs on three rows, not one, which corrected two
ledger sentences; and the grid probe read only `DIVERGE` headings, so it printed NOT RECORDED for
exactly the rows the slice had pinned.

**Pass 2, over the delta pass 1 never saw** - the new pinning test, the S60b slice file, spec
amendment 30, the ROADMAP and OPTIMISATION-NOTES paragraphs, both probes and the benchmark notes.
Six findings raised, six reproduced, six fixed: the benchmark claim that three workloads had been
re-measured on a quiet machine was not supported by any artifact and one re-run read 1.32x, ABOVE
the gate (settled by the A/B above, which is what the notes now claim); the grid probe's new
REVERSE paragraph said all three of its rows spell `(?r)` when only one does, and the reason the
mask is safe is different - only the spelling is compiled, never the row's pattern, and 0 of 6,380
wave rows carry the flag, which I counted rather than asserted; OPTIMISATION-NOTES said ten items
moved to S60b when item 15 landed here and nine moved; S60b had copied five stale `Matcher.cs` line
references and three more besides, all re-resolved by symbol; `do_search_start` was described as
the dispatcher in three files when it is the BOOL flag (`_regex.c:588`) and `search_start`
(`:8385`) is the dispatcher; and "Phase 7 is seven slices where it was six" ignored amendment 28's
two experiment slices - it is nine where it was eight.

**The independent verifier** (amendment 16 limb (d)) then re-ran every number these notes and the
ledger quote, from the committed files, and reported each CONFIRMED, DIFFERENT or COULD NOT RUN.
Confirmed: all six `$`/`(?w)$` position lists and the three-row `(?w)` claim; both probes; the
pinning test's five upstream answers, re-run against `regex` 2026.9.10; all four controls, each
applied, run and restored, at 2/2/1/139; the ratchet and the three oracle seeds; the three A/B
ratios; and every line reference in S60b but one. Four came back DIFFERENT and all four are fixed
above: Control B's first failing test is in `BackrefAndConditionalTests`, not a
`BackreferenceMatchingTests` that does not exist; "40 measurements each" was wrong, because
DefaultJob auto-sizes and the six runs gave 12 to 62; the Debug suite reads 25 to 28 seconds, not 28;
and S60b's `PatternObject.cs:253-276` pointed at the flags, where the required-string fields are at
`:179-197`.
