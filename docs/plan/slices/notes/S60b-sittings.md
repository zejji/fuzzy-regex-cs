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
