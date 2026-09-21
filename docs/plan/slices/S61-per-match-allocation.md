---
slice: S61
phase: 7
title: Per-match allocation - the span and lazy-walk decisions implemented, MatchState buffers reused, and an allocation gate
delivers: []
---

# S61 - The bytes per match

The owner's second goal - *to minimise memory and allocations, or at least bound them* - and the
research's second-ranked target. S58 measured the two costs and put the two decisions to the owner;
this slice implements **whichever the owner chose**, and nothing else. If either decision is still
open when the slice starts, it stops and asks: this is the one Phase 7 slice that may not guess
(DECISIONS 2026-09-16, ground rules (a) and (b)).

What each `MatchState` costs is already known arithmetically (`MatchState.Create`: a `GroupData[]`
plus one `GroupData` per group, a `RepeatData[]` plus one per repeat, three `ByteStack`s, two
`long[FuzzyValue.Count]`s, and one vectorised `IndexOfAnyInRange` pass over the subject,
`MatchState.cs:501`, `:544-568`). The hazard is equally known: a rented buffer held across a
`yield return` is one an abandoned iterator never returns.

## Scope

1. **The span decision, as signed off** (`FuzzyRegex.cs:338`, `:873`). If the owner took the
   threading: `ReadOnlySpan<char>` through `MatchState` and the `try_match_*` path, with the
   `yield`-crossing case served by the lazy-walk shape of item 2 and not by a copy reintroduced
   under another name. If the owner declined it: delete nothing, rewrite the two
   `OPTIMISATION-NOTES` rows to say *declined, with the measured cost and the date*, so it is not
   re-proposed, and move on. Either way the outcome is written down with S58's numbers beside it.
2. **The lazy-walk shape, as signed off** (`Iteration.cs:216`, `:336`): a pooled state released on
   `Dispose`, or a `ref struct` enumerator that is not an `IEnumerable<T>`. Whichever lands,
   **abandonment is the test**: a `foreach` broken after two matches on a 1 MB subject returns
   every rented buffer, proven by `PoolDisciplineTests`' debug `ArrayPool` wrapper (S52b), not by
   inspection. The per-step `timeout` divergence recorded in DIVERGENCES is revisited in the same
   commit: if the shape makes a per-walk timeout natural, take it and update the row; if not, say
   why the row stands.
3. **`MatchState` buffers pooled or reused across steps** so a walk costs one state, not one per
   match - the `OPTIMISATION-NOTES` row that calls it "one vectorised pass over the subject per
   match where `Matches` costs one in total". Reset, do not reallocate. Capture storage reused
   across runs and across POSIX best-match saves (`Matcher.cs:2065`, `:9126`, `SaveBestMatch`)
   comes with it or is deferred with a row; a partially reused state is worse than neither.
4. **`Iteration.cs` per-step state lifted**, and with it the duplication the notes predict: if the
   per-step state goes, `Split` becomes `[.. EnumerateSplits(...)]` and `Iteration.cs:487`'s
   duplicated loop goes with it. `OracleWaveTests.The_lazy_walks_answer_exactly_what_the_eager_ones_do`
   is what stops the two drifting and must stay green throughout.
5. **An allocation gate.** With the noise floor known, set the per-match allocation budget the
   owner's notes call for: `tools/compare-benchmarks.ps1` fails when allocated bytes/op rises
   beyond S58's allocation floor on any workload, and the committed baseline is updated so the next
   slice regresses against the new number. A speed win that allocates more is declared in the
   commit message, not hidden.
6. **Nothing lands that changes an oracle answer.** Reuse is where stale state leaks between
   matches, so the reset path is tested directly: the same state reused across two matches gives
   the answers two fresh states give, over a wave, not over one example.
7. **Ledger entry 18, the one inherited bug whose fix is an allocation change** (assigned here by
   S57, 2026-09-21). A repeated capture group costs hundreds of bytes per repetition: the engine
   pushes one `MatchBodyTailStateData` block per repetition that nothing pops while the repeat runs,
   even where the body is deterministic and there is nothing to backtrack into. This port is worse
   than the thing it reproduces - about 3x `regex`'s bytes a repetition, which is itself about 2x
   stdlib `re`'s - and it hits its 1 GB backtracking bound at `n = 4,000,000` where upstream still
   manages 6,000,000. **A body with no alternative should cost O(1) state per repetition.** The test
   the entry asks for is `FullMatch("(ab)*", "ab" * 4_000_000)` succeeding - the size this port fails
   at today, asserted as a size rather than as a measured byte count. When it goes green the test is
   rewritten to upstream's own 6,000,000 ceiling, which is what
   `InheritedIssueTests.cs:116-118` already says to do. Where upstream's own bytes go is
   explicitly not established, so this is a fix here and not a port of one. It is the last entry on
   Phase 6's inherited-bug fix list, so **Phase 6 does not close until it lands**; if the allocation
   work is declined or deferred, say so in writing and hand the entry back to a slice of its own
   rather than leaving it unowned.

## Verification

- Before and after, same machine, same session, driver idle:
  `dotnet run -c Release --project bench/FuzzyRegex.Benchmarks -- --filter '*' --job medium
  --exporters json --artifacts artifacts/bench/<date>-S61-<before|after>`, compared with
  `pwsh -File tools/compare-benchmarks.ps1` against the committed baseline at S58's floors.
  Allocated bytes/op is the primary number here and time is the secondary one; report both per
  workload. The scan pair S54 pinned (1 MB subject, walked to the end and stopped after two) is the
  workload that has to move.
- `pwsh -File tools/check-ratchet.ps1` GREEN.
- `pwsh -File tools/run-oracle.ps1` GREEN at its three default seeds, `ExpectedDivergences` strict.
- `dotnet run --project tests/FuzzyRegex.Tests -- --treenode-filter "/*/*/PoolDisciplineTests/*"`
  and the same for `ThreadSafetyStressTests` and `OptimiserTrapsTests`, green and named in the
  commit message.
- `pwsh -File tools/run-aot-tests.ps1` and `pwsh -File tools/run-aot-smoke.ps1` GREEN, binary size
  against 6,972,928 bytes.
- `pwsh -File tools/update-public-api.ps1` **if** the lazy-walk shape changes the public surface (a
  `ref struct` enumerator does); `PublicAPI.Unshipped.txt` committed with the diff reviewed and the
  owner's sign-off quoted in the commit message.

## Done when

- [ ] Both S58 decisions are recorded as signed off before any code is written; neither is guessed.
- [ ] The chosen span and lazy-walk shapes implemented, or a declined decision written up with its
      measured cost and date.
- [ ] Abandoned-iterator buffer return proven by the debug `ArrayPool` wrapper, not by inspection.
- [ ] One state per walk, reset rather than reallocated; the reset path proven over a wave.
- [ ] Allocation gate live in `compare-benchmarks.ps1`; baselines updated so the next slice ratchets
      against the new number.
- [ ] Measured before and after, time and bytes per workload, in the commit message; the
      OPTIMISATION-NOTES rows implemented are deleted with their comments, and anything deferred
      gains a `ponytail:`/`Phase 7` comment and a row.
- [ ] Ledger entry 18 fixed - `FullMatch("(ab)*", "ab" * 4_000_000)` succeeds - and the entry's
      status rewritten, or the entry handed to its own slice in writing.
- [ ] Any structural divergence recorded in `docs/plan/SYNC-DIVERGENCE.md` with a
      `sync-divergence:` marker; `tools/check-sync-divergence.ps1` green.
- [ ] Ratchet, oracle at three seeds and AOT green; blind review (hunt: a rented buffer not returned
      on an abandoned walk; state reset that leaves a group, repeat or fuzzy counter from the
      previous match; a `Span` copied back to a string to cross a `yield`; a timeout or cancellation
      check lost in the rewritten loop; `Split` and `EnumerateSplits` drifting apart), commit.
