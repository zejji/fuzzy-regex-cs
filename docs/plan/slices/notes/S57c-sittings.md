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
