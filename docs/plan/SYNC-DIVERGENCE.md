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
| `Engine/MatchStateCache.cs`, `Engine/MatchState.cs` (`Init`, `Release`) | `state_init_2` takes `groups_storage`, `repeats_storage` and `stack_storage` from the pattern (`_regex.c:577-579`, taken at `:18300`, `:18341`, `:18500`) under the pattern lock (`acquire_state_lock`, `:20847`), builds the rest of `RE_State` fresh, and `state_fini` hands the three back (`:18684-18711`) | The whole `MatchState` is kept in one slot, taken with `Interlocked.Exchange` and put back with `Volatile.Write`, the shape of the built-in `Regex._runner`. `MatchState.Init` reassigns every field a call can dirty instead of building a new state. No lock | `ManyInputsBenchmarks`, one pattern over 100,000 short inputs: `FuzzyPhraseOneIsMatch` 969 B a call to 0, `ValidateEmails` 1,887 B to 0, `ParseLogLines` 2,176 B to 448 B (S61 sitting 1, `--job short --inProcess`). Time 0.68x to 0.98x of before on all ten rows (`--job medium`, S61 sitting 4 notes; the machine was not quiet, and the quiet-machine run the notes name decides) | A field upstream adds to `RE_State` needs a line in `Init`; `MatchStateCacheTests` compares a reused state with a new one field by field and fails on a forgotten one. A change to what upstream caches changes nothing here, since everything is cached | S61, 2026-09-23 |
