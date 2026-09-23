# Structural divergences from upstream's shape

The port mirrors `upstream/regex/_regex_core.py` in `Parsing/` and `upstream/src/_regex.c` in
`Engine/`, structure and all, so that a future upstream diff maps onto our files mechanically. Phase
7 optimises, and an optimisation that changes the shape of a function - splits it, inlines it,
reorders its arms, replaces its data structure - makes that mapping stop being mechanical at exactly
that point.

Owner ruling, 2026-09-18 research review (c): **the divergence is allowed; the note is compulsory.**
A future `sync-upstream` slice must be able to find every place where the two no longer line up
without reading the whole engine, and must be told what to do when upstream's own version of that
code changes.

**This file is the index, the source is the record.** Every divergence carries a comment at the line
and the table below names it, the same pairing `OPTIMISATION-NOTES.md` uses for deferrals.

```
grep -rn -i 'sync-divergence:' src/FuzzyRegex --include=*.cs
```

`tools/check-sync-divergence.ps1` enforces both halves - a marker with no row, and a row with no
marker - and the ratchet calls it, so neither half can be forgotten. It is deliberately
file-granular: line numbers move under CSharpier and under the next edit, and a check that goes red
on a reformat is a check people learn to bypass.

## What goes here, and what does not

- **Here:** the port's code no longer has upstream's shape at that point. A rewritten loop, a
  different data structure, a function upstream has and we do not (or the reverse), an arm
  reordered so the fast case comes first.
- **Not here:** a *behavioural* difference from upstream. That is `docs/DIVERGENCES.md` if it is
  deliberate, and an oracle divergence with a ledger entry if it is not.
- **Not here either:** an optimisation we have *not* done. That is `docs/plan/OPTIMISATION-NOTES.md`.

## The marker

At the line, in a comment:

```csharp
// sync-divergence: <what upstream does> / <what we do instead> / <why>.
// Re-aligning: <what a sync slice should do when upstream touches this>.
```

## How to write a row

One row per divergence. The minimum gain that justifies one stays deferred until S62 has concrete
examples to set it from (S58 scope item 7); until then the measured gain is recorded and the owner
judges it.

| Column | What it must say |
|---|---|
| Where | The file, as a path under `src/FuzzyRegex`, in backticks. The script reads this cell. |
| Upstream's shape | What `_regex.c` or `_regex_core.py` does there, with its line reference. |
| Ours | What this port does instead. |
| Measured gain | The number that bought the divergence, and the benchmark that produced it. An unmeasured divergence is not a divergence, it is a rewrite. |
| Re-aligning | What a `sync-upstream` slice does when upstream changes that code. |
| Decided | Slice and date. |

## The ledger

| Where | Upstream's shape | Ours | Measured gain | Re-aligning | Decided |
|---|---|---|---|---|---|
| `Engine/Matcher.cs` (`StringSearch`), `Engine/PatternObject.cs` (`ReqStringText`) | `string_search` (`_regex.c:6596`) picks between `simple_string_search` (`:5231`) and `fast_string_search` (`:5846`), and `build_fast_tables` (`:6298`) builds Boyer-Moore bad-character tables lazily ON THE NODE, under a lock, the first time a node is searched for | One `MemoryExtensions.IndexOf` over the slice, against a needle built once in `Compile`. No tables, no lock, no lazy mutation. `SimpleStringSearch` stays as the fallback for a needle holding an unpaired surrogate and for the partial-match retry | `IndexOf` sweeps 100,000,000 code units of a non-matching subject in **14.1 ms** (measured 2026-09-20, Release, this machine, busy). **The head-to-head against a ported `fast_string_search` was not run**: no such port exists, and writing one to race it is work this divergence exists to avoid. The decisive argument is not speed anyway - it is S52b's contract, which `ThreadSafetyTests` enforces: a compiled pattern is immutable and shareable, and upstream's lazily-built table is a write to a shared node | If upstream changes `build_fast_tables` or the `fast_string_search` skip loop, nothing here has to move - the search is not a transliteration of it. If upstream changes WHICH POSITIONS `string_search` may return (its bounds, its partial handling, its `req_pos` caching), that is ours to follow, and it is in `LocateRequiredString` rather than here | S60, 2026-09-20 |
| `Engine/FuzzyLiteralFilter.cs` | `basic_match` (`_regex.c:11767-11814`) runs no prefilter under a fuzzy section: the required-string search is off, because an error can delete the character it would look for, so every start position is attempted | A pattern that is exactly one fuzzy ASCII literal with a bounded budget `k` is cut into `k + 1` pieces (Navarro 2001, section 8.1). Before each forward attempt, `Matcher.BasicMatch` moves the start to the earliest position an untouched piece allows, or refuses the subject when no piece occurs; a reverse search only refuses. Off for partial matching and for a non-ASCII stretch of subject | `ManyInputs` `FuzzyPhraseOneIsMatch` **2.424 s to 64.2 ms**, `FuzzyPhraseThreeSeparatePasses` 7.438 s to 164.9 ms, allocation 92.39 MB to 87.18 MB (2026-09-23, Release, `--inProcess`, back to back on this machine with other sessions idle) | Nothing to follow unless upstream adds a fuzzy prefilter of its own, or changes what a fuzzy section can match: an error that damages two characters at once would break the one-edit-one-piece argument in the type's remarks | S60b, 2026-09-23 |
