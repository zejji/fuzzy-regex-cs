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

- [ ] Research recorded with sources; API shaped from it; every input-dependent method covered.
- [ ] Tests for timeout and cancellation on every overload; oracle compares `timeout` rows.
- [ ] Benchmark before/after quoted; PORTMAP rows; DECISIONS entry.
- [ ] Ratchet GREEN, blind review (hunt: a poll skipped on the fuzzy or partial retry paths; a
      token checked only at match start), commit.
