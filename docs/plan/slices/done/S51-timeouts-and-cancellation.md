---
slice: S51
phase: 6
title: Per-call timeouts on every input-dependent method, and CancellationToken support where .NET precedent says so
delivers: []
---

# S51 - Timeouts and cancellation

Owner request, 2026-09-13 (spec amendment 22). Upstream takes a per-call `timeout=` on every match
method (`_main.py:253-298`); the built-in `Regex` offers a constructor `matchTimeout` and static
overloads with one. This port has the engine half (`check_timed_out` polled on the hot loop,
`RegexMatchTimeoutException`, the constructor's `matchTimeout`) and lacks the per-call surface. Placed
before Phase 7 because the poll sits on the matcher's hot path and optimisation must measure the
final shape.

## Scope

- **Research first, and write it down before shaping the API.** Primary sources, not memory: the
  .NET Framework Design Guidelines on cancellation and timeouts; which synchronous CPU-bound or
  blocking BCL APIs take a `CancellationToken` and where they place it (`SemaphoreSlim.Wait`,
  `BlockingCollection.Take`, `Task.Wait`, `Parallel.For` via `ParallelOptions`, `Stream.CopyTo`
  versus `CopyToAsync`); how recent first-party libraries shape it (`System.IO.Hashing`,
  `System.Text.Json`, `Microsoft.Extensions.*`, `System.Threading.Channels`); whether to offer both a
  timeout parameter and a token, or a token only with callers composing `CancellationTokenSource`
  (`Regex` and `HttpClient` offer both). Record the decision in DECISIONS with the precedent quoted.
- **Timeout overloads** on every method whose running time depends on the input: `IsMatch`,
  `Match`, `MatchAtStart`, `FullMatch`, `Matches`, `Replace`, `ReplaceFormat`, `Split`, and the
  static conveniences; the constructor's `matchTimeout` remains the default. Parity with upstream's
  per-call `timeout=`, so no divergence entry.
- **`CancellationToken`** where the research says, polled at the same engine site as the clock,
  throwing `OperationCanceledException` carrying the token. This is the one step past both upstream
  and `Regex`, so it gets a PORTMAP "beyond upstream" row.
- **One check site.** The engine already reads the clock in one place; a per-call value replaces
  the per-pattern one for the call. No second mechanism, no allocation on the hot path.
- **Recorder parity**: upstream's `timeout=` and this port's must fire on the same shapes; the
  recorder already records `timeout` rows, so add a `timeout` comparison to the oracle for rows
  where upstream timed out.

## Verification

- Gap tests: a pathological pattern times out and cancels within tolerance on every overload;
  the exception carries the pattern and the token; a cancelled scan yields what it had so far or
  nothing, as the research decides and documents.
- Micro-benchmark of the hot loop before and after (BenchmarkDotNet, `bench/`): the added poll
  costs nothing measurable, or the cost is quoted.

## Done when

- [x] Research recorded with sources; API shaped from it; every input-dependent method covered.
- [x] Tests for timeout and cancellation on every overload; oracle compares `timeout` rows.
- [x] Benchmark before/after quoted; PORTMAP rows; DECISIONS entry.
- [x] Ratchet GREEN, blind review (hunt: a poll skipped on the fuzzy or partial retry paths; a
      token checked only at match start), commit.

## Closing notes (2026-09-15, one sitting)

**Landed.** Every one of the 27 public members of `FuzzyRegex` whose running time depends on its
input now takes `TimeSpan? timeout = null` and `CancellationToken cancellationToken = default` -
17 instance, 10 static, counted by reflection in
`TimeoutAndCancellationTests.Every_input_dependent_public_method_takes_both_a_timeout_and_a_token`.
`null` means the pattern's `MatchTimeout`, so no existing caller changes behaviour. Suite
**6,083 / 6,083 passing, 0 skipped**, ratchet GREEN, baseline 5,922 -> **5,975**; oracle GREEN at
seeds 7, 4242 and 20260915.

**The research is a probe, not a recollection, and it moved the design.**
`tools/probes/bcl-sync-cancellationtoken-apis.ps1` reflects over the running framework:
**28 synchronous (non-`Task`-returning) BCL methods take a `CancellationToken`, and 28 of 28 put it
last**; exactly 1 of the 28 makes it optional. `Regex` takes a `matchTimeout` on **11 static methods
and 0 instance methods** - so .NET's own regex has no per-call budget on a compiled pattern, and
upstream's `timeout=` keyword (`_main.py:253-298`) is the nearer precedent. That is why both a
timeout and a token are offered rather than a token alone: the two exceptions mean different things
to a caller, and a `CancellationTokenSource(timeout)` allocates a timer per call where the engine's
`Stopwatch` poll allocates nothing. The probe's **first run was wrong and its own output showed it**:
classifying by `ReturnType.FullName` silently counted every `JsonSerializer.DeserializeAsync`
overload as synchronous, because `FullName` is `null` for an open generic return such as
`ValueTask<TValue>`. Fixed to match on `Namespace.Name`; the headline count fell from 47 to 28. The
comment naming that trap is in the probe.

**One check site, and it is upstream's own second half.** `Matcher.SafeCheckCancel` is
`safe_check_cancel` (`upstream/src/_regex.c:2266`), whose two halves are `check_timed_out` and
`PyErr_CheckSignals`. The port's comment had recorded the second as having "no counterpart here";
the token IS that counterpart, being the only way a .NET caller can interrupt a running match. All
three callers reach it through upstream's `state.Iterations == 0` gate - a `ushort` stepped by
`0x100`, so it wraps every 256 turns - so the token costs one field read per 256 iterations.

**The engine check is provably load-bearing.** Replacing
`state.CheckTimedOut() || state.Cancellation.IsCancellationRequested` with `state.CheckTimedOut()`
alone leaves **all 17 cases** of `Cancellation_stops_every_input_dependent_method` still running
after 2 minutes, where they pass in about 50 ms with the check present. The test cannot pass without
the code.

**Benchmark: the added poll costs 8 bytes an operation and no time this benchmark can resolve.**
`bench/` had no benchmark class at all before this slice; `MatchingBenchmarks` is the first, and S54
owns the real baseline set. HEAD `49a0467` in a throwaway worktree versus the working tree,
BenchmarkDotNet default job. **Three BEFORE runs and four AFTER runs**, because a single pair of
runs cannot separate an effect from noise here - and, as below, because the first AFTER pair was not
measuring what it claimed to:

| Benchmark | BEFORE (3 runs) | AFTER (4 runs) | Allocated BEFORE | Allocated AFTER |
|---|---:|---:|---:|---:|
| `BacktrackingFailure` | 83,889 / 85,613 / 87,239 us | 83,230 / 83,826 / 84,134 / 92,205 us | 2,096 B | **2,104 B** |
| `LiteralScan` | 304.2 / 305.1 / 308.3 us | 287.9 / 292.3 / 304.3 / 314.2 us | 856 B | **864 B** |
| `FuzzyScan` | 1,638.7 / 1,686.6 / 1,698.6 us | 1,583.1 / 1,616.1 / 1,621.6 / 1,703.7 us | 784 B | **792 B** |

**Allocation is +8 bytes per operation on every workload**, reproduced on all four AFTER runs and
all three BEFORE runs: the `CancellationToken` field added to `MatchState` is one object reference,
and `MatchState` is built once per operation. Not per match and not per loop turn.

**Time is not resolvable.** The AFTER ranges straddle and mostly sit below the BEFORE ranges; the
spread *within* AFTER on `BacktrackingFailure` is 10.8% where the gap between the two versions'
medians is under 1%, in the direction of AFTER being faster, which the change cannot cause.
Arithmetic agrees: `BacktrackingFailure` is roughly 1,024 checks per operation at 18 characters, so
even at a pessimistic 2 ns a read that is under 0.003% of 84 ms.

**The first AFTER pair was contaminated and the independent verifier is what caught it.** Those runs
reported allocation identical to BEFORE (2,096 / 856 / 784 B), which adding an 8-byte field cannot
produce. The cause is in the re-run recipe: **while the `49a0467` worktree exists, BenchmarkDotNet
resolves `FuzzyRegex.Benchmarks` by name and finds two projects.** For the verifier that surfaced as
`System.NotSupportedException: Found more than one matching project file for FuzzyRegex.Benchmarks
... Benchmark project names needs to be unique`; here it did not throw, and the main-tree run
measured HEAD's binary instead. The lesson is the general one this repo keeps relearning: a number
that agrees with the null hypothesis *too exactly* is a reason to check the apparatus, not to
believe it.

**Re-run recipe, corrected.** `git worktree add .claude/worktrees/<name> 49a0467 --detach`, copy
`bench/FuzzyRegex.Benchmarks/MatchingBenchmarks.cs` into it (HEAD has no benchmark class, and the
file compiles there because it calls only `IsMatch(string)` and `Count(string)`), run
`dotnet run -c Release --project bench/FuzzyRegex.Benchmarks -- --filter "*MatchingBenchmarks*"`
**from inside the worktree**, then **remove the worktree** and run the same command from the main
tree. Never with both present. Not `--job short` either: at 20 characters and the short job the
error bar was 21% of the mean, which is why `BacktrackingFailure`'s subject is 18. Check the
Allocated column first - 2,096/856/784 is HEAD and 2,104/864/792 is this slice, so it tells you
which binary actually ran.

**One scope bullet was NOT done, on the recorder's own evidence, and this is the part to read.** The
slice asked to "add a `timeout` comparison to the oracle for rows where upstream timed out".
`record-oracle.py`'s `timed_out` docstring already argues against exactly that, and the argument
holds: upstream "did not reject the pattern, it simply never finished", so scoring a port that *does*
answer as diverging - and a port that also hangs as agreeing - gets both halves backwards. A port
that answers where upstream hangs is better, not wrong. What was done instead is the thing that
actually gives S51 oracle coverage: **all seven call sites in `OracleComparer.Run` now pass the row
deadline PER CALL** rather than through the constructor, so every row of every wave exercises the new
plumbing. Verdicts unchanged, wave GREEN at three seeds.

**Review.** One blind pass (Opus, brief per `docs/VERIFICATION.md`). **One finding raised, one
reproduced, one fixed.** `FuzzyRegex.LimitsFor` read the token before validating `timeout`, so
`Match("a", timeout: TimeSpan.Zero, cancellationToken: <cancelled>)` threw
`OperationCanceledException` where the same call without the token threw
`ArgumentOutOfRangeException` - a caller's bug hidden behind the cancellation on exactly the runs
where it is hardest to see, and the built-in `Regex` validates unconditionally. Reproduced here as a
failing test first (`A_bad_timeout_is_rejected_even_when_the_token_is_already_cancelled`: "Expected a
<System.ArgumentOutOfRangeException> to be thrown, but found <System.OperationCanceledException>"),
then fixed by validating first. **No second blind pass**: the fix reorders two statements inside the
private `LimitsFor`, which is where the reviewer found the bug and which it had already analysed - no
public API, no tooling, nothing the reviewer had not seen. The reviewer also ran down and could not
reproduce seven other hypotheses from the brief, including skipped polls on the fuzzy
(`do_simple`/`enhanced`/`best`) and partial retry paths, a per-call budget leaking into
`MatchTimeout`, and overflow in `ToTicks` at `TimeSpan.MaxValue` (the `double`->`long` conversion
saturates).

**Independent verifier** (spec amendment 16 limb (d)): a fresh Opus agent, briefed with nothing but
the commit-ready tree, re-ran every number above. **Seven CONFIRMED, one DIFFERENT, none COULD NOT
RUN.** Confirmed: the ratchet totals and the baseline in `tests/parity-baseline.json`; the oracle's
GREEN at 7 / 4242 / 20260915; all five probe numbers; 27 members as 17 instance and 10 static; the
mutation leaving all 17 cancellation cases running past two minutes (reverted and hash-verified back
to `ea9f04c`); the reviewer finding's test and the validate-before-read order; seven per-call sites
in `OracleComparer`. **The DIFFERENT was the allocation claim**, and it was right - see the
benchmark section above, which is rewritten around its measurement rather than mine. It also found
the worktree collision in the re-run recipe, now corrected there.

**No negative control on the oracle**, because this slice changes no matching behaviour: the wave's
job here was to prove the per-call plumbing moved no verdict, which it did at three seeds.

**For the next slice.** `Match.NextMatch` takes neither a timeout nor a token and runs under the
pattern's budget, which is where the built-in `Regex.Match.NextMatch` sits; it takes no arguments at
all, so there is nowhere to put them without inventing a scanner object. That is recorded in
DECISIONS and in `Iteration.Next`'s own `<exception>` tag because it looks like an oversight.
`bench/FuzzyRegex.Benchmarks` now has three benchmarks and no committed baseline - S54 owns that.
