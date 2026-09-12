---
slice: S41
phase: 5
title: ENHANCEMATCH - do_enhanced_fuzzy_match and the capture save/restore trio
delivers: [fuzzy-enhancematch]
---

# S41 - `ENHANCEMATCH`

The smaller of the two "improve the match" modes: after finding a fuzzy match, try again inside
its span with a tighter error limit until the fit stops improving. Depends on S40. Six tests.

All line references are `upstream/src/_regex.c` unless marked.

## Scope

- **`do_enhanced_fuzzy_match`** (`:17862-18026`) replaces the `fuzzy-enhancematch` seam at
  `Matcher.cs:6588`. Read the whole loop before porting: it narrows `slice_start`/`slice_end` to
  the previous best match's span, lowers `max_errors` to one below the best so far (not below
  `RE_MAX_ERRORS`, which is 10, `:203`), and ends on a perfect match or on the first run that fails
  to improve.
- **The dead `same_match` check is NOT ported as live code** (owner decision 2026-09-12, DECISIONS).
  At `:17942-17943` upstream computes `same_match` and overrides it to `FALSE` on the next line, so
  the `same_span_of_group` loop never runs and the early exit `if (same_match || ...)` reduces to
  `total_errors == 0`. Evidence it is deliberate: the 2014 and 2015.09 releases had the check live
  (`if (same) break;`) in one combined best/enhanced loop, and release 2015.11.5 - the Hg issue 165
  "Performance / hung search" rework that split the loop into `do_simple`/`do_enhanced`/`do_best` -
  introduced `same_match` already overridden. Analysis: the check is only an EARLIER exit; honouring
  it can never find a match the current code misses and can lose one (a run that improves 2 to 1
  errors on the same span stops there instead of trying for 0), and the override's whole cost is
  one failing run per enhanced match on an already-narrowed slice. Port the effective behaviour;
  leave the two upstream lines and the loop as a comment quoting `:17942-17955`, and move
  `same_span_of_group` (`:11646`) to PORTMAP's deliberately-not-ported table with this evidence.
- **Experiment (cheap, do it):** port the honoured check behind an internal `static` switch for the
  duration of the slice, run the `fuzzy` generator with `(?e)` both ways at three seeds, and count
  `BasicMatch` runs per row. Expected: results identical or better with the override, and at most
  one extra run per row. Quote the counts in the closing notes and remove the switch before commit.
- **Ranking rule, shared with S42.** `better = state->total_errors < fewest_errors` (`:17930`) ranks
  runs by error COUNT. Owner decision 2026-09-12: this port ranks by COST (`total_cost`, `:9649`),
  ties by fewer errors, then earliest, for both `(?e)` and `(?b)`. Port upstream's count ranking
  first and get the tag green, then switch, exactly as S42 does; the two slices must use one
  comparison helper. With unit costs the two rules agree, so every ported test is unaffected; the
  divergence is confined to cost equations and is pinned by a gap test plus an
  `ExpectedDivergences` entry whose predicate requires `(?e)` and a non-unit cost.
- **`save_captures` / `restore_groups` / `discard_groups`** (`:17403`, `:17468`, `:17500`), whose
  only callers are this function and S42's. `GroupData.Copy` already snapshots live spans (the
  S32 `save_best_match` precedent); decide whether these three collapse onto it or need upstream's
  shape for S42's repeated use, and say which in PORTMAP.
- **`save_fuzzy_changes` / `restore_fuzzy_changes`** (`:9899`, `:9930`) if S38 did not already port
  them for the POSIX copy.
- The restored `slice_start`/`slice_end` at exit (`:18005-18006`) is what makes a following scan
  step correct: pin it with a `finditer` test under `(?e)`.

## Verification

- Un-skip `needs:fuzzy-enhancematch` (6 tests: `FuzzyEnhanceMatchTests.cs` and the flagged
  siblings elsewhere, each of which sits beside a note naming its unflagged sibling per DECISIONS
  2026-08-30).
- Gap tests: a match `(?e)` improves and one it cannot; a cost equation where cost ranking and
  count ranking disagree, asserting the cost-ranked answer with upstream's answer quoted beside it; `(?e)` with `(?r)`; `(?e)` in a scan; the
  counts and changes of the improved match.
- **Add `(?e)` to the `fuzzy` generator** on every body shape. GREEN at three seeds, 2000 rows.
- Negative controls: `max_errors` not lowered between runs; the slice not narrowed; the best
  groups not restored when the last run is worse.

## Done when

- [ ] Tag delivered; counts in closing notes.
- [ ] `fuzzy` generator with `(?e)` green at three seeds.
- [ ] PORTMAP rows for the five ported symbols; `same_span_of_group` in the deliberately-not-ported
      table with the 2015 evidence; experiment counts in the closing notes.
- [ ] Ratchet GREEN, baseline updated, blind review (hunt: `max_errors` allowed to reach
      `PY_SSIZE_T_MAX` again after the first run; groups restored from a stale snapshot), commit.
