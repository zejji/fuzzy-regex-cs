---
slice: S32
phase: 4
title: POSIX leftmost-longest matching
delivers: [posix-matching]
---

# S32 - POSIX leftmost-longest matching

Eight tests and a small mechanism, placed last because it changes what `SUCCESS` means: under
`RE_FLAG_POSIX` the matcher does not stop at the first match but records it and keeps
backtracking. Everything Phase 4 added to the backtrack paths therefore gets exercised once more,
under a flag that forces exhaustive backtracking.

## Scope

All line references are `upstream/src/_regex.c`.

- **The hook in the `SUCCESS` arm** (`:15178-15186`): if POSIX, `check_posix_match` then
  `goto backtrack`. **The exit in the `FAILURE` backtrack arm** (`:15685-15688`): if
  `found_match`, `restore_best_match` and succeed.
- **The helpers**: `check_posix_match` (`:11602`), `save_best_match` (`:11493` - saves the
  captures too, read it to the end), `restore_best_match` (`:11565`), `same_values` (`:11438`)
  and `equivalent_nodes` (`:11453`). State fields `best_match_pos`, `best_text_pos` (`:493`),
  `found_match` (`:534`), reset at `:3427`.
- **Not this slice**: `init_best_list`/`add_to_best_list` (`:17532`, `:17552`) and the rest of
  the `RE_BestList` family are `do_best_fuzzy_match`'s (`:17614`) and stay Phase 5, whatever
  PORTMAP's POSIX row currently says - correct the row.
- **Interaction with `MatchTimeout`**: POSIX exhausts the backtracking, so a pattern that was
  fast at first-match can be slow here. Nothing to build; note it in the option's XML doc.

## Verification

- **Un-skip** `needs:posix-matching` (8 tests, `RegressionsPosixMatchingTests.cs`).
- **Oracle generator `posix`**: alternations whose first alternative is a prefix of a later one
  (`a|ab|abc`), nested and repeated, with captures whose spans differ between the first match and
  the longest, under `(?p)` and the `Posix` option, through search, match, findall and sub, and
  under `(?r)`. Zero divergences. Negative controls: `check_posix_match` preferring shorter;
  `restore_best_match` not restoring captures.
- **Add `posix` to the default oracle list.**

## Done when

- [x] Tag delivered or stragglers retagged.
- [x] Oracle wave green; controls recorded in full.
- [x] PORTMAP: the five helpers and the two arms; the best-list row corrected to Phase 5.
- [x] Ratchet GREEN, baseline updated, blind review (hunt: captures from the first match
      surviving into the longest; `found_match` not reset between search positions), commit.

---

## Closing notes (2026-09-12)

**All 8 `needs:posix-matching` tests pass, and the port was as small as the slice predicted:** three
helpers and the two arms. `MatchState` gained `BestMatchPos`, `BestTextPos` and a nullable
`BestMatchGroups`; `FoundMatch` and its reset in `InitMatch` were already there from S16, so nothing
had to be threaded through. Ratchet GREEN at 5759 tests, 5574 passing (5466 distinct ids, baseline
updated from 5449).

**Upstream's lazy allocation does not survive the port, and should not.** `save_best_match`
(`:11493`) is two thirds `capacity` arithmetic and `safe_realloc`; `GroupData.Copy` already makes a
snapshot holding exactly the live spans, so `SaveBestMatch` is four lines. Reusing the storage across
saves is a Phase 7 question. `RestoreBestMatch` does copy span-by-span into the live `GroupData`
objects rather than swapping the array, because a group's captures array is grown in place elsewhere
and the identity of those objects is what the rest of the match holds.

**Two scope corrections to the slice file, both recorded in PORTMAP.**

1. **`same_values` (`:11438`) and `equivalent_nodes` (`:11453`) are not POSIX's.** They sit next to
   the POSIX helpers in `_regex.c` and the slice file listed them as part of the family, but their
   only caller in the whole file is `:11771`, the `do_search_start` suppression in `basic_match`:
   `if (do_search_start && pattern->req_string && equivalent_nodes(start_pair.test,
   pattern->req_string)) do_search_start = FALSE;`. That is the required-string prefilter, so they
   belong to Phase 7 with the rest of it. Neither is ported and neither was needed.
2. **The best-list row was already wrong in the other direction**, as the slice file said: the
   `RE_BestList` family is `do_best_fuzzy_match`'s and stays Phase 5. PORTMAP's single seven-symbol
   row is now three rows - the three helpers S32 ported, the four best-list functions at Phase 5, and
   the two prefilter ones at Phase 7. The symbol-audit counts in that table were edited by hand and
   no longer match `.scratch/symbol-audit.py`'s 2026-09-01 output; re-running it is S33's.

**One upstream oddity worth knowing before a sync diff lands on it.** The FAILURE backtrack arm
writes `return RE_OP_SUCCESS;` (`:15688`) where every other arm returns an `RE_ERROR_*` code. It is
harmless - `RE_OP_SUCCESS` is 1 (`_regex.h:20`) and so is `RE_ERROR_SUCCESS` (`_regex.c:104`) - but a
future sync that renumbers either constant turns it into a real bug. The comment above
`MatchStatus.Success` in the ported arm says so.

**`MatchTimeout` matters much more under this flag, and the XML doc now says so.** POSIX does not
stop at the first match; it keeps backtracking through every remaining path. Measured on the row that
first showed it: 15ms without the flag, 17.9s with it, in a Debug build. Nothing was built for this -
the cancellation check is already in the backtrack loop as well as the advance loop, which the new
`Posix_matching_is_exhaustive_and_a_timeout_still_applies` test pins.

### The `posix` generator, and the one thing it got wrong first

Added to `tools/record-oracle.py` and to the default list in `tools/run-oracle.ps1`. Its measured
profile is in its own docstring and is not repeated here.

**Its first version emitted a backtracking bomb, and that is worth recording because the failure
looked exactly like a port defect.** Seed 31 row 1407 of 2000 was

    (?:(?:(?:é*(.*?))+|(?:😀|😀é)+)*?|((é)|(?<g0>éa)|(éaé))??){1,3}(?:a|aé){1,3}

over a five-character subject, and the wave went RED on a `RegexMatchTimeoutException`. It was never
a divergence: the port and upstream both answer four matches, in 1.1s and 0.69s respectively in their
optimised builds, and the port takes 17.9s in the Debug build the oracle consumer runs. POSIX had
turned a 15ms row into an exhaustive one. The fix is in the generator, not the engine: **a quantifier
now goes on a piece only if the piece holds none already**, so `chain` and `captured` (alternations
of plain literals) take one and `optional`, `tail` and `nested` do not. An exponential pattern is a
property of the pattern, so a generator that emits one measures the build configuration rather than
the port.

**Oracle results.** Default list (now eighteen generators, `posix` included), 5400 rows: GREEN at
seed 20260913. `posix` alone, 2000 rows at each of seeds 31, 4242 and 7: GREEN, 6000 for 6000. One
earlier default run at seed 49690127 went RED on a single row, `(?r)\b\b\B` searching `'.'` with
`partial=True` - that is the **known `partial`/`search_start` family**, not S32's, and it carries the
family's own signature: upstream's `search` answers a zero-width partial and upstream's own `match`
and `fullmatch` answer `None` for the identical row, because `search_start` is consulted on a search
and nowhere else. It is already pinned in `Gaps/Engine/PartialMatchingTests.cs`, and
`tools/run-oracle.ps1`'s Generator note predicts it reds a default run about one time in seven.

### Negative controls

Both are registered in `tools/controls.json`, so re-running them is
`python tools/run-controls.py --slices S32` and no reconstruction from prose is needed. Delete
`.scratch/control-waves/` first after any generator change. Figures below are the final re-run,
against the exact code and generator this slice commits, at three seeds each.

> **Control A, `S32-A`**: in `Matcher.cs`, `CheckPosixMatch`, change
> ```
>         if (newLength > bestLength)
> ```
> to
> ```
>         if (newLength < bestLength)
> ```
> Wave: `posix`, 2000 rows. Result: seed 31 **440 diverge**, seed 4242 **451**, seed 7 **442**.

> **Control B, `S32-B`**: in `Matcher.cs`, `RestoreBestMatch`, change
> ```
>             bestGroup.Captures.AsSpan(0, bestGroup.Count).CopyTo(group.Captures);
> ```
> to
> ```
>             bestGroup.Captures.AsSpan(0, 0).CopyTo(group.Captures);
> ```
> Wave: `posix`, 2000 rows. Result: seed 31 **338 diverge**, seed 4242 **364**, seed 7 **373**.

Neither is thin: both fire on between a sixth and a quarter of the wave, and neither moves by more
than 35 rows across three seeds. Control A is much broader than the 57-in-488 figure the generator's
docstring quotes for "POSIX changes the answer", because preferring the shorter match does not merely
fail to lengthen - it picks the shortest match there is, which is usually shorter than leftmost-first
would give too.

### Gap tests

`Gaps/Engine/PosixMatchingTests.cs`, nine tests, every expected value measured against `regex`
2026.7.19 and quoted beside the assertion. The eight ported tests are all forward, all through the
inline `(?p)`, and all a `match` or a `sub`; these cover the compile-time option, that leftmost still
beats longest, a group the shorter match set being unset in the longer one, a repeated group's whole
capture list being replaced rather than appended to, both the forward and the reverse arm of
`check_posix_match`, the flag through `findall`/`split`/`sub`/`fullmatch`, a zero-width best match,
and the timeout.

### Review

**Two blind passes, one finding, zero defects in this slice's code.**

The first pass saw the whole diff and reported **no findings** against the hunt list the slice named
(captures surviving from the first match into the longest, `found_match` not reset between search
positions) plus aliasing, the span-copy bounds, the reverse arithmetic and the generator. It raised
**one finding out of scope**: `regex.compile(r'(?r)(ab)+').fullmatch('xabz', 1, 3)` answers `None`
upstream where this port matches `(1, 3)`.

That claim was treated as a hypothesis and **reproduced independently before anything was done with
it**. It survived, and it is not POSIX's: it reproduces identically with and without `(?p)`, and it
needs three things at once - reversed, a general repeat rather than a `*_ONE` one, and a slice
narrower than the subject. Only `fullmatch` sees it; `match` and `search` agree, a forward `(ab)+`
agrees, and `(?r)a+` agrees. It is the same slice-versus-text bounds family S31 pinned for partial
matching. It is now pinned in `Gaps/Engine/ReverseMatchingTests.cs` as a deliberate divergence with
all five measurements quoted, **and it is left unfixed**: which side is right is genuinely open -
upstream refusing to fullmatch `'ab'` against the slice that is exactly `'ab'` looks wrong - and
calling that upstream's bug needs the research and the second opinion such a claim gets.

The second pass covered the only thing the first never saw, that one pinned test, and checked each
of its five quoted upstream measurements and the `(pos, endpos)`-to-`(beginning, length)` translation
by running them. **No findings.**
