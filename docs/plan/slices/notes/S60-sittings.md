# S60 sittings

Per-sitting notes for `docs/plan/slices/S60-prefilter-family.md`. The slice file stays the spec;
working state lives here.

## Sitting 1 - 2026-09-20 (00:37) - S57 work, under the S60 driver

`docs/plan/slice-log.jsonl` records this sitting against `S60-prefilter-family` with outcome
`checkpoint`, but the commit it produced is `3f1bf91 S57 (checkpoint)` and its notes went to
`docs/plan/slices/notes/S57-sittings.md`. That is not a mislabelled slice: the driver was launched
for S60 and found S57 still in flight in `docs/plan/slices/`, so the sitting did S57's two items
that **S60 depends on**:

- **the native-AOT gate** (S57 item 2), which had been red since `148c3bf` on one `IL2065`. S60's
  own verification requires `tools/run-aot-tests.ps1` and `tools/run-aot-smoke.ps1` GREEN with the
  binary size compared against 6,972,928 bytes. A gate that is already red cannot answer whether
  `SearchValues` changed anything, so S60 could not have started against it;
- **the oracle report** (S57 item 4), which found the default wave GREEN at seeds 7 and 4242 and
  **1 divergence at seed 20260920**, plus 3 more in a 126,080-row wave at seed 7. S60's gate is
  "oracle GREEN at three seeds, `ExpectedDivergences` strict", and the same argument applies: a
  wave that is red before the prefilter lands cannot tell a prefilter defect from an existing one.

**S57 is NOT closed here** (orchestrator instruction, 2026-09-20 03:25): it closes Phase 6 and
depends on S56's survivor triage, which waits for the Stryker queue. Its slice file stays in
`docs/plan/slices/` and `docs/plan/STATE.md` keeps its next-sitting list.

## Sitting 2 - 2026-09-20

Machine is NOT quiet: the Stryker mutation queue and the S73 demo session are still running (see
the S57 sitting-1 note). S60's verification includes a **benchmark** before and after, which is a
timing instrument and therefore does need a quiet machine; the quiet-machine protocol applies and
is followed before any number is recorded as a baseline comparison.

### What landed

Scope item 1's **case-sensitive forward arm only**, plus item 15 and the item 5 constraint:

- `Matcher.LocateRequiredString` - `locate_required_string` (`:11082-11141`), the `Opcode.String`
  case. Absent required string, the subject is refused before the first attempt; present, the
  attempt may start at `found_pos - req_offset`.
- `Matcher.StringSearch` / `Matcher.SimpleStringSearch` - `string_search` (`:6596`) and
  `simple_string_search` (`:5231`). `fast_string_search` and `build_fast_tables` are deliberately
  NOT ported; see the SYNC-DIVERGENCE row.
- `PatternObject.ReqStringText` - the needle, built once in `Compile`, `null` when the required
  string holds an unpaired surrogate (which is what makes the vectorised search answer identically
  to the character-at-a-time one).
- `PatternObject.HasSkipVerb` - item 5, implemented rather than asserted. The start-position JUMP is
  withheld from any pattern that can run a `(*SKIP)`; refusal is not.
- `tests/.../Gaps/Engine/RequiredStringPrefilterTests.cs`, 19 tests, every expectation from a real
  `regex 2026.9.10` run recorded in `tools/probes/upstream-required-string-prefilter.py`. Item 15's
  `a*a` rows are in there and were written before the locator.

`Opcode.String`'s `req_pos` fast path in `basic_match` is **live for the first time** - it was dead
code before this slice, and Control F below measures how much of the suite now runs through it. The
other five `String*` arms stay unreachable and keep their comments.

### What did not land, and why

Items 2, 3, 6, 8-14, 16, 17 - `search_start`, `try_match`'s test-node arm, the encoding arms, the
rarity gate, named-list membership, the fuzzy reject filter, fixed-distance sets, per-position
pruning, literal-after-loop, multi-string alternation search, the start-code bitmap, leading `.*`
anchoring. None is blocked; the sitting stopped at the point where one mechanism was complete and
pinned. **No `SearchValues<char>` yet** (item 4's first half): the required-string search wants
`IndexOf` over a substring, not a character set, and the set search is item 16's. So `src/` still
has zero `SearchValues` uses, and the slice file's box stays unticked.

Also not done, and owed before this slice closes:

- **The benchmark.** Nothing timed, because the machine is not quiet. `ReferenceBenchmarks.
  BacktrackingPort` uses `(a|a)*b` at n=18 and now measures the prefilter refusing the subject
  rather than the backtracker, which is a change of what that number MEANS and has to be said out
  loud when it is re-run.
- **A judgement on oracle row 5014**, below.

### Gates

| Gate | Result |
|---|---|
| Full suite | 6451 / 6451, 0 failed |
| `tools/check-ratchet.ps1` | GREEN; baseline updated to 6343 distinct ids (was 6324) |
| `tools/check-sync-divergence.ps1` | GREEN, 2 marked files, both paired |
| `tools/run-aot-tests.ps1` | GREEN, 6448 passed / 3 skipped in the AOT binary |
| `tools/run-aot-smoke.ps1` | GREEN; **binary 6,985,216 bytes against the 6,972,928 reference, +12,288 (+0.18%)**, and see the jitter finding below |
| `tools/run-oracle.ps1` | **RED at 1 of 3 seeds** (20260920), 1 divergence of 6380. Seeds 7 and 4242 GREEN |

**The oracle row is not this slice's.** Row 5014, `(partial) search flags=0x400a`, pattern
`(?:[^\p{L}](*SKIP)[[:digit:]]|[^a-f])([a]?)\g<1>\b` over `'AAaa '`: upstream `(5,0)` partial, port
`(4,1)` partial. S57's sitting-1 report had already found exactly one divergence at this seed, and
this sitting proved it is the same one and predates the prefilter, by stashing `src/` back to
`3f1bf91` and re-running that seed alone: `agree 6350 expected 22 resource 7 diverge 1 of 6380`,
same row, same answers. Re-run it with:

```
git stash push -- src/ ; pwsh -File tools/run-oracle.ps1 -Seed 20260920 ; git stash pop
```

It wants judging (the port's `(4,1)` is the LEFTMOST partial and upstream's `(5,0)` skips it, which
is the shape of the already-pinned `search_start` family), but judging it is amendment-16 work on a
row that has nothing to do with the required string, so it is the next sitting's first item and not
a thing to fold into this one.

**Two more seeds, run because three green-or-explained seeds is not the same as knowing the shape
of what is red.** Seed 999: **GREEN**, 0 of 6380. Seed 31337: **RED, 2 of 6380** -

```
DIVERGE row 3633 (interactions) search flags=0x108
  pattern  '(?r)^(?P<g1>[\p{L}||\p{N}]){1,}(?:[a\d](*SKIP)[\w\s]|\w)$'   subject '\n𝟮𝔘𝔘'
  upstream 0:(0,1) partial     port 0:(0,0) partial
DIVERGE row 5200 (partial-sliced) search flags=0x8
  pattern  '(?:[^\p{L}](*SKIP)\D|[^a-f])[[:alpha:]]'   subject '0bA.'   slice utf16 [1, 4)
  upstream 0:(4,0) partial     port 0:(3,1) partial
```

Both are **pre-existing**, proved without touching the working tree: a detached worktree at
`3f1bf91` (`git worktree add .claude/worktrees/s60-control --detach 3f1bf91`, then
`git submodule update --init upstream` - a fresh worktree has no `upstream/`, and the recorder dies
without it), the same seed run there, and

```
diff TestResults/oracle/report-31337.txt \
     .claude/worktrees/s60-control/TestResults/oracle/report-31337.txt   ->  IDENTICAL
```

The worktree was removed afterwards. Neither pattern holds a literal string at all, so
`reqChars` is empty, `PatternObject.ReqString` is `null` and `LocateRequiredString` returns
`state.TextPos` on its first line - the prefilter cannot reach these rows even in principle. All
three red rows across the two seeds are `(*SKIP)` plus partial matching, which is one family and
the same one as row 5014, and judging that family is one next-sitting job rather than three.

### Negative controls, all re-run against the committed code

Each is an edit to `src/FuzzyRegex/Engine/Matcher.cs`, built with
`dotnet run -c Debug -p:UseSharedCompilation=false --project tests/FuzzyRegex.Tests` and the filter
shown. These are suite controls, not oracle waves, so there is no generator or seed to record - the
fixture is the committed test files.

The counts below are sitting 2's, when the suite held 6451 tests. **Sitting 4 re-ran all four
against the tree it committed** (10:21-10:24 on 2026-09-20, suite now 6459, baseline GREEN at
`failed: 0  succeeded: 6459`), scripting the edit-run-restore cycle so the file is restored byte
for byte: **A 2 failed of 6459, B 2 failed of 6459, E 1 failed of 19, F 139 failed of 6459** - the
same tests and the same numbers, the two full-suite ones larger only by the eight tests added since.
Each control edits one anchor with a plain string replace, so it can be redone by hand from the
snippets below; the cycle is 40 seconds a control because the Debug suite runs in 25 to 28 seconds,
depending on what else the machine is doing.

**Control A, the verb guard.** In `LocateRequiredString`, change

```csharp
                bool useOffset = pattern.ReqOffset >= 0 && !pattern.HasSkipVerb;
```

to `bool useOffset = pattern.ReqOffset >= 0;`. Full suite: **2 failed of 6451** -
`BacktrackingVerbTests.Skip_past_a_required_string_tries_a_start_position_upstreams_prefilter_skips`
and `RequiredStringPrefilterTests.A_skip_verb_keeps_the_prefilter_from_choosing_where_the_first_
attempt_starts`. This is the control that matters most: the first cut of the slice shipped without
the guard and this is what caught it.

**Control B, the astral limit walk.** In the same method, replace

```csharp
                    limit = state.SliceStart;
                    for (long i = 0; i < pattern.ReqOffset + reqString.Values.Count && limit < state.SliceEnd; ++i)
                    {
                        limit = state.NextPos(limit);
                    }
```

with the transliterated addition
`limit = (int)long.Min(state.SliceStart + pattern.ReqOffset + reqString.Values.Count, state.SliceEnd);`.
Full suite: **2 failed of 6451** - `BackrefAndConditionalTests.A_backreference_spanning_an_astral_
character_and_a_bmp_one_matches_the_whole_span` and `RequiredStringPrefilterTests.An_astral_
required_string_is_not_cut_short_by_the_offset_limit`.

**Control E, the sweep's base.** In `StringSearch`, change `return textPos + found;` to
`return found;`. `RequiredStringPrefilterTests`: **1 failed of 19** -
`The_prefilter_searches_the_slice_and_not_the_subject`.

**Control F, the newly live `req_pos` fast path.** In `basic_match`'s `Opcode.String` case
(`Matcher.cs:7542`), change `state.TextPos = state.ReqEnd;` to `state.TextPos = state.ReqEnd + 1;`.
The line is not unique on its own - the six `String*` arms all carry it - so anchor on the comment
above it:

```csharp
                        // the prefilter has already compared is not compared a second time.
                        state.TextPos = state.ReqEnd;
```

Full suite: **139 failed of 6451**. The point of this one is reachability: that arm was dead code
before S60 and the number says the suite exercises it heavily now.

**Control C did not survive, and that is the finding of the sitting.** The first cut chunked the
sweep into 64 Ki blocks with a cancellation poll between them, and a test claimed the poll fired.
Removing the poll changed nothing: 19 of 19 still passed, rebuilt, twice. The reason is that
`MatchState.InitMatch` sets `Iterations = 0` on **every** attempt (`MatchState.cs:749`), so
`basic_match`'s opening `state.Iterations == 0 && SafeCheckCancel(state)` (`:5102`) is an OPEN gate
every time, and a spent budget is always caught a few instructions before the prefilter runs. The
poll could only ever fire mid-sweep, which is a race with the machine and not testable. Measured the
stretch it was capping - `IndexOf` over 100,000,000 code units of a non-matching subject takes
**14.1 ms** (Release, this busy machine, 2026-09-20), so the largest string .NET can hold sweeps in
about 150 ms - and deleted the chunking, which also deleted the `needle.Length - 1` overlap it
needed. The test it left behind now says what it can actually prove, and its comment records why
the stronger claim is unprovable.

**Control D is retired with the chunking.** It broke the overlap
(`from + _prefilterChunk + needle.Length - 1` to `from + _prefilterChunk`) and fired 1 of 19; there
is no chunk boundary to break any more. The test it fired on survives as
`The_prefilter_finds_a_needle_deep_inside_a_long_subject`, because a needle 65,534 units in is the
row any future block-at-a-time sweep needs.

### The AOT binary size is not reproducible build to build

Three runs of `tools/run-aot-smoke.ps1` over the SAME source, this sitting: 6,985,216 bytes, then
6,982,144, then 6,985,216 again. A 3,072-byte swing with nothing changed but the build - so a
number quoted from one run carries about +/- 3 KB of noise, and the gate's own output does not say
so. S60's +12,288 against the slice's 6,972,928 reference is four times the observed jitter and so
survives it, but "the binary grew by N bytes" is not a claim this gate can support below about
4 KB. Worth a `run-aot-smoke.ps1` change that either builds twice and reports the pair, or states
the tolerance beside the number; that is maintenance, not S60.

### Line references in `OPTIMISATION-NOTES.md` drift, and three of them were wrong

S60 changed `Matcher.cs` by 420 added lines against 32 removed (`git diff --numstat`), and every
`Engine/Matcher.cs:NNN` below the insertion points moved with them. The rows this sitting rewrote
were re-resolved against the file as it now reads: the locator's `default:` arm is `:5012`, the
withheld `(*SKIP)` jump is `:4946`, the `try_match` test-node arm is `:5794` - that last one the
only stale row that survives from HEAD, where it says `:5404`. The three numbers an earlier draft
of these rows carried (`:4916`, `:4930`, `:5784`) were wrong when written, mid-sitting, and were
corrected before anything was committed, so `git show HEAD:docs/plan/OPTIMISATION-NOTES.md` will
not show them. The in-code comment and the two records that quoted `basic_match`'s cancel gate as
`:5082` now say `:5102`.

**The rows this sitting did not touch are still stale** - `:388, :4718, :4772, :10048, :10157` for
the `search_start` family reads as `:394, :5095, :5184, :10437, :10441`-ish today, and the other
`Matcher.cs` rows will have moved too. That is maintenance, not S60, and it wants doing as its own
commit with a check that re-resolves each row against a `ponytail`/`Phase 7` marker rather than by
hand - `SYNC-DIVERGENCE.md` already went file-granular for exactly this reason.

### Review

**Pass 1, over the code and test diff.** Five findings raised, five reproduced, five fixed. They
were: `build_fast_tables` cited as `_regex.c:5772` when it is `:6298` (four places - `Matcher.cs`,
`PatternObject.cs`, `PORTMAP.md`, `SYNC-DIVERGENCE.md`); `basic_match`'s cancel gate cited as
`:5082` when it is `:5102` (in-code comment, test comment, notes); the `SimpleStringSearch` comment
citing `:5168` for a gate at `:5242`/`:5102`; a test-file header claiming `string_search_rev` is
ported when only the forward arm is; and the `(a|a)*b` comment claiming a row goes red without the
prefilter, when stashing `Matcher.cs` and `PatternObject.cs` back to `3f1bf91` leaves the fixture
19 of 19 green at 16.5 s against 0.4 s. Each was reproduced before the edit; the last two were
false claims and were deleted rather than restated.

**Pass 2 was needed**, because pass 1's reviewer explicitly did not read the docs delta and the
fixes above had since changed five documents. It ran over that delta alone and raised three, all
three reproduced, all three fixed: the notes' `git diff --numstat` figure (`413`/`25`; the command
gives `420`/`32`); the probe's header repeating the `string_search_rev` claim pass 1 had killed in
the test file; and two upstream answers quoted in the test file - `finditer('needle', 'needle x
needle y needle')` and `(?:..(*SKIP)x|q)x` on `'ab cd xx'` - that had no row in
`tools/probes/upstream-required-string-prefilter.py`, so re-running the probe could never print
them. Both rows are in the probe now and it prints `[(0, 6), (9, 15), (18, 24)]` and `None`,
matching what the tests quote. The reviewer also noticed the working tree mutating under it
(`Matcher.cs:4946` flipping to the Control A edit and back) - that was this session running the
final control sweep, not a stale tree.

**The independent verifier** (fresh Opus, commit-ready tree, amendment 16 limb (d)) re-ran the
probe, the numstat, the full suite, all four negative controls and every upstream and port line
citation. Everything CONFIRMED except one, and two it was not asked to run:

- **DIFFERENT, and fixed**: the five unported arms of `locate_required_string` were cited as
  `_regex.c:11142-11364`. `:11142` is the closing brace of the `RE_OP_STRING` case; the five arms
  run `:11143-11365` (`case RE_OP_STRING_FLD:` at `:11143`, then `:11187`, `:11231`, `:11276`,
  `:11321`). Corrected in `Matcher.cs` (six comments), `PORTMAP.md` and `OPTIMISATION-NOTES.md`.
  The forward arm's own `:11082-11141` is confirmed - function at `:11082`, `case RE_OP_STRING:`
  at `:11098`, its `break;` at `:11141`.
- **COULD NOT RUN**: the three AOT sizes and the +12,288 arithmetic, for want of wall clock inside
  its budget. Those three numbers are this session's own `run-aot-smoke.ps1` runs and nobody else
  has reproduced them, which is exactly why the section above says the gate cannot support a claim
  below about 4 KB.
- **NOT ATTEMPTED, by instruction**: the differential oracle and the benchmark, both too slow for
  the verifier's budget. The oracle's own three-seed evidence is in the Gates table above.

The tree it was handed came back byte-identical: `420`/`32` on `Matcher.cs`, zero CRLF, all four
control sites back to their committed form.

### For the next sitting

1. Judge the `(*SKIP)`-plus-partial oracle family - rows 5014, 3633 and 5200 (all above) - so the
   gate can go green at every seed. All three are proved pre-existing against `3f1bf91`.
2. Benchmark before/after on a quiet machine, and say what `BacktrackingPort` now measures.
3. Items 2, 3, 6, 8-14, 16, 17.

## Sitting 3 - 2026-09-20 (05:56) - the route to close, written before the work

The skill's third-sitting rule: write down how the rest could be done in ONE sitting, then do that.
Deadline for this sitting is 09:54 local, so about 3h 45m.

**Why the rest is not one sitting of porting.** Item 2 is not a wiring job. `do_search_start`
dispatches roughly thirty `search_start_*` functions upstream (`_regex.c:7859-8400+`, one per
boundary opcode plus a `_rev` twin each); that is a sitting on its own, and items 8-14, 16 and 17
are ten further research-derived optimisations, each wanting its own measurement. Trying to land
them here would end in a checkpoint, which is the outcome the rule exists to stop.

**So the route is: close S60 on what a prefilter slice must prove, and move the unported items to
a successor slice rather than to prose.** Scope item 7 of the slice file already sanctions this -
"a partial landing is acceptable, a silent one is not". In order:

1. **Judge the `(*SKIP)`-plus-partial family as ONE batch** (rows 5014, 3633, 5200). It blocks the
   slice gate ("oracle GREEN at three seeds", and today's third default seed IS 20260920) and it is
   S57's item 1 as well, so one judgement clears both. PCRE2 is the second engine: it has both
   `(*SKIP)` and `PCRE2_PARTIAL_SOFT`; .NET has neither and TRE neither. One amendment-16 ceremony
   over the batch, not one per row.
2. **Benchmark before/after.** The machine is still not quiet - the Stryker queue is mid-chunk in
   `.claude/worktrees/stryker` - and the owner's 2026-09-19 rule allows pausing a background job for
   a blocking benchmark (`.scratch/pause-stryker.ps1` in that worktree, resume by relaunching
   `.scratch/run-queue.ps1`). If the sandbox refuses the pause, the PIDs are reported and the
   benchmark stays owed rather than being recorded from a loaded machine.
3. **The unported items move to a successor slice file**, carried verbatim, with their
   OPTIMISATION-NOTES rows kept and their in-code comments kept. That is a phase-plan change, so it
   is a numbered spec amendment plus a ROADMAP row in the same commit.
4. Close: boxes ticked or explicitly unticked with the reason, blind review over the whole diff,
   independent verifier, commit.

Checklist on disk, ticked as they land:

- [x] 1. `(*SKIP)`-plus-partial judgement, three rows, one verdict
- [ ] 2. Benchmark before/after (or the pause refused and it is recorded owed)
- [ ] 3. Successor slice file + spec amendment + ROADMAP row
- [ ] 4. Ratchet, AOT, oracle three seeds, blind review, verifier, commit

### The judgement: three rows, three doors each, and one of them nearly went the wrong way

All three are `(*SKIP)` plus a partial `search`, all three were proved pre-existing against
`3f1bf91` by sitting 2, and all three turn out to be **new rows of families this file already
judged** rather than a new question. Port right on each. The probe is
`tools/probes/upstream-skip-partial-anchor-grid.py`, written this sitting because
`gate-divergence-doors.py` caps its anchor sweep at three hits - which finds the leftmost `pos` on
a forward row but not the highest `endpos` on a reversed one, and one of these rows is reversed.

| Row | Upstream | Port | First anchor its own search tries | `(*PRUNE)` | Verb deleted |
|---|---|---|---|---|---|
| 20260920 / 5014, forward | (5, 5) | (4, 5) | pos 4 -> (4, 5) | (4, 5) | (4, 5) |
| 31337 / 5200, forward, sliced | (4, 4) | (3, 4) | pos 3 -> (3, 4) | (3, 4) | (3, 4) |
| 31337 / 3633, REVERSED | (0, 1) | (0, 0) | endpos 1 -> (0, 1) **see below** | (0, 0) | (1, 4) complete |

Codepoints throughout. The two forward rows are `partial-retry-carried-slice-forward`'s rows 4 and
5 exactly - upstream's zero-width partial at the far end of what it searched, where the first
anchor it must try answers what this port answers - and they became its rows 8 and 9.

**Row 3633 is the one that nearly went the wrong way, and it is worth writing down.** Read off the
anchor sweep alone it says the PORT skipped an anchor: a reversed search tries the highest `endpos`
first, upstream's own `match(0, 1, partial=True)` answers (0, 1), and that is upstream's answer,
while this port answers (0, 0), the lowest anchor. That reading is wrong, and the reason is the one
this file already states about the stepwise walk: **passing `endpos=1` sets `slice_end` to 1 and
MAKES `$` true there**, so the sweep reproduces the defect instead of testing it. The pattern ends
in `$`; upstream's own `$` is true at 0 and 4 alone. Upstream's answer ends at 1, where its own `$`
is false - which is `end-of-line-reads-a-skip-moved-slice`'s whole signature
(`try_match_END_OF_LINE`, `_regex.c:7110`, is the one edge predicate of eight that reads
`slice_end`; `RE_OP_SKIP` under `(?r)` writes that field at `:14553`). The `(?w)` control runs here
and is sharp, because `(?w)$` is true at [0, 4] too, so 1 is not a line end the twin would create:
`$` spelled out as `(?:(?=\n)|(?!\n|.))`, `(?w)$` and `(*PRUNE)` all answer (0, 0), this port's
answer. It became that entry's row 6 and its first PARTIAL SEARCH - every row there before it is a
split, a scan or a substitution. [Sitting 4 correction, from the blind review: sitting 3 wrote "row
5" here and in three other places. The array holds six rows and the new one is the SIXTH; row 5 is
the pre-existing `(?r)\p{ASCII}{1,3}(*SKIP)ﬀ$` / `sﬀﬀ` substitution. Sitting 3 also called seed
31337 part of "the default three-seed gate": the defaults are 7, 4242 and the date
(`tools/run-oracle.ps1:256`), so 31337 is an extra seed sitting 2 ran beyond the gate, and only row
5014's seed 20260920 came from a default.]

Gate after the three rows were added: `pwsh -File tools/run-oracle.ps1 -Seeds 20260920,31337` -
**GREEN, no row diverged at either seed**. Seeds 7 and 4242 were green in sitting 2 and this change
touches only the test project's judged-row lists, so no engine behaviour moved. Ratchet GREEN,
6451 / 6451, baseline unchanged at 6343.

**NO BLIND REVIEW AND NO VERIFIER OVER THIS DIFF.** The sitting was cut short at 92% of the
five-hour allowance window with an instruction to commit a green checkpoint, so items 2, 3 and 4
are untouched and the amendment-16 ceremony over this judgement is owed. A future sitting must run
it over the whole S60 diff since `14aad0a`, not only over the code, because what landed here is
evidence and prose.

### For sitting 4

1. **Blind review and the independent verifier over this sitting's diff** - the three judged rows,
   their two entry paragraphs and `upstream-skip-partial-anchor-grid.py`. The probe is in
   `tools/probes/`, so the verifier can re-run every number above.
2. One pinning test for row 3633's shape in `Gaps/Engine/PartialMatchingTests.cs` - the entry is
   row-keyed and its `PinnedBy` tests are all scans, so the first partial search in that family has
   no permanent test of its own yet.
3. Then items 2, 3 and 4 of this sitting's checklist, in that order.

## Sitting 4 - 2026-09-20 (08:00) - the owed ceremony, the pinning test, and the split

This sitting closes S60. It ran sitting 3's four owed items in order, and the only new engine code
is none: everything here is a test, a probe, prose or a plan change.

### What the blind review over sitting 3's diff found, and what survived reproduction

The pass was blind, over `1f55858` and the working tree. Findings are listed with how each was
checked, because a reviewer's claim about upstream is a hypothesis like any other.

1. **MAJOR, reproduced and fixed: row 3633 is the entry's SIXTH row, not its fifth.** Sitting 3
   wrote "row 5" in four places (`_endOfLineReadsMovedSliceOurs`'s comment, the entry's prose, the
   `PinnedBy` string, and this notes file). Counting the lines of
   `_endOfLineReadsMovedSliceRows` settles it: the literal holds six rows and the new one is last.
   Row 5 is the pre-existing `(?r)\p{ASCII}{1,3}(*SKIP)ﬀ$` / `sﬀﬀ` substitution, which the
   `PinnedBy` string was therefore mis-naming.
2. **Reproduced and fixed: seed 31337 is NOT one of the gate's three defaults.**
   `tools/run-oracle.ps1:256` reads them as 7, 4242 and the date, so 31337 is an extra seed sitting
   2 ran beyond the gate. Sitting 3 called it "the default three-seed gate" in three places, and
   the new test's doc comment repeated it; all four now say what it is.
3. **Fixed without argument: the S60 paragraph sat in the middle of the entry's prose**, ahead of
   paragraphs describing earlier rows. It is now last, where the row order puts it, and it names
   the `(?w)` control's result rather than an ordinal the file contradicts elsewhere.
4. **MAJOR, reproduced and fixed: the grid probe's `$` sweep inherited the REVERSE bit.**
   `dollar_positions` compiled each spelling with `row["flags"]` unchanged, and under REVERSE
   `match` anchors at `endpos` instead of `pos`, so every position answers and the sweep is
   meaningless. Settled by a two-run control rather than by reading the code:
   `'ab\ncd'` under MULTILINE answers at `[2, 5]`, under `MULTILINE|REVERSE` at
   `[0, 1, 2, 3, 4, 5]` (regex 2026.9.10, 2026-09-20). Both probes now mask the bit off, and
   `tools/probes/upstream-dollar-positions-for-moved-slice-rows.py` - written this sitting, and
   reading its six rows straight out of the committed ledger literal - ends with that control so
   the next reader does not have to take it on trust.
5. **Reproduced and fixed, and it corrected the ledger twice: the `(?w)` control runs on three
   rows, not one.** The new probe prints every row's `$` and `(?w)$` positions: rows 38101, 5721
   and 3633 are the three where the twin creates no phantom line end. The entry said
   "IT RUNS ON ONE ROW ONLY" (true of the two rows it then held) and called row 5721 "the first
   row ... on which the `(?w)` CONTROL CAN RUN" (row 38101 already ran it). Both sentences now
   carry a bracketed sitting-4 correction naming the probe.
6. **Reproduced and fixed: the grid probe read only `DIVERGE` headings.** A pinned row changes
   prefix - `report-31337.txt` of 2026-09-20 06:08 heads row 3633's block
   `EXPECTED end-of-line-reads-a-skip-moved-slice row 3633` and holds no DIVERGE heading for it -
   so the RECORDED columns printed `NOT RECORDED` for exactly the rows the probe exists to
   explain, and its "re-run the gate" hint would never have fixed that. It now matches both
   prefixes and prints upstream's `0:(0,1)` partial against the port's `0:(0,0)`.

### The pinning test

`PartialMatchingTests.A_reversed_partial_search_of_a_skip_ends_where_the_line_really_ends` pins
row 3633's shape: the port's zero-width partial at (0, 0) with `g1` unset, plus the three controls
that agree with it - `(*PRUNE)`, `(?w)$` and `$` spelled `(?:(?=\n)|(?!\n|.))` - and the verb-free
spelling, which answers a COMPLETE (1, 4) in codepoints and is printed rather than counted. The
provenance table in its doc comment is upstream's own run of 2026-09-20 on regex 2026.9.10 through
`tools/probes/upstream-skip-partial-anchor-grid.py`, not this port's output.

### The benchmark, and the machine that was not quiet after all

`pwsh -File tools/compare-benchmarks.ps1 -Job medium -ArtifactsPath
artifacts/bench/2026-09-20-S60-after`, 08:31 to 09:13, 59 benchmarks, against the committed
baseline for this machine (`bench/baselines/windows-x64-13th-gen-intel-core-i7-13850hx/net10.0.json`,
the S58 MediumRun). Verdict **GREEN** - nothing above the 1.25x gate on time or allocation.

**The prefilter is where the slice said it would be, and the size of it is the story:**

| workload | baseline | now | ratio |
|---|---|---|---|
| `ReferenceBenchmarks.BacktrackingPort` | 87,169,133 ns | **126.6 ns** | 0.00x, alloc 0.53x |
| `ReferenceBenchmarks.LiteralPort` | 17,355,123 ns | **84,542 ns** | 0.00x |
| `WorkloadBenchmarks.LiteralMatch` | 22,161,244 ns | **104,503 ns** | 0.00x, alloc 0.96x |
| `WorkloadBenchmarks.PartialMatch` | 47,086,253 ns | **160,922 ns** | 0.00x |

`BacktrackingPort` is the one to read carefully, because **it no longer measures what its name
says**. Its pattern is `(a|a)*b` over a subject with no `b` in it, so the required-string locator
now answers "the needle is not in the subject" before the backtracker runs at all: 87 ms to 127 ns
is the prefilter refusing the search, not the backtracker getting faster. The backtracking cost it
was written to watch is no longer under that number, and S62's inner-loop work needs a workload
that still reaches the matcher.

**Stryker restarted mid-run and the tail of the suite is contaminated.** The queue was paused at
08:31:44 before the launch, but `.claude/worktrees/stryker/.scratch/after-parsing.ps1` - a watcher
that waits for the exhaustive parsing chunk's report and then relaunches the queue, and which
`pause-stryker.ps1` deliberately does not kill - fired at 08:54 and ran until 09:19:41. The load
guard's own log is the evidence: `avg=1%` per five-minute summary through 08:53, then 16%, 34%,
27%, 34%, 26%. Three workloads read worse than the baseline in that window - `FuzzyLong` 1.21x,
`FuzzyBudgetThree` 1.18x, `CompileLargePattern` 1.15x, all above S58's 1.13 noise floor and all
inside the 1.25x gate. **Pausing Stryker for a benchmark means pausing `after-parsing.ps1` too.**

**Those three are not this slice, and it took an A/B to say so rather than an argument.** Re-running
them against the baseline kept giving a third number - an isolated `CompileLargePattern` read
317,051 ns against the baseline's 241,010 ns, which is 1.32x and ABOVE the gate - and a ratio
against a four-day-old baseline cannot separate the code from the machine. So the code was made the
only variable: `git worktree add --detach .claude/worktrees/bench-pre-s60 14aad0a~1` (commit
`3f1bf91`, the last one before S60 touched the engine; `git diff --stat 14aad0a~1 HEAD -- bench/`
prints nothing, so both sides run the same workloads), then the same three benchmarks in each tree,
back to back on the same machine, 10:58 to 11:02 on 2026-09-20 (`.scratch/ab-compile-bench.ps1`,
BenchmarkDotNet 0.15.8, **DefaultJob both sides**, which auto-sizes: counted from the artifacts,
pre gives 62/15/24 result measurements for CompileLargePattern/FuzzyBudgetThree/FuzzyLong and post
gives 37/45/12, so the two sides are not measurement-for-measurement equal):

| workload | pre-S60 (`3f1bf91`) | post-S60 | post/pre |
|---|---|---|---|
| `CompileLargePattern` | 282,588 ns | 290,002 ns | **1.026** |
| `FuzzyBudgetThree` | 85,978 ns | 89,390 ns | **1.040** |
| `FuzzyLong` | 178,312,217 ns | 183,681,233 ns | **1.030** |

This was NOT a quiet machine - the Stryker queue had restarted at 10:47 and the load guard reads
`avg=19%`, `avg=24%`, `avg=20%` over the three five-minute windows the A/B sits in - but the load is
the same on both sides, 90 seconds apart, and it is the ratio that is being read. A quiet re-run
would tighten these numbers; it cannot move a 1.03 to the far side of 1.25.

All three are under S58's 1.13 noise floor, so the slice costs these workloads nothing measurable.
The remainder is the machine: pre-S60 code reads 282,588 ns on `CompileLargePattern` today against
the committed baseline's 241,010 ns, and the baseline's own header warns that 13 of its rows were
taken while the machine was not quiet. The comparison to re-take is the baseline, which is S63's
job; a MediumRun A/B would sharpen these three numbers but cannot change their direction.

### The split: S60b

Items 2, 3, 6, 8-14, 16 and 17 move to
`docs/plan/slices/S60b-search-start-and-the-researched-prefilters.md`, **keeping S60's item
numbers** so every `ponytail:` comment and OPTIMISATION-NOTES row already in the tree still
resolves. Spec amendment 30 and a ROADMAP paragraph record the phase-plan change in this same
commit; OPTIMISATION-NOTES' two paragraphs that said "S60 items 8-17" now point at S60b.
