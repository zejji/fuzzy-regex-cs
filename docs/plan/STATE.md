# Current state

**S86 is done** (`docs/plan/slices/done/S86-repeated-capture-group-bytes.md`). A repeat now puts
upstream's bytes on the backtracking stack, 82 B a repetition for `fullmatch('(ab)*')` (was 151),
so `(ab)*` over `'ab' * 6_000_000` matches as upstream does. Ledger entry 18's amplification is
fixed; upstream's own cost remains the report.

**S89 and S61 are done**; closing notes in `docs/plan/slices/done/`.

**S60b is in flight** (`docs/plan/slices/S60b-search-start-and-the-researched-prefilters.md`, notes
in `docs/plan/slices/notes/S60b-sittings.md`). Its item 3 rewrote `TryMatch` and sends SUCCESS to
the default arm. S86 added a SUCCESS arm there: whichever merges second keeps it (DECISIONS
2026-09-24), or `RepeatTests`'s byte test goes red.

## Queue

1. The orchestrator re-records the full benchmark baseline in process on merged main. S86 asks for
   `*WorkloadBenchmarks*` on the quiet gate: its one hot-path change is a compare in `TryMatch`.
2. S60b item 3 and onwards.
3. **S90**: three partial rows over a full-folded ligature (`docs/plan/slices/S90-partial-fold-rows.md`).

## Waiting on the owner

- O(1) backtracking state per repetition for a body with no alternative (DECISIONS 2026-09-24,
  OPEN). A divergence from upstream in memory only; no code until decided.

## Findings that need a slice

1. Ledger 33's residuals loop to the 1 GB limit, as upstream does: `(?:(?:(?:x){d<=1})+y){e<=5}`
   over 'y', and a body with a group, `(?:(?(1)c|z)|()(?:x){d<=1})+d` over 'cd' (`SHORTCUT:`s).
2. Upstream `(?b)(?:(?:x){d<=1}){1,3}y` over 'y' gives no answer in 20 s; the port gives (0, 1)
   with one deletion. Not investigated (ledger 33).
3. A non-fuzzy search over text outside the BMP allocates in proportion to the text (S61 notes).
