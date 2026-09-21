# S57 sittings

Per-sitting notes for `docs/plan/slices/S57-coverage-backstop-and-phase-close.md`. The slice file
stays the spec; working state lives here.

## Sitting 1 - 2026-09-20

Machine is NOT quiet: the Stryker mutation queue runs in the `stryker` worktree (3 runners, ~10 h
left) and an Opus session (S73) runs npm builds in the `demo` worktree. Nothing in S57 is a timing
measurement - coverage, oracle waves, controls and the AOT publish are all correctness instruments,
so load makes them slower and not wrong. The one item that WOULD need a quiet machine is re-taking
benchmark baselines, and S57 does not re-take them: the Phase 7 handover hands over S54's committed
baselines and S58's method, it does not re-measure. So no `quiet-request.txt` is expected for this
slice; if one becomes necessary the reason gets written here first.

### Checklist

- [x] 1. Gate item 1, skips: ZERO. `docs/STATUS.md` says 100.0% parity, 1967 of 1967 ported tests
      passing, 0 skipped in every area, and "None: every test enabled". `grep -rn 'Skip("needs:'
      tests/` returns 0 lines. Nothing regressed since S42.
- [x] 2. AOT test gate: DONE. Reproduced red first (one `IL2065` at
      `Conventions/PublicApiDocumentationTests.cs(62)`, then `MSB3077`), then fixed, then GREEN:
      6432 total, 0 failed, 3 skipped, 39.7 MB native binary. Decision and reasoning below.
- [x] 3. Coverage backstop: DONE. 918 uncovered lines to 878, six wholly-unentered opcode arms to
      **zero**, eight members still unreached and each judged. Three new gap-test files; the number
      Phase 7 regresses against is below.
- [ ] 4. Gate item 2, oracle: the extra wave (`fuzzy,interactions` at 99991 and 57057) is GREEN, its
      one red row judged and pinned. **The default wave at `-Count 6000` is RED at all three seeds -
      3, 3 and 14 rows, twenty distinct - and none of it is S60's doing.** Sitting 3 below; the rows
      go to S57b and the phase does not close until they are judged.
- [x] 5. Gate item 3, mutation: quoted from the queue's own reports - **7,342 tested, 0 survived**
      across api, substitution, parsing and 59 engine chunks. S56 was never run as a slice.
- [x] 6. Gap-test provenance audit: PASSES. **0 DIFFERENT, 0 without provenance.** Detail below;
      one follow-up owed (I did not spot-check the agent's calls - see the caveat).
- [x] 7. Known-bug ledger table: all 24 entries tabled with their final state. **The gate's question
      gets a NO**: five are inherited and unfixed (17 in part, 18, 19, 20, 21), all parked by S50
      and none scheduled. Parked as named blockers in STATE.md.
- [x] 8. Symbol accounting: `tools/check-symbols.py` exit 0, **567 / 567, identical to S43**;
      prose-accounted 36 where S43 had 38, both steps down explained.
- [x] 9. Controls re-run for S44-S56 at their seed and at 99991, against measured clean baselines.
      S53b-A registered and reproduces; S48b-A has lost its wave signal but its suite instrument
      fires harder than recorded; four are thin or dead exactly as their own slices recorded.
- [ ] 10. Bookkeeping: CHANGELOG, ROADMAP Phase 6 rate and estimate, STATE.md, STATUS.md, spec table.
      **BLOCKED until S57b**: all of it writes that Phase 6 is closed, and the gate is red.
- [ ] 11. Phase 7 handover. Blocked for the same reason.
- [ ] 12. Ratchet GREEN, blind review, verifier, commit. Done once, for sitting 3's CHECKPOINT
      commit (record at the end of this file); it runs again over items 10 and 11 when S57b is green.

### Log

**The AOT gate (item 2), decided.** Reproduced red before touching anything: one
`Trim analysis error IL2065` at `Conventions/PublicApiDocumentationTests.cs(62)` on
`type.GetMembers(BindingFlags)`, then `MSB3077` from `ilc`. Of the slice's three options,
ANNOTATING is not actually available - the `Type` flows out of `Assembly.GetExportedTypes()`,
which carries no `DynamicallyAccessedMembers` of its own, so a `[DynamicallyAccessedMembers]`
helper parameter only relocates the same warning to its call site. EXCLUDING the test from the
native publish would drop AOT coverage of the convention that the shipped package is documented,
and the package is exactly what an AOT consumer gets. So SUPPRESSED, as `IL2065` added to the test
project's existing `NoWarn` list rather than as a separate `UnconditionalSuppressMessage`: the five
neighbouring reflection warnings are already handled there, and one mechanism is easier to audit
than two. The reason is written out in `FuzzyRegex.Tests.csproj` beside them.

What makes the suppression honest is a new floor rather than an assertion. `FuzzyRegex` is a
`TrimmerRootAssembly`, so its members are not trimmed - but the test's two existing floors (>10
exported types, >1000 doc entries) both SURVIVE trimming, so neither could ever have caught it.
The test now counts the members it actually checked and fails below 100. Measured under the JIT:
110. The native run then passed, so the published binary is standing proof that the rooted assembly
keeps its members - the gate re-proves the suppression every time it runs.

`tools/run-aot-tests.ps1` GREEN: 6432 total, 0 failed, 3 skipped, 39.7 MB. The 3 skips are the
pre-existing object-graph walks that read BCL private fields, which have no AOT accessor and run
under the JIT instead.

**A stale-report trap in the oracle tooling, found and fixed (not in the slice's scope, but the
gate's own evidence depends on it).** `run-oracle.ps1` copied `wave-<seed>.jsonl` and
`report-<seed>.txt` only when a seed went RED. So `report-<seed>.txt` meant "the last time this
seed was red", while every consumer reads it as "what this seed did". Measured 2026-09-20: the
default wave was GREEN at 4242, `report-4242.txt` was still the red one from 2026-09-18 16:43
(`diverge 3`), and `tools/probes/gate-divergence-doors.py` asked every judged family's control
about those three rows and printed them beside the rows that really did diverge, with nothing to
tell them apart. The copy is now unconditional, so a green seed overwrites its own report with a
truthful green one. Verified: `-Seeds 4242` now GREEN and `report-4242.txt` reads
`agree 6347 ... diverge 0 of 6380 rows`.

**Oracle (item 4), and a `-Count` misreading that paid.** The gate as written is the DEFAULT wave
(`-Count 300` a generator, which is the ~6000 rows the ROADMAP and the skill both mean). Run at the
three default seeds: **GREEN at 7 and 4242, RED at 20260920 with 1 divergence of 6380.**

Before that I had read "6000 rows" as `-Count 6000` and run it at seed 7. `-Count` is PER
GENERATOR, so that is 126,080 rows - twenty times the gate - and it came back
`agree 125536 unsupported 0 expected 468 timeout 2 resource 71 diverge 3`. Those three rows are
real and are kept, because a deeper wave finding rows the gate's depth cannot reach is the gate
doing its job rather than an accident to discard.

**Four diverging rows, and they are NOT one family.** `gate-divergence-doors.py` (saved to
`.scratch/doors-s57.txt`) puts each judged family's own control to each row:

- seed 20260920 row 5014, `partial`, `(?:[^\p{L}](*SKIP)[[:digit:]]|[^a-f])([a]?)\g<1>\b` over
  `'AAaa '`. Upstream `(5, 5)` partial, port `(4, 1)`. BOTH the `(*PRUNE)` spelling and the
  verb-deleted spelling answer `(4, 5)` - this port's answer. So the only thing standing between
  the two engines is the slice bound `(*SKIP)` moves, which is the `search-start-elsewhere`
  signature.
- seed 7 row 102670, `partial-sliced`, reversed, with a slice. Upstream `(2, 0)`, port `(2, 1)`.
  Here the `(*PRUNE)` spelling answers `(2, 2)` - UPSTREAM's answer - and only deleting the verb
  gives `(2, 3)`. That is a DIFFERENT signature from row 5014 and must not be waved through with it.
- seed 7 row 75921, `interactions`, `(?e)` fuzzy: spans agree, the fuzzy CHANGE LISTS do not
  (upstream `[s:2]`, port `[i:4][d:4]`).
- seed 7 row 76160, `interactions`, `split` under `(?b)`: 4 parts each, different parts.

None of the four is judged yet. Judging them is the next sitting's first job; nothing here is
deferred, it is simply not finished.

**Coverage backstop (item 3), measured.** `dotnet run --project tests/FuzzyRegex.Tests
--configuration Release -- --coverage --coverage-output-format cobertura`, read by
`.scratch/coverage-report.py` (kept as `.scratch/coverage-s57.txt`):

    src/FuzzyRegex: 39 files, lines 82984/83895 = 98.9%, branches 11468/13942 = 82.3%

**The gate's actual question is answered: NO file in `src/FuzzyRegex` is unreached by every test -
zero of 39.** The percentages are for Phase 7 to compare against, not targets.

Read the numbers with one correction in mind: cobertura lists a `<line>` under EVERY class that
contains it, so the first pass of the script reported exactly double - 824 uncovered lines in
`Matcher.cs` where there are 412. The script now deduplicates by line number and counts a line
covered if any class hit it. Anyone re-deriving these figures with a different reader should check
that first.

The 911 uncovered lines, worst first: `Engine/Matcher.cs` 412 of 3373, `Parsing/Nodes.cs` 172 of
1372, `Engine/NodeCompiler.cs` 97 of 836, `Parsing/ParseFunctions.cs` 64 of 1026,
`Unicode/UnicodeProperties.g.cs` 61 of 25065 (generated), `Engine/GuardList.cs` 36 of 112.
**`GuardList.cs` is the one to look at first**: it is proportionally by far the worst at 32%
uncovered, and it is small enough to classify line by line in one go. Classifying all 911 as
"tested" or "fidelity dead code" is the next sitting's work.

**Gap-test provenance audit (item 6): PASSES, and the instrument cost an hour rather than a
sitting.** Owner rule 2026-09-15 says every gap test's expected value names the real upstream run
it came from. ~935 candidate assertions across 74 Gaps files is exactly the shape that cost S52 18
sittings of hand-judging, so it was scripted instead (`.scratch/replay-provenance.py` and
`replay_worker.py`): extract each quoted Python call out of each comment, RE-RUN it on regex
2026.9.10, and compare the answer to what the comment claims. 599 calls extracted and replayed; a
mechanical judge (`.scratch/judge-provenance.py`) settled 396 of them outright, leaving 203 whose
shape needed a person. Those 203 went to an independent Opus agent.

    599 replayed | 396 mechanically agreed | 203 to the agent
    agent: 182 agrees, 0 DIFFERENT, 21 deliberate, 0 without provenance

**Zero DIFFERENT and zero missing-provenance is the result the rule exists to get.** All 21
non-agreeing rows are deliberate differences that each name their own justification: ledger 11
(fuzzy counts and change lists, 3 rows in `FuzzyMatchingTests.cs`), the Turkic default-folding
divergence (9 rows in `CaseFoldingTests.cs`), `bestmatch-ranks-by-cost`, `search-start-partial`,
the `(?e)` cost ranking, the upstream `(?b)` bug pinned by
`tools/probes/upstream-bestmatch-loses-a-partial.py`, and a reported reversed zero-width
partial-flag difference.

Two instrument notes, both worth keeping. An earlier attempt scored set MEMBERSHIP of quoted lines
against freshly re-run `tools/probes/upstream-*.py` output and gave 38 confirmed against 767
unmatched - a useless signal, because most provenance is a hand-written call-and-answer rather than
a pasted probe line. That number is not a finding and was never reported as one. And the replay
SEGFAULTED the interpreter (exit 139) partway through, because at least one quoted shape crashes
upstream deliberately (the POSIX fuzzy crash, ledger 9); the worker was moved into a child process
that appends one flushed JSONL line a row, with the parent restarting past whatever index killed
it. Final tally: 599 parsed, 6 interpreter crashes, none lost.

**Caveat, owed to the next sitting: I did not spot-check the agent's own calls.** A subagent's
verdict is a hypothesis like any other, and "0 DIFFERENT" is the answer that most deserves a probe.
The sitting was stopped by the allowance window with the spot-check open. The one to do first is
`PartialMatchingTests.cs` around line 1180
(`A_skip_does_not_block_the_repeat_retreat_a_partial_needs`), because it rests on a VERSION-DRIFT
claim - that 2026.7.19 answers `(0, 3)` and 2026.9.10 answers `(3, 3)` - which, if the agent read it
wrongly, is a real divergence rather than a deliberate one. It is covered by ledger 15 and
`tools/probes/upstream-skip-blocks-a-repeat-retreat.py`, so the check is one probe run.

The agent also flagged two comments that print spans in this port's `(index, length)` form while
reading like Python's `(start, end)`: `DemoEngineContractTests.cs:519` and
`PartialMatchingTests.cs:796`. Both assertions are CORRECT; only the notation is ambiguous. A tidy-up,
not a finding.

**Two tests fail under coverage instrumentation and pass without it, and it is a latent flake
rather than a coverage artefact.** `DemoEngineContractTests.One_match_cannot_carry_unbounded_capture_spans`
and `A_partial_answer_whose_capture_list_was_clipped_says_truncated`. The suite is 6432/6432 in
Release with no instrumentation; under it, both fail with `KeyNotFoundException` on
`GetProperty("matches")` at 2s 026ms and 2s 049ms. That is not a coincidence:
`DemoEngine.MatchTimeout` is `TimeSpan.FromSeconds(2)`, so the demo returned `{"error": ...}`
instead of a match object. Both tests assert a SIZE cap (`truncated: true`) but only reach it if
the engine gets enough CPU inside a two-second wall-clock budget, so a slow enough CI box or a
loaded machine can fail them with no coverage tool involved. The fix is a real decision about what
the demo contract promises and is NOT taken here; it is the next sitting's, with this evidence.

**Sitting closed by the allowance window at 94%, on the orchestrator's instruction to commit a
green checkpoint.** Ratchet GREEN before the commit: 6432 tests, 6432 passing, 6324 distinct ids
against a 6324 baseline, 0 failed, 0 skipped.

**No blind review and no verifier this sitting, deliberately.** Both are slice-CLOSE steps and the
slice is not closing: S57 stays in `docs/plan/slices/`, so this is a checkpoint. Starting a
four-minute inspection plus a review pass inside a 6% window is how a sitting ends uncommitted, and
uncommitted work is work the driver throws away. What lands here is three self-contained,
independently verified changes - the AOT suppression (proved by the native gate running green with
a new member-count floor), the oracle stale-report fix (proved by re-running seed 4242 and reading
the fresh report), and notes. The review and the verifier are owed over the WHOLE slice diff before
S57 moves to `done/`, and the next sitting must not treat this checkpoint as having had them.

## Sitting 2 - 2026-09-20

Machine IS quiet this time: the orchestrator reports the Stryker queue FINISHED, so there is no
mutation load. Two light Opus sessions may run (S56 reading reports, S74 review agents doing npm
bursts). As in sitting 1, nothing in S57 is a timing measurement, so no `quiet-request.txt`.

### Where sitting 1's four "unjudged" rows actually stand, settled before doing any new work

Sitting 1 closed naming four diverging rows as the next sitting's first job. Reading the record
first rather than re-measuring turned three of the four into work that is already done, which is
the "same result, cheapest route" rule paying for the twenty minutes it took:

- **Seed 20260920 row 5014 IS ALREADY PINNED, by S60, after sitting 1 saw it.** S57's checkpoint is
  `3f1bf91` (01:33 today); S60's close is `762faaf` (11:22). `partial-retry-carried-slice-forward`
  in `ExpectedDivergences.cs` now carries it as ROW 8, with the anchor grid as evidence, and S60's
  entry text explicitly proves the row pre-existing **against 3f1bf91** by stashing `src/` - so it
  is neither S60's doing nor S57's. Sitting 1's RED at 20260920 was a true reading of a tree that no
  longer exists. This is not ticked on S60's say-so: the gate re-run below is what settles it.
- **Seed 7 rows 75921 and 76160 are NOT new and are NOT the default gate's.** They come from the
  126,080-row `-Count 6000` wave sitting 1 ran by misreading the gate, and they are S52's own
  long-standing residue, measured and deliberately left unpinned with the reasoning in DECISIONS
  2026-09-15 (lines 481-482) and `S52-sittings.md:1218-1236`. 76160's separating control - the
  `(*PRUNE)` ablation - provably does not terminate at 300 s, so the control that would judge it
  cannot be run at all; 75921's count cannot be settled from the row because upstream's own totals
  are self-consistent even though its change list is not.
- **Seed 7 row 102670 is the only one of the four with no prior record** (`partial-sliced`,
  reversed, `(*SKIP)`), and its signature genuinely differs from 5014's: here the `(*PRUNE)`
  spelling gives UPSTREAM's answer and only deleting the verb gives this port's, which is the
  opposite way round from 5014. Judging it is real work and is done below.

The distinction that matters for the gate, and for item 7: **the gate is the DEFAULT wave**
(`-Count 300`, ~6,380 rows a seed), which is what the ROADMAP and the skill both mean by "6000
rows". The 126k-row wave is a widening sitting 1 ran on top. A row only that wave reaches is not a
red gate; and an UNJUDGED row is "neither engine is shown wrong", which is not the same thing as a
known bug left in the port. Item 7 asks the second question.

### Order this sitting works in, and why

Cheap and owed first, so that nothing already promised is lost to the window, then the two big
bounded items, then the close:

1. the provenance spot-check owed by sitting 1 (one probe run);
2. item 8, symbol accounting (`tools/check-symbols.py` exists);
3. item 5, mutation scores quoted from S55/S56;
4. item 7, the ledger table;
5. item 3, classifying the 911 uncovered lines - the biggest correctness item left;
6. item 9, controls for S44-S56 (`tools/run-controls.py` and `controls.json` exist, but the range's
   later slices are not in the registry yet);
7. item 4, the default gate wave re-run LAST, because pinning changes what it reports;
8. items 10-11 bookkeeping and handover, then item 12.

### Step 1, done: the provenance spot-check, and what it found in sitting 1's own instrument

Sitting 1 reported the provenance audit as **"599 replayed | 396 mechanically agreed | 203 to the
agent | 0 DIFFERENT"**, and owed the next sitting a spot-check of the agent's own calls on the
grounds that "a subagent's verdict is a hypothesis like any other, and '0 DIFFERENT' is the answer
that most deserves a probe". That was the right instinct and the probe found something.

**58 of the 599 rows never reached upstream at all.** The replay died inside its own harness before
asking, and every one of those 58 then fell into the 203 the agent judged - 29% of the agent's pile
was rows for which there was no upstream answer on the page. "0 DIFFERENT" was therefore a statement
about **541 asked rows, not 599**. Three defects in `.scratch/replay-provenance.py`, all in its
extractor rather than its comparison:

1. **It prefixed `regex.` onto a bare op call** (its lines 75-77). A comment quoting a COMPILED
   pattern's method - `p.match(subject, pos)` - became module-level `regex.match(pattern, string)`,
   so the subject was read as the pattern and `pos` as the subject. 26 rows, all `TypeError`.
2. **Comment shorthand is not a Python name.** `VERSION1`, `F|I`, `LONG`, `pat`, `s`, `subject`,
   `that`, `FUZZY` are written for a human reader. 32 rows, all `NameError`.
3. **It read one comment line at a time**, so a call split across two lines was truncated.

All 58 have now been asked, with each row's pattern and subject taken from the test body under its
own comment rather than invented, and **every one confirms**. The audit's conclusion survives; it now
rests on 599 asked rows instead of 541. The instrument is committed as
`tools/probes/upstream-provenance-unasked-rows.py` (67 cases, several rows carrying more than one
call), so the verifier can re-run it - a probe left in `.scratch/` is COULD NOT RUN by amendment 16.

Three things worth keeping from the run:

- **The megabyte subjects are self-checked before anything is asked.** `LONG`, `LONG_PARTIAL` and
  `DENSE` are C# fields the comments name in shorthand, so the probe rebuilds them from
  `OptimiserTrapsTests.cs:73` and asserts the lengths that file's own line 102 states -
  1048631 / 1048625 / 102455, all OK - and exits if any is wrong. A wrong subject would otherwise
  produce a confident wrong verdict, which is the failure mode this whole step exists to catch.
- **The one row measured on the OLD upstream still holds on the new one.** `PartialMatching:333` is
  S40c's row, recorded "Measured 2026-09-13 on regex 2026.7.19". Re-asked on 2026.9.10 it still
  answers `(0, 2) partial`, and its greedy twin control at :335 still answers None. That is the
  version drift checked where it could actually bite, not argued away.
- **My own first transcription of that row was wrong and the probe caught it** - I asked
  `^([^a-f]??)([\w\s])` on `'a.Aa'` and got `(0, 1)` against the claimed `(0, 2)`. The comment says
  `^([^a-f]??)([\ ])$` on `' \r'` with `V1|M|I`. A DIFFERENT is a hypothesis about the comment until
  the call has been read back off the comment, and here the comment was right and I was not.

**One real defect in the comments, fixed in this commit.** `ReverseMatchingTests.cs:290` quoted its
five calls WITHOUT the `partial=True` that is the entire question, so as written the first line
answers None rather than the partial it records. The recorded ANSWERS were all correct - they
reproduce exactly once the flag is restored - but a provenance comment that does not reproduce its
own answer is not provenance, which is the point of the owner's 2026-09-15 rule. The comment now
carries the flag on all five calls, spells out `F|I`, and records the 2026-09-20 re-run.

Item 6 is therefore complete, including sitting 1's caveat.

### Step 2, done: item 8, symbol accounting

`python tools/check-symbols.py`, exit 0:

```
upstream/src/_regex.c function definitions: primary (brace-depth) 567, cross-check (bare '}' count) 567
named in docs/PORTMAP.md outside its accounting section: 299
not individually named there: 268
named nowhere and in no cited line range, accounted for in PROSE by a family row: 36 (pinned: 36)
Seam.For occurrences under src/: 4
```

**567 and 567, identical to S43, S36 and S26.** `Seam.For` is still 4 and still the four `default`
arms of exhaustive switches that S43 reported with its reason; nothing has changed there.

**The prose-accounted set is 36, where S43 recorded 38, and both steps down are the pin working as
designed rather than drift.** S44 removed `scanner_iternext` (named outright in the sync log). This
sitting removed `search_start_STRING_REV`, and the first run of the tool this slice made was RED on
exactly that:

```
STALE PIN: PORTMAP.md now accounts for these by name or line range, so they must come out of PROSE_ACCOUNTED: search_start_STRING_REV
```

Worth writing down HOW that was settled, because two plausible explanations were both wrong:

- **Not a PORTMAP edit to its own family row.** `git log -L 425,425:docs/PORTMAP.md` shows the
  `search_start_*` (`:7859`-`:8373`) row's range unchanged since S26.
- **Not S44's line shift.** `git -C upstream show 1760a206:src/_regex.c` (the pre-sync commit) puts
  `search_start_STRING_REV` at line 8373, exactly where 2026.9.10 puts it. The sync did not move it.
- **It was S52d.** Its `init_match` row cites `(:8335-8382)` for "its three reversed string helpers",
  and 8373 is inside that. So the name is now covered by an explicit cited range, which is stronger
  than a family row's prose shorthand, and the pin must let it go. Documented at the line in
  `tools/check-symbols.py` in the same shape S44 used for `scanner_iternext`.

The tool is not run by the ratchet or any hook (S44 noted this), so a stale pin sits until someone
runs it. That it went stale from a doc edit three slices ago, and was caught by a tool that fails in
BOTH directions, is the pin earning its keep.

### Step 3, done: item 5, the mutation scores, and what they say about S56

The slice asks for "S55/S56's mutation scores quoted" and hands Phase 7 "the mutation scores as the
floor Phase 7 must not lower". **S56 was never run as a slice** - it sits in
`docs/plan/slices/blocked/` waiting on the overnight engine queue - so the numbers below are the
queue's own reports, read straight out of each chunk's `reports/mutation-report.json` in
`.claude/worktrees/stryker/TestResults/stryker/` rather than out of any session's summary.

| chunk group | killed | timeout | **survived** | other |
| --- | --- | --- | --- | --- |
| `api` | 237 | 0 | **0** | - |
| `substitution` | 261 | 0 | **0** | - |
| `parsing` (`-recovered` + `-remaining`) | 2,156 | 29 | **0** | 4 RuntimeError |
| `engine-rand-01`..`-59` | 4,613 | 46 | **0** | 14 RuntimeError |
| **total** | **7,267** | **75** | **0** | 18 RuntimeError |

**Mutation score 100%: 7,342 mutants tested, zero survivors, across every chunk the queue ran.**
The api/substitution/parsing rows reproduce S55 sitting 4's recorded numbers exactly (its
2,156/29/4/2,189 for parsing is 1,629+527 killed, 14+15 timeout, 3+1 RuntimeError, and
`parsing-recovered`'s 543 `Pending` are precisely the 543 that `parsing-remaining` then ran) - which
is a cross-check on the aggregation, not a second source.

Two things checked rather than assumed before quoting any of it:

- **Every one of the 59 engine reports postdates S55's validity cutoff.** S55 ruled INVALID anything
  timestamped before 2026-09-17 13:00, because those runs mislabelled survivors as Timeout. The
  engine reports run 2026-09-18 16:03 to **2026-09-20 12:15**, the last of them this morning. The
  three chunk directories with no report at all - `api-failed-1350`, `api-failed-1354`,
  `parsing` - are the runs S55 already discarded as invalid or superseded, not missing work.
- **The 18 RuntimeError mutants are not hidden survivors.** All 14 engine ones are in
  `NodeCompiler.cs` and all are the same mutation: `args.Code += n` flipped to `-= n`, or the
  increment deleted (`:517`, `:603` x2, `:705`, `:761`, `:862`, `:998`, `:1079`, `:1105`, `:1223`,
  `:1305`, `:1336`, `:1367`, `:1596`), with Stryker's reason "test host crashed or became
  unreachable while testing mutant". Winding the opcode-emission cursor backwards crashes the host,
  which is the same shape S55 judged for parsing's four StackOverflow mutants: detected, excluded
  from the score by the tool, not a place a test is missing.

**The consequence for S56 is that its scope is empty.** S56 is "read the engine's survivors and kill
each with a test or record why it is equivalent", and there are none to read. S57 does not close
another slice on its own say-so, so this is recorded here and in the Phase 7 handover as evidence for
that decision rather than taken as made: the queue finished, the score is 100%, and what S56 was
scheduled to do has no input. The floor Phase 7 must not lower is **zero survivors at 7,342 mutants**.

### Step 4, done: item 7, every ledger entry's final state - and the phase DOES have blockers

Entries 1-15 are S48's inventory (`S48-verb-and-partial-inherited-bugs.md:58-72`), which was built
row by row and independently verified at the time; this sitting re-checked only the two rows S48
left open, since those are the ones that could have moved. Entries 16-24 are read from
`docs/plan/upstream-reports/LEDGER.md`'s own status paragraphs.

| # | final state |
| --- | --- |
| 1 | upstream-only; port pinned right, permanently |
| 2 | upstream-only; port right (PCRE2 10.47 agrees with this port) |
| 3 | upstream-only; port right |
| 4 | upstream-only; port right |
| 5 | six doors; five closed by S40a/S40b, the sixth was SHARED and **fixed here (S48)** |
| 6 | upstream-only; **fixed here (S35)** |
| 7 | inherited; **fixed here (S45)** |
| 8 | upstream-only; port right |
| 9 | upstream-only; **port side WHOLLY CLOSED** - crash half S43, count half S48b |
| 10 | **CLOSED** - upstream fixed it in 2026.8.30, both clamps ported by S44 |
| 11 | A/B **fixed here (S47)**, C/D **fixed here (S48b)**; E, F and G upstream-only |
| 12 | inherited; **fixed here (S46)** |
| 13 | upstream-only; never reproduced here, mechanism traced by S47c |
| 14 | inherited; **fixed here (S47)** |
| 15 | upstream **regression in 2026.9.10**; this port never had it (S40b) |
| 16 | upstream-only; POSIX drops the leftmost-longest match |
| 17 | inherited; **fixed here (S50)** for the issue's shape - **two other orderings PARKED** |
| 18 | inherited **and amplified** (about 3x `regex`'s bytes a repetition) - **PARKED by S50** |
| 19 | inherited - **PARKED by S50**; two fix designs built and reverted |
| 20 | inherited - **PARKED by S50**; S50 proved it is the same bug as 19 |
| 21 | inherited - **S50 FIXED IT AND REVERTED**; the sound fix is PCRE2's `hitend` model, a slice |
| 22 | inherited; **fixed here (S50b)** |
| 23 | upstream-only; POSIX invents a match the flagless engine does not make |
| 24 | **CLOSED** - owner ruled, **fixed here (S52d)** |

**The gate item asks me to confirm no entry is "inherited, unfixed", and I cannot: five are.**
17 (in part), 18, 19, 20 and 21. The slice file says what to do about that - "if one is, the phase is
not closed: park it as a blocker" - so they are parked as named blockers in STATE.md rather than
written off. Each was parked deliberately, by S50, with the mechanism established and the reasoning
recorded in its entry; none is a surprise and none is an unexamined "inherited, unfixed" of the kind
the gate is hunting. Read together they are three pieces of work, not five:

- **19 + 20 are one bug** (S50 proved it), `\m`/`\M` against a fuzzy section, upstream issue 563/564.
- **21 needs the PCRE2 `hitend` model** - new match state, a decision about which span a
  hitend-derived partial reports, and its own oracle pass. Already carried as a named blocker.
- **18 is a memory-cost bug and is the one Phase 7 naturally owns**, since it is the only entry whose
  fix is an allocation change: 611 bytes a repetition here against upstream's 192 and `re`'s 99.
  S61 (per-match allocation) is where it belongs, and it is not yet written into that slice.

**The real finding is that nothing in the queue schedules any of them.** The owner's rule of
2026-09-12 is that every conclusively identified bug is fixed before 1.0, inherited ones included,
and S69 is the 1.0 release. Between here and S69 the queue holds S60b, S61, S62/b/c, S63, S68 and
S74 - performance, docs and the demo. So on the plan as written these five would arrive at 1.0
unfixed, not by a decision but by never having been given a slice. That is a plan gap rather than a
code defect, which is why this slice records it and does not invent slices for it: creating them
amends the ROADMAP and the design spec together, which is the owner's call.

### Step 5, done: item 3, the coverage backstop - and the two demo tests that were blamed on load

**Sitting 1's coverage data was stale and was thrown away.** It ran at 01:28; `git log` shows S60
changed `Matcher.cs` at 05:50 (`14aad0a`), so its line numbers described a tree that no longer
existed - which is how a lookup at `Matcher.cs:9186` came back "NOT A COVERABLE LINE" for a line
whose text I had just read. Re-run against HEAD as `s57-coverage2.cobertura.xml`, then once more at
the end of this sitting as `s57-coverage3.cobertura.xml`, which is the run every number below comes
from.

**The two flaking demo tests are not flaky and the machine was not loaded.** One-parameter
measurement, `tools/probes/demo-cap-timing.cs` (committed, so it can be re-run):
`One_match_cannot_carry_unbounded_capture_spans` matches in 27 ms and
`A_partial_answer_whose_capture_list_was_clipped_says_truncated` in 44 ms, against the demo's
two-second `MatchTimeout` - a 45-to-70-fold margin, and handing the budget down does not change it
(21 ms and 36 ms with the clock polled). The plain suite is 6,475 tests in 7 s and green. The SAME
suite under `--coverage` takes 33 s and fails those two at 2s 044ms; the 61 tests of that class
alone under coverage pass. So the cost is instrumentation, and reaching the span cap costs 50,000
spans by definition, so no smaller subject dodges the clock. Recorded in the tests' own remarks. No
production seam was added for it: the coverage run is a one-off analysis tool and not a gate, and a
test-only timeout override in shipped demo code would buy nothing the gate needs.

**What the backstop found, and what was done about it.** The unit the slice asks for is "every file
or branch with NO test at all", so the lines were grouped into the member that holds them and the
`case` arm that holds them, and only the wholly-unreached ones were read by hand.

| | before | after |
|---|---|---|
| uncovered lines in `src/FuzzyRegex` | 918 | 878 |
| members holding an uncovered line | 265 | 257 |
| members **wholly** unreached | 15 | 8 |
| `BasicMatch` opcode arms with **no** covered line | 6 | **0** |

Three test files closed the reachable ones, each expectation measured against regex 2026.9.10 on
2026-09-20 and quoted beside the assertion:

- `Gaps/Engine/NestedSetOperationTests.cs` (7 cases) - a set operation as a MEMBER of another,
  `[[[a-z]--[aeiou]]&&[a-m]]`. Those six dead arms were `MatchesMember`'s and `MatchesMemberIgn`'s
  `SetDiff`/`SetInter`/`SetSymDiff` cases; `InSetInterIgn` was dead whole. The suite tested set
  operations one level deep only, and upstream's own `test_set` never nests one inside another.
  Verified by a coverage run over that class alone: `:445`, `:447`, `:449`, `:608`, `:615`, `:622`
  and `:689` all go from 0 to 2 hits.
- `Gaps/Engine/WordFlagOpcodeTests.cs` (4 cases) - `\m` and `\M` under `(?w)`, and a reversed `.`
  under `(?w)`. `(?w)` was tested against `\b` and `$` but never against these, leaving
  `TryMatchDefaultStartOfWord`, `TryMatchDefaultEndOfWord`, `AtDefaultWordStartOrEnd`,
  `TryMatchAnyURev` and two `ZeroWidthOpcode` overrides unrun. Same verification: `:1537`, `:1801`,
  `:1845`, `:1856`, `Nodes.cs:309` and `:316` go from 0 to 2.
- `Gaps/Engine/PartialMatchingTests.cs`, two new tests - a repeat that ran out mid-body at the end,
  and a reversed fuzzy match that ran off the left. **These do NOT close their coverage lines and
  say so**: `AtEnd` and `SteppedPastTheLeft` still read zero. That is the honest outcome rather than
  the one I first wrote into the remarks, and the call site already explained it - `Matcher.cs:5796`
  says the arm is unreachable until Phase 7 restores `try_match`'s test-node arm. The tests are kept
  because the behaviour they pin is real and had no test.

**The eight members still wholly unreached, each judged:**

- `Seam.Tag` and both `Seam.For` overloads (33 lines) - the marker thrown for a capability no slice
  has ported. Unreached means everything is ported; S43 reported the same four occurrences.
- `Nodes.cs:375` and `:397` `ZeroWidthOpcode` (2) - `Failure` and `Prune` throw where upstream has
  no `_opcode` attribute at all. Fidelity dead code by construction.
- `AtEnd` and `SteppedPastTheLeft` (2) - above; dead pending Phase 7, documented at the call site.
- `MatchState.GuardRepeatRange` (6), with `GuardList.GuardRange` (34 of 36) behind it - sitting 1's
  finding: reached only from the `GreedyRepeatOne` backtrack case when the retreat loop exits with
  `pos == limit` and no tail match. Recorded, not tested.

The remaining 683 uncovered lines sit inside members the suite does reach, and no opcode arm is
wholly unentered, so they are sub-branches of exercised code rather than untested capabilities.
**Stryker cannot be quoted as covering them**: its 0 survivors are 7,342 mutants TESTED out of
285,965 generated, the rest `Ignored` by the chunking - a mutation score, not a coverage claim.
(Sitting 4 corrected this line: it first read 7,579, which the per-chunk table above does not
support - 7,267 killed plus 75 timeout is 7,342, and 18 RuntimeError rows sit outside both.)

The number for Phase 7 to regress against: **878 uncovered lines, 257 members, 0 wholly-unentered
opcode arms**, from `s57-coverage3.cobertura.xml`, 6,475 tests.

### Step 6, done: item 9, the S44-S56 controls at their own seed and at 99991

`tools/controls.json` holds **nine** controls in the S44-S56 range - S45-A/B, S46-A/B/C/D, S48-A,
S48b-A/B - and `--check` resolves every site. The fresh seed 99991 is inserted at **index 1** of each
control's seed list, which is the convention `run-controls.py:215-220` states, so
`--seeds 2` runs the slice's own recorded seed plus the fresh one.

**The runner prints no baseline, and half of these controls signal in the `expected` column rather
than in `diverge`**, so the unmutated numbers were measured first, on the same cached waves through
the same consumer (`.scratch/s57-control-baselines.py`, which imports `run-controls.py` and calls
`wave_for` + `consume` with no mutation applied):

| wave | clean result |
|---|---|
| `case-folding` 2000 seed 7 | agree 1996, expected 4, diverge 0 |
| `case-folding` 2000 seed 99991 | agree 1995, expected 5, diverge 0 |
| `fuzzy` 6000 seed 7 | agree 5992, expected 8, diverge 0 |
| `fuzzy` 6000 seed 99991 | agree 5992, expected 7, diverge 1 |
| `interactions` 6000 seed 7 | agree 5903, expected 22, diverge 2 |
| `interactions` 6000 seed 99991 | agree 5926, expected 11, diverge 4 |
| `interactions` 6000 seed 31337 | agree 5918, expected 9, diverge 10 |
| `substitution` 600 seeds 7 and 99991 | agree 600, diverge 0 |

Mutated, against those baselines:

| control | own seed | 99991 | verdict |
|---|---|---|---|
| S45-A `turkic-all-cases-guard` | 7: expected 4 -> **0** | 5 -> **1** | fires at both, 4 rows each |
| S45-B `turkic-full-fold-expansion` | 7: 4 -> 4 | 5 -> **4** | dead at seed 7 *as S45 recorded*; 1 row at 99991 |
| S46-A `bestmatch-doubled-guard` | 7: expected 8 -> **6** | 7/1 -> **6/0** | fires, 2 rows each |
| S46-B `bestmatch-guard-off-by-one` | 7: identical | identical | **dead on the wave**, as S46 recorded |
| S46-C `bestmatch-stops-ranking` | 7: diverge 0 -> **1** | 1 -> **2** | thin, 1 row each |
| S46-D `posix-restore-keeps-the-losing-counts` | 7: diverge 2 -> **31** | 4 -> **28** | strong |
| S48-A `bestmatch-walk-reads-the-verb-moved-slice` | 7: expected 22 -> **21** (1 row) | identical | effectively dead, as S48 recorded |
| S48b-A `counts-restored-without-truncating-the-change-list` | 31337: 10 -> 10 | 4 -> 4 | **lost its wave signal** - see below |
| S48b-B `posix-restore-leaves-the-running-totals-stale` | 31337: diverge 10 -> **8** | 4 -> **1** | fires; rows move to agree, not away |
| S53b-A `sub-keeps-only-the-slice` | 7: diverge 0 -> **37** | 0 -> **32** | strong; newly registered, see below |

Three things in that table are findings rather than ticks.

**S48b-A no longer fires on the wave.** S48b recorded 8 diverge unmutated and 6 mutated at seed
31337; today the same mutation moves nothing at 31337 (10 and 10) or at 99991 (4 and 4). Its other
recorded instrument still works, and harder: with `FuzzyChanges.Count > changeCount` flipped to `<`,
`dotnet run --project tests/FuzzyRegex.Tests -c Release -- --treenode-filter
"/*/*/FuzzyCountsAndChangesTests/*"` fails **7** tests, where S48b recorded 2 - later slices added
tests to that class. So the control is alive, and the wave is the wrong instrument for it, which is
exactly what S48b's own notes predicted ("the oracle is blind to mechanisms C and D by
construction").

**S45-B, S46-B, S46-C and S48-A are thin or dead on the wave**, each in the way its own slice
recorded - none has decayed. S46-B must **never** be run against the suite unattended: S46 measured
the same mutation hanging the test host past 600 seconds at 19 GB resident.

**S53b's Control A was registered, because it fits the schema and was prose-only.** It reproduces its
recorded figure exactly - 37 divergences of 600 at seed 7 - and gives 32 at 99991, against a clean 0
at both. The other unregistered S44-S56 controls cannot live in `tools/controls.json` as it stands,
and the reason is the schema rather than an oversight:

- **S50's Control C and S50b's four** are *suite* controls with no wave at all; the runner's only
  verdict is the oracle report's summary line.
- **S52d's Control A** runs the whole default wave list (22 generators), where an entry names one
  generator.
- **S53b's Control B** signals through a consumer test (`The_lazy_walks_answer_exactly_what_the_eager
  _ones_do`), not through the diverge column the runner parses.

Teaching the runner a `suite` mode and a multi-generator wave is a tool change, not a phase-close
one; it is handed to Phase 7 in STATE.md rather than done here.

## Sitting 3 - 2026-09-20

Item 4, the oracle gate, walked properly. It is the item the two earlier sittings left open, and
walking it turned the slice around: **the gate is RED, the phase cannot close today, and the red is
twenty rows deep.**

### The gate's extra wave: one row, judged and pinned

`pwsh -File tools/run-oracle.ps1 -Generator fuzzy,interactions -Seeds 99991,57057` was red at 99991
with one row, 525, drawn again as row 225 of the 6000-row `interactions` wave at the same seed - one
row twice, not two rows:

    (?r)^(?:[^a-f]{3,}(?P<g1>[a-f])(?P<g2>[[:digit:]])){s<=1,i<=1,d<=1}(?:[a-f](*SKIP)\s|\p{Nd})
    subject '\r\n\U0001F600', partial search
    upstream (0, 2) partial     this port (0, 1) partial

Judged this port's way, and pinned as row 13 of `partial-retry-reversed-slice`. Both of that entry's
arguments were re-run on it rather than assumed from row 5, because this is the first row of the
family that is ANCHORED and carries a fuzzy section - the two things a reader would suspect first:

    as drawn, (*SKIP)   (0, 2) partial   <- upstream
    verb -> (*PRUNE)    (0, 1) partial   <- this port, and the row's own recorded pruneOutcome
    verb deleted        (0, 1) partial
    match(0, endpos)    None at 3 and 2, (0, 1) at 1, (0, 0) at 0
    fuzzy deleted       (0, 1) partial   - so the fuzzy section is not what moves the span
    anchor deleted      (0, 2) partial   - nor the anchor
    forward twin        (0, 3) upstream = (0, 4) UTF-16 here, both engines agreeing

Measured 2026-09-20 on regex 2026.9.10, `tools/probes/upstream-partial-retry-reversed-anchored.py`;
port half `tools/probes/s57-skip-partial-span.cs`. Permanent test:
`PartialMatchingTests.A_reversed_skip_does_not_lengthen_an_anchored_fuzzy_partial_either`.
Re-run after the pin: **GREEN at both seeds**, 593 agree / 2 expected / 5 resource / 0 diverge.

### "6000 rows" means `-Count 6000`, and nobody had run it since S52

The slice's gate line says "default wave at three seeds, 6000 rows". That is `-Count 6000` - 126,080
rows a seed, about a minute a seed (`docs/plan/ROADMAP.md:163` and `:211`), NOT the 300-row default
wave. The 300-row default wave is green at three seeds and has been all slice; that is what recent
slices' "oracle GREEN at seeds 7, 4242, 20260920" lines mean, S60's included.

    pwsh -File tools/run-oracle.ps1 -Count 6000

    seed 7          agree 125536  expected 468  timeout 2  resource 71  diverge 3
    seed 4242       agree 125507  expected 501  timeout 1  resource 68  diverge 3
    seed 20260920   agree 125565  expected 440  timeout 5  resource 56  diverge 14

**Twenty distinct rows.** Reports and waves are kept per seed at
`TestResults/oracle/report-<seed>.txt` and `wave-<seed>.jsonl`.

### It is not S60's doing - measured, not reasoned

S60 landed the required-string prefilter hours earlier, which is exactly the kind of change that
could move a search row, so the first question was whether these twenty are its regressions. They
are not:

    git worktree add .claude/worktrees/pre-s60 --detach 14aad0a~1
    git -C .claude/worktrees/pre-s60 submodule update --init upstream
    pwsh -NoProfile -File .claude/worktrees/pre-s60/tools/run-oracle.ps1 -Count 6000 -Seeds 7,4242,20260920

    pre-S60 (3f1bf91):  diverge 3, 3, 15      HEAD today:  diverge 3, 3, 14

Same three-and-three at the two fixed seeds, and S60 is one row BETTER at the date seed. The
worktree was removed afterwards; the three lines above recreate it. The recorder itself has changed
since S52's close (S52c added the metamorphic invariants, S53b touched it), so a seed does not draw
the same 126,080 rows it drew then and "S52 closed green at 6000" cannot be read as a regression
window. Dating the rows is a scripted bisect, and it belongs to the slice that fixes them.

### The twenty rows, and a first-pass reading of the doors

`python tools/probes/gate-divergence-doors.py` puts every judged family's own control to every
diverging row. It stopped after four of the date seed's fourteen on the first run: upstream itself
raises `IndexError: tuple index out of range` inside `get_firstset` when it compiles seed 20260920
rows 81232 and 87091, and three door sites compiled the row outside a `try`. Fixed here (`sweep`,
`stepwise` and the `no partial` door), so one row that upstream cannot compile no longer costs the
other ten.

| # | seed:row | generator | operation | shape | first reading |
|---|----------|-----------|-----------|-------|----------------|
| 1 | 7:75921 | interactions | search partial | `(?e)`, atomic, fuzzy | fuzzy CHANGES differ on an equal span |
| 2 | 7:76160 | interactions | split | `(?b)` `(*SKIP)` | verb-deleted door = this port exactly; `(*PRUNE)` times out |
| 3 | 7:102670 | partial-sliced | search partial | `(?r)` `(*SKIP)` | SPLIT doors: `(*PRUNE)` = upstream, verb deleted = this port |
| 4 | 4242:102408 | partial-sliced | fullmatch partial | `(?r)` `$` | this port flags PARTIAL on a span upstream calls complete |
| 5 | 4242:103412 | partial-sliced | match partial | `(?r)` group call in a conditional | this port loses a zero-width group-3 capture |
| 6 | 4242:107083 | partial-sliced | search partial | `(?r)` sliced | this port's partial is one unit longer than upstream's |
| 7 | 20260920:72433 | interactions | search partial | `(*PRUNE)` fuzzy | fuzzy counts (1,0,0) upstream, (2,0,0) here, same span |
| 8 | 20260920:74399 | interactions | finditer-overlapped | `(?r)` `(*SKIP)` `\L` | upstream 4 matches, this port 3; the doors give 3 |
| 9 | 20260920:77601 | interactions | search partial | `(?r)` `(?p)` `(*SKIP)` | `(*PRUNE)`/verb-deleted (0,1), upstream (0,4), this port (0,2) |
| 10 | 20260920:81232 | lookaround | split | `(?r)` lookbehind | UPSTREAM CANNOT COMPILE IT - `IndexError` in `get_firstset` |
| 11 | 20260920:87091 | conditionals | fullmatch | `(?r)` nested conditional | UPSTREAM CANNOT COMPILE IT - same `IndexError` |
| 12 | 20260920:98050 | partial | search partial | `(*SKIP)` `\M` | upstream (0,5), the whole subject; doors (4,5); this port (6,2) |
| 13 | 20260920:98935 | partial | search partial | `(?r)` `(*SKIP)` | `(*PRUNE)` gives this port's (0,1); upstream (0,3) |
| 14 | 20260920:99286 | partial | search partial | `(?r)` `(*SKIP)` | upstream (0,0), doors (0,3), this port (0,6) - THREE answers |
| 15 | 20260920:107737 | partial-sliced | search partial | `(?r)` group call | upstream never answers at any anchor; this port answers (1,2) |
| 16 | 20260920:115501 | verbs | subf | `(?r)` `(*PRUNE)` | upstream substitutes 1, this port 0 |
| 17 | 20260920:116420 | verbs | sub | `(?r)` `(*PRUNE)` atomic | upstream substitutes 1, this port 0 |
| 18 | 20260920:120699 | fuzzy | fullmatch | `(?b)(?i)` nested cost limit | upstream no match, this port matches with (0,1,1) |
| 19 | 20260920:122739 | fuzzy | fullmatch partial | `(?e)` fuzzy sets | counts (3,0,0) upstream, (1,1,0) here, same span |
| 20 | 20260920:125387 | fuzzy | fullmatch partial | `(?b)(?e)` | upstream no match, this port matches |

Read that last column as WHAT THE DOORS SAID, not as a verdict. Several rows (2, 13) walk straight
into existing families; several (4, 5, 15, 16, 17) look like this port answering where upstream does
not, which is the shape of a defect here rather than of a divergence to pin; rows 10 and 11 are an
upstream crash on a pattern this port handles, which is a ledger candidate, not an oracle pin. None
of them is judged, and none of them will be judged by reading the table - each needs its mechanism,
its probe, and its pin or its fix.

### What this means for the phase

Phase 6's exit gate is the oracle at 6000 rows. It is red, twenty rows red, and judging twenty rows
to this project's standard is not a tail-end task - S52 spent eighteen sittings on thirty-seven. So:

- **S57 does not close Phase 6 today.** It commits as a checkpoint with the gate item still open.
- **A new slice, S57b, takes the twenty rows**, scripted in one pass the way S52's nineteenth sitting
  did it, and dates them with a bisect. Spec amendment and ROADMAP paragraph written with it.
- Items 10 and 11 (bookkeeping, Phase 7 handover) stay open with it, because both of them WRITE that
  the phase is closed, and it is not.

### Review, verifier, and what this sitting commits

**Blind review** (fresh Opus, diff plus every untracked file, reproduction-only brief): one finding,
reproduced and fixed. `DemoEngineContractTests.cs:361` quoted the suite as 6,459 tests, which was
S60's STATE figure, not the figure the coverage measurement ran against; the tree gives 6,476 and
the measurement ran 6,475. The comment now says 6,475 on the day and that the number only grows.
Everything else it checked came back clean, including the 13-rows-13-answers pairing in
`ExpectedDivergences.cs`, all four new probes running, and every `file:line` the sitting added. No
second pass was needed: the one fix is a doc comment in a file the reviewer had already read.

**Independent verifier** (fresh Opus, commit-ready tree, eight claims): all eight CONFIRMED - both
row-13 probes line for line, `PartialMatchingTests` 35/35, the extra wave GREEN (593/2/5/0 of 600),
the gate's three summary lines (diverge 3, 3, 14; about 11 minutes for all three seeds), the pre-S60
control rebuilt from the recreate commands at `3f1bf91` (3, 3, 15), the doors probe running all
fourteen date-seed rows instead of stopping at four, `demo-cap-timing` within 2 ms of its comment,
and `14aad0a~1` = `3f1bf91`. It removed its worktree and left the tree as it found it.

**Gates at this commit.** Ratchet GREEN, 6,476 tests, baseline updated to 6,368 distinct ids. Oracle
GREEN at the three default seeds. The 6000-row gate is RED and that is the checkpoint: S57b takes it.

## Sitting 4 - 2026-09-21

Items 10, 11 and 12, and the blocker sitting 3 left. S57b had since taken the twenty rows and the
6000-row gate was green, so the only question left was the one the gate itself asks last.

### The blocker, and why scheduling was the answer

The gate's ledger bullet says every entry must end in one of four states - fixed here, port right and
pinned, upstream-only, or owner decision pending with the evidence - and that an "inherited, unfixed"
entry means the phase is not closed. Sitting 2's table found five: 17 (in part), 18, 19, 20, 21. The
port carries all five, and `tests/FuzzyRegex.Tests/Gaps/UpstreamIssues/InheritedIssueTests.cs` is the
proof - five tests whose names assert the inherited answer ("still does not", "still loses", "still
denies"), which is a stronger source than the ledger's own prose.

The finding that decided it: **nothing in the queue scheduled any of them.** Not one pending slice
named an entry. So the owner's rule - no known bug ships - would have been broken by default rather
than by decision, and re-tabling the five in a fifth document would not have changed that. They were
given slices instead:

| entry | where it goes | why there |
| --- | --- | --- |
| 19, 20 | **S57c** (new) | one bug per S50; the pin the engine can restore |
| 21 | **S57d** (new) | `hitend`, and both of S50's measured narrowings |
| 18 | **S61 item 7** | the only one whose fix is an allocation change, which is S61's subject |
| 17 | **owner decision** | see below |

Entry 17 got no slice. Reading it in full showed its two remaining orderings need the maintainer's
option 3, unchosen upstream since 2021, so building that machinery here would invent semantics
upstream may later contradict. "Owner decision pending with the evidence" is one of the gate's own
four allowed states, so this is the gate being satisfied, not bypassed.

Consequence: **Phase 6 does not close in S57.** It closes with S61, when the last inherited entry
lands. The estimate moves from 18-23 slices to 20-25. Recorded as design spec amendment 35, with the
matching ROADMAP paragraphs, and stated rather than resolved: Phase 7 started (S60) before the fix
list emptied, under the 2026-09-16 parallel-phases decision.

### The measured rate

22 slices over 57 logged sessions is 2.59 sessions a slice (`docs/plan/slice-log.jsonl`, computed in
`.scratch/phase6-rate.py`, gitignored). S56b and S57b are absent from the log - S57b has seven
session headings, six numbered plus a resumed "Sitting 3, second half" - so about 66 sessions over
24 landed slices, 2.75, is the floor. ROADMAP's 10-15 estimate
for Phase 6 was wrong by a factor of two before this sitting added two more slices.

### Bookkeeping and the handover

CHANGELOG gained the Phase 6 entry, and it says plainly that Phase 6 is not finished and names the
entries that are scheduled rather than fixed. `docs/plan/PHASE-7-HANDOVER.md` is new: the rule that
outranks every optimisation, the numbers Phase 7 regresses against, the edge cases an optimiser is
tempted to special-case, the work Phase 6 leaves open, and how to judge a divergence.

One correction while writing it. The first draft cited `.scratch/s57-coverage3.cobertura.xml` as the
coverage source; the file no longer exists, which by amendment 16 is COULD NOT RUN, not evidence. It
was replaced by the command that re-takes the measurement, plus the two cautions that cost sitting 1
time: cobertura lists a `<line>` under every class containing it, so a naive reader double-counts
(824 uncovered lines reported in `Matcher.cs`, which has 412), and two `DemoEngineContractTests`
cases fail under instrumentation because it pushes them past the demo's two-second `MatchTimeout`.

### Review - three passes, thirteen findings, all thirteen reproduced and fixed

Unusual for this project, where roughly four findings in five do not survive reproduction. The
reason is that this sitting produced only prose, and a prose defect reproduces by opening the file
it cites. Nothing here was a false positive.

**Pass 1, eleven findings**, every one a claim that did not match its own source:

- CHANGELOG listed ledger entry 13 among the bugs "fixed here", and the port never reproduced it
  (`LEDGER.md:2122-2125`, and this file's own table row 13). Clause deleted.
- CHANGELOG said two of entry 11's doors remain upstream's alone; the table at `LEDGER.md:1505-1507`
  has three, E, F and G. Corrected, and the same stale "E and F" found in this file's table.
- S60 dated 2026-09-21 in three documents; `791d628` is 2026-09-20.
- The handover said `--job short`'s 21% error bar was S58's measurement. It is
  `docs/plan/phase7-research/SUMMARY.md:6`, 2026-09-16, three days before S58 ran.
- "Those five controls" where the same sentence enumerates seven (1 + 4 + 1 + 1).
- STATE said ledger entry 25 is unjudged; S57b judged and pinned it, and entry 26 as well.
- The Stryker line in these notes said 7,579 tested where the chunk table above it sums to 7,342.
- The handover promised eight wholly-unreached members and listed seven; `GuardRepeatRange` was the
  missing one.
- ROADMAP and the spec said the gate tabled "all 24 ledger entries" when 25 and 26 now exist.
- The handover wrote `search-start-*` entries plural; only `search-start-partial` remains.
- S61's new item 7 called 4,000,000 "upstream's own ceiling"; it is where this port fails, and
  upstream's ceiling is 6,000,000.

**Pass 2, two findings**, both in the pass-1 fixes: the handover now claimed
`search-start-skip-slice` went because an arm was ported, where
`ExpectedDivergences.cs:36-43` says S35 fixed the port and the staleness alarm caught it; and the
"three more doors" correction had left this file's own table row 11 saying "E and F". **Pass 3 over
those two: "No defects found."**

The lesson for a documentation slice: every number is a citation, and a citation nobody opened is a
guess. Nine of the thirteen were numbers quoted from a sibling document rather than from the source
that document was summarising.

### Gates at this commit

Ratchet GREEN, 6,503 tests, baseline 6,395 distinct ids; `docs/STATUS.md` regenerated and unchanged,
which is what an unchanged test count should produce. No divergence was judged in this sitting, so
there is no verifier step (spec amendment 34). No code changed: the delta is documents.
