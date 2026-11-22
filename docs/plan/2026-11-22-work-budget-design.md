# Item 16 B: a per-call work budget, design options

Design note, 2026-11-22. Checklist item 16 B, spec amendment 38 (B), DECISIONS 2026-11-21. No code yet:
the owner chooses the API from this note first. Evidence: the D166 and D167 rows of
`docs/KNOWN-DEFECTS.md`, the branches `fix/d166-chunked-sweeps` (2a9bdb9c) and `fix/d167-charged-walks`
(e331a095), and ZejPdf's `research/31-fuzzyregex-bounded-search-note.md`.

Terms used below:

- **Turn**: one pass of the matching loop or the backtracking loop in `Matcher.BasicMatch`, that is, one
  node tried or one backtrack entry popped. The Debug-only `WorkCounter.Step` counts exactly these.
- **Poll**: a call of `Matcher.SafeCheckCancel`, which asks the clock and the token whether to stop.
- **Poll gate**: `MatchState.Iterations`, a `ushort` that each turn steps by 0x100, so it wraps to zero,
  and opens the gate for one poll, once every 256 turns.
- **Work budget**: a limit, set by the caller, on how many units of work one call may do before it is
  stopped. The unit is defined in section 4.

## 1. The problem

A Release caller can bound a call today only by time (`MatchTimeout` or the per-call `timeout`) or by a
`CancellationToken`. Both are wall-clock bounds, and wall-clock bounds are not deterministic:

- **The same call gives different outcomes on different runs.** D166's measurement ran one hostile
  search five times under a 10 ms timeout on one machine: main answered `false` after 68 to 119 ms. A call
  that needs 8 ms on a quiet machine finishes; on the same machine under load it throws. A server that
  scans the same document twice can answer once and time out once, and the bug report cannot be
  reproduced on the developer's machine.
- **Tests that rely on time are flaky.** This repository already learned this: the failed-call memo test
  went red on 2026-09-28 because the machine was busy (D13), which is why `WorkCounter` exists and why
  `ExpiresOnCheck` replaced real timeouts in the poll tests. Both are internal and the first is Debug only.
  A Release caller has no equivalent.
- **Time measures the machine as well as the work.** A budget of 10 ms means something different on a
  laptop, in a container with a CPU quota, and on a CI runner.

A work budget counts what the engine did, not how long it took. The engine is deterministic, so the same
pattern, options and subject use the same amount of work on every run and every machine. A caller can
measure the work its real inputs need, set a budget at a multiple of it, and know that an input which
passed once passes always.

ZejPdf's requirement R2 states the need: "Every unit of work the input can enlarge is charged to a budget
and can be cancelled." Spec amendment 38 (B) commits the port to "a public per-call work budget, charged
at the poll sites", with "its API, unit and per-method scope settled in a design note before any code".

## 2. What the engine does today

- **Polls are counted in turns.** `start_match:` (`Matcher.cs` ~15963) polls when `Iterations == 0`; the
  matching loop (~16401) and the backtracking loop (~17831) add 0x100 per turn. Other gated sites use the
  same counter: `SimpleStringSearch` (~14521), the outlier search (~15672), the ignore-case screen
  (`RequiredStringScreen.cs:718`).
- **The gate is reset per match.** `MatchState.Init` (~2014) and `MatchState.InitMatch` (~2808) set
  `Iterations = 0`. `InitMatch` runs for each match of a scan (six call sites), so a scan of many short
  matches, each under 256 turns, never wraps the counter. That is harmless for polling, because every
  attempt opens with a poll, but a budget charged only at wraps would never be charged there.
- **The clock is skipped when there is no timeout.** `MatchState.CheckTimedOut` returns at once when
  `Timeout == NoTimeout`. With no timeout and no token, a poll costs one call and two tests.
- **D166 (branch) polls the prefix sweeps.** `StringSearch` and `Matcher.SweepLeadingStrings` search in
  chunks of `max(SweepChunkLength = 65,536, 4m)` start positions, overlapping by m - 1, with a poll before
  every chunk but the first.
- **D167 (branch) charges walks to the gate.** `Matcher.ChargeTurns(state, turns)` adds a walk's length
  to `Iterations` (one turn per character walked by `CountOne`) and saturates it one step short of
  wrapping, so the next turn polls. The fuzzy filter's piece searches run in D166's chunks
  (`FuzzyLiteralFilter.PolledIndexOf`, `PolledLastIndexOf`). Its doc comment already says "Spec amendment
  38 item B plans a work budget driven by the same count."
- **An internal work cap already exists.** The partial-match second pass stops forking after
  `256 * (slice length + 1)` forks (`Matcher.cs` ~9731, `ForkBudgetSpent`) and answers a partial instead
  of running on. It shows the engine can stop on a count; it is not a caller-facing budget.
- **The test seam.** `FuzzyRegex.ExpiresOnCheck` (internal, `init`) makes the k-th clock check report the
  budget spent. It works only when a timeout is set, and it counts polls, not work.

## 3. How other engines expose a limit (fetched 2026-11-22)

| Engine | Limit | Unit and scope | When exceeded |
|---|---|---|---|
| PCRE2 | `pcre2_set_match_limit`, `pcre2_set_depth_limit`, `pcre2_set_heap_limit` | Match limit: "an internal counter in pcre2_match() that is incremented each time round its main matching loop"; for an unanchored pattern "the count restarts from zero for each position in the subject string". Default "10 million". Heap limit in KiB | Error codes `PCRE2_ERROR_MATCHLIMIT`, `PCRE2_ERROR_DEPTHLIMIT`, `PCRE2_ERROR_HEAPLIMIT`. The match limit "also applies to pcre2_dfa_match(), though the counting is done in a different way", and to JIT "(but in a different way)" |
| Oniguruma | `onig_set_retry_limit_in_match`, `onig_set_retry_limit_in_search`, and `..._of_match_param` variants | "retry counts"; in match default 10,000,000, in search default 0 ("0 means unlimited"). The plain setters are global; the `OnigMatchParam` ones are per call | A negative error code |
| ICU | `uregex_setTimeLimit` | "The units of the limit are steps of the match engine"; "By default, the matching time is not limited" | "a match operation will fail with an error if the limit is exceeded" |
| .NET `Regex` | `MatchTimeout`, `REGEX_DEFAULT_MATCH_TIMEOUT` | Wall clock, the "approximate maximum time interval"; checked at "its next timing check" | `RegexMatchTimeoutException`. No step limit and no `CancellationToken` parameter |
| .NET `NonBacktracking` | none | "guarantees linear-time processing in the length of the input" | n/a |
| RE2 | `max_mem` only | Memory for the compiled program and DFA cache, default 8 MiB; "If this happens too often, RE2 falls back on the NFA implementation." No timeout or step option | n/a |
| Python `regex` (upstream, 2026.11.19) | `timeout=` | "The timeout (in seconds) applies to the entire operation"; no step or backtrack limit | `TimeoutError: regex timed out` |

Sources, all read 2026-11-22: <https://www.pcre.org/current/doc/html/pcre2api.html>,
<https://raw.githubusercontent.com/kkos/oniguruma/master/doc/API>,
<https://unicode-org.github.io/icu-docs/apidoc/released/icu4c/uregex_8h.html>,
<https://learn.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.regex.matchtimeout>,
<https://learn.microsoft.com/en-us/dotnet/standard/base-types/regular-expression-options>,
<https://raw.githubusercontent.com/google/re2/main/re2/re2.h>, <https://pypi.org/project/regex/>.

What the survey says:

1. **A counted limit is ordinary practice** in backtracking engines (PCRE2, Oniguruma, ICU). Engines with
   a linear guarantee (RE2, `NonBacktracking`) bound the work by construction instead. Neither .NET's
   backtracking engines nor upstream has one, so this is a port-only feature, marked NOT UPSTREAM'S.
2. **Nobody promises the count is stable.** PCRE2 says outright that its other matchers count "in a
   different way"; ICU calls its unit "steps" and maps it only roughly to time.
3. **Per position or per call.** PCRE2's match limit and Oniguruma's "in match" limit restart at each
   start position, so they bound one attempt, not a search; Oniguruma added a separate "in search" limit
   for the whole call. A caller who wants to bound a call needs the second kind.
4. **Errors are distinct.** Every counted limit reports its own error, separate from a timeout.

## 4. The unit: what is charged

The unit is a **work unit**. The design question is how other kinds of work convert into it.

| Work | Charge | Where | State today |
|---|---|---|---|
| A turn of the matching or backtracking loop | 1 | the poll gate, 256 at each wrap, the remainder flushed (see below) | gate exists |
| A character walked inside one turn (`CountOne`) | 1 per character | `ChargeTurns` | D167 branch |
| Gated scan loops (`SimpleStringSearch`, outlier search, ignore-case screen) | 1 per step of their loop | their existing 0x100 steps | gate exists |
| Start positions swept by a vectorised search (`StringSearch`, `SweepLeadingStrings`, fuzzy piece searches, `ScanChunk`) | 1 per 16 positions, rounded up per chunk (provisional, see below) | per chunk, next to the D166/D167 poll | chunk loop on branches |

**Option U1: turns only** (PCRE2 and ICU style). Simple to explain. But a sweep over 10^8 characters costs
nothing, which defeats the point: the hostile cases that motivated item 16 are sweeps and walks.

**Option U2: turns plus code units, weighted (recommended).** Every kind of work that grows with the input
is charged, so the budget bounds every path amendment 38 lists. Vectorised sweeps get a weight below one
because a vector compare examines 16 or more code units at once; the weight is a constant in our code, not
a property of the machine, so the count stays deterministic. The weight of 1/16 is a starting point; the
build slice measures turns per millisecond and swept positions per millisecond on the benchmark rows and
sets the weight so that one unit costs roughly the same time on each path, then records the measurement
next to the constant.

**Option U3: separate counters** (turns, characters walked, positions swept, each with its own limit).
Most precise, but a caller then has to size three numbers, and the right ratio between them is exactly
what U2 measures once for everyone.

Rules that keep U2 honest:

- **Charge by our own count, never by what a BCL call did inside.** The charge for a chunk is the number
  of start positions in it, not how many candidates `IndexOf` verified. This keeps the count independent
  of the vector width the runtime picked (128, 256 or 512 bits) and of .NET's search internals.
- **The known ceiling.** On hostile text a sweep costs up to m times its charge (m the literal's length),
  because each false candidate is verified. D166 measured one hostile chunk at up to ~50 ms for a
  4,000-unit literal. Charging `chunk * ceil(m / 16)` would cover that but would over-charge ordinary text
  by the same factor, so a budget sized on clean input would trip on the same clean input with a longer
  literal. Recommended: charge positions, and document that the budget bounds the number of positions and
  turns, while time per position grows with the literal's length.
- **Flush the remainder.** A charge only at wraps loses up to 255 turns at every `Init` and `InitMatch`,
  so a scan of short matches would never be charged. The state therefore keeps a `long` total that the
  gate, `ChargeTurns` and the chunk loops add to, and `Init`, `InitMatch` and the end of the call add the
  turns since the last wrap (`Iterations >> 8`) before resetting the gate. `ChargeTurns` keeps saturating
  the `ushort` for the poll cadence, but adds the full walk to the total.
- **Charges must not depend on earlier calls.** A cache warmed by an earlier call (the pattern cache, a
  searcher built lazily on first use, as D163's options note proposes for Teddy) must not change what a
  later call is charged. Each new cache gets a test: first call and second call report the same `Used`.

**Stability across versions.** Promise determinism within one package version: the same version, pattern,
options, named lists and subject give the same count on every run, machine, runtime and thread. Do not
promise the count across versions. Every optimisation changes it (fixing D168's quadratic `[ab]*\d` would
cut its count by orders of magnitude), and freezing it would turn every performance fix into a breaking
change. PCRE2 and ICU make the same choice. The release notes name any change that moves the count of a
benchmark row by more than a factor of two, so a caller knows to re-measure.

## 5. Scope: which paths charge

**Charged**: everything that runs per attempt or grows with the subject inside one call: the matching and
backtracking loops, `CountOne`, every gated scan, every chunked sweep (D166, D167), the fuzzy literal
filter's chunk scan and piece searches, and the item C scans that run per candidate.

**Not charged, and why**:

- **Compiling the pattern.** It is not per call, its cost grows with the pattern, which the caller
  controls, and .NET bounds it nowhere either (D105 already took a 500-group compile to 9.7 ms).
- **One-off whole-subject vector scans** that run at most a few times per call: the span overloads' copy
  into a pooled buffer, the surrogate and ASCII checks ZejPdf lists in `MatchState` and
  `FuzzyLiteralFilter`. Each is O(n) at vector speed and cannot repeat, so the work outside the budget is
  at most a small constant times the subject's length. A scan of this kind that turns out to run per
  attempt moves to the charged list (that is a defect row, as D166 was).
- **The caller's own code**: a `MatchEvaluator` passed to `Replace`. The timeout still covers it, as
  upstream's does; the budget counts only the engine.
- **Building results**: `Groups`, captures and replacement strings, bounded by the size of the output.

**Per method**: the budget covers one operation exactly as the timeout does today. For `Matches`,
`EnumerateMatches`, `EnumerateSplits` and `Count`, that is the whole enumeration, charged lazily as it
runs (the `ExpiresOnCheck` remarks in `FuzzyRegex.cs` already say "The count runs for the whole
operation"). `Match.NextMatch` takes no arguments; with a budget object (option C below) the `Match`
carries it on, so the walk keeps charging the same budget.

## 6. API shape

Constraint for every per-call option: adding a parameter to the 34 shipped methods that take
`TimeSpan? timeout` is a binary break ("DISALLOWED: Adding, removing, or changing the order of
parameters", learn.microsoft.com/en-us/dotnet/core/compatibility/library-change-rules, read 2026-11-22),
and package validation against 1.2.0 would refuse it. So any per-call option means new overloads. Of the
34, 16 are statics; the statics are conveniences that compile or fetch a cached pattern, so the
recommendation below extends only the 18 instance methods.

**Option A: a pattern-level limit** (like `MatchTimeout`): `new FuzzyRegex(pattern, options,
matchTimeout, workLimit)` and a `WorkLimit` property.

- For: smallest surface (one or two constructors, one property); mirrors how .NET sets a timeout.
- Against: the caller cannot read how much a call used, which the ROADMAP row asks for ("set and read")
  and which is how a caller calibrates; one limit for every call of a cached pattern, whatever the
  subject's size; no way to charge several calls (one page's searches) to one budget.

**Option B: a per-call `long? workLimit` parameter**, next to `timeout`.

- For: matches the existing per-call `timeout` idiom; nothing new to allocate.
- Against: still cannot read `Used` (except from the exception); 18 new overloads whose only difference
  from existing ones is an optional parameter, which collides in overload resolution unless the new
  parameter is required and placed early, at which point it reads badly (`IsMatch(s, 5000)`: 5000 what?).

**Option C: a budget object, per call (recommended).** A public sealed class, say `FuzzyRegexWorkBudget`,
with a constructor taking the limit, `Limit`, `Used` and `Remaining`. New instance overloads take it as a
required parameter after the subject, with `beginning`, `length`, `timeout` and `cancellationToken` after
it unchanged:

```csharp
var budget = new FuzzyRegexWorkBudget(limit: 2_000_000);
bool hit = regex.IsMatch(page, budget);
long used = budget.Used;            // read after the call, also after an exception
```

- For: the caller can read `Used`, which is how they calibrate; one budget can be shared by several
  calls, which is ZejPdf's "one budget per page"; `Match.NextMatch` and lazy enumerations carry it
  naturally; a required parameter of a new type cannot be confused with existing overloads; the object is
  the same shape as `CancellationToken`, which callers already pass.
- Against: 18 new overloads and a new type; the object is mutable, so sharing it between concurrent calls
  makes `Used` depend on scheduling (see thread safety below); the call allocates nothing, but the caller
  allocates the budget.

**Option D: a settings object** bundling timeout, budget and token, say `FuzzyMatchLimits`, on one new
overload family.

- For: a future fourth limit (memory, depth) needs no more overloads.
- Against: moves away from .NET's `(timeout, cancellationToken)` parameters that this API copied
  deliberately; a caller who passes both the bundle and the old parameters needs a precedence rule; the
  fourth limit is speculative (YAGNI).

**Option E: ambient** (an `AppContext` switch like `REGEX_DEFAULT_MATCH_TIMEOUT`, or an `AsyncLocal`).
Rejected: hidden global state, per process rather than per call, and untestable in parallel tests.

**Thread safety for C.** The engine keeps its tally on `MatchState` and adds it to the object only at
polls and at the end of the call, with `Interlocked.Add`, which costs nothing measurable at one in 256
turns. Concurrent calls sharing one budget are then safe, and the total is right, but which call trips the
limit depends on scheduling; the documentation says determinism needs one budget per thread.

**Validation.** A limit of 0 or less throws `ArgumentOutOfRangeException` at construction. A budget
already spent when a call starts throws before any work, as a cancelled token does today
(`FuzzyRegex.LimitsFor`, whose comment explains why "throws unless the work happened to be trivial" is
unusable as a contract).

## 7. The exception, and how the three limits interact

**Option X1: reuse `RegexMatchTimeoutException`.**
For: existing catch blocks handle it. Against: it is a lie the caller cannot see through; its
`MatchTimeout` property reports a time that did not run out; a caller cannot tell budget from clock, which
every engine in section 3 keeps apart.

**Option X2: a new exception derived from `RegexMatchTimeoutException`.**
For: old catch blocks still work, and the change rules allow "Throwing a more derived exception"; the type
says which limit tripped. Against: it inherits `MatchTimeout`, which is meaningless here, and an is-a
relation with "timeout" that is permanent once shipped. The only callers whose catch blocks it saves are
ones who never set a budget, and those never see it.

**Option X3: a new exception, say `FuzzyRegexWorkBudgetExceededException : Exception` (recommended),**
carrying `Input`, `Pattern`, `Limit` and `Used`. The caller who set the budget wrote the code that catches
it. This matches PCRE2's and Oniguruma's separate codes. Allowed as "a new exception in a new code path"
by the change rules, since only the new overloads can throw it.

**Interaction.** All three limits stay independent: a call can set a timeout, a token and a budget, and
the first that trips at a poll ends it through the existing `MatchStatus.Cancelled` path. When more than
one has tripped at the same poll, `MatchLimits.Cancelled` reports, in order: the token
(`OperationCanceledException`, as today, because the caller asked), then the budget, then the clock. The
budget comes before the clock because it is deterministic: a test that sets both gets the same exception
every run. The poll adds one comparison (`total >= limit`, with `long.MaxValue` meaning no budget) ahead
of `CheckTimedOut`. Upstream is unaffected: the oracle runs with no budget, so parity cannot move.

## 8. Overhead with no budget

What the no-budget path pays: the long add in `ChargeTurns` and per chunk (both already on the D166/D167
paths, which run once per walk or per 64 Ki positions), one compare per poll (one in 256 turns), and the
flush of `Iterations >> 8` at `Init` and `InitMatch` (once per match). Nothing per turn. Fields go on
`MatchState`, never as new locals in `BasicMatchCore`, whose prolog zeroing grows when its locals pass the
JIT's tracked-locals limit (D104).

How to measure, per the phase 11 rules:

1. BenchmarkDotNet whole suite, `--job medium`, pinned to the performance cores, A-B-B-A against main
   with an A-A control, on a quiet machine with LibreHardwareMonitor running. Bar: no row outside the A-A
   spread.
2. WPR CPU profile before and after on the hot fuzzy rows and the D166/D167 hostile rows: `SafeCheckCancel`
   and the charge sites show no new exclusive share.
3. Allocation check: 0 B per call on every row, with and without a budget.
4. Disassembly of `BasicMatchCore`: prolog size unchanged.
5. With a budget set: the same rows, to price the `Interlocked.Add` at polls (expected within noise).

## 9. Test plan

The budget is its own deterministic seam, so most tests need no clock and no `ExpiresOnCheck`. All in
`tests/FuzzyRegex.Tests/Gaps/Api/`, written red first.

1. **Each hostile path stops.** For the four D166 sweeps, the D167 walk and three piece searches,
   `[ab]*\d` (D168) and a catastrophic backtracker such as `(a+)+b`: a budget of N throws the new
   exception, with `N <= Used <= N + one poll interval` (255 turns, one chunk, or one walk). Each is red
   on main (it answers or runs on).
2. **Mutation per charge site**, as D166 and D167 did: remove each charge and the test for that path goes
   red; remove the flush at `InitMatch` and a scan of many short matches stops being charged.
3. **Determinism.** The same call three times, and once on another thread, reports the same `Used`. Pin
   exact `Used` for a few small cases; a change to a pinned value is a deliberate edit with a reason.
4. **Proportionality.** On linear cases, `Used` at 2L is about twice `Used` at L.
5. **No change of answer.** An internal switch (like `ExpiresOnCheck`, `init` only) gives every call a
   budget of `long.MaxValue - 1`, so the charging path is live; the ported suite and the oracle's three
   seeds then run with it on and must give answers identical to a run without it.
6. **Scope per method.** `IsMatch`, `Match` then `NextMatch`, lazy `Matches`, `EnumerateMatches`,
   `Count`, `Replace` with an evaluator (the evaluator's own work uncharged), `Split`, `EnumerateSplits`:
   each charges and each stops. One budget shared by two calls accumulates.
7. **Interaction.** Cancelled token and spent budget: `OperationCanceledException`. Spent budget and
   `ExpiresOnCheck` firing at the same poll: the budget exception. Timeout alone: existing tests unchanged.
8. **Validation.** Limit 0 or negative throws; a spent budget throws before work, including on the
   minimum-width shortcut that never reaches the engine.
9. **Caches.** First and second call on a fresh pattern report the same `Used`.
10. **API.** `PublicAPI.Unshipped.txt` lists the new members; package validation against 1.2.0 passes.

## 10. Recommendation

- **API: option C**, a `FuzzyRegexWorkBudget` object passed per call on 18 new instance overloads, carried
  by `NextMatch` and lazy enumerations, readable after the call. It is the only option that lets a caller
  read and share the budget, which calibration and ZejPdf's per-page use both need.
- **Exception: X3**, a new `FuzzyRegexWorkBudgetExceededException` with `Limit` and `Used`, checked after
  the token and before the clock.
- **Unit: U2**, turns plus characters walked plus swept positions at a measured constant weight, charged by
  our own counts, deterministic within a version and not promised across versions.
- **Scope**: everything per attempt or growing with the subject; compile, one-off whole-subject scans,
  callbacks and result building uncharged.

Before code: the owner picks the API (C, A, B or D) and the exception (X1, X2 or X3). Then a builder
writes the section 9 tests red, implements, and runs the section 8 measurements.
