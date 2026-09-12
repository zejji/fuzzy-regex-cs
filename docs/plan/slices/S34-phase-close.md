---
slice: S34
phase: 4
title: Composed oracle wave, symbol accounting, and closing Phase 4
delivers: []
---

# S34 - Composed oracle wave, symbol accounting, and closing Phase 4

The last Phase 4 slice, shaped like S26. After it every non-fuzzy construct in upstream matches,
the oracle has swept them in combination, and Phase 5's author has a handover.

## Scope

- **Widen the `interactions` generator** to compose the six new families with the S16-S25 ones:
  a lookaround round a group call inside a conditional inside a repeat, cut short under
  `partial=True`, under `(?p)`, with a `(*PRUNE)` in one alternative - the interactions no
  per-slice wave reaches. Bounded rows, seed recorded, zero unexplained divergences, every real
  one minimised into a permanent test.
- **Every Phase 4 negative control re-run** from its recorded four values at the recorded seed
  and at one fresh seed, exactly as S26 did for Phase 3; the three-run table in the closing
  notes. Raise the row count before touching a generator's weights when a control is thin.
- **Probe every remaining tag on the board** by removing its `[Skip]` attributes and running the
  suite - the 2026-09-11 rule (DECISIONS). Any test that passes is un-skipped here; any tag
  Phase 4 delivered that still has a skipped test is a defect or a mis-tag. Multi-line `[Skip(`
  attributes need a multi-line-aware removal.
- **PORTMAP accounting**: every `_regex.c` function is now ported, not-ported-with-reason, or
  deferred to Phase 5 (fuzzy) or Phase 7 (prefilters, `try_match_*`, `*_REPEAT_ONE` sub-arms).
  Re-run the S26 bucketing and quote the counts. Nothing may remain "Phase 4".
- **Phase 5 handover** in the closing notes: the 18 `Seam.For(Opcode.Fuzzy)` sites in
  `Matcher.cs`; the fuzzy-guard hole in `state_init_2` and `reset_guards`;
  `save_captures`/`restore_groups`/`discard_groups` (`:17403-17500`, Phase 5 per PORTMAP);
  `do_best_fuzzy_match` (`:17614`) and the `RE_BestList` family; `do_enhanced_fuzzy_match`; the
  five `a{e<=1:\X}`-style rejections S15/S26 recorded; and upstream issues 470, 563, 564, 596
  (fuzzy and BESTMATCH bugs, Phase 6) so Phase 5 recognises them.
- **Roadmap and budget**: Phase 4's measured sessions-per-slice from `docs/plan/slice-log.jsonl`
  against the 7-slice estimate (8 once S33 was added at the checkpoint); flag whether Phase 5's 5-8 still looks right. `CHANGELOG.md`
  gains Phase 4's entry.

## Verification

This slice's verification is the phase's: the composed wave, the control re-runs, the tag probe
and the symbol accounting, each with its output quoted.

## Done when

- [ ] Composed wave ran with seed and counts quoted; zero unexplained divergences.
- [ ] Every Phase 4 control re-run at its seed and a fresh one; table in closing notes.
- [ ] Every remaining tag probed; no test skipped on a Phase 4 tag; `docs/STATUS.md` regenerated
      and overall parity quoted.
- [ ] PORTMAP complete for `_regex.c` with nothing deferred to "Phase 4".
- [ ] `CHANGELOG.md`, ROADMAP measured rate, STATE.md saying Phase 4 is complete, Phase 5
      handover written.
- [ ] Ratchet GREEN, baseline updated, blind review over this slice's own changes, commit.
