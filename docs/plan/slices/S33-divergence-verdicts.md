---
slice: S33
phase: 4
title: Act on the divergence research - fix the port's one bug, pin what is right, settle the open case
delivers: []
---

# S33 - Act on the divergence research

Added at the owner checkpoint of 2026-09-12 after `docs/plan/2026-09-12-divergence-research.md`
judged every divergence S29, S31 and S32 had parked. Read that document first; it is the
specification for this slice. The owner's rule that motivates it: **every conclusively identified
bug gets fixed in the port, and a divergence where the port is right is pinned permanently, never
inverted later.**

## Scope

1. **Fix the reversed empty-slice partial** (port bug). Failing test first:
   `(?r)a(bc)*` on `abc` with `pos=1, endpos=1, partial=True` must be a partial at (1,1), as it
   already is forward and as upstream answers in both directions; also `pos=2, endpos=2`. Find
   the reversed partial arm(s) that compare against `TextStart` where the forward twin's
   behaviour on an empty slice is a partial - S31's notes say half of upstream's arms are
   bounded by `slice_*` and half by `text_*`; list which and port the reversed half to match
   what upstream *answers*, quoting the upstream line for each. Then the `partial-sliced`
   generator must run clean: **put it on the default oracle list.**
2. **Settle `(?r)(ab)+` fullmatch on a narrowed slice** (S32's review finding, pinned in
   `Gaps/Engine/ReverseMatchingTests.cs`). Research doc says upstream bug on the evidence so far:
   upstream fullmatches the same slice at `pos=0` and forward, and `match` succeeds. Find the
   upstream mechanism (a reverse general-repeat guard comparing against `text_start` rather than
   `slice_start` is the hypothesis - prove or refute it by reading `_regex.c` and by probing with
   `pos` varied while the slice text stays `ab`), run a blind review on the verdict, and then
   either mark the test permanent (port right) or fix the port. Do not leave it open.
3. **Re-mark the pinned tests.** In `Gaps/Engine/BacktrackingVerbTests.cs` (both "PHASE 7:
   INVERT THIS TEST" blocks), `Gaps/Engine/PartialMatchingTests.cs` (the lazy-repeat block around
   line 276 and the `\b$` block around line 310), and `Gaps/Engine/ReverseMatchingTests.cs`
   (line 14 and 273): replace every "inverts when Phase 7 lands" instruction with **"permanent:
   the port is right, see docs/plan/2026-09-12-divergence-research.md; a change here is a
   regression"**, quoting the second engine's answer in the comment. The narrowed-slice partial
   block (around line 358) is the one this slice fixes, so its test moves from pinning the
   divergence to asserting the partial.
4. **`verbs` back on the default oracle list**, recorded honestly. The wave's known divergences are
   upstream's (`search_start` bounds, the prefilter jump), so recording `verbs` rows against a
   prefilter-free upstream (S29's `PREFILTER_FREE_GENERATORS`) is the right ground truth for the
   `locate_required_string` shape but not for the `search_start` shape. Extend the wrapper so
   upstream's `search_start` is also neutralised for those rows if `_regex` exposes a way (check
   `pattern->do_search_start` and the `RE_FLAG`s; if not reachable from Python, say so), else
   list the residual rows in a strict manifest that fails when they stop diverging - the
   ROADMAP Phase 6 shape, pulled forward. Either way the default wave must be green with `verbs`
   in it, and every excluded row must be named.
5. **Draft the upstream report** into `docs/plan/upstream-reports/2026-09-12-draft.md`: one issue
   per defect (`..(*SKIP)xx` retry-below-commit; lazy-repeat partial `ba??x`; `\b$` reversed
   search inconsistency; the reversed fullmatch if item 2 confirms it), each with a minimal
   runnable reproduction pinned to 2026.7.19, the faulting function named, the fix proposed, and
   the PCRE2 answer quoted as the second opinion. **Nothing is filed** - the owner approves the
   text first; `gh` is outside the driver's allowlist on purpose.

## Verification

- Item 1: the failing test, then green; `partial-sliced` at 2000 rows, two seeds, zero divergences;
  the `partial` default wave unchanged (0 of 6800).
- Item 2: a written verdict with the upstream line reference and the blind review's report.
- Item 4: the default oracle run green with `verbs` present; the excluded rows enumerated.
- Full default oracle list at the end, and every S29 and S31 control re-run (`tools/run-controls.py
  --slices S29,S31`) against the committed code, at the recorded seeds and one fresh one.

## Done when

- [ ] Reversed empty-slice partial fixed test-first; `partial-sliced` on the default list, clean.
- [ ] `(?r)(ab)+` fullmatch settled with a verdict, a review, and either a fix or a permanent test.
- [ ] Every inverted later comment replaced by a permanent verdict quoting the second engine.
- [ ] `verbs` on the default list; residual upstream-side rows named in a strict manifest or
      neutralised at the recorder.
- [ ] Upstream report drafted, not filed.
- [ ] PORTMAP rows for any arm touched; ratchet GREEN, baseline updated, blind review (hunt: a
      "fix" that moves a bound and changes an answer no test covers - run the whole default wave,
      not just `partial-sliced`), commit.
