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

1. A non-fuzzy search over text outside the BMP allocates in proportion to the text (S61 notes).
2. Classifier coverage, narrowed 2026-09-25: a fresh `sub`/`split` row over the dotless i is now
   classified by a recorded control (the dotless i swapped for kra, `dotlessFreeOutcome`), which
   takes all 18 such hand-judged rows on its own. A row holding the dotted capital U+0130 still
   needs its row added to `turkic-default-folding-without-spans` by hand: no letter can stand in for
   it (7 of the 25 listed rows).
3. NEW, not judged: under ASCII|IGNORECASE the case properties answer differently on both engines,
   and neither looks right. Over 'aAéÉĸ' upstream's `\p{Lu}`, `\p{Ll}`, `[[:upper:]]`
   and `[[:lower:]]` match only the one ASCII letter of their own case, although its `[A-Z]` matches
   'aA' under the same flags; this port matches non-ASCII letters there although with ASCII alone
   it keeps them out. Found by the 2026-09-25 dotless-i grid (112 rows of 6,468, no Turkic letter
   involved). Needs research against the docs and a second engine before either is called right.

Done 2026-09-25 on `maint/state-findings`: the lazy-repeat phantom partial (ledger 2, fixed in
`Matcher.IsTailPartial`, S31's pin reversed), the `(?e)` deletions row (judged port-right under
ledger 25), and two classifier leaks (the full-fold doubled-guard arms claiming plain ledger 12
rows, and ablation entries tallying a port timeout). DECISIONS 2026-09-25.
