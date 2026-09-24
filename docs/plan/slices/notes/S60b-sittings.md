# S60b sittings notes

The slice file in `docs/plan/slices/` is the spec. This file is the running record of what each
sitting did, what it measured, and what it left for the next one.

## Sitting 1 (2026-09-22, afternoon) - item 2: `search_start` and its dispatcher

Landed as a green checkpoint. The benchmark measurement item 2 owes is NOT done: the machine was
in use until 22:00 and any timing taken on it is noise, so the orchestrator forbade
`tools/compare-benchmarks.ps1` for this sitting.

### What landed

- `Matcher.SearchStart`, `Matcher.MatchMany` and `Matcher.SearchStartZeroWidth` - upstream's
  `search_start` (`upstream/src/_regex.c:8385-9236`) and the thirty-odd `search_start_*` helpers,
  collapsed into one sweep, one zero-width scan and the dispatcher.
- `Matcher.StepOver`, and the defect it fixes: the continuation after a start test stepped one
  character where a `STRING` node's step is its whole length (`make_STRING_node`, `:25811`).
  Upstream can add the step to a position because its positions count characters; this port's
  count UTF-16 code units, so the step is a walk.
- `MatchState.DoSearchStart`, seeded from `PatternObject.DoSearchStart`.
- `tests/FuzzyRegex.Tests/Gaps/Engine/SearchStartTests.cs`, 14 tests, one per arm of the dispatch
  plus the two invariants below.
- Doc updates: `docs/PORTMAP.md` (the `search_start` row, and the `prefilter-free` wrapper's fate),
  `docs/COMPARISON.md`, `docs/DIVERGENCES.md`, `docs/STATUS.md`, `docs/plan/OPTIMISATION-NOTES.md`.

### The four narrowings, and what each is worth

Numbering as `SearchStart`'s remarks spell it. 1: no partial arms. 2: the predicates are the
matcher's. 3: withheld under `(*SKIP)` (`pattern.HasSkipVerb`). 4: the `min_width` bound is not
ported. Upstream's own required-string condition (`:11770`) is a fourth `&&` clause in
`BasicMatch` and is not a narrowing.

Measured during this sitting, each by deleting its clause and running the whole suite:

| Control | Clause switched off | Result |
| --- | --- | --- |
| A | narrowing 1 | 5 tests red |
| B | narrowing 3 | 0 red; oracle 0 diverging rows of 4000 at seed 31, and a green seed-7 run |
| C | upstream's required-string clause | 82 red before `StepOver`, green after it |
| D | narrowing 4 (guard restored) | 12 red - 10 fuzzy, 2 group calls |

**These numbers were taken mid-sitting and have NOT been re-run against the code as committed.**
The skill requires that final re-run and a fresh unused seed for each control that fired; both are
outstanding. Re-running them is cheap and is on the next sitting's list below.

Exact snippets for the re-run, all in `src/FuzzyRegex/Engine/Matcher.cs`:

- **Control A**, `BasicMatch` (~`:5817`): delete the line
  `state.PartialSide == MatchState.PartialNone` and promote `!pattern.HasSkipVerb` to the first
  clause of `searchStartAllowed`.
- **Control B**, same expression: delete `&& !pattern.HasSkipVerb`.
- **Control C**, same expression: the last clause cannot simply be deleted - that leaves
  `EquivalentNodes` unused and S1144 fires. Wrap it:
  `#pragma warning disable S1125` above, and
  `&& !(false && pattern.ReqString is Node required && EquivalentNodes(startTest, required))`.
- **Control D**, `SearchStart`, first statement inside `while (true)` (~`:5540`, above the
  `switch`):

  ```csharp
  long available = state.Reverse ? startPos - state.SliceStart : state.SliceEnd - startPos;

  if (available < state.MinWidth)
  {
      return MatchStatus.Failure;
  }
  ```

Wave for B's oracle figure: `tools/run-oracle.ps1`, the default generator list, 4000 rows, seed 31.

### Narrowing 3 is a judgement, not a measurement

It is kept and nothing measured supports it. Switching it off leaves the suite green and the
oracle's `verbs`, `partial-sliced` and `interactions` waves at 0 diverging rows of 4000, where the
same narrowing on `LocateRequiredString` was caught red by a pinned test the moment it was missing.

The asymmetry has a reason. The locator skips positions at which the matcher might well have
succeeded; everything in `SearchStart` skips only positions at which the start test itself fails,
and an attempt from such a position cannot reach a verb. On that argument upstream is right to have
no such condition, and upstream has none.

What the argument does not do is measure, and the two earlier defects in this area - S31's partial
arms and S35's `END_OF_LINE_rev` bound - were both found by measurement rather than by argument.
So lifting narrowing 3 is a widening for a sitting that can run the benchmarks and a blind review,
not for the one that ported the dispatcher. The experiment is: delete the clause, run the full
suite, then run the oracle's `verbs` and `partial-sliced` waves at three seeds none of this slice
has used, and judge any row that moves.

### The `STRING` arm is unreachable

Reaching it needs a `STRING` start test that is not the required string, and a pattern cannot have
one: `Sequence.get_required_string` (`upstream/regex/_regex_core.py:3698`) returns the first item
of the sequence that yields one, so a `STRING` start test is always the required string, and
upstream's own condition at `:11770` then withholds the prefilter.

Measured on 2026-09-22 against regex 2026.9.10 by wrapping `_get_required_string`
(`upstream/regex/_main.py:602`): `cat.*dog`, `(cat)dog`, `catz?dogdogdog`, `cat[0-9]*` and
`cat(?:x|y)dog` all compile to `req_offset=0, req_chars=(99, 97, 116)`. The invariant is pinned by
`SearchStartTests.A_pattern_that_starts_with_a_string_makes_that_string_its_required_string`.

The arm is ported anyway, because upstream has it. Switching off upstream's condition is the only
way to reach it, and doing so is what found the step wrong.

### Which workloads should move, and in which direction

For the sitting that runs the benchmarks after 22:00. Nothing here is measured; it is the
prediction the triage should test, so that a regression can be judged rather than re-derived.

- **Faster, and the reason the prefilter exists:** a pattern whose start test is one character or a
  small set, over a subject where that character is rare. `q[abc]d` over a long subject with few
  `q`s: the sweep is `MatchMany`, which is the same walk `CountOne` already runs, and it replaces
  one full `BasicMatch` attempt per skipped position.
- **Faster:** the anchored start tests - `^` and `$` under MULTILINE, `\b`, `\m`, `\M`, `\Z`, `\G`.
  Every position between two line breaks is skipped by a scan instead of refused by an attempt.
- **Neutral by construction:** `(?s).` (the `ANY_ALL` arm returns at once), any literal-prefixed
  pattern (upstream's required-string clause withholds the prefilter), any `(*SKIP)` pattern
  (narrowing 3), and any partial match (narrowing 1).
- **Slower, and the workloads to watch:** a start test that matches nearly everywhere, over a
  subject where it does. The sweep then costs a walk per position and skips nothing, and the cost
  is paid once per attempt on top of the attempt itself. The fuzzy workloads should be flat, since
  a fuzzy start test turns the prefilter off for the rest of the operation - if a fuzzy workload
  moves at all, that early return is the first thing to check.
- **Watch the `search`/`findall` split:** `match` and `fullmatch` consult the prefilter exactly
  once, so any movement there is noise or a compile-side cost, not the sweep.

### Outstanding for the next sitting, in order

1. **Triage the benchmarks** (after 22:00). The slice's gate says a workload that regresses beyond
   the noise floor is triaged before the sitting commits, and that triage is the first thing this
   sitting does. It may revert what this checkpoint landed. The predictions above are what to test
   it against.
2. **Re-run all four controls against the committed code** and record the numbers, plus one seed
   this slice has not used for each control that fired.
3. **The blind review** - it has not run at all on this code. `docs/VERIFICATION.md` has the
   brief. The hunt list the slice names: a prefilter that searches below a position a verb
   committed past; a `SearchValues` built per call instead of per pattern; a reverse or
   case-folded arm that skips the fold; the underflow site clamped rather than handled; a partial
   match whose run-out position moved.
4. Then the slice's remaining items - 3, 6, 8-14, 16 and 17 - each on its own sitting.

### Verification already done, and green

Full suite 6577/6577. Ratchet GREEN, baseline updated to 6469 distinct passing test ids.
Differential oracle GREEN at its three default seeds. The three permanent files the slice names:
`BacktrackingVerbTests` 20/20, `PartialMatchingTests` 54/54, `ReverseMatchingTests` 40/40.
Native AOT: `tools/run-aot-tests.ps1` 6574 passed / 3 skipped, `tools/run-aot-smoke.ps1` PASSED.

## Sitting of 2026-09-23 (night): item 10, the fuzzy literal filter

The orchestrator put item 10 first and deferred item 2's triage ("do not revert it, do not build
on its numbers"), so the list above is still outstanding except for item 10.

### What landed

`Engine/FuzzyLiteralFilter.cs` (new, marked NOT UPSTREAM'S, ledger row in SYNC-DIVERGENCE.md).
It applies to a pattern that is exactly one fuzzy section over a literal, such as
`(?:amber lantern){e<=2}`. With at most k edits, a match must contain one of k+1 equal pieces of
the literal unchanged (Navarro's pigeonhole filter). The filter finds each piece's next
occurrence and lets the search jump to the earliest start any of them allows
(occurrence - piece offset - k), or refuses the subject when no piece occurs at all.

- k is the tightest of `e`, the sum of the per-kind limits, and the cost equation divided by its
  cheapest weight. No filter when k is unbounded or leaves pieces too short.
- Search only; withheld under partial matching. Reverse searches use it only to refuse.
- ASCII only: the first non-ASCII character in the stretch it would search switches it off for
  the rest of the operation. An ordinal case-insensitive search is a superset of the engine's fold
  only when both sides are ASCII (KELVIN SIGN, long s, `ß`, `ﬁ` are the pinned counter-examples).
- The per-piece occurrences are cached on the stack of `BasicMatch`, so the shared filter object
  stays immutable. `ThreadSafetyTests` lists the new field.

### A real bug the new generator found

Under `(?r)` with full case folding, the literal is split into a chain that the engine walks from
the literal's end, while each node keeps its own characters in reading order. The first version
concatenated the nodes in chain order and scrambled the pieces. `fuzzy-literal` seed 7 row 1468:
`regex.search(r'(?fi)(?r)(?:stone fine){s<=1,i<=1}', 'xebaxsizdrfkSTone Fineoociokw r lo')` gives
span (12, 23), counts (1, 1, 0); the port found nothing. Fixed by putting a reverse node in front of
the ones before it; pinned by `A_reverse_chain_is_read_back_into_the_literal_s_own_order` and a
row in the reverse theory, both red before the fix.

### Numbers

ManyInputs, `--inProcess`, run from `bench/`, one job at a time. Load: my processes only
(VBCSCompiler and python idle). Allocation is deterministic; times are the gate.

| Row | Before | After | Allocated before / after |
|---|---|---|---|
| FuzzyPhraseOneIsMatch | 2.424 s | 64.18 ms | 92.39 / 87.18 MB |
| FuzzyPhraseOneMatch | 2.470 s | 57.28 ms | 92.39 / 87.18 MB |
| FuzzyPhraseOneEnhanced | 2.497 s | 60.72 ms | 95.61 / 90.4 MB |
| FuzzyPhraseThreeSeparatePasses | 7.438 s | 164.93 ms | 277.12 / 261.46 MB |
| FuzzyPhraseThreeAlternation | 6.907 s | unchanged (7.24 s paired) | 92.5 / 92.5 MB |
| FuzzyPhraseThreeNamedList | 7.383 s | unchanged (7.36 s paired) | 92.5 / 92.5 MB |

The two Three rows are not covered by this shape and ran with `--warmupCount 2 --iterationCount 8`
because BDN in-process refuses a 7 s op otherwise. The usage-answers mode printed identical output
before and after (FuzzyPhraseOneIsMatch 2038, the Three rows 5107).

### Oracle

New generator `fuzzy-literal` in `tools/record-oracle.py`, in the default wave. The `fuzzy`
generator almost never draws the filter's shape (control H below fired on 1 row in 4000), so
this one does: 1-3 words from a small list, planted copies with up to 4 edits, 20% of rows with a
fold substitution (Kelvin, long s, `ß`, `ﬁ`), 20% with a fold placed so it is the only defect in its
piece, reverse at 0.4, BESTMATCH and ENHANCEMATCH, every operation, partial and pos/endpos.

- Baseline (final code): `fuzzy-literal` 2000 rows, seed 7: 0 diverge; seed 99: 0 diverge.
- Default wave, three default seeds: 7 GREEN, 4242 GREEN, 20260923 RED on rows 3752
  (interactions, split) and 5185 (partial-sliced, search). Both diverge on the base commit
  8dd746e too; left alone as instructed.

### Controls, final re-run against the committed code

All on `Engine/FuzzyLiteralFilter.cs` unless named, generator `fuzzy-literal`, 2000 rows, seed 7
then the fresh seed 99.

- Control E, the partial-matching guard. In `Matcher.cs` BasicMatch, change
  `search && state.PartialSide == MatchState.PartialNone ? pattern.FuzzyLiteralFilter : null;`
  to `search ? pattern.FuzzyLiteralFilter : null;`. Result: 9 diverge, then 5.
- Control G, the start bound. In `NextStart`, change
  `start = Math.Min(start, (long)found[j] - Offsets[j] - MaxErrors);` to
  `start = Math.Min(start, (long)found[j] - Offsets[j]);`. Result: 46, then 72.
- Control I, the ASCII check. In `NextStart`, change
  `if (text.AsSpan(from, searched - from).ContainsAnyExceptInRange('\0', '\x7F'))` to
  `if (text.AsSpan(from, searched - from).ContainsAnyExceptInRange('\0', '￿'))`.
  Result: 31, then 30.
- Control J, the reverse chain order. In `TryCreate`, change
  `values.InsertRange(nodeIsReverse ? 0 : values.Count, node.Values);` to
  `values.InsertRange(values.Count, node.Values);`. Result: 4, then 1. Thin even at reverse 0.4,
  because a scrambled piece still matches often; the structural gap test is the deterministic pin.
- Control H (measured mid-sitting, on `fuzzy` + `fuzzy-anchored`, 2000 each, seed 7): an early
  `if (Pieces.Length > 0) { return NoMatch; }` in `NextStart` gave 1 diverge of 4000. That is the
  reason `fuzzy-literal` exists.

Gap-test controls A-F on `FuzzyLiteralPrefilterTests` are in the entry above this one.

### New findings, not caused by the filter (both persist with the filter ablated)

- `fuzzy-literal` seed 20260923, 2000 rows, row 1611: `(?b)(?e)(?fi)(?r)(?:fine){e<=7}`
  fullmatch over 'oelFin becf'. Upstream gives no match; the port matches (0, 11) with counts
  (0, 7, 0). A reverse BESTMATCH fullmatch engine divergence. Needs its own slice.
- `fuzzy-anchored` seed 20260923, 2000 rows, row 5821: `(?b)(?r)\m(?:😀\d😀){e:[a-z]}` subf.
  Same status.

The default wave's 300-row `fuzzy-literal` slice does not reach either row, so the default oracle
stays green on them.

### Verification

Suite 6620/6620, ratchet GREEN, baseline 6512 distinct ids. Native AOT 6617 passed / 3 skipped;
AOT smoke GREEN.

### Review

One blind pass, Opus, with the `docs/VERIFICATION.md` brief. It raised one finding, and it
reproduced: two test comments quoted `(?i)` calls whose answers hold only under `regex.VERSION1`,
which the probe passes and the comments left out. Under V0, `(?i)` does not fold `ß` or `ﬁ`. The
assertions were right, because the port defaults to V1. Fixed by writing `(?V1i)` in the two
comments and stating V1 in the file's provenance note; both corrected calls re-run in Python give
the quoted spans. It found no behaviour defect. It ran 14,000 rows of its own through the oracle,
with 41 constraint forms, injected folds and every mode. Every divergence was a `(?b)`/`(?e)` row
with a cost equation or `(?p)`, and each diverged identically with the filter nulled. No second
pass: the only change after it was those three comment lines, checked directly above.

## 2026-09-23, item 10 extended: alternations and named lists

The orchestrator's next instruction: cover a fuzzy section whose body is an alternation of literals
or a named list, the ManyInputs rows FuzzyPhraseThreeAlternation and FuzzyPhraseThreeNamedList.

### What landed

`FuzzyLiteralFilter.TryCreate` now reads the section's body as a set of literals, one per path
through its branches. The pigeonhole argument holds for each literal on its own, so each is cut
into its own `k + 1` pieces, and the start bound is the minimum over every piece of every literal.
A subject is refused only when no piece of any literal occurs.

- `(?:amber lantern works|copper field studio|violet stone archive){e<=2}` is 9 pieces;
  `(?:\L<phrases>){e<=2}` over the same three is the same 9, since a named list compiles to a
  branch.
- The optimiser moves text the branches share out of the alternation: `amber lantern works|amber
  stone archive` compiles to `amber ` then a branch. The walk hands each arm its own copy of what
  it has read, so the shared text is on every literal. Under `(?r)` the shared part is a suffix,
  and a single shared character is a CHARACTER node, not a STRING; both are accepted and pinned.
- A negated or zero-width CHARACTER node is refused (`node.Match && node.Step != 0`), as is any
  node that is not a string, a character or a branch: a set, an empty arm, a repeat.
- At most 32 pieces in all (`MaxPieces`). One literal may have `k` up to 31; three may have 9.

**One pass or one search per piece.** The orchestrator suggested searching all the branches'
pieces in one `SearchValues<string>` pass. I kept one cached `IndexOf` per piece and raised the
cap instead, and measured it: the three-phrase alternation, 9 pieces, runs at 171.7 ms, against
159.7 ms for the three one-phrase passes (3 pieces each) it replaces. The cost is level with the
per-phrase work, so a single pass has little to win at this size. It would matter for a named list
of dozens of phrases; the `SHORTCUT:` comment on `MaxPieces` names that as the upgrade, behind a
benchmark.

### Numbers

ManyInputs, `--inProcess`, run from `bench/`, one job at a time, before (HEAD 2a63273's
`FuzzyLiteralFilter.cs`) and after back to back. Load: my processes only; no other benchmark or
oracle job running. The Three rows use `--warmupCount 2 --iterationCount 8`.

| Row | Before | After | Allocated before / after |
|---|---|---|---|
| FuzzyPhraseThreeAlternation | 6.798 s | 171.7 ms | 92.5 / 87.51 MB |
| FuzzyPhraseThreeNamedList | 6.935 s | 168.0 ms | 92.5 / 87.52 MB |
| FuzzyPhraseOneIsMatch | (item 10: 64.18 ms) | 61.38 ms | 87.18 MB both |
| FuzzyPhraseOneMatch | (57.28 ms) | 57.65 ms | 87.18 MB both |
| FuzzyPhraseOneEnhanced | (60.72 ms) | 60.16 ms | 90.4 MB both |
| FuzzyPhraseThreeSeparatePasses | (164.93 ms) | 159.67 ms | 261.46 MB both |

The usage-answers output is byte-identical before and after (FuzzyPhraseOneIsMatch 2038, the
three Three rows 5107).

### Oracle

New generator `fuzzy-alternation`, in the default wave. It shares `fuzzy-literal`'s row builder
(`_fuzzy_literal_row`, split out; `fuzzy-literal` seed 7, 2000 rows, is byte-identical before and
after the split). Each row draws two or three phrases from the same small word list, so branches
often share a first or last word, in one of three forms: `a|b|c`, `\L<phrases>`, or an alternation
inside one phrase (`kelvin (?:kelvin|oak) fine`). Planted copies are of a branch chosen at random,
and the targeted fold row folds one branch.

- `fuzzy-literal` + `fuzzy-alternation`, 2000 rows each: seed 7 0 diverge, seed 99 0 diverge.
- Default wave, three default seeds: 7 GREEN, 4242 GREEN, 20260923 RED on rows 3752 and 5185
  only, as on the base commit.

### Controls

On `Engine/FuzzyLiteralFilter.cs`, generator `fuzzy-alternation`, 2000 rows, seed 7 then the fresh
seed 99. Applied and reverted by string replacement (the tree held uncommitted work).

- Control L, only the first literal. Change `foreach (List<uint> values in walk.Literals)` to
  `foreach (List<uint> values in walk.Literals.Take(1))`. Result: 197 diverge, then 191.
- Control N, the shared prefix dropped from the first arm. Change
  `return Collect(node.Next1.Node, [.. values]) && Collect(node.Next2.Node, values);` to
  `return Collect(node.Next1.Node, []) && Collect(node.Next2.Node, values);`. Result: 10, then 11.
- Control O, a later literal's offsets taken as 0. Change `offsets.Add(offset);` to
  `offsets.Add(pieces.Count >= pieceCount ? 0 : offset);`. Result: 17, then 22.
- Control M, the character guard. Change `case Opcode.Character when node.Match && node.Step != 0:`
  to `case Opcode.Character:`. The generator draws no negated character, so this one is for the
  gap tests: `A_pattern_that_is_not_one_bounded_fuzzy_ascii_literal_gets_no_filter` with
  `(?:amber lantern [^x]orks){e<=1}` fails, 1 of 57.

### Verification

Suite 6644/6644, ratchet GREEN. Native AOT 6641 passed / 3 skipped; AOT smoke GREEN. The
oracle baseline and all four controls above were re-run after the review's fixes, with the same
numbers.

- Control P, the branch cap from the review. Remove `if (++_branches >= MaxPieces) { return
  false; }`: `Thousands_of_alternations_in_a_row_are_refused_without_walking_them_all` crashes the
  test host with a stack overflow (run before the fix went in).

### Review

Two blind passes, Opus, with the `docs/VERIFICATION.md` brief.

The first pass raised two findings, and both reproduced.

- `LiteralWalk.Collect` recursed once per branch on a path, so 8000 `(?:ab|cd)` in a row overflowed
  the stack while the pattern compiled, a crash nothing can catch. Upstream compiles it:
  `regex.compile(p, regex.VERSION1).search("xx" + "ab" * 8000 + "yy")` is (1, 16002). Fixed by
  refusing at the 32nd two-way branch. Each such branch adds a path, so a walk that could succeed
  (32 literals or fewer) never reaches it. Pinned by the new test, which crashed before the fix.
- The named-list test used `BeEquivalentTo`, which the native-AOT convention test forbids. I had
  run only the prefilter tests, not the whole suite. Now `Equal` in the compiler's order: longest
  first, stable between the two 19-character phrases.

It also reported one divergence the filter does not reach, recorded in STATE.md:
`(?b)(?r)(?:\L<phrases>){e<=3}` with phrases `['', 'amber lantern']`, fullmatch 'znz'. I
confirmed upstream gives None. By the reviewer's run the port gives (0, 3); I have not re-run it.

The second pass covered only the fix delta and returned "No defects found". It checked that 31
and 32 branches, and named lists of 31 and 32 entries, build the same filter with the guard on and
off. It also checked that 20,000 one-armed groups and 8000 `(?:gh)?` compile without overflow.

## 2026-09-23, 03:00 - the ASCII fast path in `SameCharIgn` (orchestrator item 1)

Landed in 1cb7cc6. **It is not yet reviewed.** This sitting had no Agent tool, and a nested
`claude -p` reviewer needs approval that a non-interactive session cannot give. The brief is ready
in `.scratch/review-brief.txt` (gitignored, so it may be gone; it is the VERIFICATION.md brief with
the scope "commit 1cb7cc6", the deliberate Turkic and LOCALE behaviour, and the hunt list below).
The next sitting reviews 1cb7cc6 before it builds anything else.

Hunt list for that review: an ASCII pair where the fast path and the enumeration disagree; a caller
passing a value that is not a codepoint; a caller that relied on LOCALE throwing through this path;
an IGNORECASE answer that moved against upstream (KELVIN SIGN, LONG S, the four Turkic I's, under
`(?i)`, `(?ai)`, `(?fi)` and `{e<=1}`); a test that still passes with the fast path wrong.

What landed: two ASCII characters under the ASCII or Unicode encoding compare by the ASCII case
rule. `SameCharIgnTests` checks it against the enumeration over 128 x 128 pairs and the eight
combinations of the ASCII, LOCALE and UNICODE flags.

### Controls

In `Matcher.SameCharIgn`, 1 of 6 `SameCharIgnTests` fails for each.

- Control A, the letter check dropped. Change
  `return (ch1 ^ ch2) == 0x20 && (ch1 | 0x20) - 'a' <= 'z' - 'a';` to
  `return (ch1 ^ ch2) == 0x20;`. First mismatch: `flags 0x0 U+0000 U+0020: True, want False`.
- Control B, the LOCALE guard dropped. Change
  `if ((ch1 | ch2) < 0x80 && encoding != CaseEncoding.Locale)` to `if ((ch1 | ch2) < 0x80)`.
  First mismatch: `flags 0x4 U+0000 U+0001: False, want NotSupportedException`.

The test is exhaustive, so there is no seed to vary.

### Numbers

ManyInputs `FuzzyPhraseOne*`, Release, `--inProcess`, run from `bench/FuzzyRegex.Benchmarks`,
after then before, back to back. **Not quiet:** another session's MediumRun of ManyInputs and
Workload (PID 3092) was running the whole time.

| Row | Before | After | Allocated |
|---|---|---|---|
| FuzzyPhraseOneIsMatch | 63.32 ms | 66.62 ms | 87.18 MB both |
| FuzzyPhraseOneMatch | 63.03 ms | 61.84 ms | 87.18 MB both |
| FuzzyPhraseOneEnhanced | 65.86 ms | 64.25 ms | 90.4 MB both |

Inside the error bars (1-2 ms). The 13.4% profile figure came from ce4ca5a, before item 10's
filter began skipping most start positions on these rows, so the call may no longer be hot there
(not measured). The item landed with its time gate open. The SYNC-DIVERGENCE row says: re-measure on a quiet
machine, over an IGNORECASE workload the filter does not reach, and delete the fast path if it gains
nothing.

### Verification

Suite 6650/6650, ratchet GREEN. Permanent files: BacktrackingVerbTests 20/20, PartialMatchingTests
54/54, ReverseMatchingTests 40/40. Oracle GREEN at 7 and 4242; 20260923 RED on 3752 and 5185 only,
as on the base commit. AOT 6647 / 3 skipped; the first publish failed in `link.exe` with exit 1318
and passed on the next run, unchanged. AOT smoke GREEN, 6.68 MB.

## 2026-09-23, 03:35 - 1cb7cc6 reviewed and re-measured

### Review

One blind pass (Opus, the VERIFICATION.md brief, scope `git show 1cb7cc6`, the hunt list above):
**No defects found.** It compared 11 IGNORECASE pattern forms over every ASCII pair against
`regex` 2026.9.10 through the public API: 360,448 cases under `(?a)` and `(?u)`, 0 differences.
Under `(?L)` the port refuses every case, as before the commit (LOCALE is not a public option,
`docs/DIVERGENCES.md`). `CaseEncoding` has three values and the test covers all of them. Suite
6650/6650. Nothing to reproduce, nothing fixed, no second pass needed.

### Re-measure

The profile's hot call is rarely reached on `FuzzyPhraseOne*` now that item 10's filter skips most
start positions, so this used IGNORECASE patterns the filter does not accept. Stopwatch probe in
`.scratch/sci/` (deleted): 200,000 characters of words drawn with `new Random(7)` from `the Quick
brown FOX jumps over lazy dog Hay stacks needle Straw barn field`, one space after each; per
pattern, one warm `Count`, then the median of 9. Release. "Before" is the same source with the
guard changed to `if (ch1 == 0xFFFFFFFF && (ch1 | ch2) < 0x80 && encoding != CaseEncoding.Locale)`.
Runs alternated. Not quiet: another session's MediumRun of `ManyInputs` was running (about one
core), which alternation spreads over both sides but does not remove.

| Pattern, `IgnoreCase` | Before | After |
|---|---|---|
| `(?:hay\w?stack){e<=2}`, 396 matches | 129.2 / 132.6 ms | 119.2 / 120.0 ms |
| `qu\w+ck\s+b`, 378 matches | 6.3 / 6.0 ms | 5.0 / 5.2 ms |
| `(\w{3}) \1`, 726 matches | 20.5 / 20.6 ms | 20.8 / 20.4 ms |

About 9% and 17%, well outside the run-to-run spread. The backreference shows no change (not
profiled, so why is not known). The fast path stays and the SYNC-DIVERGENCE row now carries these numbers.

## 2026-09-23, 04:00 - item 2's controls re-run, and its review half done

**Controls**, re-run against the code at 232dee0 using the exact snippets in sitting 1's notes, with
the whole suite each time (6650 tests): A 5 red, B 0 red, C 0 red, D 12 red. These are sitting 1's
figures, now reproduced on committed code. Only A and D fired, and both are suite controls with no
seed. B's oracle figure (seed 31) has still not been re-run at a fresh seed.

**Blind review of 20be6aa**, one pass (Opus, the VERIFICATION.md brief, the slice's hunt list):
**no defects found so far, but the pass stopped early** because the allowance hook hit 93%.
- It built HEAD and a copy with `searchStartAllowed` forced false. It traced `SearchStart` to
  confirm it runs, then ran 149,292 generated cases through both builds. Each case asked six
  questions: finditer, overlapped, partial, search, match and fullmatch. It covered every dispatch
  arm, `(?r)`, the zero-width tests, `(?w)` and `\X`, surrogates, case-fold traps, and `pos`/`endpos`.
  Prefilter on and off differed on 0 of those cases.
- Both builds differ from `regex` 2026.9.10 on the same 18,513 rows. The ones it sampled are the
  pinned reversed-partial divergence, for example `(?r)a` over `abcab` from pos 1. Those rows are
  not SearchStart's, but they were not checked one by one.
- **Not done:** an exhaustive sweep of every UTF-16 start and slice (mid-pair starts included)
  under `(?m)`, `(?r)` and `(?i)`, which hit the 600 s limit; and `tools/run-oracle.ps1`.

The next sitting finishes this pass: that sweep, in chunks under 600 s, and the oracle at three
seeds. Only then does it go on to the benchmark triage.

## 2026-09-23, 05:15 - item 2 triaged, its review finished, one defect fixed

### Benchmark triage: item 2 is kept

Measured by the orchestrator on a quiet machine, 04:38-04:55: 95318dd (before) against 20be6aa
(after), both with 6a3d761's `bench/`, `ManyInputs`, `--job medium --inProcess`, before and after
alternated per row. Raw output in the main checkout:
`artifacts/bench/2026-09-23-queue/item2-{before,after}-<row>.txt`.

| Row | Before | After |
|---|---|---|
| FuzzyPhraseThreeNamedList | 6.582 s | 6.680 s (+1.5%, inside S58's 1.13 floor) |
| RedactDigits | 106.8 ms | 62.14 ms (1.72x faster) |
| ValidateEmails | 71.43 ms | 70.54 ms |
| ParseLogLines | 67.96 ms | 68.62 ms |

Allocated bytes are identical on every row. The earlier 1.93x slowdown on the named-list row was
contamination from builds running during the measurement. Also recorded in
`docs/plan/OPTIMISATION-NOTES.md`.

### The sweep the first pass did not finish, and what it found

This sitting had no Agent tool, so it ran the outstanding checks itself. Harness in
`.scratch/sweep/` (scratch, not kept): each pattern compiled twice, the second with
`PatternObject.DoSearchStart` cleared by reflection, which `MatchState` reads once per operation.
61 pattern bodies (every dispatch arm, the zero-width tests, `\X`, surrogates, the case-fold
traps) under 13 flag prefixes (`(?m)`, `(?r)`, `(?i)`, `(?fi)`, `(?w)`, `(?a)(?i)`, `(?V1)(?i)`
and their combinations), over 7 subjects holding pairs, CRLF and Turkic I's. Every UTF-16
`(beginning, length)`, mid-pair included, and six calls each: `Match`, `MatchAtStart`,
`FullMatch`, `Matches`, overlapped and partial.

**Result on 20be6aa's code: 605 of 1,623,960 cases differed**, all of one shape: a reverse
zero-width start test (`^`, `\b`, `\B`, `\M`) with a `beginning` that splits a surrogate pair.
Example: `(?r)\B` over `"😀😁x😂\na"`, slice (3, 5), gave `(2, 0)` with the prefilter on, a match
outside the slice; off, no match. `SearchStartZeroWidth` stepped back with `PrevPos`, which walks
the whole pair from 4 to 2. Upstream's `search_start_BOUNDARY_rev` (`:7907-7913`) cannot pass
`slice_start` because its positions are codepoints.

Fix: the scan gives up when a reverse step lands below `SliceStart`, which is where the matcher
itself refuses to start. Pinned by
`SearchStartTests.A_reverse_zero_width_scan_stops_at_a_slice_start_that_splits_a_surrogate_pair`,
which compares every slice of that subject against the unfiltered engine for the four tests. It
failed before the fix (`(?r)\M (6, 2)`: want no match, found `(5, 0)`) and passes after it.
**After the fix: 0 of 1,623,960.**

### Verification

Suite 6651/6651, ratchet GREEN, baseline 6543. Oracle: seeds 7 and 4242 GREEN; 20260923 RED on
rows 3752 and 5185 only, the two that diverge on the base commit 8dd746e.

### Review

Item 2's blind pass is now complete: the first reviewer's 149,292 cases found nothing, and the
sweep above found one defect, reproduced and fixed in 8fed3de. **The fix itself has not had its
blind pass.** It is eight lines in one method and never seen by a reviewer, so it owes one delta
pass. This sitting could not dispatch one: there was no Agent tool, and a nested `claude -p`
reviewer was refused approval. The brief is in `.scratch/review-delta.txt` (gitignored); its hunt
list is a reverse scan now refusing a position inside the slice, a forward scan passing a
`SliceEnd` that splits a pair, another arm answering outside the slice, and a lookbehind or
partial slice reaching this path. The sweep covers every one of those shapes and shows 0
differences, which is evidence but not the review.

The orchestrator's second review item, 1cb7cc6's `SameCharIgn` fast path, was already done at
232dee0 (no defects, 360,448 cases against upstream; the "03:35" section above). It was not run
again.

## 2026-09-23, 05:35 - 8fed3de's fix reviewed

One blind pass, the brief above, Opus. **No defects found.** The reviewer wrote its own sweep
rather than reusing ours: 53 patterns under 10 flag sets (`\G`, `{e<=1}`, lookbehinds and
`\p{Cs}` included), subjects holding lone high and low surrogates, slice starts on a lone low
surrogate and pairs ending exactly at `SliceStart`, six calls per slice. On HEAD: 534,240 cases,
0 differences with the prefilter on against off. On a copy with the fix removed: 763 differences,
for example `(?r)^` over `"😀a😀\n😁"` at slice (1, 1) answering 0 with the prefilter on. So the
sweep detects the defect. The new test fails on the reverted copy (`(?r)\M (6, 2)`) and passes on
HEAD. The forward scan cannot pass a split `SliceEnd`, because `TextEnd` equals it
(`MatchState.cs:639-642`) and `NextPos` stops there. Build clean; suite 6651/6651. Item 2 is
closed.

### Item 3, mapped but not started

Not started at 05:40: with the 06:39 deadline, an engine item could not get through its oracle
wave, the three permanent files, a benchmark, a review and the four-minute inspection. The map,
so the next sitting starts on the code:

- Ours: `Matcher.TryMatch` (`Matcher.cs:2983`). Its remarks explain the reduction: FAILURE is
  reported as SUCCESS, and only PARTIAL is propagated (S31).
- Upstream: `try_match` (`_regex.c:7671-7860`). It is one `switch` over about 40 test opcodes,
  each calling its `try_match_*` helper. The six `STRING*` arms return directly and, on success,
  jump past the string by `next->match_next` / `next->match_step`. The `SUCCESS` arm checks
  `match_all`.
- `NextNode.MatchNext`/`MatchStep` already exist (`Node.cs`) and the optimiser fills them
  (`Optimiser.cs`), so the graph is ready.
- There are 12 callers. Each is already faithful: it returns on a negative status and treats
  FAILURE as "do not enter" (for example `Matcher.cs:5752`, `:6424`). So the change is confined to
  `TryMatch` and its helpers: stop turning FAILURE into SUCCESS, and add the `STRING*` jump.
- Risk: the one S31 found. Upstream stops inside a branch where the reduction carried on, so
  partial matching is the area to hunt. Run `PartialMatchingTests` early and often.

## 2026-09-23, 05:40 - the orchestrator's review request for 1cb7cc6

The orchestrator asked, at 05:40, for a blind review of 1cb7cc6 before its 06:40 merge. That review
already exists: the "03:35" section above, one pass, no defects, 360,448 cases against `regex`
2026.9.10. `git log 232dee0..HEAD -- src/FuzzyRegex/Engine/Matcher.cs` lists only 8fed3de, whose
eight lines are in the reverse zero-width scan and had their own pass ("05:35"). `SameCharIgn` is
unchanged since the review, so it was not reviewed again: VERIFICATION.md allows one pass per
unreviewed change. Nothing unreviewed is on the branch. Item 3 is next.

## 2026-09-23, 23:10 - item 3 landed: `try_match` answers for every arm it has

### What changed

`Matcher.TryMatch` now answers the way upstream's `try_match` (`_regex.c:7671-7860`) does. Before,
it reported every FAILURE as SUCCESS and only passed PARTIAL on. Now:

- The one-character tests (`MatchOne`) and the zero-width position assertions
  (`TryMatchZeroWidth`) answer for themselves. On success the matcher resumes at
  `next.MatchNext`, one character on when the test is the node itself (`MatchStep`), where upstream
  adds `match_step`. The step is `Step`, so it walks a surrogate pair as one character.
- The six STRING arms go through `TryMatchString`, which was `IsStringTestPartial`. Running out
  of text gives PARTIAL or FAILURE, a mismatch gives FAILURE, and a success resumes past the
  string (`match_step == 0 ? start_pos : text_pos`, `:7383` onwards). The two full-fold arms also
  fail when the string ends part-way through a character's folding (`:7473`, `:7546`).
- A fuzzy test still answers SUCCESS. The SUCCESS arm (`:7832`) stays in the default arm, because
  upstream's checks `match_all` against `text_start` (DECISIONS, 2026-09-12).

All 12 callers already treated FAILURE as "do not enter", so nothing outside `TryMatch` changed
except one poll, below.

### One regression, caught by the suite

The first full-suite run had one red test,
`OptimiserTrapsTests.A_timeout_fires_inside_a_long_scan_rather_than_only_between_matches`, and it
passed when run alone. Cause: `\b\B` over a megabyte. `try_match` now refuses `\B` itself, so
`SearchStart`'s retry loop walked the whole subject without returning to the matching loop, and
nothing polled the clock after the first check. Alone, a cold JIT had spent the 1 ms before that
first check, so the test passed by luck. Fixes:

- `SearchStart`'s retry loop polls on the same `Iterations` gate as `SimpleStringSearch`.
- The test makes one warm-up call first. With the warm-up and without the poll it fails every
  time ("no exception was thrown"). With the poll it passes.

### Correctness evidence: the differential sweep

Harness in `.scratch/tm-sweep/` (gitignored, so rebuild it from this description). It is a console
project referencing the library, built twice: once against the working tree and once against a
worktree of the base commit 8730ce4. Patterns are every prefix in `""`, `(?i)`, `(?fi)`, `(?r)`,
`(?ri)`, `(?rfi)`, `(?m)`, `(?s)`, `(?w)`, `(?V1)(?i)` and `(?b)` joined to each of these 64
bodies:

```
abc|abd  (?:ab|ac)d  (?:ab|AC)x?  (?:ss|ß)x  (?:ab|cd)  (?:a|b)*c  (?:ab)*c  (?:ab)+?c
(?:ab){2,3}  (?:[ab]c)*  (?:\w\d)+  (?:^a|b)  (?:\bab|c)  (?:.b|c)*  (?:\p{L}x)*  (ab)*
(ab|cd)*e  (?:a|ab)(?:c|bcd)  (?:x|xy)*z  (?:a.)*abc  .*?abc  (?:ab)*ab  (?:İ|x)  (?:ﬃ|y)z
(?:😀a|b)  (?:ab|cd){e<=1}  (?:(?:ab){e<=1}|cd)x  (a|b)*?b  (?:$|a)b?  (?:\Z|b)  (?:\B|a)c
(?:[^a]b|a)+  (?:SS|x)*  (?:ab|a)*?c  (?:abc)?abd  (?:ab)?(?:abc)+  (?:xy|x)+y  (a)|(ab)
(?:\d|\s)+  (?:\X|a)b  (?:(?=a)|b)a  (?:(?<=a)b|c)  (?:a{2}|b)+  (?:ff|ﬀ)i  (?:K|k)+
(?:ab)*+c  (?>ab|a)b  (?:ab|ac)*?(?:ad|ae)  (?:x|(ab))*d  (?:\m|\M)a  (?:ss)*ß  (?:[a-c]|z)+?
(?:i̇|x)  (?:ı|I)+  (?:ff|x)  (?:xs|y)  (?:fi|x)y?  (?:sx|s)  (?:abc|abd)+  (?:😀😀|x)+
(?:😀x|😀)  (?:(ff)|(f))+  (?:ffi|ff)i?  (?:is|s)
```

The subjects are `abcabdacd`, `ABab ACac`, `xyxyzxz`, `ßssSSxﬃz`, `😀a😀b`, `aab\nab cd`,
`İiIı x`, `ab1 c2 d`, `ffiﬀiKk`, the empty string, `cdeabe`, `xßyﬃ` and `😀😀x😀`. One row per
UTF-16 `(beginning, length)` slice, mid-pair included. Each row records ten calls: `Match`,
`MatchAtStart`, `FullMatch`, `Matches` and overlapped `Matches`, each with and without `partial`.
It prints the span, the partial flag and every group.

**Result: 325,952 rows, byte-identical between the base and the final code** (the final code
includes the poll). The first, narrower generator (244,728 rows, without the last ten bodies and
the last two subjects) was also identical. The generator is exhaustive, so there is no seed to
vary.

The AtEnd guard in END_GREEDY_REPEAT's body test, which comments called unreachable, is reached on
786 rows both before and after. `(?:ab)*c` over `ab` with `partial` reaches it; regex 2026.9.10
answers `(0, 2)` partial for search, match and fullmatch, and so does the port. That is now pinned
by `PartialMatchingTests.A_repeat_body_and_tail_that_both_run_out_are_partial_over_the_repeat`.

### Negative controls

Each control is one edit in `Matcher.cs` and a count of rows that differ from the base output. C, D
and E were run against the 325,952-row generator. A and B were run against the 244,728-row one and
were not re-run on the wider generator. All five ran before the poll was added. The poll changes
no answer without a timeout, and the final sweep matches base exactly.

- Control A, one-char step dropped. In `TryMatch`, change
  `nextPosition = new Position(next.MatchNext!, Step(state, textPos, next.MatchStep));` to
  `nextPosition = new Position(next.MatchNext!, textPos);`. **16,852 rows differ.**
- Control B, string end inverted. In `TryMatchString`, change
  `end = next.MatchStep == 0 ? textPos : pos;` to `end = next.MatchStep != 0 ? textPos : pos;`.
  **6,218 rows differ.**
- Control C, forward fold tail off. Below `// The string ended part-way through a character's
  folding (:7473).`, change `if (foldedPos < foldedLen)` to
  `if (foldedPos < foldedLen && foldedLen > 99)`. **54 rows differ**, for example `(?i)(?:ff|x)`
  over `ﬃ`.
- Control D, reverse fold tail off. Below `// (:7546.)`, the same change. **84 rows differ**, for
  example `(?ri)(?:SS|x)*`.
- Control E, zero-width arm always fails. Change
  `status = TryMatchZeroWidth(state, test, textPos);` to `status = MatchStatus.Failure;`.
  **12,082 rows differ.**

C fired only after the generator was widened with the fold bodies and subjects (the first run gave
0). It is the thinnest of the five.

### Bytes, not time

S89 was running beside this sitting, so no time is claimed. `ManyInputs`, `--job short
--inProcess --memory`, after then before (the base from the 8730ce4 worktree):

| Row | Allocated before | Allocated after |
|---|---|---|
| RedactDigits | 32,838,650 B | 32,838,650 B |
| ParseLogLines | 44,800,456 B | 44,800,498 B |
| FuzzyPhraseThreeNamedList | 0 | 0 |

ParseLogLines differs by 42 B in 44.8 MB. `TryMatch` allocates nothing, so that is likely
measurement noise. A `--job dry` run showed differences of about 300 B each way, which is the
first-call noise a dry job carries. The short-job means (busy machine, not evidence) were 145 ms
before and 156 ms after on the named-list row, and within a few per cent elsewhere.

**Filters for the orchestrator's quiet run:** `ManyInputs` rows `ValidateEmails`, `ParseLogLines`,
`RedactDigits` and `FuzzyPhraseThreeNamedList`, which run through branches and repeats whose body
or tail is a one-character, zero-width or string test, and `CountStringMegabyte` as the floor check.
Item 3 is an optimisation, so if the gate shows no gain the answer is to revert `TryMatch` to the
S31 form, not to keep it for fidelity: the sweep shows either form gives the same answers.

### Verification

Suite 6777/6777, ratchet GREEN. Permanent files: PartialMatchingTests 55/55, BacktrackingVerbTests
20/20, ReverseMatchingTests 40/40. Oracle GREEN at all three default seeds. AOT 6774 passed, 3
skipped; AOT smoke GREEN, 6.69 MB.

### Review

One blind pass (Opus, the VERIFICATION.md brief, scope the uncommitted diff, the hunt list above):
**No defects found.** It wrote its own sweeps rather than reusing ours. The first had 70 new
bodies (lookbehind strings, `\K`, conditionals, recursion, group calls, atomic and possessive
groups, `\G`, single-character repeats before string tails, surrogates, and the folds `ß`, `ﬃ`,
`ﬆ`, `ẞ`, `ǰ`, `ΐ`, `İ` and the Kelvin sign) under 10 prefixes: 300,560 rows, 0 differ from base.
The second had 17 fuzzy bodies under 7 prefixes, `(?b)` and `(?e)` included: 31,031 rows, 0 differ.
It also ran 8 patterns over 2^21 `a`s through every public entry point with a 1 ms timeout and
a cancelled token: the same exception type as base every time. Suite 6777/6777, oracle GREEN at
three seeds. Nothing to reproduce, nothing fixed, no second pass needed.

It reported one thing outside this diff, and base behaves the same: `\b\B` over 2^21 `a`s with a
1 ms timeout returns False after about 120 ms. The subject has no boundary, so
`SearchStartZeroWidth` scans the whole of it, and that loop has no poll. It is in STATE.md as a
finding.

## 2026-09-24, 00:50 - item 6 closed as never ported; item 17 landed

### Item 6: neither encoding arm is ported, ever

Both arms reproduce a known upstream bug, so porting them would add a bug the port does not have.
Evidence from regex 2026.9.10 (`.scratch/item6.py`, 2026-09-23):

- `same_char_ign_turkic`: `(?fi)fi` over `fı` matches `(0, 2)`, but `(?fi)(f)i` and `(?fi)i` give
  None. The first answer comes from the Turkic comparison in `string_search_fld`; the other two
  come from the matcher's own fold, and they disagree.
- `*_has_property_ign` under ASCII: `(?ai)\p{Ll}*` over `KsKK` matches `(0, 0)` for both match and
  search, but `(?ai)\p{Ll}+` gives `(0, 2)` for match and `(1, 2)` for search, and `(?ai)\p{Ll}`
  over `A` gives `(0, 1)` for match and None for search. The search-start sweep and the matcher
  answer differently.

S22's three pins of these inconsistencies are now permanent under the owner rule of 2026-09-12. The
comments in `Encodings.cs` and `Matcher.cs` say so, PORTMAP rows 131, 260 and 263 say "Never
ported", the two OPTIMISATION-NOTES rows are gone, and DECISIONS has the 2026-09-23 entry. A later
`string_search_fld` must compare with `SameCharIgn`. S22's three self-inconsistencies have no
upstream-report ledger entry; that is in STATE.md as a finding.

### Item 17: what it became

The item asks for PCRE2's leading `.*` auto-anchoring. Upstream already has most of it: after a
failed attempt, the FAILURE arm of `basic_match` (`_regex.c:15719`) skips the run of characters a
leading single-character repeat matched. Under DOTALL that run is the rest of the subject, so
`(?s).*a\d` is linear. But the arm tests `start_node` itself, and a capture group in front makes
the start node a `START_GROUP`. So `(?s)(.*)a\d` retries from every position, in upstream and in
the port:

| Characters | regex 2026.9.10 | Port before | Port after |
|---|---|---|---|
| 2,000 | 75.8 ms | 175.8 ms | 1.10 ms |
| 4,000 | 301.6 ms | 706 ms | 2.20 ms |
| 8,000 | 1,239 ms | 2,643 ms | 4.30 ms |
| 16,000 | | 10,388 ms | 8.83 ms |

Stopwatch, Release, one warm-up call, over `'a' * n`, with another session running (probe
`.scratch/ds/`; upstream from `.scratch/lr.py`). `(?:.*)` and `(?>.*)` were already fast in both.

`PatternObject.FindLeadingRepeat` now looks through the capture groups in front of a greedy repeat,
and the FAILURE arm uses the result (`PatternObject.LeadingRepeat`). The skip is sound when the
failed attempt tried every end position in the run, because an attempt starting inside the run can
only reach those same end positions. A greedy repeat tries them all. A lazy one need not: the
blind review found that `(*SKIP)`, `(*PRUNE)` or a partial run-out can stop it short, so the lazy
repeat is not looked through. A group round the repeat changes only what the group holds, so the
look-through is also guarded on nothing reading it: no referenced group (backreference or
`(?(n)...)`), no group call. It is off for fuzzy and reversed patterns. The fuzzy guard is the slice's own; control C below
shows no row that needs it. Ledgered in SYNC-DIVERGENCE.

Not done: a top-level alternation whose every branch starts with `.*`, which PCRE2 also anchors.
The start node there is a branch, and no benchmark row has that shape.

### Correctness evidence: the differential sweep

Harness in `.scratch/lr-sweep/` (gitignored). A console project built against this tree and
against a worktree of the base commit 52ba79b. Patterns are every prefix in `""`, `(?i)`, `(?s)`,
`(?m)`, `(?r)`, `(?p)`, `(?b)`, `(?e)`, `(?w)`, `(?V1)` and `(?fi)` joined to each of these 93
bodies:

```
(a*)b  (a+)b  (a*?)b  (a+?)b  (a{2,})b  (a{0,2})b  (a*)  (a+)  (a*)$  (a*)\b  ((a*))b
(?<n>a*)b  ((a*)b)c  (a*b)c  (a*)(b)  (.*)b  (.*?)b  (.*)ab  (.+)b$  (\w*)\d  (\d+)x
([ab]*)c  ([^b]*)b  (\s*)\w  (a*)(?=b)  (a*)(?!a)  (a*)(?<=a)b  (a*)\Gb  \G(a*)b  (a*)\Kb
(a*)(*SKIP)b  (a*)(*PRUNE)b  (a*)(*COMMIT)b  (a*)(*FAIL)|b  (a*)b|c  (a*)b|(a*)c
(a*)(?:b|ab)  (a*)a  (a*)aa  (a*)(a)  (a*+)b  (?>(a*))b  (a*)b(?1)  (a*)\1b  (a*)(?(1)b|c)
(a*)b{e<=1}  (?:(a*)b){e<=1}  (😀*)x  (ß*)s  (a*)\B  (a*)^b  (a*)\Z  (a*)(?:)  ()a*b  (a)*b
(?:a*)b  a*b  (?:(a*))b  (a*)b(a*)  (x*)y*z  (a|b*)c  (a*)(?r:b)  (a*)\X  (a{0,3})b
(a{1,2})c  (a{0,1})b  (a{2})b  (.{0,2})x  (\d{0,2})x  (a{0,2}?)b  ((a{0,2}))b  (a{1,3})a
([ab]{0,2})c  (\s{0,1})\w  (a{0,2})b{e<=1}  (a{0,2})\1b  (\w+) \1  (a*)b\1  ((a*))\2b
(?<n>a*)(?P=n)b  (a+)\1  (\d*)x\1  (a*?)(*SKIP)b  (a*?)(*SKIP)  (a*?)(*PRUNE)b
(a{0,2}?)(*SKIP)b  (a*?)\G  (a*?)\B  (a*?)(?<=aa)b  a*?(*SKIP)b  (a*)(*SKIP)  (a+?)(*PRUNE)
```

The subjects are `aaab`, `aaac aab`, `aabab`, `baaa`, `aaaa`, `abab\nab`, the empty string, `a`,
`123 45x`, `  ab`, `😀😀x`, `ßssx`, `aAaB`, `xyyz xz`, `aaa\nb`, `aaaab ab` and `12x 123x`. One row
per UTF-16 `(beginning, length)` slice. Each row records ten calls: `Match`, `MatchAtStart`,
`FullMatch`, `Matches` and overlapped `Matches`, each with and without `partial`, with the span,
the partial flag and every group.

**Result: 400,411 rows, byte-identical between the base and the final code.** 11 patterns fail to
compile in both. Narrower generators of 211,431, 325,611 and 356,411 rows were identical on the
code before the review's fix too; the last ten bodies were added from the review's finding and
catch it (control D). The generator is exhaustive, so there is no seed to vary.

### Negative controls

Each control is one edit, and a count of rows that differ from base. All four were re-run against
the final code and the final 400,411-row generator.

- Control A, reference guard off. In `PatternObject.FindLeadingRepeat`, change
  `&& !pattern.GroupInfoList.Exists(static group => group.Referenced);` to
  `&& !pattern.GroupInfoList.Exists(static group => group.Referenced && false);`. **2,420 rows
  differ**, over five bodies: `(a*)\1b`, `(a{0,2})\1b`, `((a*))\2b` and `(?<n>a*)(?P=n)b` at 576
  rows each, and `(a*)b\1` at 116. On the first generator it was 386 rows, all `(a*)\1b`. The
  conditional `(a*)(?(1)b|c)` gives no row, because group 1 is always set once the pattern starts.
- Control B, skip at the maximum. In `Matcher.BasicMatch`, FAILURE arm, change
  `if (skipCount < leadingRepeat.Values[2])` to `if (skipCount <= leadingRepeat.Values[2])`.
  **452 rows differ**, over six bounded bodies (`(a{0,2})b`, `((a{0,2}))b`, `(a{0,1})b`,
  `(a{0,3})b`, `(.{0,2})x`, `([ab]{0,2})c`), and only under `(?i)` and `(?fi)`. Before the
  review's fix it was 504, the extra 52 from the lazy `(a{0,2}?)b`.
  Why the other prefixes give no row was not traced; a search-start prefilter that moves past the
  run is the likely reason. On the first generator it was 22 rows over one body, which is why the
  bounded bodies were added.
- Control C, fuzzy guard off. In `FindLeadingRepeat`, delete the `!pattern.IsFuzzy` line and its
  `&&`. **0 rows differ.** The guard is caution, not a proven need.
- Control D, lazy look-through back. In `FindLeadingRepeat`, change
  `return node.Op == Opcode.GreedyRepeatOne ? node : null;` to
  `return node.Op is Opcode.GreedyRepeatOne or Opcode.LazyRepeatOne ? node : null;`. **4,344 rows
  differ**, over seven bodies: `(a*?)(*SKIP)` 1,552, `(a*?)\G` and `(a*?)\B` 924 each,
  `(a*?)(*SKIP)b`, `(a*?)(*PRUNE)b` and `(a{0,2}?)(*SKIP)b` 288 each, `(a*?)(?<=aa)b` 80.

### Verification

After the review's fix: suite 6779/6779, ratchet GREEN (baseline 6668 to 6671 distinct ids). The
first suite run failed
`ThreadSafetyTests.Every_field_reachable_from_a_compiled_pattern_is_write_once_or_allowlisted` on
the new field; `LeadingRepeat` is now on its allowlist with the writer named. Permanent files:
PartialMatchingTests 55/55, BacktrackingVerbTests 20/20, ReverseMatchingTests 40/40. Oracle GREEN
at all three default seeds (7, 4242, 20260924; 0 diverge). AOT 6779 tests, 0 failed, on three
runs; one earlier run after the fix exited 2 and the failing test's name was not captured, and it
did not come back in three re-runs on a machine another session was loading. AOT smoke GREEN,
6.69 MB. `tools/check-sync-divergence.ps1` GREEN.

**Filters for the orchestrator's quiet run:** none of the `ManyInputs` rows opens with a capture
group round a single-character repeat, so no existing row should move. Run `ValidateEmails`,
`ParseLogLines` and `RedactDigits` as a no-regression check on the one extra null test per failed
attempt. The gain itself is asymptotic and pinned by
`OptimiserTrapsTests.A_leading_repeat_inside_a_capture_group_is_skipped_like_a_bare_one`.

### Review

Two blind passes (Opus, the VERIFICATION.md brief).

**First pass, the whole diff: one finding, reproduced, fixed.** The look-through reached lazy
repeats too, and that changed answers: `(?i)(a*?)(*SKIP)b` over `aab` gave None where base and
regex 2026.9.10 give (2, 3), and `(a*?)(*SKIP)` over `aaab` gave two empty matches where both give
five. Its own sweep of 225,438 rows differed on 40 patterns, every one a lazy leading repeat inside
a group. I reproduced it with `.scratch/lazy.py` against upstream and the reviewer's harness, wrote
the failing test `A_lazy_leading_repeat_inside_a_capture_group_is_not_skipped`, and restricted the
look-through to greedy repeats. The reviewer's sweep is then identical to base, and control D
above catches the old code. The item 6 claims checked out against upstream.

**Second pass, the fix only: no defects found.** Its sweep of 96 greedy-focused bodies under 8
prefixes over 39 subjects, verbs, `\K`, `\G`, lookbehinds, bounds, folds and surrogates included,
gave 672,808 rows and 0 differences. It removed the lazy guard and saw the new test fail, then put
it back. Suite 6779/6779; Release build with analyzers, 0 warnings.

It reported one bug outside this change, which base has too and I reproduced: `(?i).{0,2}x` (and
`(.{0,2})x`) over `"😀x😀😀x"` with a slice that cuts a surrogate pair throws
`IndexOutOfRangeException` from `MatchState.CharAt` at beginning 1, length 1, and `FullMatch` and
`Matches` never end at beginning 4 or 6. Repro `.scratch/rv2/Program.cs`. It is in STATE.md.

### The rest of S60b in one sitting

Open: items 8, 9, 11, 12, 13, 14 and 16, each independent of the others. What worked for items 3
and 17: a failing timing test first, then the change, then a base-vs-new sweep built from
`.scratch/lr-sweep/Program.cs` with the item's bodies swapped in, then two or three controls
re-run on the final code. Items 9 and 14 use the same `SearchValues<string>` machinery, so build
it once for both. Items 11 and 16 both reject start positions before an attempt, at the same two
call sites. To finish in one sitting, take them in the order 9+14, 16, 11, 12, 13, 8, sweep each,
and leave the quiet-machine timings to the orchestrator as items 3 and 17 did. An item whose
quiet-run gain is nil is reverted, not kept.

## 2026-09-24, 01:40 - item 14 landed: an alternation of literals searched at once

### What it is

A single required literal is found with one vectorised `IndexOf`. A pattern that opens with an
alternation had nothing: `search_start` turns the fast search off for a branch, and the zero-width
first-set check the compiler puts in front of the branch screens one character. So `cat|dog` tried
every position that holds a `c` or a `d`, and an alternation of many words tried nearly every
position.

`PatternObject.FindLeadingStrings` walks from the start node through capture groups, nested
branches and the first-set check, and collects each branch's first literal. If every path ends in
a case-sensitive `String` or `Character` node, the literals become one `SearchValues<string>`
(Ordinal) at compile time. Before each search attempt `Matcher.BasicMatch` jumps to the earliest
of them in the rest of the slice, or fails when there is none. The engine still picks the branch.

Shapes the compiler already covers get nothing new: branches that share a prefix are split, so
`abc|abd` starts with a `String`, and single characters become a set, so `a|b` has no leading
strings.

Guards, and why:

- Search only, not partial: a partial match can end part way through a literal (control A).
- A literal holding a surrogate code unit is refused: a search for a lone low surrogate can stop
  inside a pair, where the engine never starts (control B). An astral literal such as `😀a` is a
  code point above 0xFFFF and is kept.
- No `(*SKIP)`, not fuzzy, not reversed: every start-position jump in the port leaves these alone.
  Controls C and D show no row that needs the fuzzy or SKIP guard; a fuzzy section opens with a
  `FUZZY` node, which the walk refuses anyway.
- Case-folded literals are refused. `(?i)zebra|yak` is unchanged (23.3 ms before and after).

`SearchValues` rather than item 10's one cached `IndexOf` per literal: that cache lives in one
`basic_match` call, so a literal absent from the text would be rescanned on every match.

### Timings

Stopwatch probe `.scratch/ds/` against the base worktree's `.scratch/ds-base/`, Release, both with
`<TieredCompilation>false</TieredCompilation>`, one megabyte of random English words, median of five
`Matches(...).Count`, another session running (2026-09-24):

| Pattern | Before | After |
|---|---|---|
| `straße\|café` | 14.88 ms | 0.12 ms |
| `alpha\|bravo\|charlie\|delta\|echo` | 23.89 ms | 0.12 ms |
| `zebra\|yak` | 14.08 ms | 0.12 ms |
| `(?:zebra\|yak)\d` | 13.58 ms | 0.11 ms |
| `(zebra\|yak)` | 14.16 ms | 0.12 ms |
| `cat\w+\|dog\d` | 15.23 ms | 1.31 ms |
| eight-word `amber\|basil\|...` | 33.06 ms | 0.11 ms |
| `quick\|lazy\|woods` | 19.15 ms | 9.57 ms |
| `fox\|dog` (28,926 matches) | 17.13 ms | 3.66 ms |
| `(?:zebra\|yak\|)x` (14,570 matches) | 3.90 ms | 3.10 ms |
| `(?i)zebra\|yak` (not covered) | 23.32 ms | 23.46 ms |

Tiering matters here. With tiered compilation on, the first 100 ms or so of
`SearchValues<string>.IndexOfAny` calls run tier-0 code at 300 to 500 ns a call against 8 to 13 ns
warm, and the first probes showed false regressions on dense hits for that reason. A short-lived
process that matches a dense alternation a few thousand times pays that start-up cost.

regex 2026.9.10 takes 162 ms for the four-megabyte search in the new test
`OptimiserTrapsTests.An_alternation_of_literals_searches_for_all_of_them_at_once`, which failed on
the base code with `RegexMatchTimeoutException` at its 25 ms budget.

### Correctness evidence: the differential sweep

Harness `.scratch/ls-sweep/` (gitignored), `python run.py <base|new> <label>`, built against this
tree and against the base worktree at 1182e76. Prefixes `""`, `(?i)`, `(?s)`, `(?m)`, `(?r)`,
`(?p)`, `(?b)`, `(?e)`, `(?w)`, `(?V1)`, `(?f)`, joined to each of these bodies:

```
ab|cd  ab|bc  abc|bcd  ab|b  a|bc  x|yz  ab|cd|  ab|cd|e  (ab|cd)  (ab)|(cd)  (?:ab|cd)e
(ab|cd)\1  (ab|cd)(?1)  (?:ab|cd)(?=c)  (?:ab|cd)(?!d)  (?<=a)(?:bc|cd)  (?:ab|cd)\b
\b(?:ab|cd)  (?:ab|cd)$  (?:ab|cd)\Z  \G(?:ab|cd)  (?:ab|cd)\G  (?:ab|cd)\Kx
(?:ab|cd)(*SKIP)x  (?:ab|cd)(*PRUNE)x  (?:ab|cd)(*COMMIT)x  (?:ab|cd)(*FAIL)|d
(?:ab|cd)x{e<=1}  (?:ab|cd){e<=1}  (?:ab|cd)*  (?:ab|cd)+  (?:ab|cd)?x  (?>ab|abc)d
(?:ab|abc)d  (?:foo|)bar  (?:a(?:b|c)|d)e  ((?:ab|cd))x  (?:ab|cd)|x+  a+|bc  [ab]c|de
😀a|b😀  😀|x  \ud83d|a  \ude00|x  \ude00a|yz  (?:ß|ss)x  (ab|cd)(?(1)x|y)  (?:ab|cd)(?r:c)
(?:ab|cd)\X  (?:aa|ab)*?b  (?:ba|ab)+b  (?:a|ab)(?:c|bcd)  (?|(ab)|(cd))x  (?P<n>ab|cd)(?P=n)
(?:abc|bcd|cde|def)  (?:x|xy|xyz)z  (?:ab|cd)[^x]  (?:ab|cd).  (?:ab|cd)^x
```

Subjects `abcd`, `xcdab`, `bcdabc`, `abab cdcd`, the empty string, `a`, `yyzx`, `fobarbar`,
`abde cde`, `😀ab😀`, `ßssx sx`, `abcdcx`, `aabab`, `abx cdx` and `"😀a"`, one row per
UTF-16 `(beginning, length)` slice. Each row records `Match`, `MatchAtStart`, `FullMatch`,
`Matches` and overlapped `Matches`, each with and without `partial`, plus `Split` and `Replace`.

**Result: 246,917 rows, identical between base and the final code.** The generator is exhaustive,
so there is no seed to vary.

### Negative controls

One edit each, then reverted; the count is rows that differ from base on the final generator.

- Control A, partial guard off. In `Matcher.BasicMatch`, change
  `search && state.PartialSide == MatchState.PartialNone && !pattern.HasSkipVerb` to
  `search && !pattern.HasSkipVerb`. **121,401 rows differ.**
- Control B, surrogate guard off. In `PatternObject.FindLeadingStrings`, change
  `if (node.Values.Exists(static c => c is >= 0xD800 and <= 0xDFFF))` to append `&& false`, and
  replace `char.ConvertFromUtf32((int)c)` with
  `c <= 0xFFFF ? ((char)c).ToString() : char.ConvertFromUtf32((int)c)` so that a lone surrogate
  converts. **45 rows differ**, all `\ude00a|yz`, under nine prefixes.
- Control C, fuzzy guard off. Delete `pattern.IsFuzzy ||` from the first line of
  `FindLeadingStrings`. **0 rows.**
- Control D, SKIP guard off. Change the condition in control A to
  `search && state.PartialSide == MatchState.PartialNone`. **0 rows.**
- Control E, walk accepts anything. In `FindLeadingStrings`, change `default: return false;` to
  `default: return true;`. **4,959 rows differ**, among them `ab|cd|`, `(?i)(?:ab|cd)|x+` and
  `(?i)ab|b`.

### Verification

Suite 6780/6780, ratchet GREEN (baseline 6671 to 6672 distinct ids), after adding
`PatternObject.LeadingStrings` to the `ThreadSafetyTests` allowlist. Permanent files:
BacktrackingVerbTests 20/20, PartialMatchingTests 55/55, ReverseMatchingTests 40/40. Oracle GREEN
at all three default seeds. AOT smoke GREEN, 6.84 MB. AOT tests 6780, 0 failed, on two runs; the
first run, made while the AOT smoke was publishing beside it, exited 2 and its failing test was not
captured. A re-run under the same load was green. That is the second such unexplained exit on this
slice (see item 17's notes). `tools/check-sync-divergence.ps1` GREEN.

Item 14 had no `ponytail:` comment and no OPTIMISATION-NOTES row to delete.

**Filters for the orchestrator's quiet run:** no `ManyInputs` row opens with an alternation of
case-sensitive literals, so none should move. The gain is pinned by the new timing test.

### Review

One blind pass (Opus, the VERIFICATION.md brief): **no defects found**, so no second pass. It
compiled each pattern twice and nulled `LeadingStrings` on one copy by reflection, which isolates
the jump. A targeted sweep of 75 patterns over 19 subjects (32,150 rows, 25 patterns jumping) and a
random fuzz at seeds 1, 2 and 3 (814, 2,205 and 2,162 jumping patterns) gave 0 differences. It
covered empty branches, anchors, `\K`, `\G`, lookarounds, verbs, atomic groups, recursion,
conditionals, scoped `(?i:...)`, full folds, `(?r)`, `(?V1)`, `(?w)`, astral literals, lone
surrogates in the subject, every slice, overlapped and partial matching, `Split` and `Replace`.
Suite 6780/6780, oracle tests 34/34, Release build 0 warnings. Its comparison with Python differed
on 1,275 rows with the jump on and off alike, so none come from this change. I sorted them
(`.scratch/ls-review/cmp2.py`): 519 are `Replace` rows given Python's `<$0>` syntax; the rest are
slice labels (UTF-16 against code points), a compile error labelled differently where both
engines refuse `(?:ab(*COMMIT)x|cd)` and `(?:ab(*ACCEPT)|cd)x`, and 508 `(?r)` partial rows
where regex 2026.9.10 gives None (`.scratch/revp.py`), which is ledger entry 24, the upstream
reverse-partial bug the port fixes on purpose. Probes in `.scratch/ls-review/` and
`.scratch/ls-fuzz/`.

## 2026-09-24, 02:45 - item 9 landed: long alternations and named lists indexed by first code units

An alternation of n members compiles to n - 1 chained 2-way `BRANCH` nodes, and a named list is
one of them. Upstream's `BRANCH` arm (`_regex.c:12079`) calls `try_match` on each alternative in
turn, so a list of a thousand words costs a thousand tests at every position the list is tried.
The slice spec suggested a per-list `SearchValues<string>` or trie. A trie would have to replace
the branch walk and keep its order, captures and backtracking, which is a second matcher; the
index below keeps upstream's walk and only skips the steps that cannot succeed.

`Engine/BranchChain.cs` indexes each run of at least eight alternatives (`MinAlternatives`), when
at most half of them have no key. An alternative's key is the first one or two UTF-16 code units
of its test node, when that test is a non-fuzzy forward case-sensitive `STRING`, or a `CHARACTER`
with `Match` set. On entering a chained branch node with `TextPos < SliceEnd`, the matcher jumps
to the first alternative from there that can start with the text's next units, or to the run's
tail. It uses the second unit only when that unit is inside the slice too. Why that is sound:
`TryMatchOne` and `TryMatchString` fail on a mismatch inside the slice, and a failed `try_match`
pushes nothing, so a skipped alternative is one upstream would have tried and dropped.

First-unit keys alone were not enough. About forty of the thousand members share each first
letter, and `\b\L<w>\b` over a megabyte went from 1,840 ms to only 269 ms. Keying on pairs took
it to 54 ms.

### Timings

`.scratch/ds` list mode: a megabyte of words, Release, tiering off, 1,000 members.

| Pattern | Before | First unit | Two units | regex 2026.9.10 |
|---|---|---|---|---|
| `\L<w>` | 135.9 ms | 25.2 ms | 10.6 ms | 3,523 ms |
| `\b\L<w>\b` | 1,840 ms | 269 ms | 54.0 ms | 1,030 ms |
| ` \L<w>` | 1,852 ms | 226 ms | 18.9 ms | 1,000 ms |
| `\w+ \L<w>` | 6,282 ms | 825 ms | 167.6 ms | - |
| `(?i)\L<w>` | 9,275 ms | not indexed | 9,426 ms | 4,637 ms |

Ten members, against 1182e76 (before item 14, which also helps `\L<w>` at the top level):
`\b\L<w>\b` 31.9 to 25.2 ms, ` \L<w>` 24.5 to 12.3 ms, `\w+ \L<w>` 195 to 140 ms. No short run
got slower.

Case-insensitive lists are the obvious next step and are not done: a folded key needs every
case variant of the first code point, and full folding (`ß` against `ss`) breaks the "one test,
one first unit" rule. Upstream is twice as fast as this port on `(?i)\L<w>`.

### The timing test

`A_long_named_list_tries_only_the_members_that_can_start_here` searches a megabyte for
`\b\L<words>\b` with 1,001 members inside a 500 ms budget. It times out without the index (Build
call commented out, run alone). Run alone it takes 80 ms; inside the full suite it took 700 ms,
because the rest of the suite runs alongside it, so it is `[NotInParallel]`, which brought it back
to 79 ms in the suite. The ten-member list beside it pins upstream's answers for one- and two-unit
members, a shared first unit, an astral first character, `(*SKIP)`, an anchor, and a partial match
whose second unit is past the slice.

### Sweep and controls

`.scratch/ls-sweep/Program.cs`, rewritten for item 9: 41 bodies, runs of eight or more
alternatives and two named lists (`l` holds 16 members including the empty string, `😀x`, lone
`\ud83d` and `\ude00`, `ß` and `ss`; `p` holds nine members sharing `a`), under 11 flag prefixes,
over every slice of 23 subjects, each slice asked `Match`, `MatchAtStart`, `FullMatch`, `Matches`
and overlapped `Matches` with and without partial, plus `Split` and `Replace`. Base is the
worktree `.claude/worktrees/base-8dd746e` at 1182e76. **214,291 rows, 0 differ.**

The sweep is exhaustive, so there is no seed to vary. Controls, applied one at a time by
`.scratch/ctl9.py <letter>`, which edits the file, runs the sweep and restores it:

- A, in `Matcher.BasicMatch`: `if (node.Chain is { } chain && state.TextPos < state.SliceEnd)`
  to `... && state.TextPos < state.Text.Length)`. **0 rows.** No row reaches a branch past the
  slice end, so this guard and the index bound agree on every row swept.
- B, in `BranchChain.Skip`: `if (textPos + 1 < sliceEnd)` to `if (textPos + 1 < text.Length)`.
  **2,295 rows.**
- C, in `BranchChain.Prefix`: `!(test.Op == Opcode.String ||` to
  `!(test.Op is Opcode.String or Opcode.StringIgn or Opcode.StringFld ||`. **230 rows.**
- D, in `Prefix`: `(test.Op == Opcode.Character && test.Match)` to `test.Op == Opcode.Character`.
  **3,132 rows.**
- E, in `Skip`: `int next = Next(_unkeyed, from);` to `int next = int.MaxValue;`, so no unkeyed
  alternative is tried. **15,980 rows.** (Before the review fix, when every list repeated the
  unkeyed ones, the same fault was `=> [.. keyed.Concat(unkeyed).Order()]` to
  `=> [.. keyed.Order()]` in `Index` and gave 7,157.)
- F, in `Prefix`: `test is null\n            || (test.Status & NodeStatus.Fuzzy) != 0` to
  `test is null`. **3,108 rows.**
- G, in `Skip`: `if (_byFirstOnly.TryGetValue(first, out int[]? only))` to
  `if (from < 0 && _byFirstOnly.TryGetValue(first, out int[]? only))`, so a one-unit member is
  never tried when the next two units are in the slice. **3,683 rows.**

Every number above is from the final code, after the review fix: A 0, B 2,295, C 230, D 3,132,
E 15,980, F 3,108, G 3,683, and the unfaulted sweep 0 of 214,291.

C and D found nothing on the first generator, which had no uppercase subject and no negated
single-character alternative; the numbers above are after adding `ABcd AB`, `SSx ßX`, `xYZab`,
`cabd` and three bodies.

### The oracle wave

The spec asks for an oracle wave over list-heavy patterns, and every existing generator draws
two to four alternatives, below the threshold. New generator `long-alternation` in
`tools/record-oracle.py`: eight to fourteen members of up to three characters from
`a b c x y ß 😀`, as an alternation or a named list, in one of 14 settings under 8 flag prefixes,
with partial matching and slices. It has no lone surrogate: the consumer's JSON reader refuses one
("Cannot read incomplete UTF-16 JSON text as string with missing low surrogate"), so the sweep
covers those.

At 2,000 rows: seed 7 agree 1,998, expected 2, diverge 0; seed 4242 one divergence and seed
20260924 two. The first run of seed 20260924 was misread as one; the blind review counted two.
All three are `search` with `partial=True` and `(*SKIP)` and carry `searchOnlyPartial` true.
Rows 1441 and 1065 are case-insensitive, which the index does not touch, and row 377 diverges
the same with the Build call commented out:

- seed 20260924 row 377: `(?:ac|yb|yb|ß|yy|yx|😀y|aab|cb|y|a|cb|ba)(*SKIP)b|c` over
  `'x b😀😀a'`: upstream partial (2, 6) code points, port partial (7, 1) UTF-16, which is
  upstream's own `pruneOutcome`.
- seed 4242 row 1441: `(?i)(?:\L<words>)(*SKIP)b|c` over `'ß 😀b'`, words
  `['', 'a', 'b', 'baa', 'by', 'b😀', 'cbx', 'xa', 'xa', 'ßy', '😀x', '😀y']`: upstream partial
  (0, 4) code points, port partial (4, 1).
- seed 20260924 row 1065: `(?i)(?:bx|b|c😀|😀a|😀ß|a|xb|cx)(*SKIP)b|c` over `'yabbßbayßx😀'`
  sliced at UTF-16 [5, 12): upstream partial (5, 11) code points, port partial (10, 2).

They look like the search-start family in `ExpectedDivergences.cs` (upstream's search reports a
partial its own match denies), but that family is judged row by row, and a judged divergence
needs the amendment-16 evidence and a verifier. So `long-alternation` stays off the default list,
like `fuzzy-overhang`, with the three rows named in `tools/run-oracle.ps1`'s help. Judging them is
open work for S60b.

### Verification

Suite 6781/6781, ratchet GREEN (baseline 6672 to 6673 distinct ids) after adding `Node.Chain` and
`Node.ChainIndex` to the `ThreadSafetyTests` allowlist. BacktrackingVerbTests, PartialMatchingTests
and ReverseMatchingTests pass inside that run. Oracle GREEN at all three default seeds.
`tools/check-sync-divergence.ps1` GREEN with the new row. AOT: not run to the end.
`tools/run-aot-tests.ps1` started at 02:26 on a machine with under 1 GB free, and its compiler
(csc, PID 44916) sat at 64 CPU seconds from then on. It was left running, not killed. The AOT
suite is still owed for item 9.

Item 9 had no `ponytail:` comment and no OPTIMISATION-NOTES row of its own to delete.

**Filters for the orchestrator's quiet run:** no `ManyInputs` row uses a named list or eight
alternatives, so none should move. The gain is pinned by the timing test.

### Review

One blind pass (Opus) over item 9's whole diff. It compared every answer with the index on and
off: 14,300 generated patterns, 6,063 of them indexed, 0 differences. It raised two findings, and
both reproduced.

1. **Quadratic memory.** `Index` copied the unkeyed alternatives into every key's list, and a
   pair's list also copied its first unit's one-unit list. With half of n alternatives unkeyed and
   the rest on distinct first units, that is about n^2/4 entries per dictionary. The reviewer's
   probe measured a 494 MB build at 16,000 alternatives and 1,966 MB at 32,000. Reproduced with a
   new test, `An_indexed_alternation_with_many_unkeyed_members_builds_in_linear_space`: 8,000
   alternatives, the build asked to stay under 8 MB, and it allocated 131,338,720 bytes. Fixed.
   Each list now holds only its own keyed alternatives, and `Skip` takes the nearest entry from up
   to three lists (the pair or first-unit list, the one-unit list, the unkeyed list). The test
   passes. The probe now gives 4 MB at 16,000 and 9 MB at 32,000, and whole-pattern compile went
   from 8.3 s to 2.9 s at 32,000. The cost is one more lookup per chained branch. `.scratch/ds`
   list mode at 1,000 members, on the same loaded machine: `\L<w>` 10.6 to 11.1 ms,
   `\b\L<w>\b` 54 to 58 ms, ` \L<w>` 18.9 to 20.7 ms, `\w+ \L<w>` 168 to 180 ms. That is
   still 17 to 30 times faster than before item 9. The sweep and controls above were re-run after
   the fix.
2. **Wrong count in `tools/run-oracle.ps1`'s help.** Seed 20260924 gives two divergences, not
   one; the second, row 1065, is case-insensitive and so not indexed. Fixed in the help text and
   in the oracle section above.

The fix changed `BranchChain.Skip` and `Index`, so a second blind pass (Opus, same brief,
told not to run the suite because memory was short) reviewed that delta only. It called `Skip`
directly, 2,597,284 times over 1,751 indexed runs at every start index, text position and slice
end, and compared each answer with a plain scan of the old merged lists. It also compared 900
patterns (388 indexed) with the index on and off. It found 0 differences and 9 MB for the
32,000-alternative build, and returned "No defects found."

## 2026-09-24, 03:15 - the three `long-alternation` rows judged; the generator joins the default list

The wave was re-run at 2,000 rows and gave the same three rows: seed 4242 row 1441 and seed
20260924 rows 377 and 1065. `python tools/probes/gate-divergence-doors.py 4242 20260924` and
`python tools/probes/upstream-partial-anchor-reachability.py
tools/probes/s60b-long-alternation-partial-search-rows.jsonl` put the family's questions to each.

| Row | Upstream | Port (codepoints) | Upstream's span in its own grid | `(*PRUNE)` | Verb deleted |
|---|---|---|---|---|---|
| 1441 | (0, 4) partial | (3, 4) partial | no | (3, 4) partial | (3, 4) complete |
| 377 | (2, 6) partial | (5, 6) partial | no | (5, 6) partial | (5, 6) partial |
| 1065 | (5, 11) partial | (10, 11) partial | no | (10, 11) partial | (10, 11) partial |

On each, the port's span is in the grid and is the first anchor that answers. That is the
standard rows 14 to 27 of `search-start-partial`'s second arm were judged by, so the three became
its rows 28 to 30. Row 377's upstream span starts at 2, not 0, at the first character that
can start a member (the subject is `x b😀😀a`), so like row 16 it is not the whole searched region.

After the edit: `pwsh -File tools/run-oracle.ps1 -Generator long-alternation -Count 2000` GREEN at
seeds 7, 4242 and 20260924 (expected 2, 3 and 4, diverge 0), and the default wave, now with
`long-alternation`, GREEN at all three seeds.

### Verifier and review

The verifier (Opus, fresh) re-ran every claim with its own script against regex 2026.9.10: for each
row, upstream's search answer, its absence from the grid, the port's span as the first answering
anchor, the `(*PRUNE)` and verb-deleted answers, and the port's rendered answer through
`run-oracle.ps1 -Rows`. 18 of 18 CONFIRMED. One blind review pass (Opus) re-ran both waves, checked
that the embedded rows equal the probe file and that the two arrays line up (30 entries each), and
ran the oracle suite, 34 of 34 including `Every_expected_divergence_still_diverges`. It returned
"No defects found." No code changed after either, so no second pass.

### Item 9's owed AOT run

At 46314d8 (item 9's code unchanged since 4e8f887): `tools/run-aot-tests.ps1` GREEN, 6782 tests,
6779 passed, 3 skipped by design, 0 failed. `tools/run-aot-smoke.ps1` GREEN, 7,244,800 bytes
(6.91 MB), up from 6.84 MB after item 14 and 6,972,928 bytes at S60.
