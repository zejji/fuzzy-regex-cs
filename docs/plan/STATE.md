# Current state
**S90 is done** (`docs/plan/slices/done/S90-partial-fold-rows.md`). The three partial rows over a
full-folded ligature are port-right. Each is a fold repair (ledger 28, 29 or 30) stacked on ledger
11 mechanism B, and a new oracle entry, `full-fold-fix-behind-an-innermost-count`, claims them. No
engine change. The fuzzy and default waves both give diverge 0 at all three seeds. S86, S89 and
S61 are done too; closing notes in `docs/plan/slices/done/`.

**S60b is in flight** (`docs/plan/slices/S60b-search-start-and-the-researched-prefilters.md`, notes
in `docs/plan/slices/notes/S60b-sittings.md`). Its item 3 rewrote `TryMatch` and sends SUCCESS to
the default arm. S86 added a SUCCESS arm there: whichever merges second keeps it (DECISIONS
2026-09-24), or `RepeatTests`'s byte test goes red.

## Queue

1. The orchestrator re-records the full benchmark baseline in process on merged main. S86 asks for
   `*WorkloadBenchmarks*` on the quiet gate: its one hot-path change is a compare in `TryMatch`.
2. S60b item 3 and onwards.

## Waiting on the owner


- O(1) backtracking state per repetition for a body with no alternative (DECISIONS 2026-09-24,
  OPEN). A divergence from upstream in memory only; no code until decided.

## Findings that need a slice

0. **FIRST, before optimisation (owner rule 2026-09-25): a case-insensitive literal, range or set
   inside a scoped encoding folds with the PATTERN's encoding, not the scope's.** `(?i)(?a:k)`,
   `(?a:(?i:k))` and `(?i)(?a:[a-z])` match the Kelvin sign U+212A, and `(?i)(?a:s)` matches U+017F,
   here and upstream; CPython's re refuses them, and `(?ai)(?u:k)` is the reverse. Only property
   nodes carry an encoding today (`NodeStatus.EncodingKind`), so the fix carries it on CHARACTER_IGN,
   STRING_IGN, RANGE_IGN and SET_*_IGN too and uses it wherever those fold: the matcher, string
   search, fuzzy matching, full folding, and every prefilter. Ledger entry 35 names it; the
   consistency grid behind it is 60 of 1,056 questions, all this shape.
0b. **The native-AOT allocation tests are flaky**: `AllocationTests` (S61) fail about one full
   native run in three (4 of 12, 2026-09-25) with 280 B and no GC in the window, in different tests
   (`A_warm_IsMatch_...`, `A_warm_Count_...`, and 7,744 B across two GCs in the span walk). Every test
   passes alone, and the JIT suite never fails. CI runs this gate on three systems, so it will go red
   intermittently. Not yet explained: not a GC trim (a forced gen-2 collection between warm-up and
   measurement allocates nothing under JIT), not a per-call cost (the same calls are 0 B in 8 runs of
   12). Next: an EventListener on GCAllocationTick inside the native run to name the type.

1. A non-fuzzy search over text outside the BMP allocates in proportion to the text (S61 notes).
2. Classifier coverage, narrowed 2026-09-25: a fresh `sub`/`split` row over the dotless i is now
   classified by a recorded control (the dotless i swapped for kra, `dotlessFreeOutcome`), which
   takes all 18 such hand-judged rows on its own. A row holding the dotted capital U+0130 still
   needs its row added to `turkic-default-folding-without-spans` by hand: no letter can stand in for
   it (7 of the 24 listed rows).

Done 2026-09-25 on `maint/state-findings`: the lazy-repeat phantom partial (ledger 2, fixed in
`Matcher.IsTailPartial`, S31's pin reversed), the `(?e)` deletions row (judged port-right under
ledger 25), and two classifier leaks (the full-fold doubled-guard arms claiming plain ledger 12
rows, and ablation entries tallying a port timeout). DECISIONS 2026-09-25.
