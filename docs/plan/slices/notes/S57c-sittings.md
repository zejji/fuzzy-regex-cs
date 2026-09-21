# S57c sittings

Per-sitting working notes. The slice's spec is `../S57c-the-anchor-pin-the-engine-can-restore.md`.

## Sitting 1 (2026-09-21)

Landed the fix, the tests, the probes and the oracle entry. Stopped on the allowance hook with the
documentation and the review outstanding.

### What the fix is

Upstream bans a fuzzy insertion at one position per matching operation:
`permit_insertion = !search || text_pos != search_anchor` (`upstream/src/_regex.c`:10214), with
`search_anchor` fixed once in `init_match` (`:3410`). The ban exists so that a search does not spend
an insertion to start one character early, which a plain search would find anyway by advancing. When
the pattern begins with a position assertion, though, the search cannot advance to a later start:
`\b(?:abc){i<=1}` over `"xabc"` can only begin at 0, because that is the one position where `\b`
holds, and upstream's ban costs it the whole match - it answers `[]` where this port answers
`['xabc']` (measured 2026-09-21 on regex 2026.9.10; sitting 2 corrected this paragraph, which had
quoted a row where upstream and this port agree). That is issues 563 and 564, ledger entries 19
and 20, which are one bug.

The port narrows the ban. `Optimiser.FindAnchorGuards` collects the pattern's leading position
assertions at compile time into `PatternObject.AnchorGuards`, and `Matcher.AnchorIsPinned` permits
the insertion only where a guard holds AT the anchor and FAILS one character on. That second
conjunct is the whole of the narrowing: it says the assertion pins the match here and nowhere
adjacent, so no later start can find the same match and the insertion is not buying an early start.

### The one-step-on narrowing is load-bearing

S50 measured it against upstream's own `test_fuzzy` rows 51, 52, 54 and 56, on its own
implementation of the pin. Sitting 1 re-measured it on the current tree - see Control A below, and
the correction in sitting 2: against the shipped rule the fault reddens rows 51 and 56, not four.

### Control A, the negative control

In `src/FuzzyRegex/Engine/Matcher.cs`, in `AnchorIsPinned`, drop the one-step-on conjunct. The file
reads:

```csharp
        foreach (Node guard in guards)
        {
            if (
                TryMatchZeroWidth(state, guard, state.SearchAnchor) == MatchStatus.Success
                && TryMatchZeroWidth(state, guard, onePastAnchor) == MatchStatus.Failure
            )
            {
                return true;
            }
        }
```

and the fault replaces that `if` with:

```csharp
            if (TryMatchZeroWidth(state, guard, state.SearchAnchor) == MatchStatus.Success)
```

**Result on the ported suite** (`dotnet run --project tests/FuzzyRegex.Tests -c Release`):
6504 total, **4 failed**, against 0 failed on the restored code. The four:

- `Answers_what_upstream_answers(Fuzzy matching against a word list)` - the demo example built on
  upstream's `test_fuzzy` word-list rows.
- `A_word_start_anchor_before_a_fuzzy_section_matches_at_position_zero_here` - gives `{"x abc"}`
  where upstream and the port both answer `{" abc"}`.
- `A_fuzzy_named_list_finds_each_word_in_turn` - gives `{"cot", " dog"}` for `{"cot", "dog"}`.
- `A_fuzzy_named_list_searching_backwards_reports_matches_leftmost_first`.

**Result on the oracle wave**: it does not fire. `pwsh -File tools/run-oracle.ps1 -Generator
fuzzy,interactions -Count 6000 -Seeds 7` gave `agree 11891  unsupported 0  expected 36  timeout 2
resource 71  diverge 0  of 12000 rows`, `Oracle: GREEN`, with exactly the same four rows classified
`fuzzy-insertion-at-a-pinned-anchor` as the unbroken run and the same 36 expected rows overall. The
broken narrowing changed no wave row's answer at that seed.

That is a finding about the generators, not a tick, and it is the honest record: **the suite is this
rule's instrument, the wave is not**. The shape the narrowing decides is narrow - a leading position
assertion that holds at the anchor, still holds one character on, and a fuzzy section with insertion
budget immediately after it - and the wave draws its assertions and its fuzzy sections
independently, so it lands on that conjunction about four times in 12,000 rows and never with a
subject where the two rules differ. Sitting 2 should either widen a generator to reach it or record
in the closing notes that it did not, with these numbers.

Open point for sitting 2: the second seed. The skill wants every control re-run at a seed the slice
has not used. Since the control does not fire on the wave at all, the second seed belongs on the
wave figure (to confirm the zero is not a seed accident), not on the suite figure, which has no
seed.

### Where the slice stands

Done: `Optimiser.FindAnchorGuards`, `PatternObject.AnchorGuards`, `Matcher.AnchorIsPinned` and its
eight call sites, the `ThreadSafetyTests` allowlist entry, three tests in `InheritedIssueTests`, the
Python and PowerShell probes with their sections 1-7, `tools/probes/s57c-anchor-pin-rows.jsonl`, the
`fuzzy-insertion-at-a-pinned-anchor` entry in `ExpectedDivergences.cs` keyed on an ablation
(`OracleComparer.RunWithoutTheAnchorPin`), and the `OracleWaveTests` guard that the entry accounts
for nothing it should not.

Measured green on this tree: ported suite 6504/6504, `OracleTests` 27/27, the default wave at seeds
7, 4242 and 20260921, and `-Count 6000` at the same three seeds (agree 125532/125503/125527,
expected 475/508/494, diverge 0/0/0).

Still to do: the `docs/DIVERGENCES.md` row, `docs/PORTMAP.md`, ledger entries 19 and 20 marked
fixed, the ratchet and its baseline, the blind review, the independent verifier over the judged
rows, and the closing notes.

## Sitting 2 (2026-09-21, 14:22-16:07) - stopped before it could commit

Took the sitting-1 open point to an answer, wrote the documentation, ran the review and the
verifier, and was stopped at 16:07 because the owner needed the machine quiet. Nothing was
committed. The work was kept as stash `b37c93a` and the branch `rescue/s57c-sitting2`, and sitting
3 applied it onto `main` rather than starting again; read that sitting's notes for what survived
and what did not. Everything below is sitting 2's own record, left as it wrote it. No engine code
changed: the only edit to `src/FuzzyRegex/Engine/Matcher.cs` since the checkpoint commit `45d3f2f` is
the `AnchorIsPinned` remarks paragraph at `:2013-2016`, corrected by the verifier finding below. The
controls break and restore the file in place.

### Why Control A cannot fire on a generator wave

Sitting 1 left one question: the control reddens the ported suite but not the wave. Sitting 2 ran
it down and the answer has two halves, both measured.

**Half one: a new generator did not reach the shape.** `fuzzy-anchored` was added to
`tools/record-oracle.py` for this - the `fuzzy` generator with one of `\b`, `\m`, `\M`, `$`, `\B`
prepended to every pattern, so every row has the leading assertion the rule needs. (It is a
separate generator rather than a widening of `fuzzy`, because each generator draws from
`random.Random(f"{seed}:{name}")`: widening `fuzzy` in place reshuffled its whole stream and
surfaced an unrelated pre-existing divergence, seed 7 row 331. The `if guarded:` branch takes no
draw, so `fuzzy`'s own stream is byte-identical at every seed.) On the fixed code the new
generator is green - 2000 rows at seeds 7, 4242 and 20260921 give agree 1997/1992/1995, expected
3/8/5, diverge 0. With the fault applied it gives **the same rows, the same counts, at all three
seeds**. The extra shape the fault needs is narrow: an assertion that holds at the anchor AND one
character on, before a fuzzy section with insertion budget, over a subject where the two rules
pick different matches. Prepending an assertion is not enough to produce it by chance.

**Half two: the family's own entry would absorb it if it did.** `fuzzy-insertion-at-a-pinned-anchor`
is keyed on an ablation - empty `PatternObject.AnchorGuards` and the port reproduces upstream's
recorded answer - and that ablation removes the mechanism, not the rule's shape. A broken shape is
still restored by emptying the field, so it is classified as the family rather than reported.
Measured on seven directed rows, `tools/probes/s57c-one-step-on-rows.jsonl`:

| Code | Entry | Result |
|---|---|---|
| shipped | live | GREEN, 1 expected, 0 diverge |
| shipped | disabled | 1 diverge (the judged row) |
| fault | live | **GREEN, 3 expected, 0 diverge** |
| fault | disabled | 3 diverge (rows 1 and 4 are the fault's) |

The third line is the finding: a wave run over rows that the broken rule answers wrongly is green.
No predicate that answers by running this engine can do better, because the reference run is
broken too; the S48b precedent of keying on recorded rows and answers does not fit a family that is
open-ended by construction. So the limitation is written into the entry's own `Reason` text and
into the control test's comment, and the rule's shape is held by tests that carry upstream's
answers instead: the three `InheritedIssueTests`, and the ported `test_fuzzy` named-list rows.

### Controls, as run against the committed code

**Control A, `anchor-pin-without-the-one-step-on-test`** (registered as `S57c-A` in
`tools/controls.json`, so `python tools/run-controls.py --ids S57c-A` applies and reverts it). In
`src/FuzzyRegex/Engine/Matcher.cs`, in `AnchorIsPinned`, replace

```csharp
            if (
                TryMatchZeroWidth(state, guard, state.SearchAnchor) == MatchStatus.Success
                && TryMatchZeroWidth(state, guard, onePastAnchor) == MatchStatus.Failure
            )
```

with

```csharp
            if (TryMatchZeroWidth(state, guard, state.SearchAnchor) == MatchStatus.Success)
```

- Wave `fuzzy-anchored`, 2000 rows, seed 7: agree 1997, expected 3, **diverge 0**. Seed 4242:
  agree 1992, expected 8, diverge 0. Seed 20260921: agree 1995, expected 5, diverge 0. Identical to
  the unbroken code at all three seeds, rows included. **The control does not fire on the wave**,
  for the two reasons above.
- Ported suite, `dotnet run --project tests/FuzzyRegex.Tests -c Release`: **4 of 6504 red**, against
  0 red on the restored code. `Answers_what_upstream_answers(Fuzzy matching against a word list)`,
  `A_word_start_anchor_before_a_fuzzy_section_matches_at_position_zero_here`,
  `A_fuzzy_named_list_finds_each_word_in_turn` (`test_fuzzy#51`) and
  `A_fuzzy_named_list_searching_backwards_reports_matches_leftmost_first` (`test_fuzzy#56`). Rows
  52 and 54 stay green: S50's "51, 52, 54 and 56" was measured against S50's own implementation of
  the pin, and this sitting's verifier caught the figure being repeated as if it described the
  shipped rule. Re-measured 2026-09-21 on the committed code, twice.
- Directed rows, `pwsh -File tools/run-oracle.ps1 -Rows tools/probes/s57c-one-step-on-rows.jsonl`,
  7 rows, no seed: **3 diverge with the entry disabled, against 1 on the restored code.** To
  disable the entry, change its `Applies` lambda in
  `tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs` from
  `static (row, ours) => OnlyTheAnchorPinExplainsIt(row, ours)` to
  `static (row, ours) => row.Number < 0 && OnlyTheAnchorPinExplainsIt(row, ours)`. (`false &&` does
  not build: `error S1125: Remove the unnecessary Boolean literal(s)`.)

### Upstream answers re-measured today

Section 8 of `python tools/probes/issue-563-anchor-rule.py`, added by this sitting because a
sentence in sitting 1 had quoted a row where upstream and this port agree:

```
8. what the one-character-on test decides
  \b(?:abc){i<=1}              over 'xabc'       -> []
  \b(?:abc){i<=2}              over 'x abc'      -> [' abc']
  \b(?:abc){i<=2}              over 'ab abc'     -> [' abc']
  \B(?:abc){i<=2}              over 'xy abc'     -> ['y abc']
```

The empty answer is the bug: `\b` holds at position 0 of `'xabc'` and nowhere else, so upstream's
ban on an insertion there costs the whole match, and this port answers `['xabc']`. The other three
are rows where a later start finds the same match, so the ban changes nothing and both engines
agree - which is exactly what the one-character-on test preserves.

### The new generator comes off the default list, and S57e takes the row it found

The blind review ran `fuzzy-anchored` at a seed this slice had not used and the wave went red:

```
pwsh -File tools/run-oracle.ps1 -Generator fuzzy-anchored -Count 2000 -Seeds 1234567
DIVERGE row 1982 (fuzzy-anchored) finditer flags=0x0 version=V0
  pattern  '(?b)(?r)\m(?:.fo){e<=2}'
  subject  'x fx'
  upstream matches 1 | match 0:(0,4)[(0,4)] last=-1/- fuzzy=(1,1,0)[s:4][i:2][d:]
  port     matches 1 | match 0:(0,4)[(0,4)] last=-1/- fuzzy=(1,1,0)[s:4][i:1][d:]
```

Reproduced here, same row, same numbers. Same span, same error counts, same substitution position;
the engines disagree only on where the insertion is recorded. It is not this slice's rule - under
`(?r)` the leading `\m` is not at the head of the reversed graph, so `PatternObject.AnchorGuards`
is empty on that row and `AnchorIsPinned` never runs, and an ablation that empties the field leaves
both answers unchanged. Upstream is inconsistent with itself there, which is where S57e should
start: `(?r)\m(?:.fo){e<=2}` over `'x fx'` records the insertion at index 1 and `(?b)(?r)\m(...)`,
same span and counts, records it at 2.

Judging that needs amendment 16's whole ceremony, so it is S57e rather than a widening of this
slice. Meanwhile the generator is **off** `run-oracle.ps1`'s default list, because the default
third seed is `Get-Date -Format yyyyMMdd`: a generator red at some seeds is red on some days, and
every later slice would inherit it. Recorded as design spec amendment 36 with the matching ROADMAP
paragraph. The hold-out is written in both tools, so S57e has two paragraphs to delete.

### Review

One blind pass, one reviewer, on the brief from `docs/VERIFICATION.md`. **Four findings raised,
four reproduced, four fixed** - an unusually high survival rate for this repo's four-in-five rule,
and all four were of the same kind: a number or a file that did not match what the text claimed.

1. `fuzzy-anchored` red at seed 1234567, the row above. Fixed by taking the generator off the
   default list and giving the row S57e, rather than by judging it here.
2. `tools/probes/s57c-one-step-on-rows.jsonl` was untracked while a shipped source comment named it
   as the reproduction. Committed with the slice.
3. `InheritedIssueTests` said "upstream gives [] to all four" above five rows. Re-measured all five
   against regex 2026.9.10: every one is `[]`. The count was wrong, the answers were not.
4. `ThreadSafetyTests` said the writable-field allowlist names "forty-three" in four places; it
   named 45 before this slice and 46 after it. Counted from the initialiser and corrected.

The independent verifier then re-ran the judged rows from the committed files: the four rows in
`tools/probes/s57c-anchor-pin-rows.jsonl` byte-identical to the `_anchorPinRows` constant, every
upstream answer quoted in `DIVERGENCES.md`, `COMPARISON.md`, `InheritedIssueTests.cs` and the probe
CONFIRMED against a real 2026.9.10 run, both `_regex.c` line references CONFIRMED, and no gap test
resting on this port's own output. It returned **one DIFFERENT**, which is the correction above:
the control reddens `test_fuzzy` rows 51 and 56, not 51, 52, 54 and 56. That figure came from S50,
measured against S50's own `MatchState` implementation of the pin, and three places here had
repeated it as though it described the shipped rule. Re-measured twice on the committed code and
corrected in `Matcher.cs`, `InheritedIssueTests.cs`, `ExpectedDivergences.cs`, `DIVERGENCES.md` and
these notes; the historical statements that name S50 are left as history. The verifier's one other
note is cosmetic and left alone: the `_regex.c` comment quoted at `:10213` spans `:10211-10213`.

A second blind pass ran over the delta the first reviewer never saw - the default-list change, the
two hold-out paragraphs, the count corrections, the S57e slice file, and the spec and ROADMAP
amendments.

*(Sitting 2's record stops here, mid-sentence: it was interrupted before it could write what that
second pass returned. No report of it survives in the tree, so sitting 3 treated the whole delta as
unreviewed and ran a fresh pass over it.)*

## Sitting 3 (2026-09-21, 17:00-19:30) - recovered sitting 2's work and committed the slice

Sitting 2's work was in a stash, not on a branch anyone had rebased, and `main` had moved on: the
`phase9-demo` merge landed between the checkpoint and this sitting. Applying the stash gave three
conflicts, all in files that are generated or appended to rather than edited in place
(`docs/STATUS.md` and `tests/parity-baseline.json` taken from `main` and regenerated, the sitting
notes merged by hand). Nothing in `src/` conflicted, and the engine's behaviour is exactly the
checkpoint's: `git diff 45d3f2f -- src/FuzzyRegex/Engine/Matcher.cs` shows one hunk, the
`AnchorIsPinned` remarks paragraph, and no executable line.

### Checking sitting 2's claims rather than inheriting them

A recovered draft is a set of assertions by a session that is no longer here to defend them, so each
central claim was re-run before it was kept. All of these reproduced exactly:

- Both 6000-row waves, at seeds 7, 4242 and 20260921: GREEN, diverge 0, with the same agree and
  expected counts sitting 1 recorded.
- The generator-stream argument: adding `fuzzy-anchored` leaves every other generator's rows
  byte-identical at all three seeds.
- The five rows in `InheritedIssueTests` that the comment says upstream answers `[]`: all five are
  `[]` against regex 2026.9.10.
- The S57e row's two spans and insertion positions under `(?b)`.
- The writable-field allowlist count behind `ThreadSafetyTests`: 46.

One claim did not survive, and it was a real defect rather than a stale number.

### The defect: a documentation example the gate rejected

`tools/check-doc-examples.ps1` reads the expected output of a `docs/COMPARISON.md` example from the
comment after each `Console.WriteLine`. Sitting 2's new section wrote upstream's answer into that
comment as well:

```
FAIL docs/COMPARISON.md #42
  expected: xabc      (upstream: no match)
  actual:   xabc
```

The example was right and the comment was not: the file's own convention puts an aside after ` - `,
where the gate reads it as annotation rather than as output. Fixed by following the convention, and
the example is now pinned by `ComparisonSamples`.
`A_fuzzy_section_may_open_with_an_inserted_character_at_the_search_anchor` runs the snippet and
asserts `xabc`, so the page cannot drift from the engine unnoticed.

### Controls, re-run against the code being committed

Three runs, all after the last code change, all on this tree.

- **Ported suite under Control A:** `4 of 6510 red`, against 0 red on the restored code. The same
  four tests sitting 2 named, `test_fuzzy` rows 51 and 56 among them. The total moved because the
  tree gained tests since sitting 2 measured it (the demo merge, and this sitting's new sample), so
  the figure in `ExpectedDivergences.cs` was corrected from 6,504 to 6,510.
- **Wave `fuzzy-anchored`, 2000 rows:** seed 7 agree 1997, expected 3, diverge 0; seed 4242 agree
  1992, expected 8, diverge 0; seed 20260921 agree 1995, expected 5, diverge 0. Identical to the
  unbroken code, which is the finding sitting 2 wrote up: the control cannot fire on a wave.
- **Directed rows, all four combinations,** `-Rows tools/probes/s57c-one-step-on-rows.jsonl`:

  | | entry live | entry disabled |
  |---|---|---|
  | shipped rule | diverge 0, 1 row classified here | diverge 1 |
  | Control A | diverge 0, 3 rows classified here | diverge 3 |

  The first column is the point of the whole table. Break the rule's shape and the ablation-keyed
  entry absorbs the broken answers instead of reporting them, which is what the entry's own Reason
  text warns about. The second column is the same measurement with the entry switched off, and it
  is how the first column is read as 1 against 3 rather than as two greens.

  It also settles a fair question about the wave result above: a control that changes nothing could
  mean a mutation that never reached the compiled engine. It reached it. The same mutation, the same
  build path, moves this table from 1 to 3.
