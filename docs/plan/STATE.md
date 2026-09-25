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

1. Upstream `(?b)(?:(?:x){d<=1}){1,3}y` over 'y' gives no answer in 20 s; the port gives (0, 1)
   with one deletion. Not investigated (ledger 33).
2. A non-fuzzy search over text outside the BMP allocates in proportion to the text (S61 notes).
3. CANDIDATE INHERITED BUG, ledger 2's family (2026-09-25 triage, not yet researched or blind-
   reviewed). A lazy repeat before a one-character tail reports a partial that cannot complete:
   `regex.match(r'(?r)ab??', 'c', partial=True)` is (0, 1) partial on both engines, where
   upstream's greedy `(?r)ab?`, its repeat-free `(?r)a` and its forward mirror `b??a` are all None.
   The port carries upstream's guard as-is at `Matcher.IsTailPartial` (`CharacterRev` arm, and
   likely the forward `Character` arm, which S31 pinned on `([^a-f]{3,}?)x` over '__AAb').
4. `(?e)(?:(?:abcd){s<=4}|(?:(?:x){d<=1})+)` over 'zzzz': upstream (0, 4) with four
   substitutions, the port (0, 0) with one deletion. Predates ledger 33's change (the 2026-09-25
   engine review measured it at b265dc3 too). Not triaged.
5. Classifier coverage, from the same triage. `turkic-default-folding` claims only rows with spans
   (`TurkicLettersCovered` has no arm for `sub`/`split`), so every fresh seed that draws a Turkic-I
   substitution needs its row added to the row-keyed `-without-spans` sibling (seven on
   2026-09-25). And `full-fold-backreference-leftovers`' doubled-guard arm claims rows with no
   backreference, e.g. `(?b)(\p{L}){i,d}c` over '\nc', so it is wider than its name.
