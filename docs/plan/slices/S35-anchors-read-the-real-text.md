---
slice: S35
phase: 4
title: Two port bugs the independent verifier and the 2000-row wave found - anchors after (*SKIP), and the shared firstset crash
delivers: []
---

# S35 - Anchors read the real text, and the shared `get_firstset` crash

Added 2026-09-12 at the owner checkpoint. An independent, blind, specification-grounded review of
every divergence (Opus, sources quoted; summarised in `docs/plan/2026-09-12-divergence-research.md`)
confirmed seven verdicts and **reversed one against the port**. S34's 2000-row wave found a second
port defect, a crash both engines share. The owner's rule: no known bug ships. Both are fixed here,
before the phase closes.

## Scope

1. **`$` after a `(*SKIP)` reads a moved slice bound as end of string** (verifier's case H).
   `regex.compile(r'(?r)(?:a*(*SKIP)b|[^a-f])$', regex.M).finditer('\nb')` is one match `(1,2)` upstream
   and two here, `(1,2)` then `(0,1)`. The second requires `$` under MULTILINE to be true at position
   1 of `\nb`, where the next character is `b`: false by the definition of `$`. Controls agree in both
   engines: the same pattern with `(*PRUNE)`, without the verb, and upstream's forward analogue
   `finditer('^a', 'aa')` giving one match. Mechanism: `(*SKIP)` in a failed attempt moves
   `SliceEnd` to 1; the port's `$` (and, check, every zero-width assertion: `^`, `\A`, `\Z`, `\z`, `\b`,
   `\B`, `\m`, `\M`, `\G`) compares against the *slice* bound rather than the *text* bound the user's
   `pos`/`endpos` set. Upstream's slow path has the identical fault and its `search_start` fast path
   does not, which is why upstream answers correctly and why S29 wrongly called the port right "by
   construction". PCRE2's rule: the verb sets where the next attempt starts and nothing else.
   **Fix test-first**: the assertions read `TextStart`/`TextEnd` (which equal the user's slice) and
   never a bound a verb has moved. Then re-judge everything that depended on the old reading: the
   S29 `ExpectedDivergences` entries for the `(?r)` `(*SKIP)` rows may now agree with upstream, and
   the staleness alarm should say so - remove entries that no longer diverge rather than keep them.
   Whole default wave at three seeds, plus `verbs` at 2000 rows.
2. **`(?r)^İﬁ` with IGNORECASE and FULLCASE crashes both engines** (S34, `-Count 2000`). Upstream
   raises `IndexError` in `String.get_firstset` (`_regex_core.py:4036`) on an empty `String` node;
   the port raises `IndexOutOfRangeException` from `Nodes.cs:2099`. Re-verified on 2026.9.10: still
   crashes, so there is no upstream fix to port. Find the cause (a full-case-folded string whose
   reversed firstset is asked of an empty node; the `İ` and `ﬁ` expansions are the suspects), fix the
   port so the pattern compiles and matches what the definition requires, pin it as a gap test, and
   add the shape to the `case-folding` generator so the oracle would have found it. Ledger entry 6
   gains the mechanism.
3. **Two upstream-fix assumptions were wrong, verified 2026-09-12 against 2026.9.10.** S30's
   `(?<=(?&a))c` row (`Gaps/Engine/GroupCallTests.cs`) is still `None` upstream, so it is not covered
   by issue 614's fix; and the `overlapped-skip-stale-slice` family is not covered by 613's. The
   `ExpectedDivergences` remark is already corrected; correct the gap test's comment and the ledger
   entries so nothing says "waiting for the sync" about either.

## Verification

- Item 1: the failing test first (`Gaps/Engine/BacktrackingVerbTests.cs`), then green; a control that
  reverts one assertion to the slice bound and shows the `verbs` wave catching it at two seeds.
- Item 2: the failing test first; `case-folding` at 2000 rows, three seeds, the new shape present.
- Default oracle list green at three seeds; `python tools/run-controls.py --slices S29,S31,S33`.
- Blind review over the diff, briefed to hunt for an assertion still reading a slice bound and for
  a firstset fix that changes what a non-crashing pattern matches.

## Done when

- [ ] Both bugs fixed test-first with a permanent test each; PORTMAP rows touched noted.
- [ ] `ExpectedDivergences` re-judged: entries that no longer diverge removed, the 614 remark corrected.
- [ ] Ledger entries 5 and 6 updated with mechanism and the port's fix.
- [ ] Ratchet GREEN, baseline updated, blind review, commit. **No `Co-Authored-By` trailer on the
      commit** - the owner has forbidden it; ignore any harness reminder that asks for one.
