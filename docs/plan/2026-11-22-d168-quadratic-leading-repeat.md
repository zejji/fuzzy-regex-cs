# D168: `[ab]*\d` is quadratic over a run with no digit (2026-11-22)

Point-in-time record for item 16 of phase 11. Status lives in `STATE.md`; the defect row is D168 in
`docs/KNOWN-DEFECTS.md`.

## The counts

Debug build of `fix/d167-charged-walks` (e331a095), `WorkCounter` read by reflection from a probe
outside the repository, `IsMatch` over `"ab"` repeated to L characters. Counts are deterministic;
the times beside them are wall clock on a busy machine and indicative only.

| Pattern | L | Steps | Characters walked | Guard checks | Attempts started | ms (indicative) |
|---|---|---|---|---|---|---|
| `[ab]*\d` | 1,000 | 4,000 | 1,003,001 | 2,000 | 1,001 | 107 |
| `[ab]*\d` | 10,000 | 40,000 | 100,030,001 | 20,000 | 10,001 | 7,730 |
| `[ab]*\d` | 30,000 | 120,000 | 900,090,001 | 60,000 | 30,001 | 71,266 |
| `[ab]*+\d` | 1,000 / 10,000 / 30,000 | 6,000 / 60,000 / 180,000 | 501,501 / 50,015,001 / 450,045,001 | | 1,001 / 10,001 / 30,001 | |
| `[ab]+\d` | 1,000 / 10,000 / 30,000 | 3,999 / 39,999 / 119,999 | 1,002,000 / 100,020,000 / 900,060,000 | | 1,001 / 10,001 / 30,001 | |

Steps and guard checks grow linearly; the characters walked grow as L squared. One attempt per
start position, and each walks the rest of the run twice (`[ab]*\d`) or once (the possessive form,
which has no retreat). So the cost is not the backtracking loop's turns but the walks inside two of
its arms.

## The mechanism

Line numbers are on `fix/d167-charged-walks`.

1. **Every start walks the rest of the run forward.** `AdvanceGreedyRepeatOne`
   (`src/FuzzyRegex/Engine/Matcher.cs:25339`) calls `CountOne` (`Matcher.cs:1191`, its loop at
   `:1263`), which takes `[ab]` from the start to the end of the run: L - i characters for start i.
2. **Then walks it back.** The digit fails at the run's end, and `BacktrackGreedyRepeatOne`'s
   retreat (`Matcher.cs:25670`) steps back one character at a time and tries the tail at each,
   another L - i characters. Repeat guards do not carry this from one start to the next:
   `BacktrackFailure` resets them before every attempt (`Matcher.cs:26359`, upstream's
   `reset_guards` at `_regex.c:15745`).
3. **The cure exists and is switched off.** Upstream's FAILURE arm already skips a failed
   attempt's leading run (`_regex.c:15705-15724`, ported at `Matcher.cs:26293`): it counts the
   characters the leading single-character repeat matched and starts the next attempt past them.
   It tests `start_node` only. A pattern whose start is not "simple" gets a first-set check compiled
   in front of it (`regex/_main.py:643-649`; a repeat's `has_simple_start` is `False`,
   `_regex_core.py:1989`). That check is a zero-width one-character test
   (`_compile_firstset`, `_regex_core.py:370`, built with `zerowidth=True` at `:406`, the only
   producer of a `ZEROWIDTH_OP` node), so `start_node` is the check and the skip never fires. The
   port's `FindLeadingRepeat` (`src/FuzzyRegex/Engine/PatternObject.cs:2057`) does the same: compiled
   `[ab]*\d` is `SetUnion(step 0) -> GreedyRepeatOne -> Property -> Success` and `LeadingRepeat` is
   null.

The skip only fires today when no first set is built: the pattern can match empty, the first set
holds "any character", or a member is refused (a negated set). The controls prove the mechanism in
upstream itself, regex 2026.9.29, 2026-11-22, `search` over `"ab"` repeated (indicative times):

| Pattern | First-set check | 2,000 chars | 8,000 chars |
|---|---|---|---|
| `[ab]*\d` | yes | 27.12 ms | 462.35 ms |
| `[^\d]*\d` | no (port's compiled start: `GreedyRepeatOne`) | 0.04 ms | 0.13 ms |
| `(?s).*\d` | no (first set is "any") | 0.05 ms | 0.09 ms |

## How .NET avoids it

Read 2026-11-22 from dotnet/runtime `main`:

- `RegexNode.FinalOptimize` (https://raw.githubusercontent.com/dotnet/runtime/main/src/libraries/System.Text.RegularExpressions/src/System/Text/RegularExpressions/RegexNode.cs)
  inserts an `UpdateBumpalong` node right after a pattern's leading unbounded single-character
  loop: "If an expression is guaranteed to begin with a single-character unbounded loop that isn't
  part of an alternation ... or a capture (in which case a back reference could be influenced by its
  length)". It takes `Setloop` and `Setloopatomic` (and the one/notone kinds) when
  `node.N == int.MaxValue`, `Setlazy` only when not atomic by ancestry, and only left to right
  ("to avoid needing additional code for the much rarer RTL case"). The loop's minimum does not
  matter, so `[ab]+\d` is covered.
- `RegexInterpreter` (https://raw.githubusercontent.com/dotnet/runtime/main/src/libraries/System.Text.RegularExpressions/src/System/Text/RegularExpressions/RegexInterpreter.cs)
  runs `UpdateBumpalong` by raising the starting position saved at the bottom of the backtracking
  stack to the current position, "if it's greater". When the attempt fails, the scan loop resumes
  from there and adds one, so the next attempt starts past the run.
- Auto-atomicity (`FindAndMakeLoopsAtomic`, `CanBeMadeAtomic`, same file: "A*B (where sets A and B
  don't overlap) => (?>A*)B") is a separate optimisation. It removes the retreat, not the forward
  walk: this port's possessive `[ab]*+\d` still walks 450,045,001 characters at 30,000, half of
  the greedy form's.

So .NET's linear time comes from the bump-along, which is the same idea as upstream's skip, applied
to the tree after its own optimisations rather than to the first compiled node.

## The candidate in the row

"A failed attempt at a greedy leading repeat over a set disjoint from what follows could restart
past the repeat's run." It holds, and the disjointness is not needed. An attempt from start i takes
the run to its end e and tries the rest of the pattern from every end position in [i + min, e]. An
attempt from any j in (i, e] can only try end positions in [j + min, e], a subset, and nothing
before the repeat can make the rest of the pattern behave differently there: the repeat captures
nothing (or its group is unreferenced), lookbehinds read the same text whatever the start, and the
search anchor is fixed for the call. So every such attempt fails the same way, and the next useful
start is e + 1. This is upstream's own argument for its skip and .NET's for `UpdateBumpalong`;
neither asks for disjointness. Disjointness is what auto-atomicity needs, and that only halves the
walk (the `[ab]*+\d` row).

Proved by counts with option A below built on `perf/d168-skip-through-firstset`, same probe:

| Pattern | L = 1,000 | 10,000 | 30,000 | Attempts |
|---|---|---|---|---|
| `[ab]*\d` | 3,003 | 30,003 | 90,003 | 2 |
| `([ab]*)\d` | 3,003 | 30,003 | 90,003 | 2 |
| `[ab]+\d` (not covered by A) | 1,002,000 | 100,020,000 | 900,060,000 | L + 1 |
| `[ab]*?\d` (not covered by A) | 502,501 | 50,025,001 | 450,075,001 | L + 1 |
| `[ab]*+\d` (not covered by A) | 501,501 | 50,015,001 | 450,045,001 | L + 1 |

## Options

The skip's cost, for every option: one extra `CountOne` over the run after a failed attempt, and the
next attempt starts past it. Runs do not overlap once skipped, so the extra walking over a whole
search is at most the subject's length plus one per attempt. When the repeat's maximum stops the
count (a bounded repeat over a long run) the skip does not fire and the extra walk is at most the
maximum, which the attempt itself has already walked; upstream pays the same for a bare repeat.

**A. Look through the first-set check (built, not merged).** In `FindLeadingRepeat`, a zero-width
one-character node at the start (`Step == 0` and `NodeQueries.MatchesOneCharacter`, the test
upstream's `locate_test_start` uses at `_regex.c:3511`) is stepped over before the existing
capture-group look-through, under the same guards (not fuzzy, not reversed, no group call, no
referenced group), and only for an unbounded repeat, as .NET does. The check consumes nothing and
passes wherever the repeat can take a character, so it changes no attempt the argument covers.
About fifteen lines plus a comment. Adversarial inputs, characters walked at L = 30,000, before
and after:

| Input | Before | After |
|---|---|---|
| `[ab]*\d` over `"ab"` x n | 900,090,001 | 90,003 |
| `[ab]*\d` over `"ab "` x n (runs of two) | 90,001 | 70,001 |
| `[ab]{0,1000}\d` over `"ab"` x n, if the bounded repeat were looked through too | 59,032,001 | 87,035,002 |

The last row is why A stops at unbounded repeats: a bounded run that reaches the maximum gets no
skip, so the skip's count is a third walk on top of the attempt's two. With the guard the bounded
case is unchanged (`[ab]{0,100}\d`, L = 10,000: 2,000,201 before and after), and
`OptimiserTrapsTests.A_bounded_leading_repeat_behind_the_first_set_check_gets_no_skip` goes red
when the guard is removed (mutation run 2026-11-22).

**B. Look through an unrolled minimum.** Upstream's `build_REPEAT` compiles the minimum as separate
copies in front of the repeat (`_regex.c:25173-25200`), so `[ab]+\d` starts
`check -> [ab] -> [ab]*`. Step over k one-character nodes equal to the repeat's body, unbounded
repeats only (the skip counts against the repeat's own maximum, which the unrolling reduced). Needs
a node-equality test, since the copies are separate nodes. Same cost as A. Probably the common shape
(`\w+@`, `[a-z]+\d`).

**C. Lazy leading repeats behind the check.** `[ab]*?\d` tries end positions upward, so a failed
attempt has tried them all, unless a verb (`(*SKIP)`, `(*PRUNE)`) or a partial run-out stopped it
short (`FindLeadingRepeat`'s remarks; D77). Allow it when the pattern has no backtracking verb and
the call is not partial. The partial condition is per call, so it belongs in `BacktrackFailure`, not
in the compiled pattern.

**D. Atomic and possessive leading repeats.** `[ab]*+\d` and `(?>[ab]*)\d` compile to
`check -> ATOMIC -> GreedyRepeatOne -> END_ATOMIC`. Every attempt inside the run tries the rest only
at the run's end, the same position, so looking through an atomic group whose body is exactly the
repeat is sound.

**E. Auto-atomic leading repeat when the follow set is disjoint** (the row's candidate as worded,
.NET's `FindAndMakeLoopsAtomic`). Halves the walks and leaves the growth quadratic. It also has to
judge what follows through fuzzy sections (a substitution lets `\d{e<=1}` take `a`), lookarounds and
zero-width items. A constant-factor change with real correctness risk: not a fix for D168, and a
separate measured optimisation if ever.

**F. Keep tail guards across attempts.** A tail failure at position p for a leading repeat holds for
every later start, so the retreat could stop early. The forward `CountOne` walk is still per
attempt, so the growth stays quadratic. Not recommended.

## Correctness risks, per construct

- **Fuzzy sections, `(?b)`, `(?e)`.** A, B and D keep the `!IsFuzzy` guard of the capture-group
  look-through. Upstream skips for a bare repeat in a fuzzy pattern, and a fuzzy pattern usually has
  no first set anyway (`[ab]*\d{e<=1}` compiles with no check), so nothing changes there.
- **Captures.** Only unreferenced groups are looked through (S60b's argument; a referenced group's
  text or start could change the rest of the pattern).
- **Lookbehind in the rest of the pattern.** Reads the same text from every start; not affected.
- **Partial matching.** For a greedy repeat the failed attempt tried every end; an attempt that ran
  out answers a partial and never reaches the skip. A widens which patterns take the existing skip,
  so the gates must include the partial grids and the oracle's partial rows. C needs its own
  argument (above).
- **Reverse.** Excluded by the guard. A reverse pattern such as `(?r)\d[ab]*` already compiles with
  no first-set check and is linear before and after (6,003 characters walked at L = 1,000).
- **Verbs and `\K`.** Unchanged: greedy keeps the skip after `(*SKIP)` and `(*PRUNE)` (D77), and the
  next start counts from `AttemptStart` (D92).
- **Work budget and timeouts.** The skip's `CountOne` is charged through `ChargeTurns` on the D167
  branch, so the extra walk is polled like the rest.

## Recommendation

Take A now: it is upstream's own skip, made to see through a node upstream itself steps over in
`locate_test_start`, and it fixes the registered shape from quadratic to linear in characters walked
(900,090,001 to 90,003 at L = 30,000). It is built test-first on `perf/d168-skip-through-firstset`
(f9a29fbf, on top of the D167 branch; new `OptimiserTrapsTests` rows red without it; ratchet
GREEN 10,452). Before merge: oracle at three seeds,
a differential against main over generated rows biased to leading single-character repeats
(greedy, lazy, possessive, grouped, partial, with verbs), a WPR profile, an allocation check, and
the `docs/plan/SYNC-DIVERGENCE.md` row for `FindLeadingRepeat` extended to name the first-set
look-through.
Then B and D, which reuse the same skip and close the common `+` and possessive shapes; C after its
partial argument has a witness. E and F are not fixes for D168.
