# Nested repeats in near-linear time, and the memo extensions

Date: 2026-10-13. Status: S2d and G2 built and measured (amendment 1, after a blind review; numbers
under "Measured"); the rest is design. Branch `perf/c5-nested` from main at a3d79986. Sections 1 to
4 were written before anything ran and are kept as written; the gates are one locked script,
`.claude/worktrees/c5-nested/.scratch/c5/gates.sh`. Every claim about our engine comes from reading the code (file:line below); every claim
about Python `regex` comes from regex 2026.9.10 on the owner's laptop, run on 2026-10-13, with the
output quoted. Estimates are labelled as estimates. Section 4 lists the experiments that settle them.

Background: `docs/plan/2026-10-11-memo-option-b.md` (Option B, merged at 4aaa924f, keeps memo
repeats' body guards across start positions) and its "Could not verify" section, which found the
cause this note works on.

## Terms

- **Attempt**: one try at matching from one start position. A search over a subject of length L
  makes up to L + 1 attempts.
- **Memo repeat**: a repeat with `RepeatInfo.FailureMemo` set (`PatternObject.cs:68-112`). Its body
  guard at p means "entering the body at p leads to no match".
- **The copy**: the first iteration that `NodeCompiler.BuildRepeat` builds in front of a repeat
  (`NodeCompiler.cs:1598-1644`). `(X)+` compiles as `(X)(X)*`.
- **R**: the outer repeat that remains after the copy, `(X)*` in that example.
- **Lookup**: one `MatchState.IsRepeatGuarded` call (`MatchState.cs:2351-2362`), counted by
  `WorkCounter.GuardChecks`.

## 1. Where the quadratic comes from

### The compiled shape

`BuildRepeat` copies the minimum number of iterations out of every counted repeat
(`NodeCompiler.cs:1613-1644`). The comment at `:1598` says "if it contains a repeat", as upstream's
does (`upstream/src/_regex.c:25166`), but the loop has no such test: it runs whenever the minimum is
above zero. Two consequences:

- The repeat that remains always has a minimum of 0 (`:1643`). So the `minCount <= 1` term of
  `FailureMemo` (`:1686`) is always true, and the Option B note's timeline ("R's minimum is 1 so the
  tail is not tried") is slightly off: R's tail is tried at every entry, and fails at once.
- The copy is built at the outer repeat depth (`subargs = args`, `:1617`, with no `++RepeatDepth`),
  while R's body is built one level deeper (`:1701`). So a repeat inside the copy is at depth 0 and
  gets a memo flag of its own; the same repeat inside R's body is at depth 1 and does not.

`(a+)+c` therefore runs as three repeats, numbered in the order `RecordRepeat` meets them (`:1675`):

| Index | What | Kind | Guards active |
|---|---|---|---|
| 0 | the copy's `a+` | `GreedyRepeatOne` (`:1727-1738`) | tail only (`Optimiser.cs:437`, upstream issue 494) |
| 1 | R, `( )*` | `GreedyRepeat`, memo repeat | body and tail |
| 2 | R's body's `a+` | `GreedyRepeatOne`, depth 1 | tail only |

### The trace, `(a+)+c` over `a`*n + `bc`

Attempt 0 (start at 0):

1. The copy's `a+` (#0) counts all n `a`s (`CountOne`, `Matcher.cs:1056`), then walks back one
   character at a time from n (backtrack arm, `Matcher.cs:18468-18660`; tail guard written at
   `:18494`).
2. At each stop q it enters R. R's head (`Matcher.cs:14728-14771`) checks R's body guard at q (not
   set the first time), enters the body: #2 counts from q to n, then its forward loop "unmatch until
   it's not guarded" (`Matcher.cs:14947-14975`) asks #2's tail guard at n, n-1, ... . Positions above
   q + 1 are already guarded by the entries at q + 1, q + 2, ..., so that loop costs n - q lookups
   before it reaches q + 1, tries the tail there (R's `END_GREEDY_REPEAT`, body and tail both
   guarded, `:13914-13971`), and fails.
3. R's body at q fails, so R's backtrack arm records the body failure at q (`Matcher.cs:18456-18457`),
   and R's tail `c` fails at q.

So attempt 0 costs about n²/2 lookups, plus n²/2 characters scanned by `CountOne` (a per-character
loop, `Matcher.cs:1129-1136`, not vectorised). It leaves R's body guarded at every position 1..n.

Attempt s ≥ 1 with Option B (R's body list kept, `MatchState.cs:2241-2252`, call at
`Matcher.cs:18398-18400`):

1. #0 counts n - s `a`s and walks back. Its tail guards were emptied at the start of the attempt
   (`MatchState.cs:2253`), so every stop q is tried.
2. At each q, R's head finds the body guarded (`:14771`, kept by B) and does not enter it; R's tail
   `c` fails; R's backtrack arm runs.

So each later attempt costs a few steps per position it walks back over: the "about 2.5n steps per
later start" the Option B build measured, and about 1.25n² for all of them. The search is quadratic
in steps, and the larger share of it is the later attempts that walk the copy.

### `(x+x+)+y` over `x`*n + `z`, the same shape one level up

The copy is `x+x+` (#0, #1, both depth 0). In attempt s, for each length k of #0, #1 counts from k
to n and walks back over every end r, and at each r R's head finds its body guarded and its tail
fails. Per attempt that is about (n - s)²/2 positions, so the search is **cubic** in steps today,
even with B. (Python: 0.71, 4.09, 25.9 and 164.4 ms at n = 40, 80, 160 and 320, about 6.3x per
doubling; quoted in the evidence section.)

### `(?:(?:a|a)+)+$`, where the memo does not reach at all

The inner `(?:a|a)+` inside R's body is at depth 1, so it has no memo, and it explores every way to
split the run between its two identical branches: exponential in attempt 0 alone. Nothing in this
note's first options changes attempt 0, so this shape stays exponential unless option N below is
built. Upstream is exponential too (37.1, 177.7 and 820.6 ms at n = 14, 16 and 18). The oracle
relies on that: it is the `alt-nested` row of `TIMEOUT_SHAPES` (`tools/record-oracle.py:6638-6649`).

## 2. Options

Each option lists what it is, its worst case on paper (text length L, run length n, pattern size m),
why it gives the same answers, the input built to hurt it, the witness tests that must stay green,
and which of the slow-shape tests it would break.

### S2d: skip an attempt whose start is already a known failure of R (recommended first)

**What.** When the pattern begins with the copy of a memo repeat R, and R's minimum was 1 (so there
is exactly one copy; with `{2,}` the pattern starts with the first of two copies, which is not "R's
first iteration"), an attempt at s can only match
by entering the copy at s. Before running that attempt, look up R's body guard at s; if it is set,
treat the attempt as failed at once and move to the next start. One lookup per attempt. The
compiler records "the pattern starts with the copy of repeat i" in a new `PatternObject` field when
the start node is the copy's first node after optimisation (`Optimiser.cs:305-310` can move the start
node, so the check has to be made at the end of the optimiser).

**Placement (amendment 1, from the review).** The skip must behave exactly like an attempt whose
first node failed, because the `FAILURE` arm does more than step on: when the copy starts with a
`GreedyRepeatOne` (`(?:a*b)+c`) its `LeadingRepeat` skip (`Matcher.cs:18338-18374`) also runs from
the failed start, and the slice-end test decides when to stop. So the check sits where an attempt
begins, after `MatchPos` is set (after `SearchStart`, and at `next_match_2`), and on a guarded start
it jumps to `backtrack`, which pops the `FAILURE` entry `start_match` pushed and runs the arm from
`MatchPos` as for any failed attempt. That also covers the starts a prefilter chooses. The lookup
goes through `MatchState.IsRepeatGuarded` (false under fuzzy matching, counted by `WorkCounter`),
only in a search with `PartialSide == PartialNone`, and the field is set only when
`KeepsFailureMemosAcrossAttempts` holds. The `LeadingRepeat` skip stays sound after a skipped start:
it rests on the attempt at that start failing, which the guard proves as surely as running it.

**Why the answers cannot change.** The claim needed is: if entering R's body at s fails, entering the
copy at s fails. The copy is the same code built a second time (`NodeCompiler.cs:1617-1622`), and
after it comes R with a count of 0, no maximum and a minimum of 0. Compare what can follow a piece
X that ends at p':

- after R's body at s, `END_GREEDY_REPEAT` tries another iteration at p' if the iteration moved
  (`:13874`, `TextPos != Start`, or a capture change, which only referenced groups make and a
  backreference switches the memo off) and then the tail (`:13971`, the count is at least the
  minimum, 0);
- after the copy at s, R's head at p' tries an iteration at p' (`:14771`) and then the tail
  (`tryTail = node.Values[1] == 0`).

The two agree except when X matched empty (p' = s), where the copy's route also tries "R's body at
s" again. That extra route fails too, since R's body at s fails. So the copy at s fails whenever R's
body at s fails, under the same pattern-wide conditions as the memo itself
(`Optimiser.cs:704-737`). Option B's argument (`PatternObject.cs:567-583`) already covers reading a
guard written in an earlier attempt. Partial calls are excluded as B excludes them, and `\G`
patterns never have `KeepsFailureMemosAcrossAttempts` set.

No guard needs to be written: attempts only move forward (backward under `(?r)`), so the attempt at s
is the last time this pass asks about s.

**Conditions with no possible witness** (VERIFICATION rule 12, the argument instead). "The minimum
was exactly 1" (`oneCopy` in `NodeCompiler.BuildRepeat`): with two or more copies the recorded start
is the last copy's, which is never the pattern's start node, so the start-node comparison already
excludes it. The partial condition: a partial call empties every guard list at each start (the
`ResetGuards` call in the `FAILURE` arm), so the lookup could never find a guard. Both stay as
defence.

**Cost on paper.** One binary-search lookup per attempt, O(log S) for S spans in R's body list; no
allocation. A pattern without the field must pay nothing: hoist the test inside the branch that
already reads `pattern.KeepsFailureMemosAcrossAttempts` at `Matcher.cs:18398`, and confirm with the
`Long`, `LongNoMatch` and `Dense` benchmark rows.

**What it does to the three shapes (estimates).**

- `(a+)+c`: attempt 0 is unchanged (n²/2 lookups and n²/2 characters scanned); attempts 1..n each
  cost one lookup. Removes the 1.25n² steps of the later attempts.
- `(x+x+)+y`: attempt 0 is unchanged; attempt 1 runs (R was never entered at 1, since the copy is at
  least two characters wide) and costs about n²/2; attempts 2..n cost one lookup each. Cubic becomes
  quadratic.
- `(\w+\s?)+$`: already linear with B (19 steps per later start); this cuts each later start to a
  lookup and the attempt set-up.

**Adversarial input.** A pattern whose copy is at the start but whose R is rarely entered, such as
`(ab+)+c` over `ab`*n: R is entered only at even positions, so odd starts find no guard and pay the
lookup on top of today's work. That is O(log S) more per attempt, against an attempt that costs at
least one node step: under 10% only if a lookup is cheaper than a step, which the benchmark rows must
confirm. A pattern like `foo(a+)+c` gets nothing (the copy is not at the start); option S2a covers it.

**Witnesses** (regex 2026.9.10, quoted below): `(a+)+c` over `aaabaac` (4, 7) captures `['aa']`;
`(a*)+c` over `aabc` (3, 4) captures `['', '']` (empty body); `(?:a?b?)+c` over `abxabc` (3, 6);
`(a+)+?c` over `aaxaac` (3, 6) (lazy R); `(a+){2,}c` over `aaxaaac` (3, 7) captures `['aa', 'a']`
(two copies: only the last one is "R's first iteration", and the pattern then starts with the
first, so S2d must not apply); `(?r)c(a+)+` over `xcaaba` (1, 4); `(x+x+)+y` over `xxxzxxxy` (4, 8);
`(\w+\s?)+$` over `ab cd!ef gh` (6, 11); `finditer((a*)+)` over `aab` (0,2) (2,2) (3,3) and
`finditer((a*)+(?=b))` (0,2) (2,2) (`MustAdvance`); overlapped `(?:a+)+c` over `aacaac`
(0,3) (1,3) (3,6) (4,6). Plus every existing `FailureMemoTests` and `FailureMemoAcrossAttemptsTests`
row. The mutation check: the `{2,}` row and the partial exclusion must each go red when their clause
is deleted, or be written up as unwitnessed.

**Slow-shape tests it breaks.** None. Every timeout test uses a bounded repeat, a backreference
(memo off) or `(?:(?:a|a)+)+$`, whose cost is all in attempt 0.

### S2a: a memo check in front of the copy wherever it is

**What.** The general form of S2d: a new zero-width node in front of the copy that fails when R's
body is guarded at the current position and otherwise pushes a `BODY_START` entry for R
(`Matcher.cs:14070-14072`), so that backtracking past it records R's body failure there through the
existing arm (`Matcher.cs:18181-18191`). Same soundness argument as S2d. Covers `foo(a+)+c` and
`\b(a+)+c`. **Cost**: one node step and three stack words per entry into the copy; a new opcode has
to be taught to every pass that switches on `node.Op` (six `switch (node.Op)` sites in the engine,
plus `TryMatch` and the test-node logic at `Optimiser.cs:807-925`). **Recommendation**: only if a
workload needs a non-leading copy; price it against S2d first.

A variant that needs no opcode, S2b, relinks the graph at the end of the optimiser so the copy is
bypassed and R gets a minimum of 1. It is answer-identical only when the body cannot match empty
(an empty first iteration gives an extra capture: `(a*)+` over `''` captures `['', '']` upstream,
`(a*)` captures `['']`), and it cannot be switched off for a partial call, which is decided after
compiling. Not recommended.

### G2: skip a guarded span in one step

**What.** `GuardList` stores sorted spans of guarded positions (`GuardList.cs:161-196`). The forward
`GreedyRepeatOne` loop (`Matcher.cs:14947-14975`) asks one position at a time and steps back one
character each time a position is guarded. Add `GuardList.TryGetSpan(pos)` (the span holding pos)
and jump to the span's far end. In the `(a+)+c` trace, each entry into #2 then costs one lookup
instead of n - q.

**Amendment 1, from the review.**

- Only the greedy forward loop. There `CountOne` has already matched every character the walk
  passes, so the jump skips nothing but lookups. The lazy arm (`Matcher.cs:18670-18812`) tests each
  character (`IsTailPartial`, `MatchOne`, `TryMatch`) before its guard, so a jump there would skip
  those tests; it is left alone.
- The lookup goes through a `MatchState.IsRepeatGuarded` overload that also returns the span and
  keeps every side effect of the original: false under fuzzy matching, `WorkCounter.GuardChecked`,
  `NoteSecondPassGuard`. `GuardList` is never called directly.
- Spans carry a `Protect` flag and merge only when exactly adjacent and of the same kind
  (`GuardList.Guard`). So the jump goes to the far end of the one span found and the loop asks
  again there; a following span, or an unprotected one, is met on the next turn. A span can reach
  past this entry's minimum or below its start, so the jump is clamped: if it passes more characters
  than the count has above the minimum, nothing is left to try and the loop ends unmatched, as the
  one-at-a-time walk would.
- Surrogate pairs: guards on a run of astral characters are two units apart and never merge
  (`GuardList.Guard` merges only when `textPos - High == 1`), so the jump saves nothing there. The
  emoji row is an answer witness only, with no lookup bound. Merging by code point would change
  what every guard list means; not done.

**Why the answers cannot change.** The loop already skips every guarded position and stops at the
first unguarded one at or above the minimum; jumping over a span skips exactly the positions it
would have skipped one by one. The count has to come down by the same number of characters, which
needs care in reverse and for surrogate pairs (`Step`, not subtraction; the span ends are positions,
so count = characters between them, as `CountBetween` computes). The backtrack loop
(`:18584-18605`) calls `TryMatch` before the guard, and a run-out there decides partial results, so
leave that loop alone.

**Cost on paper.** O(log S) per entry instead of O(n). With S2d, attempt 0 of `(a+)+c` falls to
about n log n lookups plus the n²/2 characters `CountOne` scans; the character scan is then the only
quadratic part, the same class as plain `a+c` (Python: 0.18, 2.54 and 40.4 ms at n = 400, 1,600 and
6,400, quadratic too).

**Adversarial input.** Alternating guarded and unguarded positions (`(?:aa)+`-style patterns that
guard every other position): every span has one position, so the skip saves nothing and costs one
extra comparison per position. Within the 10% rule if the comparison is the span lookup already made.

**Witnesses.** The existing repeat and guard tests, a reverse row (`(?r)c(a+)+`), a surrogate-pair
answer row (`(\U0001F600+)+c`), and a Debug lookup bound on `\A(a+)+c` (one attempt, so S2d plays no
part) that goes red without the jump.

**Slow-shape tests it breaks.** None.

### O8: keep a top-level single-character repeat's tail guards across attempts

**What.** Option B for the tail list of a depth-0 `GreedyRepeatOne`/`LazyRepeatOne` in a pattern
where `KeepsFailureMemosAcrossAttempts` holds. Today every tail list is emptied per attempt
(`MatchState.cs:2253`). For `a+c`, attempt s would find positions s + 1..n already guarded and, with
G2, fail after its `CountOne`. **Soundness**: the tail of a depth-0 repeat is the rest of the
pattern, and B's table already argues that nothing the rest reads changes between attempts. The
tail guard is only ever written as a failure (`:18494`, `:19084`). **Gain**: small on the gate rows
once S2d lands (it removes attempt 1 of `(x+x+)+y`), larger for `x+T` shapes in general, which
Python also runs in quadratic time. **Breaks**: none of the slow-shape tests. Park until a workload
shows `x+T` failing over long runs.

### C: collapse `(x+)+` into `(x+)` (the .NET ReduceLoops idea)

**What.** .NET's `RegexNode.ReduceLoops` (`RegexNode.cs:872-935`, read 2026-10-11, notes in
`.scratch/perf-libs/notes.md`) merges a loop directly inside a loop, `(?:a+)+` into `a+`. Upstream has
no such pass.

**When the answer is identical.** Measured on upstream over every subject from `{a, b, c}` up to
length 5 or 6, with search, fullmatch, overlapped finditer and partial search, comparing spans, every
capture, `lastindex` and the partial flag (scripts in the evidence section):

| Rewrite | Rows | Differences |
|---|---|---|
| `(a+)+T` to `(a+)T`, also under `(?r)`, POSIX | 18,928 each | 0 |
| `(a+)+?T` to `(a+)T` | 18,928 each | 0 |
| `(?:a+)+T` to `a+T`; `([ab]+)+T` to `([ab]+)T`; `(\w+)+T` to `(\w+)T` | 18,928 / 26,232 | 0 |
| `(a+)*T` to `(?:(a+))?T` | 18,928 each | 0 |
| `(a{2,})+T` to `(a{2,})T`; `(?:(ab)+)+T` to `(ab)+T` | 26,232 | 0 |
| `(a+)++c`, `(?>(a+)+)c`, `(?=(a+)+c)`, `(?1)` call, `(*PRUNE)`, `(*SKIP)`, `{e<=1}`, `{s<=1}`, `(?b)` | 4,095 each | 0 |
| `((?:ab)+)+c` to `((?:ab)+)c` | 26,232 | **92**, all partial: `abab` gives group 1 (0, 4) against unset |
| `(a+)+\1` to `(a+)\1` | 4,095 | **144**: `aaa` gives (0, 3) captures `['a', 'a']` against (0, 2) |
| `(a+?)+` to `(a+?)` (lazy inner) | 18,928 | **2,043**: each iteration is one character |
| `(a*)+` to `(a*)` (empty body) | 26,232 | **12,874**: an extra empty capture |
| `(a{1,3})+` to `(a{1,3})` (bounded inner) | 26,232 | **201** |
| `(a+\|b)+`, `(?:a+b?)+` | 26,232 | **6,426**, **996** |

So the safe rule, on this evidence: a greedy single-character repeat with no maximum, alone in the
body of a repeat with no maximum (greedy or lazy), in a pattern with no backreference to that group.
Fuzzy, atomic, possessive and verb contexts showed no difference on small grids, which is not a
proof; exclude them as the memo does. Grids are evidence, not a proof; the argument is that a greedy
`x+` already takes every position the outer repeat could split it at, in the same order, so the
outer repeat only ever completes one iteration.

**Cost on paper.** Compile time only. But the result, `(a+)c`, is still quadratic per search in
steps: each attempt counts the run and tries the tail at every stop (Python `(a+)c`: 0.19, 2.61,
41.2 ms at n = 400, 1,600, 6,400). S2d plus G2 is better: linear in steps, quadratic only in the
character scan.

**Recommendation.** Do not build now. S2d and G2 reach the same shapes with fewer conditions. Keep
the grid as evidence if a workload needs the rewrite where the memo is off (a backreference to another
group, say).

### N: memo for repeats inside a memo repeat

**What.** Give a repeat Q at depth 1 or more a failure memo when every repeat around it is a memo
repeat whose body cannot match empty. **Soundness sketch.** What follows Q's body is the rest of R's
body, then `END_GREEDY_REPEAT` of R, which reads R's count (irrelevant: no maximum, minimum 0) and
R's start through "did the iteration move" (`Matcher.cs:13874`). When R's body has a minimum width
above zero, every iteration that reaches the end has moved, so the continuation depends on the
position only. When it can match empty, it does not: an iteration that started at p and one that
started earlier behave differently at p. Q's lists are shared by every iteration of R (one repeat
index), which is what makes it pay. **Cost**: Option A and B's per repeat, O(L) failures per memo
repeat per pass and the inherited O(L²) span-insert ceiling (`GuardList.cs:122`). **Gain**:
`(?:(?:a|a)+)+$` goes from exponential to about linear; `(x+x+)+y`'s attempt 0 loses its inner walks.
**Adversarial input**: `(?:(?:a|aa)+b?)+c` over long runs of `a`, many inner failure records.
**Witnesses**: a grid that deletes the minimum-width condition must find a difference
(candidate: `(?:(?:a|a)*b?)+c`), or the condition is documented as unwitnessed. **Breaks**: the
oracle's `alt-nested` timeout row (`tools/record-oracle.py:6644`) and its OracleWaveTests wave;
the AOT probe's informational rows `(a+)+$` and `(x+x+)+y` (`tools/probes/aot-smoke-slow-patterns.cs:64-69`).
Medium risk, medium value.

### K: a bounded repeat keyed on (position, count)

**What.** A repeat with a maximum has no memo, because whether another iteration is allowed depends
on the count. Key the failure on (p, c) instead. Cheaper special case first: from position p at most
R(p) = (L - p) / w more iterations fit (w = the body's minimum width, above 0), so every count c with
min ≤ c ≤ max - R(p) behaves as if there were no maximum and can share one key "unbounded". For
`(a|aa){1,99}$` over 32 `a`s and `b`, 99 exceeds anything that fits, so every count shares one key and
the search is linear.

**Cost on paper.** Keys per position: the counts below the minimum, plus the counts within R(p) of
the maximum, plus one: O(min + L/w). Worst case O(L × min(max, L/w)) states, O(L²/w) for
`(a|a){1,1000000}$`-style patterns, held in a bit set or hash set that must be pooled (warm `IsMatch`
allocates 0 bytes, `AllocationTests`), with a cap past which the memo switches itself off, as the
repeat memo already does (`Matcher.cs:9234-9235`, `_repeatMemoCap`). **Soundness**: after the body the
count is c + 1 and the continuation reads only the count and the position (Option A's argument with
the count added to the key). **Adversarial input**: `(a|aa){500,1000}$` over 2,000 `a`s: every count
below 500 is its own key, 2,000 × 500 states. **Witness that must stay green**:
`^(?:a|ab|b){0,3}c` over `abbbc` (0, 5) (`FailureMemoTests.cs:294-295`).

**Breaks.** `(a|aa){1,99}$`: `samples/FuzzyRegex.AotSmoke/Program.cs:495`,
`tools/probes/aot-smoke-slow-patterns.cs:67`, `Gaps/Demo/DemoExamplesTests.cs:111`,
`demo/.../wwwroot/examples.json:124`. `(a|a){1,999}$` and the six other bounded rows of
`TIMEOUT_SHAPES` (`tools/record-oracle.py:6638-6649`) and `OracleWaveTests.cs:2441-2457`.

### BR: backreferences, keyed on the referenced groups' spans

**What.** Today one backreference anywhere switches every memo off (`Optimiser.cs:712-717`). Add the
spans of the referenced groups at body entry to the key (the idea is Davis et al. 2021, "Using
selective memoization to defeat ReDoS", cited in the handover). **Cost on paper (my derivation, not
from the paper)**: with k referenced groups, each span has O(L²) values, so up to O(L^(1+2k)) keys;
polynomial, not linear, and a hash set with a cap is needed. In practice a group inside the repeat
holds the last iteration's span, which the entry position almost fixes: for `(a|a)*\1b` at most two
spans per position, so linear. **Adversarial input**: `(a*)(?:a|a)+\1c`, where group 1 is set before
the repeat to any of O(L) spans, so O(L²) keys per attempt. **Witnesses that must stay green**:
`a(?:.|(a)){1,}\1c` over `aabac` (0, 5) (`FailureMemoTests.cs:255-256`), and the Option B build's
`(b*)(?:(a))+\1c` over `abaac` (2, 5) and over `baacb` (1, 4).

**Breaks.** Every `(a|a)*\1...` test: `EngineWorkTests.cs:21`, `Docs/ComparisonSamples.cs:157`,
`Gaps/Api/LazyEnumerationTests.cs:33`, `Gaps/Api/PoolDisciplineTests.cs:245, 323`,
`Gaps/Api/TimeoutAndCancellationTests.cs:52`, `Gaps/Demo/DemoEngineContractTests.cs:330, 387, 888`,
`Gaps/Engine/OptimiserTrapsTests.cs:297`, `Gaps/Engine/RepeatTests.cs:39`,
`OracleWaveTests.cs:2322, 2358`, the demo's `checks.html:270` and `harness.html:159`, and the
oracle's `alt-backref` row (`tools/record-oracle.py:6648`).

**After the test seam (2026-10-15).** The C# tests above are off the `\1` shapes: onto the seam, or
onto `(?=(a|a)+$)a` where the seam cannot reach. What still breaks under BR, all black-box and left
for the BR slice:

- `demo/FuzzyRegex.Demo.Wasm/wwwroot/checks.html:270` (`(a|a)*\1\b\B`, the timeout check; its
  comment at 244-249 explains the `\1`). User-facing demo copy, so it needs the owner's review.
- `demo/FuzzyRegex.Demo.Wasm/wwwroot/harness.html:159` (the same pattern; comment at 138).
- `tools/record-oracle.py:6648`, `TIMEOUT_SHAPES` row `alt-backref`, `(a|a)+(\1)$`. Under BR the
  port answers where upstream times out, which the timeout generator counts as a divergence; drop
  the row or move it to the answer generators.
- `docs/COMPARISON.md:551` (`(a|a)*\1\b\B`, the per-call timeout example) and its pin
  `tests/FuzzyRegex.Tests/Docs/ComparisonSamples.cs:157`, which must match the doc and so moves with
  it; `tools/check-doc-examples.ps1` runs the doc's copy. Doc copy, so it needs the owner's review.

Checked and not broken by BR (no backreference): the AOT smoke app's `(a|aa){1,99}$`
(`samples/FuzzyRegex.AotSmoke/Program.cs:495`) and `examples.json:124` `^(a|aa){1,99}$`, both of
which K would break instead; `demo/web/tests/pool.test.ts:40`, which runs against a fake worker.

### CK: group-exists conditionals, keyed on which tested groups are set

**What.** Add to the key one bit per group a conditional tests (set or unset). **Cost**: at most
L × 2^g keys for g tested groups; g is small in practice and a cap covers the rest. The empty-iteration
memo already keys on tested groups' spans (`RepeatMemoKey`, `GuardList.cs:506-514`), which is the
structure to reuse. **Soundness**: a group-exists test reads only whether the group is set; with no
backreference (else BR applies too) nothing else reads groups. **Adversarial input**:
`(?:(a)|(b)|(c)|a)+(?(1)x|y)(?(2)x|y)(?(3)x|y)`, 8 keys per position. **Witnesses that must stay
green**: `(?:(a)|b)+(?(1)c|b)` over `abbb` (1, 4) and `abbbb` (1, 5) (Option B note, A1/A2), and
`(?:(a)|a)+(?(1)c|b)` over `aab` (`PatternObject.cs:78-80`). **Breaks**: none of the slow-shape tests
(none uses a conditional). Low demand: no gate row has a conditional.

## Making the slow-shape tests independent of engine speed

**Amendment 1: a separate follow-up, not on this branch.** The review found that the seam below
does not work as first written. `InitMatch` resets `Iterations` (`MatchState.cs:1966`), so priming
it beforehand is lost; calls refused before `BasicMatch` (the minimum-width check near
`Matcher.cs:19463`, the prefilters) never poll at all; and `LazyEnumerationTests` needs a budget that
two matches fit inside before the third times out. The redesign: a test-only limit that **expires on
the k-th `SafeCheckCancel`**, used only with patterns that reach `start_match`, with k chosen per
test (1 for most, enough for two matches in `LazyEnumerationTests`). The first version is kept below
for the record.

**Built (2026-10-15, `perf/test-seam`).** The limit is `FuzzyRegex.ExpiresOnCheck`, an internal
`init`-only integer on the instance (0, off, by default), carried through `MatchLimits` into
`MatchState.ExpiresOnCheck` with a per-operation count, `MatchState.ClockChecks`. It is read in
`MatchState.CheckTimedOut` after the no-budget return and before the `Stopwatch` read, so it costs
nothing on a call with no budget and one integer test per clock read on a call with one, and the
clock is read only behind the polling gates (once per 256 loop turns). It sits on the instance, not
in a static, because the thread-safety tests forbid writable statics and pin the thread-statics to
the Debug work counters, and because tests run in parallel. Two consequences of that choice:

- The seam fires only in a call that carries a budget, so a seam test also proves the budget
  arrived (the per-method test pairs each throwing call with an unbudgeted one that answers).
- It cannot reach a pattern built where the test cannot set it: the static conveniences (built in
  the pattern cache), `DemoEngine.Run` and the oracle's row runner. Those keep a real slow pattern,
  `(?=(a|a)+$)a` (Debug 33, 118, 441, 1,643 and 6,412 ms at n = 14 to 22, 2^n, 2026-10-15),
  each marked `SHORTCUT:`.

Measured k, by stepping it up from 1: `ab` over `ababab` makes two clock checks for two matches and
four for all three, so k = 3 is "two fit, the third does not". The per-method tests use `a+` over
`baab` with k = 1. Red proofs (`.scratch/seam/redproof/summary*.txt`): with the engine never
reporting expiry, with the per-call budget dropped, with the pattern budget dropped, with the seam
moved ahead of the no-budget test, and with the match collection draining its walk on the first
read, each converted test that checks that piece went red. The follow-up for BR, which this slice
leaves alone, is the black-box list below the "Breaks" paragraph of BR.

The handover's trap (`.claude/driver/optimisation-handover-2026-09-24.md:229-237`) is that about
fifteen tests need a pattern that is slow, and every memo extension makes some of them fast. Two
groups need different fixes.

**Tests that check the timeout and cancellation plumbing** (the exception, the pool, lazy
enumeration, thread safety, the per-call timeout): they need a match that *reaches a timeout check
after its deadline*, not a slow pattern. Proposal: an internal test seam on `MatchLimits`
(`MatchLimits.cs:17-40`), "already expired", that sets the deadline in the past and primes
`Iterations` to `0xFF00` so the first step opens the gate, the trick `CountFork` already uses
(`Matcher.cs:8004`). Any pattern that takes one step then times out on every machine, at any engine
speed, with no flakiness under load. The tests reach it through the existing `InternalsVisibleTo`
(as `EngineWork` and `WorkCounter` do). Files: `EngineWorkTests`, `LazyEnumerationTests`,
`PoolDisciplineTests`, `TimeoutAndCancellationTests`, `ThreadSafetyTests` (named by the handover; no
slow pattern found there by grep today), `OptimiserTrapsTests` (the timeout test), `RepeatTests`,
`ComparisonSamples`, and the C# half of `DemoEngineContractTests`. One test per public API keeps a
real slow pattern, so the real clock path stays covered.

**Black-box callers that cannot reach internals** (the AOT smoke app, the wasm demo's `checks.html`,
`harness.html` and `examples.json`, the oracle's `TIMEOUT_SHAPES`, the AOT probe): use a shape that
no planned option reaches, a repeat inside a lookahead, `(?=(a|a)+$)a` (upstream 1.2, 4.7, 18.7,
77.7 ms at n = 12, 14, 16, 18: 2^n). It is already the oracle's `alt-same-lookahead` row. Mark each
use `SHORTCUT:` naming the ceiling (Option C of the 2026-09-26 design, depth-tagged failures, would
make it fast) and the upgrade (an engine-side step budget exposed through an option, if C is ever
built). The demo's example text needs the owner's copy review, since it is user-facing.

Do this before K, BR or N, as its own slice. S2d, G2 and O8 need none of it.

## 3. Recommended order and expected gains

| Order | Option | Changes the class of | Breaks slow-shape tests | Risk |
|---|---|---|---|---|
| 1 | S2d | `(x+x+)+y` cubic to quadratic; `(a+)+c` later attempts to O(1) | none | low (one lookup, a short argument) |
| 2 | G2 | attempt 0 of every nested shape: lookups O(n²) to O(n log n) | none | low (answer-identical loop change) |
| 3 | test seam (k-th poll, see amendment 1) | nothing; unblocks 5-7 | (rewrites them) | low |
| 4 | O8 | `x+T` failing searches: steps O(L²) to O(L log L) | none | low to medium |
| 5 | N | `(?:(?:a\|a)+)+$` exponential to near-linear | oracle `alt-nested` | medium |
| 6 | K | bounded repeats exponential to polynomial | `{1,99}`, `{1,999}` users | medium |
| 7 | BR, then CK | backreference and conditional patterns | `\1` users | medium to high |
| - | S2a, C | (parked; see their sections) | none | - |

**Expected gains, estimates** (to be replaced by experiment E1/E2 numbers):

| Row | Python 2026.9.10 | Ours with B (measured 2026-10-12) | S2d (estimate) | S2d + G2 (estimate) |
|---|---|---|---|---|
| `(a+)+c`, n = 400 | 100.06 ms | 9.1 ms | 2.5 to 4 ms | 0.5 to 1.5 ms |
| `(a+)+c`, n = 200 | 15.84 ms | 2.2 ms | 0.6 to 1 ms | 0.15 to 0.4 ms |
| `(\w+\s?)+$`, 400 words | 312.11 ms | 1.30 ms | 0.4 to 0.8 ms | about the same |
| `(x+x+)+y`, n = 320 | 164.4 ms | not measured | quadratic | quadratic only in the character scan |

How the `(a+)+c` estimates are made: with B the later attempts cost about 1.25n² steps against
attempt 0's n²/2 lookups and n²/2 scanned characters, and a step costs more than a lookup, so S2d
removes roughly 60 to 75% of the time. G2 then removes attempt 0's lookups, leaving about n²/2
`MatchesMany` calls in `CountOne` (80,000 at n = 400, a few nanoseconds each). If the character scan
then dominates, vectorising `CountOne` for a plain character or range (`IndexOfAnyExcept`, as
`Matcher.cs:12653` already does) is the next step, and it helps every `x+`.

## 4. Experiments for when the machine is free (seconds each)

Both probes are written and not yet run: `.claude/worktrees/c5-nested/.scratch/c5/gc.cs` (Debug, reads `WorkCounter` by
reflection) and `.claude/worktrees/c5-nested/.scratch/c5/time.cs` (Release, P-cores, best of five). Both build the c5 worktree's
`src/FuzzyRegex`, which is main at a3d79986.

- **E1, the trace.** `dotnet run .claude/worktrees/c5-nested/.scratch/c5/gc.cs`. For `(a+)+c`, `(x+x+)+y`, `(\w+\s?)+$` and
  `(?:(?:a|a)+)+$`, it prints lookups and steps for the whole search and for attempt 0 alone (the
  pattern behind `\A`), and the later-attempt cost per start. Expected if section 1 is right:
  `(a+)+c` attempt 0 about n²/2 lookups, later starts about 2.5n steps each; `(x+x+)+y` later starts
  growing as n²; `(?:(?:a|a)+)+$` attempt 0 doubling per character.
- **E2, the times.** `dotnet run -c Release .claude/worktrees/c5-nested/.scratch/c5/time.cs`. Fills the "Ours with B" column,
  including `(x+x+)+y` and `a+c` at 400 to 6,400 for the character-scan baseline.
- **E3, S2d prototype.** In this worktree: the field, the lookup at the attempt start, E1 again.
  Expected: later-start lookups constant, about 1 per start.
- **E4, G2 prototype.** The span skip at `Matcher.cs:14947-14975`, E1 again. Expected: attempt 0
  lookups for `(a+)+c` close to n.
- **E5, differential.** Port probe (`git show 8dda4d9:tools/probes/port-probe.cs`) on Release builds
  of main and the branch, 40,000 rows biased to patterns that start with a nested repeat (empty
  bodies, lazy R, `{2,}`, reverse, `Matches` with empty matches, overlapped, partial). Zero
  differences required.

Delete `.claude/worktrees/c5-nested/.scratch/c5/` afterwards.

## Evidence (regex 2026.9.10, CPython 3.14, 2026-10-13)

Collapse grids: `.claude/worktrees/c5-nested/.scratch/c5/c5probe.py`, `c5probe2.py` and `c5probe3.py` (under a second each);
the counts are in option C's table. Sample output:

```
- (a+)+ vs (a+) rows 18928 diffs 0
(?r) (a+)+ vs (a+) rows 18928 diffs 0
(?p) (a+)+ vs (a+) rows 18928 diffs 0
- (a+?)+ vs (a+?) rows 18928 diffs 2043 ('', 'aa', 0, ((0, 2), [(0, 1), (1, 2)], ['a', 'a'], 1, False), ((0, 1), [(0, 1)], ['a'], 1, False))
(a+)+\1 vs (a+)\1 rows 4095 diffs 144 ('aaa', 0, ((0, 3), [(0, 1), (1, 2)], ['a', 'a'], ...), ((0, 2), [(0, 1)], ['a'], ...))
((?:ab)+)+ vs ((?:ab)+) rows 26232 diffs 92 ('c', 'abab', 2, ((0, 4), [[(0, 4)]], 1, True), ((0, 4), [[]], None, True))
```

Timings, one `search` each (best of three for the first six lines; the rest condensed from single
runs):

```
400 a+c=0.178ms (a+)c=0.193ms
1600 a+c=2.543ms (a+)c=2.614ms
6400 a+c=40.370ms (a+)c=41.228ms
100 (?:a+)+c=2.224ms (a+)+c=2.826ms
200 (?:a+)+c=12.670ms (a+)+c=16.085ms
400 (?:a+)+c=89.510ms (a+)+c=100.310ms
40 (x+x+)+y=0.710ms   80 (x+x+)+y=4.093ms   160 25.9ms   320 164.4ms
(?:(?:a|a)+)+$  14 37.1ms  16 177.7ms  18 820.6ms
(a|aa){1,99}$   n=12:0.1ms n=14:0.3ms n=16:0.8ms n=18:2.2ms
(a|a){1,999}$   n=12:1.1ms n=14:4.5ms n=16:17.8ms n=18:74.4ms
(a|a)*\1b       n=12:1.6ms n=14:6.4ms n=16:25.8ms n=18:104.3ms
(?:(a)|a)+(?(1)c|d) n=12:1.4ms n=14:5.8ms n=16:23.5ms n=18:93.0ms
(?=(a|a)+$)a    n=12:1.2ms n=14:4.7ms n=16:18.7ms n=18:77.7ms
```

Witness answers:

```
'(a+)+c' 'aaabaac' (4, 7) ['aa']
'(a*)+c' 'aabc' (3, 4) ['', '']
'(?:a?b?)+c' 'abxabc' (3, 6)
'(a+)+?c' 'aaxaac' (3, 6) ['aa']
'(a+){2,}c' 'aaxaaac' (3, 7) ['aa', 'a']
'(?r)c(a+)+' 'xcaaba' (1, 4) ['aa']
'(x+x+)+y' 'xxxzxxxy' (4, 8) ['xxx']
'(\w+\s?)+$' 'ab cd!ef gh' (6, 11) ['ef ', 'gh']
finditer (a*)+ over 'aab': [(0, 2), (2, 2), (3, 3)]; (a*)+(?=b): [(0, 2), (2, 2)]
overlapped (?:a+)+c over 'aacaac': [(0, 3), (1, 3), (3, 6), (4, 6)]
```

`regex.compile('(?:(?:a|a)+)+$', regex.DEBUG)` shows the branch is kept (`GREEDY_REPEAT 1 INF /
GREEDY_REPEAT 1 INF / BRANCH CHARACTER 'a' OR CHARACTER 'a'`), so that row is not reduced to `a+`.

## Amendment 1: what was written (2026-10-13, after the blind review)

S2d and G2 as described above, with the review's fixes.

- `NodeCompiler.BuildRepeat`: records the one copy's start in `RepeatInfo.FirstIterationStart`.
- `Optimiser.FindFirstIterationAtStart` (after `KeepFailureMemosSound`): sets
  `PatternObject.FirstIterationAtStart` when that start, looked through one-way branches, is the
  pattern's start node and is not R itself (an empty copy). **The start node is compared after
  stepping over any one-character test with a step of 0**: the compiler hoists one in front of a
  leading item, so `(a+)+c` starts `CHARACTER(step 0) - START_GROUP - ...` (the remarks on
  `Optimiser.FindAnchorGuards`). The first build compared the raw start node, so S2d never fired and
  both step bounds stayed red; a reflection probe showed the hoisted `Character` in front of the
  copy's `StartGroup`. Stepping over it is sound: the test reads the character at the start without
  moving, so an attempt that passes it still enters the copy at its start.
- The two new fields are allowlisted in `ThreadSafetyTests` (compile-time writes only), as
  `RepeatInfo.FailureMemo` and `KeepsFailureMemosAcrossAttempts` are; they are written by optimiser
  passes after construction, so they cannot be `readonly`.
- `Matcher.BasicMatch`: the guarded-start skip after `SearchStart` and at `next_match_2`, by
  `goto backtrack`.
- `GuardList.TryGetSpan`, `MatchState.IsRepeatGuarded(..., out GuardSpan)`, and the clamped jump in
  the greedy forward loop.
- Tests in `FailureMemoAcrossAttemptsTests`: two step-ratio bounds for S2d (`(a+)+c`, `(x+x+)+y`,
  steps at a run of 400 against 200 under 2.6), one lookup-ratio bound for G2 (`\A(a+)+c`), and the
  upstream answer rows (with `pos`, empty first iteration, `{2,}`, lazy R, reverse, `\w`, emoji,
  the leading-repeat interplay `(?:a*b)+c`, `Matches` with empty matches, overlapped, partial).
- `.claude/worktrees/c5-nested/.scratch/c5/gates.sh`: one locked run of the Debug build, the C5 tests
  with each part toggled off (the bounds must go red, the answers stay green), the E1 and E2 probes
  before and after, the ratchet, a differential of 110,000 rows against a3d79986 (60,000 biased to
  patterns that start with a nested repeat), the oracle at seeds 7, 4242 and 20260927, and pinned
  BenchmarkDotNet Workload against the base.

### Measured (2026-10-14, gates.sh)

- Debug steps for the whole search at n = 400 (E1, `.scratch/c5/gc-*.txt`): `(a+)+c` 407,784 to
  7,188; later starts of `(x+x+)+y` 584 to 12 steps each at n = 100; `(\w+\s?)+$` 19 to 0 per later
  start. `(?:(?:a|a)+)+$` unchanged (attempt 0, as predicted).
- BenchmarkDotNet `--job medium`, pinned, against a3d79986: PathologicalNestedRepeat 0.07x,
  PathologicalWordsToEnd 0.37x, PathologicalPrefixBranches 0.53x, PathologicalSameBranches 0.58x,
  nothing slower beyond the noise floor.
- Differential: 105,002 rows identical to a3d79986. The single-process Stopwatch probe (`time.cs`)
  was too noisy to quote (non-monotonic in n); BenchmarkDotNet is the record.

## Could not verify

- Every engine cost in section 1 is from reading the code. E1 checks it.
- `ReduceLoops`' exact condition: the .NET source was read on 2026-10-11 and deleted; only the notes
  remain. Option C does not depend on it.
- That S2d's lookup costs a pattern without the field nothing: settled only by the benchmark rows.
