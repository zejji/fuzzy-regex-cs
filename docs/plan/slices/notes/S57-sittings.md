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
- [ ] 3. Coverage backstop: per file and branch, every file or branch with NO test listed, each one
      tested or recorded as fidelity dead code.
- [ ] 4. Gate item 2, oracle: default wave at three seeds, 6000 rows, plus `fuzzy` and
      `interactions` at 99991 and one fresh seed.
- [ ] 5. Gate item 3, mutation: S55/S56 scores quoted.
- [x] 6. Gap-test provenance audit: PASSES. **0 DIFFERENT, 0 without provenance.** Detail below;
      one follow-up owed (I did not spot-check the agent's calls - see the caveat).
- [ ] 7. Known-bug ledger table: every entry's final state, no "inherited, unfixed".
- [ ] 8. Symbol accounting with `tools/check-symbols.py`; count vs S43.
- [ ] 9. Controls re-run for S44-S56 at their seed and 99991; thin or dead ones named.
- [ ] 10. Bookkeeping: CHANGELOG, ROADMAP Phase 6 rate and estimate, STATE.md, STATUS.md, spec table.
- [ ] 11. Phase 7 handover.
- [ ] 12. Ratchet GREEN, blind review, verifier, commit.

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
