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

No rows yet. S58 builds the mechanism before Phase 7's optimising slices need it, deliberately: the
first divergence lands in S59 or later, and a ledger written after the fact is one written from
memory.
