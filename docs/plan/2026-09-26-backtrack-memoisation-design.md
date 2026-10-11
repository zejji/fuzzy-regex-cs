# Backtracking memoisation: design

Date: 2026-09-26. Status: Option A implemented the same day (`RepeatInfo.FailureMemo`; the
DECISIONS entry of 2026-09-26 has the measurements and the witnesses); Option B not started.
Evidence probes lived in the session scratchpad (`timing.cs`, `grid.cs`, `few.cs`) and were not
kept; the numbers below are from those runs on the owner's laptop, Release build, single run each,
so read them as orders of magnitude, not benchmarks.

**As built, two details differ from the text below.** Condition 5 could not use the lists
`RecordSubpatternRepeatsAndFuzzySections` builds, because both of its call sites pass a null
parent and the lists stay empty; the compiler instead carries a `WithinSubmatch` flag into atomic,
lookaround, conditional-test and called-group bodies. And only three conditions turned out to have
a witness (the maximum, backreferences, group-exists conditionals); the others are kept without
one, with the reasons on `RepeatInfo.FailureMemo`.

## The problem in one example

`(?:a|a)+c` over `aaaa...ab c` has two identical ways to take each `a`, so a backtracking engine that
fails at the end tries all 2^n ways of splitting the run before giving up. Upstream regex takes 2.7 s
at n=24; the port takes 3.2 s. The same shape with unequal branches, `(a|aa)+c`, grows as about
1.6^n. Upstream already has a mechanism aimed at exactly this, the repeat guards
(`add_repeat_guards`, `_regex.c:23273`; `is_repeat_guarded`, `:9559`; ported as
`Optimiser.AddRepeatGuards` and `MatchState.IsRepeatGuarded`). This design explains why the guards do
not stop it, and how to make them do so without changing any answer.

## Why the existing guards do not help

A body guard on repeat R at position p means "entering R's body at p cannot lead to a match, skip
it". The engine sets one when it backtracks past the `BODY_START` marker for p
(`Matcher.cs:10556`), which is the moment every path that began by entering the body at p has
failed. That is a real failure memo.

But `END_GREEDY_REPEAT` and `END_LAZY_REPEAT` first record every position where the body *succeeded*
as an unprotected span (`state.GuardRepeat(index, rpData.Start, NodeStatus.Body, false)`,
`Matcher.cs:7315` and `:7522`, upstream `:12544` and `:12779`). `GuardList.Guard` returns without
doing anything when the position already lies in any span, protected or not. So once the body has
matched at p, the later failure at p is never recorded. In `(a|aa)+c` the body matches at every
position before anything fails, so no guard is ever set and the search is exponential.

The success mark is not a bug in isolation. It is what keeps upstream's guards correct in cases
where "failed here once" does not mean "will always fail here", because the rest of the match depends
on more than the position. The probe proves this: deleting the success mark changes an answer.

| Pattern | Subject | Current | Success mark removed |
|---|---|---|---|
| `(?:(a)\|a)+(?(1)c\|b)` | `aab` | `aab` | `ab` (wrong) |

The first path through the repeat sets group 1, which makes the conditional demand `c`, so entering
the body at position 1 fails. A second path reaches position 1 without group 1 set, and from there
`aab` matches. A memo keyed on position alone wrongly skips it. Upstream answers `aab` too
(checked with regex 2026.9.10).

## What the literature says

**Davis, Servant and Lee, "Using Selective Memoization to Defeat Regular Expression Denial of Service
(ReDoS)", IEEE S&P 2021, doi:10.1109/SP40001.2021.00032.** A backtracking matcher over a plain regex
(no extensions) becomes linear if it records every (automaton state, input position) pair that has
failed and never explores it again. Memory is a |states| x |input| bit table. Their selective
variant memoises only a subset of states, either those with in-degree above one or those that are
ancestors of a loop back-edge, which is enough to keep linearity, and they compress the table with
run-length encoding. Their extension to lookaround was later shown to be incomplete, and
backreferences need the captured positions in the key, which gives up the linear bound. (Summarised
from Fujinami and Hasuo's section 3 review, below, since the paper's PDF was not reachable.)

**Fujinami and Hasuo, "Efficient Matching with Memoization for Regexes with Look-around and Atomic
Grouping", ESOP 2024, LNCS 14577, arXiv:2401.12639.** Extends the Davis algorithm to lookaround and
atomic groups. The key point for us: inside an atomic group a failure is not a plain failure, so the
memo range must grow from `{false}` to `{Failure(j) | j up to the atomic nesting depth} and
{Success}`. They quote: "Since Java 14, Java's regex implementation has indeed used memoization for
optimization. However, this optimization is not enough to completely prevent ReDoS". They leave
backreferences out, noting that the recent work "supported [them] by additionally recording captured
positions in memoization tables".

**Ruby 3.2 match cache, Feature #19104 (bugs.ruby-lang.org/issues/19104).** Onigmo records
(position, state) pairs in a bit array whose size is "proportional to the product of the number of
cache points of regex and input size", and in Ruby's standard library and Rails the largest regex
had 81 cache points. It is switched on lazily, only once the backtrack count reaches the input
length. It is off for backreferences, subexpression calls, lookaround, atomic groups, possessive
quantifiers, absent operators, a bounded repeat nested inside another repeat such as `(a{2,3})*`, and
very large bounded repeats. Empty loops containing a capture clear the cache "as necessary to keep
the old behavior". A later change (#19725) added lookahead and atomic groups. 7.84 percent of 1,506
real regexes could not use it.

**OpenJDK `java.util.regex.Pattern` (read 2026-09-26 from `openjdk/jdk` master).** `Loop.match`
keeps a set of positions `i` where the body failed, per loop, and skips the body there. It only does
this when all of these hold: the loop is greedy, has no maximum (`cmax == MAX_REPS`), the current
count is already at or past the minimum, the pattern has no group reference (`hasGroupRef`), and the
loop is a top-level closure: any loop nested inside another quantified group, or inside a lookbehind,
is removed from the list (`topClosureNodes.subList(...).clear()`). This is the closest existing
design to ours.

**Other engines.** .NET's `RegexOptions.NonBacktracking` and V8's experimental linear engine avoid
the problem by not backtracking at all, and neither supports lookaround-heavy or backreference
patterns; they change the engine, not the memo, so they are not an option for a port that must give
upstream's answers. Berglund, van der Merwe and le Roux (NCMA 2026, arXiv:2606.26678) show that
memoising a minimum feedback vertex set (a smallest set of states touching every loop) is enough;
in this engine the repeat body entries are exactly such a set, which is why the existing guard lists
are the right place for the memo.

## When a failure memo is sound in this engine

A memo entry says "entering the body of repeat R at position p leads to no match". It is sound only
if everything that happens after that point depends on p and nothing else. Reading
`Matcher.cs` and `MatchState.cs`, the rest of the match can also read:

1. **R's own count.** After the body, count c becomes c+1, and the engine only compares it with the
   minimum and maximum. The compiler unrolls every minimum into copies (`NodeCompiler.cs:1432`), so
   a repeat node's minimum is 0 in practice; with no maximum the count never changes the outcome.
   Condition: R has no maximum and a minimum of 0 or 1 (or record only when c+1 is at least the
   minimum).
2. **Enclosing repeats' count, start and capture-change snapshot.** The rest of the match runs the
   enclosing repeat's `END_GREEDY_REPEAT`, whose zero-width progress test compares the position with
   the enclosing iteration's start. Condition: R is not inside another repeat (Java's rule).
   Relaxation for later: an enclosing repeat with no maximum is harmless if R's body always consumes
   at least one character, because the enclosing progress test is then always true.
3. **Captured groups.** Read by backreferences (`RefGroup*`) and by `GroupExists` conditionals.
   Condition: none anywhere in the pattern. Upstream's `NodeStatus.Ref` analysis already detects both,
   but only along the body or tail; a memo needs the whole pattern clean, because the group can be
   set before R and read after it.
4. **The call stack.** A group call returns to wherever it was called from. Condition: no `GroupCall`
   or `CallRef` in the pattern.
5. **Sub-match context.** Inside an atomic group, lookaround or possessive repeat, the "rest of the
   match" ends at the construct's end marker and a failure there has a different meaning (Fujinami
   and Hasuo's depth-tagged failures). Condition: R is not inside any atomic, lookaround or group-call
   body (it is not in the lists `RecordSubpatternRepeatsAndFuzzySections` builds). Atomic groups,
   possessive repeats and lookarounds *after* R are fine: each is a function of position only.
6. **Fuzzy error counts.** A fuzzy match can reach p again with a different error budget. Already
   handled: `IsRepeatGuarded` returns false whenever `IsFuzzy` (upstream `:9596`). This also covers
   BESTMATCH and ENHANCEMATCH, which are fuzzy-only.
7. **Backtracking verbs.** `(*PRUNE)`, `(*SKIP)` and `(*COMMIT)` cut the stack and `(*SKIP)` moves the
   slice start. Condition: none in the pattern (`Prune`, `Skip` opcodes). `(*FAIL)` is harmless.
8. **Partial matching.** A partial result is neither success nor failure. Condition: not a partial
   match (`PartialSide == PartialNone`).
9. **POSIX matching.** The engine keeps going after a success to find a longer one. Probably sound,
   but not needed; condition: not POSIX in the first version.
10. **The attempt's start position** (only for Option B, which keeps the memo across start
    positions). `\G` reads the search anchor, which moves with each attempt (`InitMatch` sets
    `SearchAnchor = TextPos`), so no `SearchAnchor` opcode. The empty-match rule under `MustAdvance`
    only rejects a match that ends where the attempt began, which a later attempt makes stricter,
    never looser, so an earlier failure stays a failure; still, pin it with a test or disable Option
    B under `MustAdvance` until the test exists. Captures set in an earlier attempt are the
    conditional case again: the probe that kept guards across attempts changed
    `(?:(a)|b)+(?(1)c|b)` over `abbb` from span 1-3 to 1-2, which point 3 already excludes.

Things that are *not* in the key, and why: the capture-change counter (R's progress test compares it
with the value taken at the start of the same iteration, so only the path inside the iteration
matters); lookbehind (reads the text, bounded by the slice, which is fixed for a call except under
`(*SKIP)`); `\K` (moves the reported start, not success or failure); direction (`(?r)` uses the same
positions).

## Options

### Option A: make the guard a true memo for safe repeats, per attempt

The optimiser computes one more flag per repeat, "memo-safe", from the conditions above (pattern-wide
checks once, plus the per-repeat checks: no maximum, minimum at most 1, top level, depth 0). For a
memo-safe repeat, `END_GREEDY_REPEAT` and `END_LAZY_REPEAT` skip the unprotected success mark. That is
the whole change: the existing `BODY_START` and `GREEDY_REPEAT` pops then record failures that
upstream currently throws away, and the existing `IsRepeatGuarded` checks consult them.

- Pros: a few lines; reuses the guard lists, their save and restore, and every check site; removes a
  `Guard` call per iteration, so non-pathological patterns do slightly less work. Exponential shapes
  become linear per attempt.
- Cons: guards are still reset at every start position (`Matcher.cs:10728`), so a failing search is
  still quadratic in the subject, and nested-repeat shapes such as `(a+)+c` stay cubic.
- Measured with the probe (unconditional removal, so only valid for these clean patterns):
  `(?:a|a)+c` n=24: 3224 ms to 0.05 ms; `(a|aa)+c` n=24: 64 ms to 0.08 ms; `(a+)+c` n=1000:
  6771 ms to 2430 ms; `(x+x+)+y` n=1000: 21385 ms to 9105 ms.

### Option B: Option A, plus keep the memo for the whole search call

For a memo-safe pattern with no `\G`, do not reset the memo when the search moves to the next start
position. Store it in its own per-repeat structure rather than in `BodyGuardList`, for two reasons:
the guard lists are copied onto the backtrack stack by `PushRepeats` at every lookaround, atomic group
and conditional (`Matcher.cs:2945`), and a list that grows over a megabyte subject would make each
copy cost O(subject); and a memo fact is path-independent, so it never needs restoring. A bit array of
one bit per position per memoised repeat (125 KB per repeat for a one-million-character subject) or
the existing span list without the save/restore both work; spans are smaller for runs, bits bound the
worst case. Allocate lazily, on the first recorded failure, following Ruby's lazy switch-on.

- Pros: turns the cubic and quadratic failing searches into roughly linear ones and beats upstream
  outright. Measured with the probe (also keeping upstream's other guards across attempts, which is
  more than B proposes): `(a+)+c` n=1000: 6771 ms to 48 ms (upstream 1204 ms); `(x+x+)+y` n=1000:
  21385 ms to 29 ms (upstream 3830 ms); `(\w+\s?)+$` n=1000: 154 ms to 0.8 ms (upstream 72 ms);
  `(?:a|a)+c` n=1000: 57 ms to 2 ms.
- Cons: one new structure and one more soundness condition (the start position); memory proportional
  to the subject for each memoised repeat; must also cover the tail guards of repeat-one nodes inside
  the memoised body to get the `(a+)+c` figure, which needs the same analysis for the inner repeat's
  tail (its tail is R's end marker, so condition 2's relaxation applies).

### Option C: full selective memoisation (Davis, Fujinami and Hasuo)

A (node, position) failure table at every branch and repeat entry, depth-tagged failures inside atomic
groups, cached lookaround results, and captured positions in the key for backreferences.

- Pros: covers patterns A and B exclude (repeats inside atomic groups and lookarounds, nested bounded
  repeats).
- Cons: a second memo system beside the guards, touching every opcode that pushes backtrack state;
  keys that include captures lose the linear bound anyway; high risk of an answer change in the
  fuzzy and group-call code. This is the spaghetti the owner rule warns against.

## Recommendation

Do A, then B, as two separate slices, each behind the differential grid below. A is small, removes
work, and fixes every exponential shape in the list. B is where the remaining quadratic and cubic
failing searches go, and it is what puts the port ahead of upstream rather than level with it. Stop
before C: the shapes it adds are rare, and the cost in complexity is out of proportion.

Neither option addresses the constant-factor gaps on matching workloads (the 2.7x to 5.1x figures
at small n). Those shapes are not exponential in upstream at the measured sizes; the probe shows the
port at n=24 spends 0.1 to 0.5 ms either way. They need a profile, not a memo.

## Where to measure

- The four shapes above plus `(?:a|a)+c` and `(a|aa)+c` at n = 16, 24, 1000 and 100,000, search and
  fullmatch, port against upstream on the same machine (the benchmark skill's pyperf method). Add
  them to `bench/FuzzyRegex.Benchmarks/WorkloadBenchmarks.cs` as a "pathological" group so the gate
  records them.
- The full existing suite, before and after each slice, compared with
  `tools/probes/compare-two-baselines.ps1`. Expect A to be neutral or slightly faster (one fewer guard
  call per iteration). For B, watch `Long`, `LongNoMatch` and `Dense` (one-megabyte subjects, many
  start positions) for the memo allocation and the lost reset; lazy allocation should make it
  invisible when nothing fails.
- Memory: allocations per call on the one-megabyte corpora (`AllocationTests` already pins a warm
  `IsMatch` at 0 B, so B must allocate only on first failure, and reuse through `MatchStateCache`).

## Risks

- **An answer change.** The conditional example above is the proof that the risk is real. Every
  exclusion needs a witness test that fails when the exclusion is removed; where no witness can be
  found, say so in the code comment with the argument.
- **Memory on long subjects.** Bounded by one bit (B) or one span (A) per position per memoised
  repeat. A pattern with many memoised repeats over a megabyte could reach megabytes; cap the number
  of memoised repeats, or fall back to no memo past a budget, as Ruby discusses.
- **Hidden reliance on the success mark.** Other code may expect a body position to be marked after
  success. Search: only `GuardList.Guard` and `IsGuarded` read spans, and `IsGuarded` ignores
  unprotected ones, so the only effect of the mark is to block later guards. Re-check at
  implementation time.
- **Oracle drift.** Faster than upstream is fine; different is not. Timeouts that upstream hits and we
  no longer hit change no answer.

## Test plan

1. **Failing tests first.** `(?:a|a)+c` over 30 `a`s plus `bc` completes under a small iteration or
   time budget (fails today: 2^30 steps). `(a+)+c` over 10,000 `a`s for B.
2. **Witness tests, one per exclusion, each failing with its exclusion deleted:** conditional
   (`(?:(a)|a)+(?(1)c|b)` over `aab` is `aab`); conditional across attempts
   (`(?:(a)|b)+(?(1)c|b)` over `abbb` spans 1-3); backreference; group call; nested repeat with an
   empty-matching body; a repeat inside an atomic group and inside a lookahead; `(*PRUNE)` and
   `(*SKIP)`; `\G` across attempts; partial match; `MustAdvance` with an empty match in `Matches`.
3. **Differential grid, zero answer changes.** Every pattern in a generated set (the probe's 43
   patterns covering captures, backreferences, conditionals, lookarounds, atomic, possessive,
   lazy, bounded, nested, verbs, `\G`, reverse, plus random patterns from a small grammar) against
   every subject over `{a, b, c}` up to length 6 and a few long ones, comparing every match's group
   spans from `Matches()` with the memo on and off (an internal switch used only by the test). The
   probe grid ran 47,085 rows with the success mark removed unconditionally and found no difference,
   which shows the grid is not yet sharp enough: it missed the conditional witness. Add the witnesses
   and grow it with patterns whose body branches overlap, since only overlapping branches reach the
   same position by two paths.
4. **The oracle suite** (`tests/FuzzyRegex.OracleTests`) against upstream, unchanged.
5. **Mutation:** each memo-safe condition is a clause Stryker can delete; each deletion must be killed
   by a witness from step 2.
