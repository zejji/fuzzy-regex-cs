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
the pattern begins with a position assertion, though, the search cannot advance: `\b(?:abc){i<=2}`
over `"x abc"` has to start at 2, and upstream's ban costs it the leading space, so it answers
`"x abc"` where the same pattern one character later answers `" abc"`. That is issues 563 and 564,
ledger entries 19 and 20, which are one bug.

The port narrows the ban. `Optimiser.FindAnchorGuards` collects the pattern's leading position
assertions at compile time into `PatternObject.AnchorGuards`, and `Matcher.AnchorIsPinned` permits
the insertion only where a guard holds AT the anchor and FAILS one character on. That second
conjunct is the whole of the narrowing: it says the assertion pins the match here and nowhere
adjacent, so no later start can find the same match and the insertion is not buying an early start.

### The one-step-on narrowing is load-bearing

S50 measured it against upstream's own `test_fuzzy` rows 51, 52, 54 and 56. Sitting 1 re-measured it
on the current tree - see Control A below.

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

- `Answers_what_upstream_answers(Fuzzy matching against a word list)` - upstream's `test_fuzzy`
  rows, the ones S50 named.
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

## Sitting 2 (2026-09-21, 14:22-16:07) - interrupted, and its work is kept

The owner needed the machine quiet at 16:13, so the driver and this sitting were stopped mid-run
(an oracle wave and a `dotnet test` were running at the time). No commit was made, and the driver
never reached its own rollback, so **sitting 3 must not start from scratch**: the work is on the
branch `rescue/s57c-sitting2` (also stash `b37c93a`), 18 files and 389 insertions.

Read it before writing anything: `git diff main..rescue/s57c-sitting2`. It carries most of the
documentation list this file's sitting-2 plan names - the `fuzzy-insertion-at-a-pinned-anchor` row
in `docs/DIVERGENCES.md`, new symbols in `docs/PORTMAP.md`, ledger entries 19 and 20, the roadmap
and design-spec edits - plus changes to `Engine/Matcher.cs`, `ExpectedDivergences.cs`,
`OracleWaveTests.cs`, `ThreadSafetyTests.cs`, `InheritedIssueTests.cs`, `tools/controls.json`,
`tools/probes/issue-563-anchor-rule.py`, `tools/record-oracle.py` and `tools/run-oracle.ps1`.

It is unreviewed and was written mid-sitting, so treat it as a draft to check rather than as
landed work: cherry-pick what survives reading, re-run the wave, and take the blind review and the
verifier over the result as the plan already says.

## Sitting 3 (2026-09-21) - finished the slice

`rescue/s57c-sitting2` was diffed against a stale base (`45d3f2f`), so a naive
`git diff HEAD..rescue/s57c-sitting2` showed large unrelated deletions from work HEAD had gained
independently since the checkpoint. Per-file `git log 45d3f2f..HEAD -- <file>` separated the 13
files untouched since the checkpoint (cherry-picked wholesale via `git diff HEAD..rescue/s57c-sitting2
-- <files> | git apply`) from two that had diverged (`docs/plan/ROADMAP.md`, `docs/COMPARISON.md`),
merged by hand from the rescue branch's content.

`tools/probes/s57c-one-step-on-rows.jsonl`, which the rescue branch's own diff referenced as
supporting evidence, was never actually committed there (confirmed via `git ls-tree -r
rescue/s57c-sitting2`). Rather than guess its lost content, it was rebuilt from scratch: 7 directed
rows, reasoned through the engine mechanics and confirmed by running both the shipped code and the
one-step-on-dropped fault against it. That gave four rows classified on shipped code and five under
the fault - different from the interrupted sitting's unverified claim of one vs three - so the
`ExpectedDivergences.cs` prose and the `Matcher.cs` doc comment were written to state the measured
numbers rather than repeat the lost ones. `test_fuzzy` rows 51 and 56 (not S50's original 51, 52, 54,
56) are the two that map to the four failing tests under the fault; the independent verifier
confirmed the mapping directly from each test's `[Property("Upstream", "RegexTests.test_fuzzy#N")]`
attribute (see below).

### Final verification, run in this order against the code committed

1. `pwsh -File tools/check-ratchet.ps1` - GREEN, 6509/6509 passing (6401 distinct ids).
2. `pwsh -File tools/check-ratchet.ps1 -UpdateBaseline` - baseline updated to 6401.
3. `pwsh -File tools/run-oracle.ps1` (default wave, 6380 rows, seeds 7/4242/20260921) - GREEN,
   `diverge 0` at all three seeds.
4. `pwsh -File tools/run-oracle.ps1 -Count 6000` (126,080 rows, same three seeds) - GREEN,
   `diverge 0` at all three seeds.
5. `tools/check-doc-examples.ps1` - 45 ok, 0 fail (from sitting 2's work, re-confirmed unaffected).

### Control A, `S57c-A` (`tools/controls.json`)

In `Matcher.cs`, `AnchorIsPinned`, drop the one-step-on conjunct:

```csharp
            if (
                TryMatchZeroWidth(state, guard, state.SearchAnchor) == MatchStatus.Success
                && TryMatchZeroWidth(state, guard, onePastAnchor) == MatchStatus.Failure
            )
            {
                return true;
            }
```

becomes:

```csharp
            if (TryMatchZeroWidth(state, guard, state.SearchAnchor) == MatchStatus.Success)
            {
                return true;
            }
```

Wave: generator `fuzzy-anchored`, 2000 rows. `python tools/run-controls.py --ids S57c-A`:
seed 7, `agree 1997 expected 3 diverge 0`; seed 4242, `agree 1992 expected 8 diverge 0`; seed
20260921, `agree 1995 expected 5 diverge 0`. Re-run at a fresh seed the control has not used,
13031995: `agree 1990 expected 10 diverge 0`. The classifier absorbs every row the fault reaches
into `expected` rather than `diverge`, which is the ablation's blind spot this control exists to
show - see `ExpectedDivergences.cs`'s "WHAT THE ABLATION CANNOT SEE" paragraph. The directed-rows
probe (`tools/probes/s57c-one-step-on-rows.jsonl`, 7 rows) is what actually distinguishes shipped
from faulted: `pwsh -File tools/run-oracle.ps1 -Rows tools/probes/s57c-one-step-on-rows.jsonl` gives
`expected 4` on shipped code and `expected 5` under the same fault.

### Review

One blind pass, Sonnet, brief scoped to `git diff HEAD` (17 files, 223 insertions/32 deletions) plus
the untracked `.jsonl`. One candidate finding: that `docs/plan/upstream-reports/LEDGER.md` describes
`issue-563-anchor-rule.py`'s "seventh section" (the reversed-direction rows) as added by "S57c's own
probe" although that section's diff hunk sits outside this sitting's changes. Reproduced and did not
survive: the section exists, its content matches the prose exactly, and it was added by S57c's
sitting-1 checkpoint (`45d3f2f`) - "S57c" is the whole slice, not this sitting's diff alone, so the
attribution is correct. No second pass was needed; no fixes went in. `dotnet build`: 0 warnings, 0
errors.

### Independent verifier

One fresh Opus subagent, no sight of the review or sitting 1/2's verdicts, re-ran all nine numbered
claims from the LEDGER, `ExpectedDivergences.cs` and `Matcher.cs`'s doc comment against the committed
tree, applying and reverting the one-step-on mutation itself (never with git) to get the faulted
numbers. All nine CONFIRMED, including the exact failing-test count and names under the fault (4:
the demo word-list example, `A_word_start_anchor_before_a_fuzzy_section_matches_at_position_zero_here`,
and the two `test_fuzzy#51`/`#56` tests) and the Control A seed numbers above. Tree confirmed
byte-identical to pre-verification (`git diff --stat` unchanged) after its revert.
