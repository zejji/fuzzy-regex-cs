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
2. Classifier coverage: `turkic-default-folding` claims only rows with spans
   (`TurkicLettersCovered` has no arm for `sub`/`split`), so every fresh seed that draws a Turkic-I
   substitution needs its row added to the row-keyed `-without-spans` sibling (seven on
   2026-09-25). Left as it is on purpose: S52's blind review showed that reading the recorded scan
   instead classifies a real defect (`(?i)\w` over 'xı'), and the owner's 2026-09-14 ruling
   is that such pins widen only by judged rows. The cost is a red sweep until someone adds the row.
3. Two `partial-long` `(*SKIP)` partials at seed 11 (`-Generator partial,partial-sliced,
   partial-long -Count 3000`), present before and after the 2026-09-25 work and not judged: row
   8508 `A(?:[\p{L}||\p{N}]{0}(*SKIP)[\p{L}\p{N}]|[\p{L}||\p{N}])A$` (upstream (4969,0) partial,
   port (4968,1) partial) and row 8556 `(?r)\B(?:[a](*SKIP)\W|\p{Lu})([a\d])` (upstream (0,0),
   port (0,2)). Likely ledger 5's slice family. The same wave's other ten are port timeouts.

Done 2026-09-25 on `maint/state-findings`: the lazy-repeat phantom partial (ledger 2, fixed in
`Matcher.IsTailPartial`, S31's pin reversed), the `(?e)` deletions row (judged port-right under
ledger 25), and two classifier leaks (the full-fold doubled-guard arms claiming plain ledger 12
rows, and ablation entries tallying a port timeout). DECISIONS 2026-09-25.
