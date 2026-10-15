# Known optimisations not yet implemented

Owner request 2026-09-16: keep the list so Phase 7 does not rediscover it. **This file is the
index, the source is the record.** Every deferral in the engine carries a comment at the line, and
the table below is regenerated from them, so a Phase 7 slice starts by running the command and
reading the comments, not by re-deriving what the port skipped.

```
grep -rn -i 'ponytail:\|Phase 7' src/FuzzyRegex --include=*.cs
```

Rule for slices from here on: a new deferral gets a `ponytail:` or `Phase 7` comment at the line
naming the ceiling and the lift, and one row here. A Phase 7 slice that implements one deletes the
comment and the row in the same commit. Correctness constraints on the optimiser are in
ROADMAP.md ("Phase 7 ports upstream's start optimisations without importing their answers") and
are not repeated here.

## Engine (upstream's own optimisations, deliberately deferred)

| Where | What is deferred | Notes |
|---|---|---|
| `Engine/Matcher.cs:5012` (`LocateRequiredString`'s `default:` arm) | The `STRING_REV`, `STRING_FLD`, `STRING_FLD_REV`, `STRING_IGN` and `STRING_IGN_REV` arms of `locate_required_string` (`:11143-11365`), and the `string_search_rev` / `_fld` / `_ign` searches they call | **No longer deferred as a prefilter: `Engine/RequiredStringScreen.cs` now screens all five arms (2026-09-26).** It refuses a subject only when no window could hold the required string under a rule looser than the matcher's own, jumps the start only at offset 0, never sets `req_pos`, and stands aside for partial matching. Upstream's own `STRING_REV`, `STRING_FLD`, `STRING_FLD_REV`, `STRING_IGN` and `STRING_IGN_REV` arms are still not ported - they are not transparent (DECISIONS 2026-08-31) - so `basic_match`'s five `String*` cases still keep their "unreachable until Phase 7 ports this arm" comment; the sixth, `Opcode.String`, is live. |
| `Engine/Matcher.cs:4946` | The start-position JUMP for a pattern holding a `(*SKIP)` | **Withheld deliberately, not deferred** - see `PatternObject.HasSkipVerb`. Refusal still applies; only the jump is withheld, because upstream's jump answers wrongly there and PCRE2 10.47 agrees with this port. Not a row to "implement later": lifting it would redden `Gaps/Engine/BacktrackingVerbTests.cs`, which is permanent. |
| `Engine/Matcher.cs` (`SearchStart`'s remarks and its `default:` arm) | What is left of the `search_start` family after S60b | **S60b landed the dispatcher and all thirty `search_start_*` helpers**, so this row is now the remainder, not the family. Still deferred: the `STRING_FLD`, `STRING_FLD_REV`, `STRING_IGN`, `STRING_IGN_REV` and `STRING_REV` arms, which reach `default:` here and turn the prefilter off for the rest of the operation - the same five searches the locator row above is waiting for; and upstream's `search_positions` memo, which is **not to be ported as it stands** (it is reset once per state and never when a `(*SKIP)` or a fuzzy re-run moves the slice, so its entries go stale - see `SearchStart`'s `ponytail:`). Four **narrowings are permanent, not deferrals**: no partial arms, no prefilter under `(*SKIP)`, none when the start test is the required string, and no `min_width` bound. Each is argued where it stands in `Matcher.cs`. |
| `Engine/Matcher.cs:8715` | `string_search_rev` | Part of the locator family. |
| `Engine/Matcher.cs:2765-2787`, `:3005` | Partial-match fast paths | Deferring is semantically neutral; see the class remarks. |
| `Engine/Matcher.cs:2065`, `:9126` | Capture storage reused across runs and across POSIX best-match saves (`SaveBestMatch`) | "A Phase 7 question rather than a correctness one." |
| `Engine/MatchState.cs:641`, `Engine/Node.cs:56` | `search_positions` and node search offsets | Only the prefilters read/write them; not ported. |
| `Engine/Optimiser.cs:508` | The match loop consulting the optimiser's markings | Markings are made so the graph has upstream's shape. |
| `Engine/PatternObject.cs:187` | `ReqFlags` naming a case-insensitive or reverse opcode | Read by `Compile` since before S60, but the opcode it names is still only searched for by the locator arms S60 did not port - see the first row. |

## API-level and allocation (this port's own ceilings)

| Where | What is deferred | Notes |
|---|---|---|
| `Parsing/Nodes.cs` (47 node classes), `Engine/NodeCompiler.cs` | One heap object per parse node, and a class-per-node AST that mirrors `_regex_core.py` | **Measured 2026-09-21** (Release, `GC.GetAllocatedBytesForCurrentThread`, 200 compiles each): a fresh compile costs 7,712 B / 0.010 ms for `\w+`, 26,208 B / 0.038 ms for a date pattern, 71,592 B / 0.080 ms for an email-shaped one, 86,472 B / 0.068 ms for `(?:ab{3}c){20}` and 1,569,880 B / 1.147 ms for `(?:ab{10}c){200}`. Cold path: the static conveniences cache fifteen patterns, and a constructor compiles once. The parser is already lean in the cheap ways - no `Substring` in `Parsing/`, two `new List<>` sites. The spread is node COUNT, not node cost, so the lever is the counted-repeat unrolling in the row above, not the AST's shape. An arena or struct AST is a CANDIDATE, not a refusal (DECISIONS 2026-09-21): it rewrites 9,151 lines of the most upstream-shaped code plus eight consumer files, so it is ranked on its own measured numbers once the obvious wins are gone. |
| `Engine/Substitution.cs:443`, `:618` | Template parsing: one subscript level; integer parsing limited to signed ASCII decimal | Correctness ceilings, not speed; listed so they are not lost. |
| `Unicode/CharacterNames.cs:34` | Character-name table size and lookup speed | "Phase 7 is where size and speed get measured." |
| `Engine/NodeCompiler.cs:1430` (`BuildRepeat`'s minimum-count loop) | Counted repeats are unrolled, one copy of the body per repetition, so compile memory grows with the PRODUCT of nested counts at about 250 bytes a node | Ceiling: `(a{1000}){1000}` keeps 1,005,009 nodes and 238 MB; `((a{150}){150}){150}` reached `OutOfMemoryException` in 9 s under a 1 GB cap before S56b, and the `{1000}` cubed form passed 15 GB and crashed a 32 GB machine twice (`docs/plan/2026-09-18-repeat-unrolling-investigation.md`). **S56b bounded it rather than fixing it**: `maxCompiledNodes` refuses the pattern at construction, default a million nodes. The lift is a counting loop instead of copies, and it is **not** a transparent rewrite - every guard and per-iteration effect currently sees distinct nodes, so a slice doing it owes a soundness argument (captures per repetition, fuzzy sections, the group-call guard) plus an oracle wave, not just a benchmark. Post-1.0, not Phase 7 by default. |
| `Engine/Matcher.cs:8166` | A branch no test pins; deleting it changed nothing measurable (full suite, three seeds, S42's review, four hand-built patterns) and it is kept only against a possible hang on an untested re-entry | Candidate for deletion once Phase 7 can build the re-entry case; write that test first. |

## Measurement notes for Phase 7

- **The baselines exist** (S54), in `bench/baselines/<machine-id>/net10.0.json`. A slice measures
  against them with one command, which runs the suite and prints the per-benchmark ratio:

  ```
  pwsh -File tools/compare-benchmarks.ps1
  ```

  It is RED on any benchmark more than 1.25x slower than the baseline - the v1.0 gate's own
  per-workload tolerance - and on a baselined benchmark missing from the run. **Run it rather than
  `dotnet run` by hand**: BenchmarkDotNet finds the benchmark project by searching down from the
  working directory's nearest solution file, and from the repository root that search also finds the
  copy inside every git worktree, so a hand-run dies having executed nothing. DECISIONS 2026-09-16
  has the mechanism; `bench/FuzzyRegex.Benchmarks.slnx` is the fix.
- The traps an optimiser is tempted to special-case are pinned in
  `tests/FuzzyRegex.Tests/Gaps/Engine/OptimiserTrapsTests.cs`, and **those tests are permanent** -
  a Phase 7 slice that turns one red has changed an answer.
- **`(a+)+b` is not the catastrophic shape in this port; `(a|a)*b` is.** Measured S54: `(a+)+b` is
  flat at 0.2-0.7 ms from n=18 to n=24, `(a|a)*b` goes 161 ms to 10.6 s over the same range. Both
  are pinned, so a change that moves a shape between the two classes is visible.
- **Nested repeats after C5 (2026-10-14).** The failure memo (Option B) left `(a+)+c` quadratic
  because the compiler copies the first iteration out of the outer repeat and every start walked
  the run again through the copy. C5 fails a start the outer repeat's body guard already holds
  (S2d) and walks back over a guarded span in one move (G2): `(a+)+c` at n = 400 takes 7,188 Debug
  steps against 407,784, PathologicalNestedRepeat 0.07x and PathologicalWordsToEnd 0.37x of
  a3d79986. Still open, in `docs/plan/2026-10-13-linear-nested-repeats-design.md`: `CountOne`
  scans the run once per entry (quadratic in characters, as plain `a+c` is in Python), and
  `(?:(?:a|a)+)+$` stays exponential in its first attempt (option N).
- Native AOT: the classic .NET regex `Compiled` route (IL emit) is unavailable; the constraint and the
  fast-path alternative are in ROADMAP.md under Phase 7.
- Every optimisation must keep the oracle GREEN at three seeds and `ExpectedDivergences` strict; an
  optimisation that changes an answer has ported an upstream bug (ROADMAP, owner rule 2026-09-12).

## Measured by S58's allocation attribution (2026-09-19)

Re-run any figure here with
`dotnet run -c Release --project bench/FuzzyRegex.Benchmarks -- attribution`.

| Where | What is deferred | Notes |
|---|---|---|
| `FuzzyRegex.cs:444` (`IsMatch`), `:568` (`Run`'s `visibleCaptures: true`) | A predicate call that does not build the captures it will never be asked for | **`IsMatch` allocates byte-for-byte what `Match` does** - 1,392 B at one group, 9,576 B at 32, identical at every group count measured. `IsMatch` is `Run(...).Success` and `Run` passes `visibleCaptures: true` unconditionally, so 224 of the 264 B per group a match costs are paid to produce a `bool`. Upstream's `state_init_2` takes the same flag for the same reason. **Constraint**: `visibleCaptures` also drives repeated-capture retention, so a slice taking this must prove the oracle green at three seeds - `IsMatch` rows included - rather than assume the flag is only about the returned object. |

**The paired source comment is owed.** The rule above is a `ponytail:` comment at the line plus a
row here; S58 is a measurement slice forbidden from touching `src/`, and its whole verification
claim is that `git diff -- src` is empty, so it recorded the row only. The next slice that is
allowed into `src/FuzzyRegex/FuzzyRegex.cs` adds the comment at `:444` and `:568`. S60 is the first
such slice.

## Measured by S60b's item 2 triage (2026-09-23)

`search_start` (20be6aa) against its parent 95318dd, both built with 6a3d761's `bench/`.
`ManyInputs`, `--job medium --inProcess`, before and after alternated per row, on a quiet machine
from 04:38 to 04:55. Raw output: `artifacts/bench/2026-09-23-queue/item2-{before,after}-<row>.txt`
in the main checkout.

| Row | Before | After | Change |
|---|---|---|---|
| FuzzyPhraseThreeNamedList | 6.582 s | 6.680 s | +1.5%, inside S58's 1.13 noise floor |
| RedactDigits | 106.8 ms | 62.14 ms | 1.72x faster |
| ValidateEmails | 71.43 ms | 70.54 ms | flat |
| ParseLogLines | 67.96 ms | 68.62 ms | flat |

Allocated bytes are identical on every row. An earlier 1.93x slowdown on the named-list row was
contamination: builds ran during that measurement. Item 2 is kept.

## Measured by the lazy MatchCollection (2026-10-14)

`Matches()` became lazy (owner decision, DECISIONS 2026-10-14; design
`docs/plan/2026-10-14-lazy-matches.md`). Main `fc5b190c` against perf/lazy-matches, BDN
`--job medium`, pinned to the performance cores, same session (12:25-12:53); raw output in
`.scratch/lazy-matches/bdn-{main,br}-{M,Ref}` of the main checkout. **Kept.**

| Row | Main | Branch | Ratio | Bytes main | Bytes branch |
|---|---|---|---|---|---|
| MatchesFirstTwo | 37,804 us | 0.427 us | 0.00001x | 27,477,924 | 2,536 |
| MatchesToEnd | 42,492 us | 42,330 us | 1.00x | 27,477,940 | 27,478,055 |
| MatchesToEndDense | 2,471 us | 2,652 us | 1.07x (inside the 1.10 floor) | 2,696,416 | 2,696,592 |
| EnumerateMatchesToEndDense | 2,382 us | 2,320 us | 0.97x | 2,515,000 | 2,515,000 |
| EnumerateMatchesFirstTwo | 0.270 us | 0.267 us | 0.99x | 456 | 456 |

BCL control rows (`ReferenceBenchmarks`, 12 rows) moved 0.97x to 1.07x, all inside the floor.
`MatchesFirstTwo` allocates 2.5 KB rather than `EnumerateMatchesFirstTwo`'s 456 B because a
collection read part way keeps its engine state, so the pattern's cache builds a new one for the
next call; that is the price of exact answers (the design note says why a state per match is not).
`MatchesToEndDense` at 1.07x is the iterator step and lock per match on the first `Count`; flat
on the megabyte row.

## Measured by cold-arm, BasicMatchCore's rarely used arms out of line (2026-10-14)

Cause and history: D82 part 2 in `docs/KNOWN-DEFECTS.md` and `.scratch/cold-arm/notes.md`. Kept:
50 rarely used arms (fuzzy, backreference, lookaround, conditional, call, verb, reverse string)
moved to NoInlining helpers, fewer locals in `BasicMatchCore`, AggressiveInlining on the guard
helpers, and a direct fast path for ByteStack's size and code pushes and pops. Rejected: taking
the case-folding buffers out of the frame (step 3b; it bought 9 inlinees and no frame size), and
NoInlining on the multi-site cold helpers (two are `[Conditional("DEBUG")]`, the third is a
one-line forwarder). Branch merged onto `fc5b190c` and measured against it, Release, pinned:

- Harness (`.scratch/cold-arm/next-time.txt`, min of 3, alternated): every row 0.74-0.93x
  (conditional per token 0.750, dense 0.779, backreference 0.858, nested 0.931), except a 1.3 us
  fuzzy word row, flat at the timer's resolution. Native AOT: no row slower.
- BDN `--job medium` (`.scratch/cold-arm/next-bdn-cmp-*.log`): nothing slower than the 1.10 floor;
  BCL controls flat (within 3%). CaseFoldedScan 0.75x, ParseLogLines 0.76x, ValidateEmails
  0.78x, ClassScanPort 0.79x, MatchesToEndDense 0.80x, SplitLong 0.80x, ClassScan 0.84x,
  ReplaceTemplate 0.87x, TestedGroupRepeat 0.88x, ConditionalPerTokenNoMemo 0.88x, every
  ManyInputs row 0.76-0.90x.
- The method is still at the JIT's local limit. Every inline it still refuses is refused as
  unprofitable, not for want of locals: RyuJIT scales an inline's profitability down continuously
  with the caller's local count, so further trimming buys a little each time and never reaches
  the code that `DOTNET_JitMaxLocalsToTrack=0x2000` produces. Further gains here mean splitting the
  loop itself, which is a design question rather than a trim.

## Measured by C3, the fuzzy-literal bit-parallel scan (2026-10-14)

`FuzzyLiteralFilter.LiteralScan` (design: `docs/plan/2026-10-13-fuzzy-literal-bitparallel-design.md`).
Main `fd2ad87e` against perf/c3-myers, Release, affinity 0xFFF8. Two instruments:

- BDN, `--job medium`, Workload and ManyInputs, branch before the lazy arm against tonight's
  pinned main baseline (`.claude/worktrees/c3-myers/.scratch/c3/gates/bdn-*`): FuzzyBudgetThree
  0.016x, EnhanceMatch 0.037x, BestMatch 0.044x, FuzzyPhraseOne* 0.90-0.92x,
  FuzzyPhraseThreeSeparatePasses 0.84x, the other rows 0.95-1.02x, and **FuzzyShort 1.120x**
  (283.7 ns to about 318 ns).
- A harness (`.scratch/c3/h.cs`, min of 5 reps per process, main, before and after alternated in
  9 processes each over two locked runs; the minimum of all is quoted):

| Row | Main | Before the lazy arm | After | After / main |
|---|---|---|---|---|
| FuzzyShort | 313.2 ns | 343.4 ns | 304.3 ns | 0.97x |
| FuzzyBudgetThree | 73,809 ns | 1,191 ns | 1,648 ns | 0.022x |
| FuzzyPhraseOneMatch (100,000 records) | 52.13 ms | 49.52 ms | 50.21 ms | 0.96x |

**The FuzzyShort slowdown was real and is fixed.** Its one search reaches the pieces' answer at the
real match, so the scan behind the pieces read about 20 characters and saved nothing. Behind the
pieces the scan now sits out a scan's first query (`ScanMemory.ArmSecondStage`): a Match that
succeeds at the first piece answer pays nothing, and one that fails pays one attempt before the
scan arms. The cost is about 1.4% on FuzzyPhraseOneMatch (49.52 to 50.21 ms).

**Unexplained, recorded rather than chased tonight:** FuzzyBudgetThree, which takes the
stand-alone path that the lazy arm does not touch, measured 1,191 ns before and 1,648 ns after.
A copy of the "after" source built the same way as "before" measured the same as "after" (1,626 ns),
so the build is ruled out. The suspect is code generation around `NextStart`, which grew by
one condition. It is still 0.022x of main. The upgrade is a `--disasm` comparison of the two
`NextStart` bodies.

## C6 option B, ignore-case strings for the leading screen (2026-10-15)

Option B of `docs/plan/2026-10-14-multi-string-search-design.md`, restricted, on `perf/c6-oic`.
A case-insensitive alternation of literals now gets `LeadingStrings` under
`StringComparison.OrdinalIgnoreCase` (whole-string Teddy) instead of `LeadingUnits` (first code
units) when `RequiredStringScreen.OrdinalIgnoreCaseCovers` says .NET's rule finds every match the
engine's does, and every literal has at least two characters. The rule is computed from the case
tables, so `k` and `s` (U+212A, U+017F) and, under full folding, the runs `ss`, `st`, `fi`, `ff`,
`fl`, `ffi`, `ffl` fall out of it; the design note's build section has the probe. Any literal that
fails sends the whole set to the units screen, as on main. No Matcher change: the string path
already handled `LeadingStrings`, and its Debug assertion that every match starts on a screened
literal now checks the ignore-case set on every whole match in the Debug suite and oracle. Logs in
the main checkout's `.scratch/s1016/c6-oic/`.

**Evidence.** TBD (red/green, mutations, differential, counters, oracle, AOT).

**Timing.** TBD: scheduled by the orchestrator. Rows to read: `WholeWordListIgnoreCase` (new,
Workload, `(?i)\b\L<words>\b` over the long corpus, Python pair added), `CaseBlindAlternation`
(ManyInputs, `(?i)(?:amber|copper|violet)`); every other row should be flat.

## D84, the ignore-case first-set look-through (2026-10-14)

Option A of `docs/plan/2026-10-14-multi-string-search-design.md`, on `fix/d84-ignorecase-screen`.
`FindLeadingStrings` now looks through the zero-width ignore-case first-set check (`SetUnionIgn`
or `CharacterIgn` with step 0), so `cat|dog` under IgnoreCase gets `LeadingUnits`. Logs are in the
main checkout's `.scratch/d84/`.

**Evidence.** The new tests were red before the fix (`red2.log`, 18 of 90 failed, all new rows)
and green after. In front of literal heads the ignore-case check is only ever one character
(`(?ai)(c)at|cow`) or a union of characters (`(?i)cat|dog`): `ops.txt` and `ops2.txt` probe 23
shapes and none compiles to a step-0 `RangeIgn`, `SetDiffIgn`, `SetInterIgn` or `SetSymDiffIgn`,
because `CheckFirstset` never merges characters into a range and a class head cannot lead to a
literal. Those four were dropped from the list (rule 12); taking out `CharacterIgn` turns 1 test
red, taking out `SetUnionIgn` 18 (`p10-noChar.log`, `p10-noUnion.log`). The differential against
main c4c6ea02 over 240,000 rows (`.scratch/s62/rows-c`, `rows-d` and 80,000 generated
case-insensitive alternation rows, `rows-d84.jsonl` from `gen.py`) is identical once the
elapsed-time column is stripped (`strip8-*`, `strip10-*`, `cmp`). The counters (`count-*.txt`)
show the new path live on 18,839 rows of `rows-d84` and 1,036 of `rows-c`. Oracle seeds 7, 4242
and 20260927, AOT tests and smoke (7,611,904 bytes), and doc examples are green.

**Timing** (BDN medium, P-cores): `CaseBlindAlternation`, a case-insensitive alternation of three
words over 100,000 records, 45.2 to 31.5 ms (0.70x); no other ManyInputs row moved, and the BCL
controls in Reference stayed within 1.06x. The first main run read 0.86-0.90x on untouched rows
against the branch; a second main run showed that was drift on the main side (main against itself
0.88-0.90x on the same rows), so the figures above use the second.

**ThreadSafetyTests ran out of memory, fixed in the test walk.** Three of its walks threw
`OutOfMemoryException` in `ObjectGraph.Walk` on this branch and passed on main
(`diag-br-ThreadSafetyTests.log`, `diag-main-ThreadSafetyTests.log`). The named-lists family
(`\L<words>` with IgnoreCase) now carries a `ProbabilisticWithAsciiCharSearchValues` as its
`LeadingUnits`, and walking that object did not finish in five minutes (`sv/sv.cs`, `sv.txt`).
The walk now treats `SearchValues<char>` and `SearchValues<string>` as leaves, as it does `Type`:
they are immutable BCL objects, and the field holding them was already allowlisted on that ground.

## C6 option C, the leading screen looks through zero-width tests (2026-10-14)

Option C of `docs/plan/2026-10-14-multi-string-search-design.md`, on `perf/c6-zero-width`, two
commits: word boundaries and the MULTILINE `^` (84dc713f), then lookarounds (657a3cee).
`FindLeadingStrings` walks past `\b`, `\B`, `\m`, `\M`, their default-word forms, `(?m)^` and its
Unicode-line form, and a lookaround (following its continuation, `Next2`), so `\b(?:cat|dog)\b`,
`\b\L<words>\b` and `(?<![a-z])(?:cat|dog)` get the screen `cat|dog` has. Logs are in the main
checkout's `.scratch/c6/`.

**Why it is sound.** Each of these tests the position an attempt stands on and goes on from that
same position (the zero-width arm calls `TryMatchZeroWidth` at `TextPos` and takes `Next1`;
`AdvanceLookaround` saves `TextPos` and both a passing positive body and a failing negative one
lead to `Next2`), so a match starting at `p` still opens with a literal at `p`, and the engine still
runs the test there. A Debug assertion in `BasicMatch` checks that premise on every whole match of
a screened pattern, screened or not; the Debug ratchet (8,931 tests) ran with it.

**What is refused, and why.** `\A` and `^` without MULTILINE anchor the search, which then never
consults a screen. `\Z` can be followed by no literal. The end-of-line anchors can only be followed
by a newline, which the compiler factors out in front of the branch, so the walk stops at that one
character anyway (the first build looked through them and its four structural rows stayed red).
`\G` holds at one position. A lookaround is refused when the pattern has a `\K` or `(*SKIP)`
(`AnAttemptCanMoveTheNextStart`): a `\K` in the body sets the match start, the body's end discards
its entry, and the bump-along then starts past it, so `(?=[a-z][a-z]\K)(?:cat|dog)` over `xcat` is
no match upstream and here, and `(3, 4)` with the screen. Taking that guard out turns 3 tests red
(`p3-noKeep.log`) and 81 of the 90,000 generated differential rows (`out-noKeep-c6.txt`). A
lookaround is also refused with a `(*PRUNE)` or a group call, which this change has not proved;
each guard has a structural witness (`p3-noVerb.log`, `p3-noCalls.log`) and no test's answer changes
without it. A lookaround whose body yields a `Property` first-set check, such as `(?=(\w))`, is not
screened: the walk does not look through that check.

**Evidence.** The ops probe (`ops/ops.cs`, `ops.txt`, `ops2.txt`) gives a witness shape for every
opcode added. Red before each change: 17 structural rows and 3 reference-loop rows for the first
(`red1-*.log`), 8 structural rows and 1 loop row for the second (`red2-*.log`); every answer row,
pinned to regex 2026.9.10, was already green. The differential against main b32a00e5 over 250,000
rows (`.scratch/s62/rows-c`, `rows-d` and 90,000 generated rows, `rows-c6.jsonl` from `gen.py`) is
identical once the elapsed-time column is stripped (`strip-*`, `cmp`). The counters
(`count-*-c6.txt`) show the screen running on 24,384 generated rows against main's 1,110, through a
lookaround on 14,546, `\b` 5,417, `(?w)\b` 1,364, `\m` 1,171, `\M` 1,108, `(?m)^` 354,
`(?mw)^` 206 and the default start and end of word 285 and 283. Oracle seeds 7, 4242 and 20260927,
AOT tests and smoke (7,606,272 bytes) and doc examples are green.

**Timing** (single process per side, Release, tiering off, pinned to FFF8, best of 7 runs, min of 5
alternating processes; `time-*.txt`). Over a megabyte of word-shaped text: `\b(?:cat|dog)\b` 7.00
to 0.11 ms, `\b\L<ten>\b` 24.2 to 0.15 ms, `(?<![a-z])(?:cat|dog)` 9.50 to 0.08 ms. Two shapes do
not gain. `(?i)\b(?:cat|dog)\b` (6.88 to 6.43 ms) gets the code-unit screen, which stops on every
`c` and `d` as the first-set check already did. `(?m)^(?:cat|dog)` over text with no newline (6.03
to 5.80 ms) does not return to the screen: after the first jump, `SearchStart` keeps scanning for
the first-set character and tries `^` at each one itself (its look-ahead loop), to the end of the
text. Adversaries, a literal at every hit where the zero-width test then fails, all within 0.88 to
1.02 of main: `\b\L<ten>\b` over `xamber ` repeated 1.02, `\b(?:th|ht)` over `xth ` 1.01.

**Follow-up: no first-set prefilter behind an active screen (`perf/c6-searchstart`, 2026-10-15).**
The `(?m)^` explanation above was a hypothesis; a trace proved it. Temporary counters in `Matcher`
(`.scratch/c6/ss/trace/trace.cs`, `trace-before.txt`), one `Count` over the megabyte with no
newline: `(?m)^(?:cat|dog)` made 1 attempt, 1 screen sweep and 1 `SearchStart` call, whose
look-ahead loop scanned 50,094 first-set characters and refused 50,093 of them on `^` itself.
Each shape opens with the zero-width first-set check, which is also its start test
(`.scratch/c6/ss/shape`), so the loop applies to all of them. `\b(?:cat|dog)\b` drifts the same
way, but `\b` passes at the next word-initial `c` or `d`, so the loop hands back after a few steps
(301 refusals over 153 attempts). `(?i)\b(?:cat|dog)\b` made 6,529 attempts and 44,342 in-loop
refusals; its code-unit screen and the first-set check both stop on every `c` and `d`.

The change: `searchStartAllowed` in `BasicMatchCore` is false whenever `LeadingStrings` or
`LeadingUnits` runs, so the search makes no `SearchStart` call (setup code, not a hot arm, D82).
*Soundness:* a prefilter only skips positions where the matcher would fail. Not running it skips
nothing, and each attempt still runs the start test itself. The screen stops only on a literal,
and every literal begins with a first-set character, so the prefilter could not reject a screen
hit on its first-set test. It could only reject one on the zero-width test after it, which the
attempt then runs anyway. *Worst case:* every screen hit fails its zero-width test. Each one then
costs a whole attempt (FAILURE push, group clear, bump-along) where the look-ahead loop paid one
`TryMatch`. *Adversary:* exactly that, a hit every three to five characters that `^`, `\b` or a
lookbehind refuses. The decision probe (`trace/mode.cs`, `mode-out-decision.txt`) put the units
screen in scope because `(?i)` gained too: skipping behind it took `(?i)\b(?:cat|dog)\b` from 6.7
to 2.7 ms. `SearchStart`'s scan for an ignore-case set goes one character at a time, while the
screen is a vectorised `SearchValues<char>` sweep.

Evidence. Test first: `LeadingZeroWidthTests.A_screened_search_does_not_also_run_the_first_set_prefilter`
reads a new Debug-only `WorkCounter.SearchStartCalls`. Each row has a class-spelt control that
keeps the prefilter, so the counter is shown to be live. The six rows were red (3 to 8 calls,
`ss/red-LeadingZeroWidthTests.log`) and are green after the change. The Debug ratchet was green
at 8,992 tests, and the three permanent files were unchanged and green. Differential against main
7e7a59a7 (`ss-base`), with the elapsed column stripped: identical over 310,000 rows
(`rows-c` 100,000, `rows-d` 60,000, `rows-c6` 90,000, and 60,000 new rows from `ss/gen-ss.py`
aimed at `(?m)^`, `\b` and `(?i)` heads, 20% partial and 14% reverse). The change is live on
47,963 of them (`ss/count1-*.txt`). Oracle seeds 7, 4242 and 20260927 are green.

Timing (Release, tiering off, pinned to FFF8, best of 7, min of 5 alternating processes,
`ss/timing-summary.txt`; main, then branch). `(?m)^(?:cat|dog)` over the megabyte: 6.81 to
0.072 ms. `(?i)\b(?:cat|dog)\b`: 6.57 to 2.95 ms. `(?i)(?:cat|dog)`: 3.37 to 2.92 ms.
`(?m)^(?:cat|dog)` with newlines: 0.124 to 0.086 ms. `\b(?:cat|dog)\b`: 0.121 to 0.072 ms.
`\b\L<ten>\b`: 0.164 to 0.157 ms. `(?:cat|dog)`: 0.076 to 0.082 ms. That is 6 µs, in a run that
makes fewer calls, so it is taken as noise. All twelve adversaries are 0.51 to 1.02 of main: the
units screen keeps the dense-hit cases fast, and a whole attempt turned out no dearer than a
`TryMatch` in the loop. The case-blind word list `(?i)\b\L<ten>\b` stays at 31 ms: its units
screen stops on ten first letters in either case, the `t` of `the`, `that` and `this` among them.
That is a limit of the units screen, not of this change.

## Measured by S61's quiet-machine gates (2026-09-23)

S61 implemented and deleted the three rows on the span copy, the per-step state of a lazy walk and
the `Split`/`EnumerateSplits` duplication. The deciding runs, `--job medium --inProcess` on a quiet
machine, 8dd746e (before S61) against the slice's code. Raw output is under
`artifacts/bench/2026-09-23-night/s61gate-*` in the main checkout; the full table is in
`docs/plan/slices/notes/S61-sittings.md`, sitting 8.

| Row | Before | After | Bytes before | Bytes after |
|---|---|---|---|---|
| SpanMegabyte (`IsMatch` over a 1 MB span) | 350.2 us | 161.7 us | 2,099,232 | 1 |
| CountSpanMegabyte | 1,368 us | 1,313 us | 2,099,271 | 9 |
| CountStringMegabyte | 1,046 us | 1,154 us (1.10x, inside the floor) | 776 | 9 |
| EnumerateMatchesToEndDense (100 KB string walk) | 71.1 ms | 3.03 ms | 20.8 MB | 3.0 MB |
| EnumerateMatchesSpanToEndDense (new span walk) | - | 2.73 ms | - | 17 B |

The `ReadOnlySpan<char>` overloads still copy the span, now into a buffer rented from
`ArrayPool<char>`, because `MatchState.Text` is a `ReadOnlyMemory<char>`. Reading a span in place
would need a `ref struct` state. It is worth reopening only if a benchmark shows the pooled copy
costing time; today the copy allocates nothing and both span rows above are faster than before.

## From the Rust fuzzy-regex library (reviewed 2026-09-18)

`docs/plan/2026-09-18-fuzzy-regex-rs-techniques.md` has the full table. Two items adopted, both
into S60 as its items 8 and 9 and, from 2026-09-20, into **S60b** with S60's item numbers kept
(spec amendment 30): a rarity gate on the prefilter's skip character (their highest-ROI roadmap item, borrowed
from resharp) and an Aho-Corasick or `SearchValues<string>` path for large `\L<name>` lists behind
a threshold ("Results are identical to the alternation; only the speed differs"). Everything else
there is either already in S59/S60, already deferred here (bounded-repeat unrolling), or rests on
a non-backtracking automaton or a different fuzzy algebra and must not be copied.

## External comparators (owner decision 2026-09-20; S63 item 9)

Three reference points are recorded beside our medians, for context and never for the gate:
Python mrab-regex on every workload, the Rust `fuzzy-regex` crate on the intersection only (fuzzy
literals and short alternations with one global error budget, each case asserted to return the
same spans before its time is recorded; anything else is "not comparable" because the crate uses a
different fuzzy algebra), and `System.Text.RegularExpressions` on the non-fuzzy workloads. The
table lands here when S63 runs. The crate is a ceiling for the automaton approach S60 and S62
pursue, not a target: beating it on a pattern it interprets differently proves nothing, and
losing to it may be the price of the semantics this port exists to keep.

## Research sweep of 2026-09-18 (three sources, one note)

`docs/plan/2026-09-18-optimisation-research.md` is the record: regex-automata/RE2/Navarro (§1), .NET
Regex and PCRE2 (§2), literature 2018-2026 and hardware (§3), with every quote and URL. Ten items
went into S60 (8-17); item 15 landed there and the other nine moved on 2026-09-20 to
**`docs/plan/slices/S60b-search-start-and-the-researched-prefilters.md`**, which keeps those
numbers (spec amendment 30); one workload into S58,
one verification item into S62. Two bets went to the
owner and, on 2026-09-19, into the plan as experiment slices (spec amendment 28), each free to end
in "reverted, recorded": **`docs/plan/slices/S62b-auto-atomicity-experiment.md`**,
auto-atomicity/auto-possessification (a compile time rewrite), and
**`docs/plan/slices/S62c-memoisation-spike.md`**, selective memoisation of failed positions (Davis
et al. 2021, extended to lookaround and atomic groups by Fujinami and Hasuo 2024), which is the one
published technique that makes a backtracker linear without changing its answers and the fix for
the `(a|a)*b` class; it needs the fuzzy counters in its key and is off for backreferences and verbs.
