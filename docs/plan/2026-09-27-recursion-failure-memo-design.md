# A failure memo for fuzzy recursion: design

Date: 2026-09-27. Status: research only, nothing in the engine has changed. Branch
`maint/fuzzy-exact-deletion` at a3e878d. The measurements come from a Release build on the owner's
laptop, one run per cell unless it says "median", so read them as orders of magnitude. The probes are
in `tools/probes/recursion-failure-memo/`. The counting and prototype code is kept as
`instrumentation.patch` beside them, which is not applied: `git apply` it to reproduce the numbers,
and `git apply -R` it afterwards.

## Summary

- **The hypothesis was half right.** Each recursion level really does re-enter the fuzzy sections
  with a fresh budget, and the same small problem is solved again and again. But the repeats are not
  repeated *whole states*. They are the same *call* reached under many different call stacks. A
  classic failure memo, keyed on the whole state including the call stack, would remove 82% of row
  A's work and only 7% of row B's. It leaves both exponential.
- **Every recursive call on both rows fails.** Not one of the 889,000 calls on row A (4 characters)
  or the 469,000 on row B (6 characters) ever returns. Row A has 2,390 different calls among them and
  row B has 7,326. The rest are repeats.
- **Recommended design (C1): remember calls that failed.** When a call runs out of options without
  ever returning, remember its entry key. After that, fail any call with the same key at once. The
  key holds only what a called group can read before it writes: the call target, the position, the
  fuzzy counts and limits, how far each enclosing section still is from its minimum, the spans that
  backreferences read, and the open calls ahead of the position. A prototype takes row A from 69.5 s
  to 41 ms on first run and row B from 8.2 s to 20 ms (about 3 ms each once warm). On 1,012,164
  grid rows it changed no span, group, count or edit. It did change capture *lists* on 428 rows, all
  with a group call inside a lookaround, and partial matches on 34,831 rows, so both are excluded.
- **Some exponential work is left.** Calls that *return*, followed by a caller that then fails, are
  not covered. The grid has shapes that stay slow. For those, keep the existing timeout as the
  documented limit, which raises an exception and never gives a wrong answer. Call summaries (C2)
  are the only full fix, and they are a much larger change.

## Terms

- **State**: everything the matcher can read when it takes its next step. That is the pattern node,
  the text position, the fuzzy error counts, the repeat counters, the captured groups, and the
  **saved stack** (`MatchState.Sstack`), which holds each open call's return point and the counts
  of the sections it left.
- **Failure memo**: a set of states known to lead to no match. Reaching one again means you can
  backtrack at once. This is sound (never changes an answer) only if the key holds everything the
  rest of the search can read.
- **Call**: a `GROUP_CALL` step, written `(?R)`, `(?1)` or `(?&name)` in a pattern. The called group
  runs and then returns to the caller.
- **Exit**: a call reaching its `GROUP_RETURN`, so the caller carries on.
- **Section**: a fuzzy group such as `(?:...){1<=e<=2}`. A section nested inside another counts
  its own errors from zero. A section's **minimum** (the `1` in `1<=e`) is checked when it ends.
- **Retry**: ledger 42's extra choice. It retries an exactly matched fuzzy item as a deletion.

## 1. How much of the blow-up is repeated work?

### Timings

The rows are search `(|)(?:(?:(?:(?:.)+((?:(?R)){2,}|)){2<=e<=3}(?=b))){1<=s<=1,1<=d<=2}` (row A)
and fullmatch `(?b)(?:(?:.(?:(?:(?:b)+(?R)||)){1<=e<=2}(?:c)*?){2<=d<=3})` (row B). Each subject
below is a prefix of the reported one. The answer is "no match" in every cell. Commands:
`timing.cs`, `upstream.py`.

| Row | Subject | Retry on | Retry off | Upstream 2026.9.10 |
|---|---|---|---|---|
| A | `bax` | 225 ms | 0.4 ms | MemoryError after 0.9 s |
| A | `baxb` | 378 ms | 0.6 ms | MemoryError |
| A | `baxba` | 5,047 ms | 1.5 ms | MemoryError |
| A | `baxbax` | over 60 s (69.5 s reported) | 4.4 ms | MemoryError |
| B | `xxaxa` | 123 ms | 0.4 ms | MemoryError after 0.85 s |
| B | `xxaxab` | 221 ms | 0.6 ms | MemoryError |
| B | `xxaxabx` | 1,225 ms | 1.5 ms | MemoryError |
| B | `xxaxabxx` | 8,300 ms | 5.2 ms | MemoryError |

`dotnet run` on a file-based probe builds the library as Debug unless you pass `-c Release`, and
Debug is 5 to 10 times slower. Row B showed "over 60 s" until that was fixed.

### Counting states

The patch counts every pass of the main matching loop (a **visit**, one node dispatched). It also
counts how many of those visits are *different* under each candidate key. Every key is a 64-bit
hash, so two different states could collide, but at these volumes the chance is below one in a
hundred thousand. Driver: `count-states.cs`.

| Key | What it contains |
|---|---|
| strict | everything the forward matcher can read, including the raw saved stack and every history (capture lists, change lists, absolute counters) |
| norm | the same, but each history reduced to the part that is read: current spans, and "has the counter moved since this iteration began" |
| no-stack | norm without the saved stack, which means ignoring where each call returns to |
| call-level | norm, but only the part of the saved stack above the innermost open call, plus how far each enclosing section is from its minimums, plus the open calls at or after the innermost call's position |

Row A, retry on:

| Length | Visits | strict distinct | Repeated (strict) | call-level distinct | Repeated (call-level) | Deepest call |
|---|---|---|---|---|---|---|
| 1 | 3,620 | 1,297 | 64.2% | 497 | 86.3% | 2 |
| 2 | 50,230 | 14,523 | 71.1% | 2,148 | 95.7% | 3 |
| 3 | 686,810 | 157,150 | 77.1% | 7,936 | 98.8% | 4 |
| 4 | 9,438,810 | 1,704,302 | 81.9% | 25,630 | 99.7% | 5 |

Row B, retry on:

| Length | Visits | strict distinct | Repeated (strict) | call-level distinct | Repeated (call-level) | Deepest call |
|---|---|---|---|---|---|---|
| 2 | 1,584 | 1,472 | 7.1% | 866 | 45.3% | 3 |
| 4 | 76,128 | 70,764 | 7.0% | 8,433 | 88.9% | 5 |
| 6 | 3,748,312 | 3,481,660 | 7.1% | 42,218 | 98.9% | 7 |
| 7 | 27,870,216 | 25,911,868 | 7.0% | not run | | 8 |

What the tables show:

1. **Under the full state, most of the work is genuinely different.** The number of distinct strict
   states grows about 11 times per character on row A and 7 times on row B. That is the same rate
   as the time. A perfect whole-state memo would cut row A's work by about a factor of 5 at length
   4 and row B's by 7%, and neither would stop growing exponentially. The norm key agrees (83.2% and
   11.5%), so this is not an artefact of hashing raw histories.
2. **The differences are almost all in the saved stack.** Take the saved stack out of the key
   (no-stack) and row A at length 4 has 8,254 distinct states, not 1.58 million. So what differs is
   the continuation: which chain of callers, each with its own section counts, is waiting for this
   call to return.
3. **At the level of one call, almost everything is a repeat.** Here is a small example. On row A
   at length 4, the call at position 2 with no errors yet in its section is reached under dozens of
   different stacks, because the callers below it spent their errors in different ways. The called
   group cannot see those differences. It fails the same way each time.
4. **With the retry off, the work is small**: row A at length 4 makes 13,352 visits, and row B at
   length 6 makes 11,036. The retry multiplies the ways into each call, which is why the blow-up
   appeared with ledger 42.

### Calls that never return

The opcode histogram has no `GROUP_RETURN` and no `SUCCESS` on either row. No call ever returns.

| Row, length | Calls | Distinct call entries (call-level key) | Entries that ever returned | Calls to an entry already known to fail |
|---|---|---|---|---|
| A, 4 | 888,950 | 2,390 | 0 | 886,560 (99.7%) |
| B, 6 | 468,538 | 7,326 | 0 | 461,212 (98.4%) |

**So the answer to question 1:** under the whole state, 82% of row A and 7% of row B is repetition.
The rest is genuinely distinct, and it is exponential. Under a key that describes only what one call
can see, 99.7% and 98.9% is repetition, and the distinct part grows polynomially. The design has to
work at the level of the call.

## 2. What the key must contain, and why the memo is sound

### The design being proved

When a call is made at position p with entry key K, and K is in the set of failed calls,
backtrack at once. Otherwise make the call, and note that it is open and has not returned. If
`GROUP_RETURN` runs for the call, mark it as returned. When backtracking reaches the call's own
`GROUP_CALL` entry, which happens only after every choice inside the call has been tried, and the
call never returned, add K to the set.

### The claim

If two calls have equal keys and the first ran out of choices without returning, then the second
would also run out of choices without returning. Skipping it leaves the matcher exactly where
running it would have left it. Four things must hold.

**(a) The call's run depends only on its key.** Here is everything the steps inside a called group
read, and where each one comes from:

| What the called group reads | Where it comes from | In the key? |
|---|---|---|
| Position, text, direction | the position | yes |
| Slice bounds (moved by `(*SKIP)`) | state | yes |
| Open section: node and its counts | state | yes |
| Pass limits `MaxErrors`, `MaxCost` (each `(?b)` or `(?e)` pass sets them) | state | yes |
| Whole-match totals `TotalErrors`, `TotalCost` | state | yes |
| Enclosing sections, through every call level (read by `Matcher.RaisesUnmetMinimum`, which decides whether an empty iteration counts for the "needed" rule, and by `Matcher.AllMinimumsMet`) | saved stack, via `MatchState.TryOuterSection` | yes, reduced to each section's node plus how far it still is from each minimum, counting the errors of the sections inside it. That is all those two functions compute. |
| Spans of groups read by a backreference or a group-exists conditional | state | yes, the current span of each such group |
| The re-entry guard (`MatchState.ActiveCalls`: a group may not be called again at a position where a call of it is open) | open calls | yes, the open calls at positions the called group can reach: at or after p going forwards, at or before p under `(?r)`. If a call can happen inside a lookbehind, which runs the other way, it must be every open call the lookbehind can reach, not only those at or after p. The prototype's `MinimalKey` does not build this yet: it keeps only the open calls at or after the position, so a call reached inside a lookbehind is under-keyed there. Upstream confirms the lookbehind direction matters: `(a)b(?<=(?1)b)` over `ab` gives `None` in regex 2026.9.10, because the lookbehind runs the pattern backwards and reaches the call from the far side. The implementation must either build the lookbehind-aware field or exclude a call that can happen inside a lookbehind, the same way the key excludes what it cannot yet describe. |
| The caller's repeat counters, starts and guards | state | no. The called group starts each repeat it enters at its head (group boundaries nest, so it cannot reach a repeat's end without its head), `GROUP_CALL` empties the guard lists, and the repeat memo is emptied on each entry. |
| Capture-change and edit counters | state | no. They are only compared with values recorded inside the same call. |
| The exact-deletion narrowing (`Matcher.DeletionRepeatsAnEarlierAlternative`, `Matcher.cs:4816-4819`), which compares the item being deleted against the caller's last fuzzy change | state | no. It only narrows out a branch that repeats one already explored inside the same call, so it can change how a call reaches a given point but not whether the call as a whole succeeds or fails. No witness found where it changes a memo answer. |
| Capture lists (history) | state | no. They are appended to, never read. See (b). |
| Everything else on the saved stack: return points, saved groups and repeats, frames of lookarounds and atomic groups in the callers | saved stack | no. The called group's own frames balance, and it reaches the frames below only through the section chain above. |
| Start of the attempt (`MatchPos`) | state | no. Only `SUCCESS` reads it, and a called group cannot reach `SUCCESS`, so this field itself need not be in the key. |
| Search anchor (no insertion where the search began) | state | yes, unless the set is reset at every `InitMatch`. `InitMatch` sets `SearchAnchor = TextPos` on every pass (`MatchState.cs:1153`), not once per matcher call: a search tries a new start position by calling `InitMatch` again with the new `TextPos` (`Matcher.cs:12531`), and the best-match second walk calls it once per entry, offset and error limit with the same `MaxErrors` and slice (`Matcher.cs:13088-13099`, called from `12870` and `13098`). `\G` reads the anchor (`Matcher.cs:2502`), and so does the no-insert-at-search-start rule (`Matcher.cs:5021`, `!search \|\| TextPos != SearchAnchor`). Witness: search `(?b)(?:.??(?1)\|z)(?:q){e<=1}(?(DEFINE)(\Ga))` over `zaq` answers `(1,3)` with 0 substitutions, 0 insertions, 0 deletions with the memo off; a set kept across the best-match walk's second entry gives the wrong `(0,2)` with 1 substitution, 0 insertions, 0 deletions and 4 memo hits. The same happens with `(?:\|(\Ga))` in place of the `DEFINE` group, and both agree with the memo off, and agree with each other once `(?b)` is removed. **Design fix: the failed-call set is cleared inside `InitMatch`, so it is one set per pass, not per matcher call.** That keeps the anchor and the search flag constant for the whole life of any one set, at no cost to the measured rows, whose blow-up happens within a single pass. |

**(b) A failed call leaves nothing behind.** Backtracking undoes everything the call wrote: the
current capture of each group (restored by the backtrack arm's `PopGroups`), repeats, counts, edits,
the capture-change counter, the open-call records and the section frame. There are three exceptions.
Each is a place where a construct succeeds and throws away the undo entries of its body, so a failed
path still leaves a mark:

- **Capture lists.** When a lookaround's body succeeds, the undo entries of the captures made
  inside it are thrown away, so `captures()` keeps entries from paths that later failed. Skip a
  failed call and those entries are never written. The grid found 428 such rows, every one with a
  lookaround. One example: `(?r)(a)(?:b(?:(?R)|)(?R)?(?:(?!.(?R)(?R))(?:a.))*?.){2<=e<=3}` over
  `aa` gives the same spans with and without the memo, but group 1's capture list differs. Atomic
  groups, possessive repeats and conditional tests throw entries away the same way, so they are
  excluded too, as a precaution rather than on a witness: `(a)(?:(?>.(?1))x|.)+?b` over `aaab`
  leaves no stray entry in either engine, so the mechanism exists but this shape does not trigger it.
  This only happens when a group **call** writes the capture, not any group inside a lookaround:
  `(a)(?:(?!.(?1))|.)+?b` over `aaab` gives group 1 captures `[0,1][2,1]` in both upstream regex
  2026.9.10 and the port, and so does `(a)(?:(?=.(?1))x|.)+?b`, but `(a)(?:(?!.(a))|.)+?b`, which
  captures directly rather than through a call, gives only `[0,1]` in both. Upstream is inconsistent
  with itself here, so this looks like a bug it and the port both inherited, not a design choice;
  it is queued separately for an upstream-bug check, and if it is fixed the capture-list exclusion
  can go.
- **`\K` inside such a construct.** This is the same mechanism that `PatternObject.KeepInSubmatch`
  already excludes from the failure memo.
- **Partial matching.** A path that reaches the end of the text records a partial result
  (`HitEnd`), even if it then fails. This changed 34,831 grid rows. The existing failure memo
  already switches off for partial matches (`MatchState.KeepsFailureMemo`).

**(c) Skipping gives the same next state.** The memo check runs before `GROUP_CALL` pushes
anything. Backtracking at once therefore resumes from the entry below, which is exactly where the
backtrack arm would have left the matcher after the call ran out of choices.

**(d) The first call finishes before a repeat can ask.** A second call with the same key cannot
start while the first is still open. It would be a call of the same group at the same position,
which the re-entry guard refuses. Its key would also differ, since the first call is among the open
calls it includes.

**Cuts and verbs.** A key is recorded only when the backtrack arm of `GROUP_CALL` runs. If anything
cuts the backtracking stack below the call, the arm never runs and nothing is recorded. That covers
an enclosing atomic group or lookaround that succeeds, a `(*PRUNE)`, `(*SKIP)` or `(*COMMIT)`, a
timeout and a cancellation. A verb confined to a lookaround inside the called group depends only on
the key. So verbs look safe. The grid ran verb patterns with the memo firing and found no changed
span or count. As `RepeatInfo.FailureMemo` does, keep them excluded without a witness in the first
version. The rows need neither verbs nor POSIX.

**Best-match modes.** `(?b)` and `(?e)` run several passes. Each pass is an ordinary first-match
search under tighter pass limits. The failed-call set must not survive from one pass to the next,
because the search anchor (see the row above) is reset at the start of each pass and a stale entry
keyed on the old anchor can misjudge the no-insert-at-search-start rule in the new one; clearing the
set inside `InitMatch` gives each pass its own set, so a failure recorded in one pass is still a
plain failure within that pass alone. POSIX matching goes on after a success, but a called group
never reaches `SUCCESS`. Exclude POSIX anyway (not needed, and the grid has no POSIX rows).

**How this relates to the existing memos.** `RepeatMemoHit` keys one run of one repeat. The run is
emptied whenever a call enters the repeat again, so it never sees the other levels. `FailureMemo`
is off in every fuzzy pattern (`KeepFailureMemosSound`), and upstream's guards are reset at every
`GROUP_CALL`. None of them looks at a call as a unit, which is where the repeats are.

**Where the proof stops.** It rests on the table in (a) being complete, which comes from reading
the opcodes. A missed read would make the key too small. The grid is the check on that. It is how
a prototype bug was caught: the first version of the key left out the call target, and
`(?e)()(?:a(?1)(?R)?(?=(?:(?R)|)(?1)(?:bb(?1)){e<=2})a){1<=e<=2}` over `aa` lost its match. That
row is now a ready-made witness that the call target must be in the key.

## 3. How other engines bound this

All sources below were read on 2026-09-27 unless marked.

- **PCRE2** (`pcre2api`, pcre.org/current). It does not memoise; it counts. `match_limit` (default
  10 million) counts passes through the main matching loop and returns `PCRE2_ERROR_MATCHLIMIT`.
  The count restarts at each start position tried during a search, so it bounds one attempt, not
  the whole search. `depth_limit` and `heap_limit` bound nesting and memory. A pattern can lower
  them with `(*LIMIT_MATCH=)` and similar. Measured today with libpcre2 10.47
  (`pcre2-recursion-limits.py`): `(?:(?:a|a)+(?R)?)+c` over 10 to 30 `a`s answers "match limit
  exceeded" in about 100 ms. `PCRE2_ERROR_RECURSELOOP` ("nested recursion at the same subject
  position") is the guard this port already copies as ledger 14.
- **Perl's super-linear cache** (`perlreguts.pod`, "The super-linear cache", blead, reworked in
  September 2026 by PR #24843). It keeps one "failed already" bit for each (quantifier, position)
  pair. It covers only quantifiers with no maximum, once their minimum is met, and at most 16 of
  them. It turns on only after (string length) x (number of eligible quantifiers) iterations, and it
  is reset when a backreference or `(??{...})` appears in the rest of the pattern. The document
  says nothing about recursion. The key is the position alone, like upstream's guards, so it
  cannot see a fuzzy budget or a call stack.
- **.NET `RegexOptions.NonBacktracking`** (learn.microsoft.com, "Regular expression options",
  updated 2026-07-08). It "guarantees linear-time processing in the length of the input". It gets
  there by not supporting backreferences, lookarounds, atomic groups, conditionals or balancing
  groups. .NET has no recursion at all.
- **RE2 and Rust `regex`** (not re-read today). They guarantee linear time by leaving out
  backreferences, lookaround and recursion. They are no help for a pattern that uses them.
- **Davis, Servant and Lee, "Using Selective Memoization to Defeat ReDoS", IEEE S&P 2021;
  Fujinami and Hasuo, ESOP 2024** (summarised in `2026-09-26-backtrack-memoisation-design.md`,
  not re-read today). Memoising (state, position) failures makes a plain regex linear. Atomic
  groups and lookarounds need failures tagged with how deep the cut went. Backreferences need the
  captured positions in the key, which gives up the linear bound. C1 is their idea applied at the
  boundary of each call, with the fuzzy context added to the key.
- **TRE and agrep** (not re-read today; `tre-fuzzy-check.py` records how this repo uses TRE 0.8.0).
  They do approximate matching by simulating an automaton with error counts: agrep with Wu and
  Manber's bit-parallel method (CACM 1992), and TRE with a tagged NFA. They have no backtracking and
  no recursion, so the cost is bounded by pattern size x text length x error budget.

**What applies to fuzzy matching combined with recursion.** No surveyed engine has both. The
engines that support recursion either count and give up (PCRE2) or memoise by position only
(Perl, upstream's guards), which is not sound once a budget is part of the state. The research
says a memo is sound when its key includes whatever extra state the rest of the match reads, and
that the price is a larger key. Measured here, the key stays small only if it describes one call
and not the whole stack. That is the classic "procedure summary" idea from program analysis, and
the one packrat parsers use for (rule, position). A call that failed is the simplest summary: it
has no exits. PCRE2 shows that stopping with a documented error is an accepted answer for work
that stays exponential.

## 4. Candidate designs

| Design | What it does | Rows A and B | Memory | Risk |
|---|---|---|---|---|
| C0 whole-state failure memo | the original hypothesis: key on the full state, including the call stack | A: about 5x less work, B: 7%; both stay exponential | the key includes the whole saved stack: hundreds of bytes for each of millions of states | low, but it does not fix the problem |
| **C1 memo of calls that failed** | as proved in section 2 | A `baxbax` 41 ms (first run), `baxba` 5,174 ms to 2.9 ms (median); B `xxaxabxx` 20 ms first run, 8,157 ms to 3.1 ms median | one entry per distinct failed call: 672 for row A at 6 characters, 19,380 at 15; about 100 bytes each | moderate: the key must be complete, and the grid checks it |
| C2 call summaries | also remember each call's exits, in order, and replay them to later callers | also fixes calls that return | one entry per exit | high: exits must be produced lazily, in search order, with their capture and edit changes; this touches every opcode that pushes onto the saved stack |
| C3 a documented limit | keep the existing `MatchTimeout` (and the 1 GB stack limit); optionally add a step limit like PCRE2's | bounds the time, gives no answer | none | none; an exception is not a wrong answer |
| C4 retry off inside recursion | option (b) of 2026-09-26 | fast | none | rejected: it keeps a known ordering bug, against the owner's rule that no known bug stays |

### C1 in numbers

Growth with the prototype (minimal key, first run, including JIT; `count-states.cs A 15 memo-min`):

| Row A length | 6 | 9 | 12 | 15 |
|---|---|---|---|---|
| time | 40.7 ms | 39.0 ms | 139.5 ms | 447 ms |
| failed calls recorded | 672 | 2,717 | 8,008 | 19,380 |

| Row B length | 8 | 12 | 15 | 18 |
|---|---|---|---|---|
| time | 19.9 ms | 79.4 ms | 34.9 ms | 69.3 ms |
| failed calls recorded | 741 | 2,636 | 4,898 | 9,414 |

Row A's records grow about as n^4 and row B's as n^3: polynomial, where before they grew 11 and 7
times per character. The degree comes from the key's position-valued fields.

**Cost where it does not help** (`overhead.cs`, median of 7 warm runs; prototype hashing, not
optimised):

| Pattern | Off | On |
|---|---|---|
| balanced brackets `\((?:[^()]++\|(?R))*\)`, 300 deep | 1.145 ms | 1.318 ms (+15%) |
| the same with a trailing `z`, no match over 12,000 characters | 11.9 ms | 12.4 ms (+4%) |
| `(?:a(?R)?b){e<=1}`, fullmatch over 60 a's and 60 b's | 0.030 ms | 0.043 ms |
| palindrome `^((.)(?:(?1)\|.?)\2)$` over 20 characters | 0.006 ms | 0.009 ms |

**Benchmarks.** No workload in `bench/FuzzyRegex.Benchmarks` contains a group call (checked with a
grep of `WorkloadBenchmarks.cs`, `UsageCorpus.cs` and `Corpus.cs`). A memo switched on at compile
time only for patterns with a group call therefore never runs on the benchmark suite. The
no-regression estimate is zero, by construction. I did not run BenchmarkDotNet. Confirm it with
`tools/probes/compare-two-baselines.ps1` when implementing, and add a recursion group to the
workloads so the gate sees these shapes.

To remove the small cost on ordinary recursion, **switch it on lazily**, as Perl and Ruby do.
Count calls, and start recording only once the count passes (subject length) x (number of call
sites). A pattern that never goes super-linear then pays one counter per call.

**Memory.** Store an exact key, not a hash. A fixed part (call target, position, three counts,
section node, limits and totals) plus a variable part: one small entry per enclosing section, one
per open call ahead, and one span per tested group. That is about 64 to 128 bytes with the set's
overhead. Cap the set at 2^20 entries, as the repeat memo does. Past the cap, stop recording and
keep answering from what is held. That is still correct, and the worst case is about 100 MB.

### What remains exponential

The grid also timed the rows that took more than 250 ms with the memo off. Seed 3 had 476 such
rows, not counting partial matches. With the memo, 320 of them dropped under 250 ms and 156 stayed
slow. Three of the slow shapes (`residual.cs`, 5 s cap):

| Shape (abridged) | Subject | Retry on | + memo | Retry off | + memo |
|---|---|---|---|---|---|
| `(?b)((?:(?R)\|))(?:\1b(?R)?...(?<=a)){e<=2}` | `ababa` | over 5 s | 30 ms | 306 ms | 9 ms |
| `((?:(?R)\|))(?:(?:(?:(?R)\|)(?:(?1)){d<=1}...){2<=e<=3}){e<=2}` | `ababa` | over 5 s | over 5 s | over 5 s | 197 ms |
| `(?r)((?:(?:(?R)\|)(?:b.)+(?:(?R)\|)){1<=e<=2})...{e<=2}` | `abab` | over 5 s | 2,874 ms | 36 ms | 36 ms |

The first is fixed. The second was already slow before ledger 42, so it is not new. The third is
new: its calls return and their callers then fail, which C1 does not cover and C2 would.

## Recommendation

1. **Build C1**, with the key from section 2, the three exclusions (partial matching; a
   lookaround, atomic group, possessive repeat or conditional test whose body can reach a capture
   group, a call or `\K`; and, without a witness, verbs and POSIX), lazy switch-on, and the 2^20
   cap. It is one set, reset inside `InitMatch` so it lives for one pass and not the whole matcher
   call, a check and a record in `GROUP_CALL`, a mark in `GROUP_RETURN`, and a record in the
   `GROUP_CALL` backtrack arm. It takes both reported rows from seconds to
   milliseconds, faster than main (151 ms and 57 ms), and costs the benchmark suite nothing.
2. **For the exponential part that is left, document the limit; never return a wrong answer.**
   Write in `docs/DIVERGENCES.md` and in the `MatchTimeout` documentation that a fuzzy pattern with
   recursion can take exponential time when calls return and their callers fail, and that
   `MatchTimeout` is the bound. A timed-out call raises `RegexMatchTimeoutException`. Upstream
   raises MemoryError on these shapes, so this is no worse than upstream.
3. **Keep C2 as a follow-up**, designed only if the residual shapes turn up outside a generated
   grid. Its complexity is out of proportion to what the grid shows today.

## 5. Test plan

1. **Failing tests first.** Row A over `baxbax` and row B over `xxaxabxx`, each with a 2 s
   timeout. Today they time out; with C1 they need about 40 ms on first run, which leaves 50 times
   the headroom and keeps them stable. Growth test: row A over 15 characters in under 5 s. Pin the
   search-anchor witness too: search `(?b)(?:.??(?1)|z)(?:q){e<=1}(?(DEFINE)(\Ga))` over `zaq` must
   answer `(1,3)` with the memo on, not `(0,2)`.
2. **Witnesses, one per key field and per exclusion, each failing when its clause is deleted.**
   - Call target: the `(?e)()(?:a(?1)(?R)?...` row over `aa` above.
   - Capture lists inside a lookaround: the `(?r)(a)...` row over `aa` above, minimised.
   - Partial matching: minimise one of the 34,831 grid rows.
   - Search anchor: the `(?b)(?:.??(?1)|z)(?:q){e<=1}(?(DEFINE)(\Ga))` row over `zaq` above, and the
     `(?:|(\Ga))` variant; the set must be reset inside `InitMatch` or these fail.
   - Open calls inside a lookbehind: `(a)b(?<=(?1)b)` over `ab`, which upstream answers `None`;
     confirm the port agrees once the lookbehind-aware field exists, or is excluded until it does.
   - Still to be found: position; counts; the section chain's distance from its minimums (write a
     row where the "needed" rule decides); tested spans (a backreference inside the called group);
     open calls ahead of the position going forwards; slice (a `(*SKIP)` that moves it); pass limits
     (a `(?b)` row whose second pass reuses a first-pass failure).
   - Where no witness exists, say so in the code comment with the argument, as
     `RepeatInfo.FailureMemo` does.
3. **Ablation switch.** Add `PatternObject.SkipCallMemo`, which this library never sets, like
   `SkipExactDeletionRetry`. Turn `memo-grid.cs` into a test that compares memo on and off over
   every mode (search, match, fullmatch, partial) and every flag. Widen its grammar: calls inside a
   lookbehind, `(?&name)`, `(?0)`, possessive repeats, `\K`, POSIX and bounded repeats, `\G`, and a
   `(?b)`/`(?e)` second walk over several entries and offsets. Run one more grid with
   `NarrowExactDeletions` on, to confirm the exact-deletion narrowing never changes a memo answer.
   Run three seeds of 3,000 patterns overnight (about 4 minutes each). It must show zero changes,
   capture lists included.
4. **An independent grader.** Upstream answers MemoryError on both rows, so it cannot grade them.
   Extend `tools/probes/fuzzy-reference-matcher.py` with `(?R)`, `(?n)` and lookahead, and grade
   the rows' short prefixes and the grid's small rows against it. The owner rule is a search that
   is complete and in order, and only the reference matcher checks that the memo keeps both.
5. **The oracle suite** (`tests/FuzzyRegex.OracleTests`) must stay unchanged, including the
   exact-deletion ablation rows.
6. **Benchmarks:** before and after, compared with `compare-two-baselines.ps1`. Add a recursion
   group to the workloads: balanced brackets, the palindrome, rows A and B at a safe length, and a
   growth series.
7. **Mutation:** every key field and every exclusion is a clause Stryker can delete. The tests in
   step 2 must kill each one.

## Open questions for the owner

1. **Is `MatchTimeout` enough as the documented limit,** or do you want a step limit like PCRE2's
   `match_limit` (a new option and a new exception)?
2. **Should C2 get a design now** because of the third residual shape, or wait until it turns up
   in real use?

## Review findings (2026-09-27)

A blind review found five points, folded in above, and reproduced the headline measurements:

1. **HIGH, fixed.** The search anchor is not fixed for a whole matcher call: `InitMatch` resets it
   on every pass, including each new start position tried in a search and each entry of a best-match
   second walk, and both `\G` and the no-insert-at-search-start rule read it. Witnessed with search
   `(?b)(?:.??(?1)|z)(?:q){e<=1}(?(DEFINE)(\Ga))` over `zaq`. The design now clears the failed-call
   set inside `InitMatch`, so it lives for one pass, not one matcher call.
2. **MEDIUM, fixed.** The key table was missing `DeletionRepeatsAnEarlierAlternative`, which reads
   the caller's last fuzzy change. Added to the table with the argument that it only narrows a
   branch already explored inside the same call, so it cannot change a call's success or failure; no
   witness found, and a `NarrowExactDeletions`-on grid run is now in the test plan.
3. **LOW, fixed.** The lookbehind clause is designed (every open call a lookbehind can reach, not
   just those at or after the position) but the prototype's `MinimalKey` does not build it yet. Noted
   as a gap the implementation must close, with upstream's `(a)b(?<=(?1)b)` over `ab` as the witness
   that direction matters.
4. **LOW, fixed.** The atomic-group exclusion has no witness; reworded to say so plainly, with
   `(a)(?:(?>.(?1))x|.)+?b` over `aaab` recorded as a pattern that does not trigger the mechanism.
5. **Answered.** Owner question 1 is resolved: upstream and the port agree with each other, but only
   when the capture comes through a group call, and upstream disagrees with itself between the call
   and non-call forms (`(a)(?:(?!.(?1))|.)+?b` vs `(a)(?:(?!.(a))|.)+?b` over `aaab`). This looks like
   a bug inherited from upstream, queued separately, and the open question is replaced with this
   finding.
6. **Reproduced, no change needed.** Row A and B timings and the grid's zero-change result were
   reproduced independently. Added: PCRE2's `match_limit` count restarts at each start position, so
   it bounds one attempt, not a whole search.

Also noted in passing, not part of this design: `(?b)(?:.??(?1)){e<=1}(?:x|(\Gab))` over `zab` throws
`NotImplementedException` ("needs:basic-matching - the matcher has no `SearchAnchor` yet") with the
memo on and off alike. Not a memo bug; queued separately.
