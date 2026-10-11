# Failure memo, Option B: keep the memo across start positions

Date: 2026-10-11. Status: design for the owner, nothing built. Main at a1191e65. Read-only session:
no build, test or benchmark was run for this note. Every claim about our engine comes from reading
the code (file:line below); every claim about Python `regex` comes from runs of regex 2026.9.10 on
CPython 3.14.7 on the owner's laptop, scripts in `.scratch/perf-designs/`. Estimates are labelled.

Background: `docs/plan/2026-09-26-backtrack-memoisation-design.md` (Option A landed the same day,
DECISIONS 2026-09-26) and `docs/plan/2026-09-27-recursion-failure-memo-design.md` (the call memo,
whose soundness method this note follows).

## Terms

- **Attempt**: one try at matching from one start position. A search over a subject of length L
  makes up to L+1 attempts, moving the start one character each time.
- **Pass**: one run of `BasicMatch` with `search` set, which makes all the attempts of one search
  call. An exact search makes one pass (`Matcher.cs:19294` calls `InitMatch` once, then the FAILURE
  arm moves to the next start at `Matcher.cs:18144-18244` and jumps to `start_match`, `:12650`).
- **Memo repeat**: a repeat with `RepeatInfo.FailureMemo` set (`PatternObject.cs:70-111`): top
  level, no maximum, minimum at most 1, in a pattern with no backreference, group-exists
  conditional, group call, `(*PRUNE)`, `(*SKIP)`, fuzzy or POSIX matching, and no `\K` inside a
  submatch (`NodeCompiler.cs:1676-1684`, `Optimiser.cs:701-730`).
- **Body guard at p**: an entry in the repeat's `BodyGuardList` saying "entering this repeat's body
  at position p leads to no match". For a memo repeat it is only ever a failure (Option A removed
  the success mark, `Matcher.cs:13713`, `:13958`).

## What Option B changes

Today the per-attempt reset (`state.ResetGuards()`, `Matcher.cs:18238`, body at
`MatchState.cs:2237-2249`) empties every guard list before the next start position. Option B keeps
the body guard lists of memo repeats for the whole pass. Nothing else changes: the lists are the
same objects, the same check sites read them (`Matcher.cs:13763`, `:14006`, `:14618`, `:15166`), the
same backtrack arms write them (`:18038`, `:18295`).

The 2026-09-26 design proposed a separate per-repeat structure because the guard lists were copied
onto the backtrack stack at every lookaround, atomic group and conditional. That reason has gone:
D35 already leaves a memo repeat's lists out of `PushRepeats` (`Matcher.cs:4714-4732`), so a list
that grows over the pass is never copied. B can reuse `BodyGuardList` with no new structure and no
new allocation.

### A timeline on `(a+)+c` over `aaaa` + `bc` (n = 4)

R is the outer `( )+`, a memo repeat. The inner `a+` is a one-character repeat inside it.

Today (Option A):

1. Attempt at 0. R enters its body at 0. `a+` takes `aaaa`, then gives back one `a` at a time; at
   each stop q it runs END_GROUP and END_GREEDY_REPEAT, and R tries another iteration at q. Every
   one of those iterations fails (no `c` after an `a`), so R records body failures at 4, 3, 2, 1
   and finally 0. Each entry at p costs a walk of the n - p positions `a+` gives back.
2. FAILURE: move to 1, **empty every guard list**.
3. Attempt at 1 repeats step 1 from scratch over positions 1..4, recording 4, 3, 2, 1 again.
4. And so on for 2, 3, 4 (`b`, `c` are rejected at once).

Work: about n^2/2 give-back steps per attempt, n attempts, so about n^3/6 in all. The 2026-10-11
profile matches this: 1,472,802 `IsRepeatGuarded` calls per call at n = 200, and n^3/6 = 1,333,333
(`.scratch/perf-profile/notes.md`).

With Option B:

1. Attempt at 0: the same, recording body failures at 4, 3, 2, 1, 0.
2. FAILURE: move to 1, **keep R's body list**.
3. Attempt at 1: R's entry checks the body guard at 1 (`Matcher.cs:14618`). It is set, R's minimum
   is 1 so the tail is not tried, and the attempt fails at once.
4. Attempts at 2, 3, 4: the same.

Work: n^2/2 for attempt 0, then a constant per attempt. Cubic becomes quadratic, and the quadratic
part is all in the first attempt.

`(\w+\s?)+$` over 200 times `aaaa ` and `!` behaves the same way: in attempt 0 an iteration of R
starts at every position (`\w+` can stop after any letter and `\s?` can match empty), so every
position gets a body failure, and every later attempt stops at its first check. Attempt s costs
about L - s today, so the total is about L^2/2; with B it is about L.

**Unverified on our engine:** that attempts after the first really cost a constant. This is
reasoning from the code; the first build step is a Debug `WorkCounter` count that shows it.

## When is "failed from start s" still true from a later start s'?

A body guard at p says: every path that enters R's body at p reaches no match. Option A already
argues that, within one attempt, everything after that point depends on p and nothing else. B adds
one question: what can the rest of the match read that differs between attempts? Here is every
piece of state that is set per attempt or per pass, and every engine feature the brief names.

| Feature | What it could read that changes with the attempt | Verdict for B |
|---|---|---|
| Attempt start `MatchPos` | Set per attempt (`Matcher.cs:12891`). Read by SUCCESS only to report the start, and by FAILURE's move to the next start (`:18171`). Neither decides whether a path from p fails. | Argued safe. |
| `\G` | Reads `BackslashGPosition`, which is `OperationStart` for every exact call (`Matcher.cs:3850-3861`, D79; only BESTMATCH and ENHANCEMATCH move it, and they are fuzzy, so no memo). Fixed for the pass, so `\G` is a fact about the position. | Argued safe. Exclude anyway in the first slice (no measured row needs it), with witness G1 below pinned. |
| Search anchor and `MustAdvance` | SUCCESS rejects a match ending at `SearchAnchor` when `MustAdvance` is set (`Matcher.cs:17058`, `:12846`, `:12898`). `SearchAnchor` is set once per pass (`MatchState.cs:1944`). A path that enters R's body at p ends at p or later (forward), so only a guard at p = anchor can be caused by the rejection, and every later attempt asks only about positions after the anchor. Mirrored under `(?r)`. | Argued safe; witnesses M1, M2 pinned. |
| Lookbehind reaching before s | A memo repeat is never inside a lookbehind (`WithinSubmatch`, `NodeCompiler.cs:1681`). A lookbehind after R reads text before p, possibly before s. The text and the slice are the same for every attempt of the pass. | Argued safe; witness B1 pinned. |
| Captures | Cleared per attempt (`ClearGroups`, `:18235`). Read only by backreferences, group-exists conditionals and group calls, all of which switch the memo off pattern-wide (`Optimiser.cs:706-719`). | Excluded by the existing guard. Witness A1 (the earlier probe's cross-attempt change) shows why it must stay. |
| Conditionals | Group-exists: excluded as above. Lookaround conditionals after R: a fact about position, as Option A argues; a memo repeat is never inside the test. | Excluded / argued safe as in A. |
| Group calls and recursion | Excluded pattern-wide (`GroupCall`, `CallRef`, `Optimiser.cs:716-717`). | Excluded by guard. |
| Fuzzy counts and limits | Memo off for any fuzzy pattern (`Optimiser.cs:704`), and `IsRepeatGuarded` returns false when fuzzy (`MatchState.cs:2343-2346`). | Excluded by guard. |
| BESTMATCH / ENHANCEMATCH re-runs | Fuzzy only, so excluded as above. They also call `InitMatch` per run, which resets everything anyway (`Matcher.cs:19480`, `:19830`). | Excluded by guard. |
| Partial matching and run-outs | Per-attempt flags: `AttemptTouchedTheTextToCome` and `Forks` (`Matcher.cs:12661-12669`), `ForkAttemptStart`, w-marks. The touch flag decides what SUCCESS does, and it differs between attempts, so a failure recorded in one attempt may be a success in another. `KeepsFailureMemo` argues soundness within an attempt only (`MatchState.cs:2312-2327`). | **New exclusion:** keep the per-attempt reset whenever `PartialSide != PartialNone`. No witness found yet. |
| `(*PRUNE)`, `(*SKIP)` | Excluded pattern-wide. `(*SKIP)` would also move the slice and the next start (`SkippedAttemptVerb`, `:12893`). | Excluded by guard. |
| `(*COMMIT)` | Not a verb in regex 2026.9.10: `(*COMMIT)a` raises "unknown verb", and so do `(*ACCEPT)` and `(*THEN)`. `(*FAIL)` only fails a path. | Not applicable. |
| Atomic groups | A memo repeat is never inside one. One after R is a fact about position (Option A). | Argued safe as in A. |
| `\K` | Top-level `\K` moves `MatchPos` and is undone on backtrack (`:15104-15109`); it changes the reported start, not success or failure. Inside a submatch it switches the memo off (`KeepInSubmatch`). | Argued safe; witness K1 pinned. |
| Overlapped scans | Each match of an overlapped scan is its own search call, and each call's `InitMatch` empties the lists. | Argued safe; witness O1 pinned. |
| Reverse `(?r)` | Attempts move left; positions mean the same thing; the anchor argument mirrors. | Argued safe; witness R1 pinned. |
| R's count and the enclosing repeats | Entering at count 0 (a new attempt) versus count c >= 1: after the body the count is at least the minimum (at most 1) either way, and there is no maximum. No enclosing repeat (top level). This is Option A's argument, unchanged. | As in A. |
| D17 empty-iteration states, repeat memo | Only for patterns with tested groups, which switch the failure memo off. | Not applicable. |

So B needs one condition beyond Option A's: not in a partial call. A conservative first slice also
leaves `\G` patterns out.

Upstream behaviour is not at stake here: B can only skip work whose result is already known, so
an answer that differs from main is a bug in B, never "upstream's bug".

## Cost on paper

**Memory.** None new. The lists already exist and keep their capacity between calls
(`MatchState.cs:1320-1322`). A list holds at most one span per run of guarded positions, so at most
(L + 1) / 2 + 1 spans of 12 bytes for each memo repeat. Option A can already reach that size inside
one attempt; B only keeps it for longer. The warm `IsMatch` stays at 0 bytes (`AllocationTests`),
because nothing is allocated.

**Time.** A position is guarded at most once: once guarded, the body is never entered there again
(`:14618`, `:13763`), and `Guard` returns early for a guarded position. So a pass records at most
L + 1 failures per memo repeat, against up to (L + 1) per attempt today. Each record can shift the
spans to its right (`GuardList.InsertSpan`, `GuardList.cs:139`), so the worst case is O(L^2)
element moves per pass. Today's worst case is that per attempt, O(L^3) per pass.

**Adversarial input.** `(?:aa)+c` over `a` * L + `b`. Attempt 0 guards the even positions, right to
left, each as its own span inserted at the front: about L^2/8 moves. That is today's cost too, and
upstream's (`insert_guard_span` is the same memmove). Attempt 1 guards the odd positions, each
merging two neighbours, cheap because the spans to its right are already merged. Attempts 2 and
later stop at once. Today every attempt pays its own L^2/8. So B is never worse than A here;
the single-attempt quadratic memmove is an inherited ceiling, out of scope for this note (a bit
array would remove it at the price of an allocation; record it as a SHORTCUT if B lands).

**How the memo is bounded.** By the subject length per memo repeat, by the pass (each `InitMatch`
empties it), and by the existing timeout and cancellation checks, which B does not touch
(`Matcher.cs:12651`).

## Expected gain

| Row | Ours today | Python (gap table / this session) | B, estimate |
|---|---|---|---|
| PathologicalNestedRepeat, `(a+)+c`, n = 200, IsMatch | 19.58 ms | 14.40 ms / 15.84 ms | about 0.5 to 2 ms |
| PathologicalWordsToEnd, `(\w+\s?)+$`, 200 words, IsMatch | 96.55 ms (about 1.15x of Python after today's merges, per the brief) | 69.50 ms / 71.20 ms | about 0.5 to 1 ms |

How the estimates are made (all labelled estimates, none measured on main):

- Nested: the remaining work is attempt 0's n^2/2 give-back steps, about 3/n of today's n^3/6,
  so 1.5 percent of 1.47 million guard checks at n = 200: about 0.3 ms plus fixed costs. The
  2026-09-26 probe (all guards kept across attempts, a cruder change than B) measured
  `(a+)+c` at n = 1000 at 48 ms against 2,430 ms for Option A; scaled by n^2 to n = 200 that is
  about 2 ms. Hence 0.5 to 2 ms.
- Words: attempt 0 is about 2/L of today's work (L = 1001). The same probe measured this pattern at
  1,000 characters (the benchmark's size) at 0.8 ms, from 154 ms.
- Both rows would move from failing or marginal to far ahead of Python (roughly 0.01x to 0.15x).
  B does nothing for CaseFoldAccented (no repeat) or for the TryMatch gap the profile found; those
  are separate.

**Python on these rows** (regex 2026.9.10, best of 3 to 5, `search`, every answer `None`):

| n | `(a+)+c` over `a`*n + `bc` | `(\w+\s?)+$` over `aaaa `*n + `!` |
|---|---|---|
| 50 | 0.51 ms | 3.80 ms |
| 100 | 2.80 ms | 15.83 ms |
| 200 | 15.84 ms | 71.20 ms |
| 400 | 100.06 ms | 312.11 ms |

Python grows by about 6x per doubling on the nested row (close to cubic) and about 4.3x on the
words row (quadratic). With B ours would grow by about 4x and 2x.

## Test plan

1. **Failing tests first.** Debug `WorkCounter` step budgets, red on main: `(a+)+c` over 2,000
   `a`s and `bc` under a budget a quadratic pass meets and a cubic one does not; `(\w+\s?)+$` over
   1,000 words and `!` under a budget linear in L.
2. **Witness tests.** Expected answers measured with regex 2026.9.10 (`py-witness.py`,
   `py-pins.py`):

   | Id | Pattern | Subject | Expected | What it guards |
   |---|---|---|---|---|
   | A1 | `(?:(a)\|b)+(?(1)c\|b)` | `abbb` | (1, 4) | captures across attempts; the 2026-09-26 probe that kept all guards across attempts changed this row |
   | A2 | `(?:(a)\|b)+(?(1)c\|b)` | `abbbb` | (1, 5) | the same, longer |
   | G1 | `(?:\G\|b)(?:a\|a)+c`, pos 1 | `xbaac` | (1, 5) | `\G` with a start after 0 |
   | M1 | `Matches((?:a\|a)*)` | `aab` | (0,2) (2,2) (3,3) | `MustAdvance` after an empty match |
   | M2 | `Matches((?:a\|a)*(?=b))` | `aab` | (0,2) (2,2) | the same with a lookahead tail |
   | B1 | `(?:a\|b)+(?<!^a.)c` | `abac` | (0, 4) | lookbehind reaching before the attempt start |
   | K1 | `(?:a\|a)+\Kc` | `aaac` | (3, 4) | top-level `\K` |
   | O1 | overlapped `(?:a\|a)+c` | `aacaac` | (0,3) (1,3) (3,6) (4,6) | overlapped scan |
   | R1 | `(?r)c(?:a\|a)+` | `bcaab` | (1, 4) | reverse |
   | P1 | partial `(?:a\|a)+cd` | `aacaac` | (3, 6), partial | partial call |

   A1 and A2 are witnesses in the strict sense only for the existing pattern-wide rule. The partial
   exclusion has no witness yet: build a grid of partial searches with B forced on; if it finds
   none, keep the exclusion and write the reason at the line, as `RepeatInfo.FailureMemo` does.
3. **Differential against main**, `tools/probes/port-probe.cs`, never `--no-build`: at least 50,000
   rows biased to memo-safe patterns that fail at many starts (overlapping branches, nested
   repeats, lookbehind and lookahead after the repeat, `\G` with pos, `\K`, reverse, overlapped,
   `Matches` with empty matches, partial), subjects over `{a, b, c}` up to length 8 plus long runs.
   Zero differences in spans, groups, captures and the partial flag; only timeouts may differ.
4. **Oracle** at three seeds with `ExpectedDivergences` strict; ratchet; AOT tests and smoke.
5. **Mutation:** deleting the partial clause or the `\G` clause must be caught, or the clause is
   documented as unwitnessed.
6. **Benchmarks:** the two rows plus `Long`, `LongNoMatch` and `Dense` (long subjects, many starts,
   where a persistent list could cost lookups), JIT and Native AOT, allocation column beside time.

## Options

| Option | What | Pros | Cons |
|---|---|---|---|
| B as designed (2026-09-26) | Separate per-repeat memo (bit array or span list), lazily allocated | Bit array bounds the insert cost at O(1) | A new structure and an allocation per state; its main reason (guard lists copied onto the stack) was removed by D35 |
| **Narrow B (recommended)** | Keep memo repeats' body lists across attempts; keep the per-attempt reset for partial calls and for patterns with `\G` | About ten lines at one site; no new structure, no allocation; inherits all of Option A's exclusions and witnesses; small sync cost (one `sync-divergence:` at `Matcher.cs:18238`, one ledger row) | Leaves the single-attempt O(L^2) insert ceiling (inherited, unchanged); partial calls stay at today's cost |
| No B | Leave the reset | Zero risk | Both pathological rows stay at or near the gate limit; the cubic nested case stays slower than Python as n grows |

## Recommendation

Build the narrow B as one Phase 7 slice: red step-budget tests, then the change at
`Matcher.cs:18238` (reset non-memo lists as now; skip memo repeats' body lists unless
`PartialSide != PartialNone` or the pattern has a `SearchAnchor` node), the witness table above,
the differential, oracle, AOT. Widen to `\G` later with its own grid if a workload wants it. The
gain is large (an estimated 10x to 100x on the two rows) for a small, local divergence, which is
the case the 2026-10-11 performance policy asks for.
