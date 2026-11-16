# D116: a BESTMATCH walk with a lookbehind - design note (2026-11-16)

Point-in-time record for phase 11 item 15 (the D116 row of `docs/KNOWN-DEFECTS.md`). The checklist
lives in `STATE.md`. No code was changed; every number below comes from a run quoted beside it.

**Recommendation: option E.** Withhold the D115 memo only when a lookaround that reads against the
scan holds a fuzzy section or a group call in its body, instead of for every such lookaround. It is
a compile-time change of a few lines, costs nothing at match time, makes the witness linear (Debug
steps 7,749,390 to 171,106 at 16,000 characters, answers unchanged), and covers 44 of the 49 corpus
patterns that hold such a lookaround. Confidence: high on soundness for the cases measured, medium
on completeness until a builder lists and asserts every place an attempt reads the search anchor
(VERIFICATION rule 10). Options A (a reach window for fuzzy lookbehind bodies) and C/D (leave the
rest quadratic) are what remains after E; A is not worth building for the 2 corpus patterns it
would add.

## 1. The problem

Some terms first.

- **BESTMATCH** (`(?b)`) asks for the match with the fewest errors, not the first one that fits.
- A **search** tries the pattern at one start position after another until an **attempt** (the
  pattern run from one start position) succeeds or the text runs out.
- The **search anchor** is the position a search started from. The engine treats it specially in
  two places: a fuzzy item standing on it may not open with an inserted character (upstream's
  "it's better just to start searching one character later", `upstream/src/_regex.c`:10214, and
  this port's `AnchorIsPinned` exception to it), and an empty match on it is refused after an
  empty match (`MustAdvance`).
- The **walk** is `Matcher.DoBestFuzzyMatch`. After it finds a match it searches again, from that
  match's start to the end of the text, with the error budget one below the match's, hoping for a
  better match. When there is none, that search fails only at the end of the text.

A `finditer` runs one walk per match, so with no better match anywhere, every match pays one search
to the end of the text: n matches times n characters, quadratic. D115 fixed this with a memo
(`Matcher.BestWalkKnownToFail`, `MatchState.BestWalkFailures`): once a search from F has failed
under some budget, a later search from S, past F, under a budget no wider, runs only its first
attempt (at S) and skips the rest. The argument is that every attempt after S also ran in the
search from F, under the same or a wider budget, and nothing in it depends on which search it
belongs to, because its text position only moves away from its start, so it never meets either
anchor.

A lookbehind breaks that argument: its body reads backwards from the attempt's start, so an
attempt at T can read text at S or at F. If the body holds a fuzzy item, the item sees the anchor
when it stands on it, and the two searches see different anchors. D115's fourth witness shows it
(`BestMatchWalkMemoTests`, pattern `(?b)(?:(?<!(?:abb){i<=1})x|(?:[aQ]Z|bbZ){s<=1})`, text
`aQbbx`):

1. The scan finds `aQ` at 0 with one substitution. The walk searches from 0 with no errors allowed
   and fails; the memo would record "a search from 0 under 0 errors fails".
2. The next walk starts at 2 and searches for a match with no errors. Its attempt at 4 runs the
   negative lookbehind `(?<!(?:abb){i<=1})` before `x`: the body reads `abb` backwards from 4,
   matches `bb`, and then stands at position 2 with `Q` in front of it where it needs `a`. It can
   go on only by treating the `Q` as an inserted character, an insertion made at position 2.
3. In the search from 0, position 2 was not the anchor, so that insertion was allowed, the body
   matched, the negative lookbehind failed, and the attempt at 4 failed.
4. In the search from 2, position 2 is the anchor, the insertion is banned there, the body cannot
   match, the negative lookbehind holds, and `x` at 4 matches exactly.

So the search from 0 failed where a later one succeeds. (Steps 2 to 4 are read from the code; the
run below confirms the outcome.) Main's answer is `0,2,1,0,0|4,1,0,0,0`
(start, length, substitutions, insertions, deletions). Run with the memo forced on, the second
match becomes `2,3,1,0,0` instead: wrong. This is why D115 withholds the memo from any pattern with
a lookaround against the scan (`PatternObject.LooksAgainstTheScan`, set in
`NodeCompiler.BuildLookaround` and `BuildConditional`), and that pattern keeps the quadratic cost.

The D116 witness is `(?b)(?<=\s)(?:the\s(?>[a-z]+)\s){e<=1}` over the `BestMatchWalkMemoTests`
prose. Debug steps (`WorkCounter`), main f3bcdb68, `.scratch/d116/probe.cs scale`:

| Characters | With `(?<=\s)` | Without it |
|---|---|---|
| 4,000 | 502,694 | 51,516 |
| 8,000 | 1,965,044 | 103,066 |
| 16,000 | 7,749,390 | 206,141 |

Doubling the text quadruples the steps with the lookbehind and doubles them without. The second D115
pattern does the same: `(?b)(?<=\s)(?:thx\s[a-z]+\s){e<=1}` takes 338,662 / 1,285,284 / 5,001,407.

## 2. What upstream does

Upstream regex 2026.9.29, best of 3 runs, `.scratch/d116/up116.py` (memory capped at 2 GB through a
job object, run under `timeout`), on a busy machine, so read the ratios, not the times:

| Pattern | 4,000 | 8,000 | 16,000 | 32,000 |
|---|---|---|---|---|
| `(?b)(?<=\s)(?:the\s(?>[a-z]+)\s){e<=1}` | 0.4 ms | 0.7 ms | 1.4 ms | 3.1 ms |
| `(?b)(?:the\s(?>[a-z]+)\s){e<=1}` | 0.4 ms | 0.7 ms | 1.3 ms | 2.8 ms |
| `(?b)(?<=\s)(?:thx\s[a-z]+\s){e<=1}` | 4.9 ms | 19.6 ms | 72.9 ms | 281.1 ms |
| `(?b)(?:thx\s[a-z]+\s){e<=1}` | 4.6 ms | 16.9 ms | 61.4 ms | 236.2 ms |
| `(?<=\s)(?:thx\s[a-z]+\s){e<=1}` (no `(?b)`) | 0.4 ms | 0.8 ms | 1.3 ms | 2.9 ms |

Upstream is quadratic on the `thx` shape, with or without the lookbehind, which is the D115 cost
it never fixed. On the `the` witness itself upstream is linear, but only because it gives a
different answer (33 matches at 4,000 characters against this port's 48): since D109 the port's
atomic body commits to a match that spends an error, so no exact match exists to stop the walk
early. The D116 row's "upstream is quadratic the same way" holds for the shape, not for this exact
pattern; the row should say so when it is next touched.

## 3. Options

What every option must keep: the answers identical to main on every input (the memo is an
optimisation beyond upstream, so the faithful-port rule allows it only if no answer moves).

### E. Withhold the memo only for a lookaround whose body can run a fuzzy item (recommended)

**How it works.** Today `LooksAgainstTheScan` is set for every lookaround against the scan. Set it
only when such a lookaround's body (or a conditional's test) holds a fuzzy section, or a group
call, or is compiled fuzzy by one of the oracle-only switches (`Info.UpstreamFuzzyConditionTest`,
`Info.UpstreamCallFeatures`). Everything else in the memo stays.

**Why it is sound.** A search's attempts can differ between two searches only where they read the
anchor. The places that do (from a grep of `SearchAnchor` in `Matcher.cs`, to be turned into a
cited, asserted list by the builder) are:

- `\G`, which reads it directly: already excluded (`ReadsTheSearchAnchor`).
- The insertion ban, in the fuzzy item, string and fold matchers (`PermitInsertion` lines,
  `Matcher.cs`:12041, 12223, 12408, 12522, 13289, 13432, 13573, 13895, 14065) and the guard check
  `AnchorIsPinned` they call: each fires only in a fuzzy item at `TextPos == SearchAnchor`.
- The empty-match ban at success (`Matcher.cs`:15928, 16015, 17339): it compares the match's end
  with the anchor, and an attempt that starts past S ends past S.

So an attempt at T past S can see the anchor only through a fuzzy item whose text position comes
back to S, and only a lookaround against the scan brings it back. If that lookaround's body holds no
fuzzy item, nothing inside it reads the anchor. The port compiles a lookaround body, and a
conditional's test, exact whatever section encloses it (`Parsing/Nodes.cs`:4283-4293 for D42 and
4448-4461 for D52), so a lookbehind inside `{e<=1}` such as `(?:(?<!\w)ESTONIA){e<=1}` is still
fuzzy-free inside. The fuzzy lookaround node itself (ledger entry 50, `InsertBeforeAFailedLookaround`)
inserts at T, not at S. A group call is the exception that needs care: the called group's own
fuzzy section is fuzzy wherever it is called from, so a call in the body counts as fuzzy. A nested
lookahead inside a lookbehind is covered by looking at the whole body.

**Evidence, all on main f3bcdb68 with `LooksAgainstTheScan` forced off by reflection after compile,
which is exactly what E would compile for the patterns tested** (`.scratch/d116/probe.cs`):

- Random grid, `(?b)` and `(?br)` patterns whose lookarounds against the scan hold no fuzzy item:
  20,000 rows at each of seeds 7, 4242 and 20261116, lookarounds outside the fuzzy section (also
  as conditionals): 2,799 / 2,714 / 2,761 rows where the memo changed the step count, 0 answers
  changed. Lookarounds inside the fuzzy section, seed 7, 20,000 rows each: plain lookarounds 3,937
  rows memo-active, 0 changed; conditional tests 4,046 memo-active, 0 changed.
- Control, to show the grid can go red (rule 11): the same grid with fuzzy sections inside the
  lookaround bodies, seed 7, 20,000 rows: 4,337 memo-active, **7 answers changed**, for example
  `(?b)(?:(?<=(?:xb){i<=1,d<=1})b*){i<=1,d<=1}` over `baaaba ` gains three matches.
- The four D115 witnesses: forcing the flag off changes rows 3 and 4 (fuzzy lookbehind bodies) and
  leaves rows 1 and 2 alone. Row 2 is a reversed pattern with an exact lookahead against the scan,
  so it is already a passing E row.
- Calls: the fourth witness with its fuzzy body moved into a called group,
  `(?b)(?(DEFINE)(?<g>(?:abb){i<=1}))(?:(?<!(?&g))x|(?:[aQ]Z|bbZ){s<=1})` over `aQbbx`, changes
  from `0,2,1,0,0|4,1,0,0,0` to `0,2,1,0,0|2,3,1,0,0` with the flag off, and so does the third
  witness rewritten the same way. A call in the body must therefore keep the memo withheld, and
  these two rows are its witness tests.
- The witness: 7,749,390 to 171,106 Debug steps at 16,000 characters (42,627 and 85,524 at 4,000
  and 8,000: linear), the `thx` shape 5,001,407 to 164,435, same answers.

**Worst case.** No new cost: the flag is computed once at compile time, and the memo's own cost
(a list of at most `BestWalkFailureSlots` facts per scan) is unchanged. A pattern E still refuses is
no worse than today.

**Adversarial input.** For time, none beyond D115's own. For correctness, the risk is an anchor
read this note missed; the builder's asserted list (rule 10) and the grids above are the guard.

**Size and risk.** Small: a new `CompileArgs` bit for "the body holds a call" set in
`NodeCompiler.BuildGroupCall`, and the two existing `LooksAgainstTheScan` lines move to after the
body is built and test `subargs.IsFuzzy`, that bit and the oracle switches. Whether `IsFuzzy` is set
for every fuzzy construct a body can hold (a fuzzy section reached only through a `(?(DEFINE)...)`
group, for instance) must be checked by the builder; where it is not, count it as fuzzy. No engine
control flow changes, so the faithful-port structure is untouched. Risk: low.

**Tests.** Red first: a step-count row for the witness in `BestMatchWalkMemoTests` (the existing
`A_bestmatch_scan_with...` theory with `(?<=\s)` added, under the same 1,000,000-step ceiling,
which main exceeds at 16,000 characters with 7,749,390). Then the two call rows above as new
cases of the anchor theory, and the existing four rows stay. Each exclusion (fuzzy body, call, each
oracle switch) gets a row that goes red when its part of the test is removed (rule 12).

### A. Record how far back a fuzzy lookbehind can read, and run that many attempts live

**How it works.** For a pattern E still refuses, compute at compile time the reach R: the most
text, in UTF-16 units, that any fuzzy item in a lookaround body against the scan can sit behind the
attempt's start. A later search from S then runs its attempts at S up to S + R live and skips the
rest, instead of only the first. This generalises today's "first attempt only", which is the case
R = 0.

**Why it is sound.** An attempt at T past S + R cannot bring a fuzzy item back to S, and so not to
F either, which lies before S; it runs the same in both searches but for the budget, and it failed
in the earlier one. The attempts from S to S + R can meet S and are run.

**Computing R.** `Parsing` already has `MaxWidth` per node (upstream's `max_width`); R adds, per
fuzzy section, the most insertions its constraints allow (`i`, `e`, or `cost` over the insertion
weight). It must be in UTF-16 units: `.` takes two units over a non-BMP character (checked:
`(?<=.)x` over `"\U0001F600x"` matches at 2), and full case folding lets one pattern character
match two text characters (`(?fi)(?<=ß)x` matches `ssx`), so a codepoint width must be doubled or
measured on folded strings. PCRE2 warns that a nested lookbehind reads further back than the
longest single one ("`(?<=(?<!b)a)` has a maximum lookbehind value of one, but inspects two
characters", pcre2partial, read 2026-11-16), so R must be computed over the whole nested body, not
per lookbehind. A repeat without an upper bound, a backreference or a call makes R unbounded, and
then A refuses (that is option C).

**Worst case.** O(R) attempts per later search instead of 1, so a finditer is O(n x R) rather than
O(n x n). With a large R, for example `(?<=(?:\w{1,200}){e<=3})`, each walk runs about 200
attempts: linear, but slow.

**Size and risk.** Moderate: the reach computation (new, not upstream's), a fact that stores R or
looks it up from the pattern, and a bounded search window in the walk. `MatchState` has a
refinement window (`RefineStart`, `RefineEnd`) that might serve, but `BestWalkCanRemember` asserts
it is unused during the walk, so reusing it needs its own proof. Getting R wrong in the UTF-16 or
folding cases gives a wrong answer, not a slow one. Risk: medium.

**Value.** In the corpus only 2 of 49 patterns are in A's class (section 4), both upstream's
`\m(?:word){e<=3}\M(?<!\m(?:word){e<=1}\M)` with R = 5 characters. Not worth building now.

**Tests.** Red-first step rows for a fuzzy lookbehind at several R; witness rows at T = S + R
and S + R + 1, in UTF-16 and with full folding; the control grid above (fuzzy bodies), which must
then stay at 0 changed answers.

### B. A per-position memo keyed on the lookbehind's own outcome

**How it works.** Remember, per text position, whether each lookbehind held there, and let a later
search reuse the outcome. This is the shape of Barrière and Pit-Claudel's linear algorithm for
JavaScript lookarounds: an "oracle boolean truth table, indicating [at each] string position [whether]
each lookaround holds", built before the main match ("Linear Matching of JavaScript Regular
Expressions", PLDI 2024, arXiv 2311.17620, section 4.3, read 2026-11-16).

**Why it does not fit.** It solves a different problem. The table works because an exact
lookaround's outcome is a function of the position alone. Here the cost is not in the lookbehind
but in the whole search being repeated, and for the fuzzy bodies that still need help the outcome is
not a function of position: it depends on the anchor (that is the bug) and on the error budget left
when the lookaround starts, since a lookbehind in a fuzzy section shares the section's budget. A
sound key is (position, budget left, whether the anchor lies inside the reach), which is option A
with a table added. For exact bodies E already gives the answer for free.

**Worst case.** Memory O(n x lookarounds x budgets); time no better than A.

**Size and risk.** Large and new; touches the lookaround opcodes in the hot path. Rejected.

### C. Option E plus A only for bounded reach; unbounded reach stays quadratic (refuse)

A pattern whose fuzzy lookbehind body has no bounded reach (a call, an unbounded repeat, a
backreference) keeps today's cost. With E in place this is the 3 corpus patterns that hold a call,
none of them fuzzy at the top level. Recorded in the D116 row as the known ceiling.

### D. Leave it as it is

Keep D116 open and documented. Cost: a `(?b)` finditer with any lookbehind, even an exact guard
such as `(?<=\s)`, stays quadratic,
7.7 million Debug steps at 16,000 characters where 171,106 would do. Fuzzy is the library's hot
path, so this is the weakest option.

### F. Other engines

None of the engines surveyed solves this exact problem, because none has a best-match walk with
an anchor-dependent insertion rule. RE2 does not support lookarounds at all ("`(?<=re)` ... NOT
SUPPORTED", RE2 Syntax wiki, read 2026-11-16), nor does .NET's linear `RegexOptions.NonBacktracking`
("doesn't allow ... Lookarounds", Microsoft Learn, regular-expression-options, read 2026-11-16).
PCRE2 exposes the longest lookbehind (`PCRE2_INFO_MAXLOOKBEHIND`, pcre2_pattern_info) so that an
application matching in segments knows how much earlier text to keep, which is option A's reach
idea, with the nested-lookbehind caveat quoted above. The JavaScript paper is option B. A semantic
change, such as lifting the insertion ban inside lookaround bodies, would make every lookbehind
anchor-free but changes answers, so it is out of scope for a performance item.

### Summary

| Option | Sound when | Worst case | Size | Risk |
|---|---|---|---|---|
| E | no fuzzy section or call in any body against the scan | no new cost | small (estimate: tens of lines), compile time | low |
| A | R correct in UTF-16 units over the nested body | O(n x R) per scan | moderate, new reach analysis | medium |
| B | key includes position, budget and anchor reach | O(n x looks x budgets) memory | large | high |
| C | as E and A | quadratic for unbounded reach | none beyond A | none |
| D | - | quadratic for every lookbehind | none | none |

## 4. Feasibility: how many patterns E and A would cover

**Corpus.** `tests/FuzzyRegex.Tests/Gaps/CompileParity/corpus.json`, written by
`tools/record-compile-corpus.py`: every pattern upstream's own test suite compiles (regex
2026.9.29). It is a test corpus, not a survey of real use; no real-use pattern corpus exists in
the repository. Each distinct (pattern, flags) pair was parsed with upstream's own parser and its
lookarounds against the scan classified (`.scratch/d116/corpus116.py`), applying the port's rule
that a lookaround body is compiled exact:

| | Patterns |
|---|---|
| Distinct (pattern, flags), parsed | 1,541, 1,540 |
| Fuzzy patterns | 131 |
| With a lookaround against the scan | 49 (6 of them fuzzy) |
| E: no fuzzy section or call in any such body | 44 |
| A only: a fuzzy body with bounded reach (5 characters) | 2 |
| Unbounded: a call in the body | 3 |

All 4 fuzzy patterns E covers (`(?:(?<!\w)ESTONIA){e<=1}` and its reversed and positive variants)
have the lookaround inside the fuzzy section with an exact body, the case the "inside" grids test.

**Does the port compute lookbehind widths?** Not for this purpose. `Parsing/Nodes.cs` has
`MaxWidth` on every node (upstream's `max_width`), and `LookAround.MaxWidth` returns 0 because a
lookaround is zero-width; the engine compiler tracks only `MinWidth` (`NodeCompiler.cs`). No
code computes how far back a lookbehind body reads. Option A would build that on `MaxWidth`, with
the UTF-16 and folding corrections above. Option E needs no width at all.

## 5. Recommendation, test plan and gates

**Build E; record A and C in the D116 row as the remaining ceiling.** Confidence high that E is
sound for the constructs grid-tested (exact bodies, inside and outside fuzzy sections, conditional
tests, both directions) and that it fixes the witness; medium that no other anchor read exists,
which the builder settles with rule 10's cited, asserted list before the first commit. What would
change it: a builder finding another anchor read reachable from an exact body (E would then need
that construct excluded too, or would fall back to A); a real-use survey showing fuzzy lookbehind
bodies are common (A would then earn its cost).

Test plan, in order:

1. Red: the witness step row (16,000 characters, 1,000,000-step ceiling), and the `thx` variant.
   Confirm main fails it (7,749,390 and 5,001,407 steps).
2. The two call rows from section 3 as anchor-theory cases; they pass on main and must still pass.
3. Implement E. One witness per exclusion, each shown to go red when its part is removed (rule 12):
   fuzzy body (existing rows 3 and 4), call (new rows), each oracle switch (rows run with
   `UpstreamFuzzyConditionTest` and `UpstreamCallFeatures` set).
4. Rule 10: list each anchor read with its `file:line`, and add a `Debug.Assert` that an attempt
   past S under a remembered fact never evaluates a fuzzy item at `SearchAnchor`.

Gates:

- Ratchet GREEN (`tools/check-ratchet.ps1`).
- Oracle at 3 seeds, diverging only on known rows identical to main.
- Differential against main on the D115 row sets (mix, the D111 grid, rows-c, rows-d, m176-m178,
  the adversarial rows; regenerate as the F107 row says) plus the four grids of
  `.scratch/d116/probe.cs` run against the real change, with the per-construct counts of
  memo-active rows reported (rule 11).
- Step-count rows: the witness and `thx` at 4,000 / 8,000 / 16,000 characters, and the
  flagless and no-lookbehind controls unchanged.
- WPR before and after on the witness (Release), and an allocation check (the memo list is
  per state and reused, so 0 B warm is expected; measure it).
- BDN: not needed unless the WPR profile shows a shift in a shared path; E changes compile only.
