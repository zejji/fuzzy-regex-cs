---
slice: S36
phase: 4
title: Composed oracle wave, symbol accounting, and closing Phase 4
delivers: []
---

# S36 - Composed oracle wave, symbol accounting, and closing Phase 4

The last Phase 4 slice, shaped like S26. After it every non-fuzzy construct in upstream matches,
the oracle has swept them in combination, and Phase 5's author has a handover.

## Scope

- **First, judge the three `verbs` rows still unexpected at 2000 rows** (seeds 7 and 4242; S35 left
  them). All are reversed overlapped scans with `(*SKIP)` where upstream reports MORE matches than
  this port. Probed at the checkpoint (`tools/probes/upstream-reversed-overlapped-skip.py`, and the
  recorder run on each row in isolation):
  - **Row 1567 (seed 4242)** `(?r)(?:[^\d]{2,4}(*SKIP)A|😀)$` MULTILINE on the emoji subject: upstream
    `[(4,9),(7,8)]` both in the wave and in isolation, and upstream's stateless `search(endpos=8)` is
    `(7,8)`; **this port's own stateless search at the same end position also finds it** (UTF-16
    `(12,14)`) yet its overlapped scan reports only `(8,15)`. The scan is dropping a match the engine
    finds, most likely a slice bound a `(*SKIP)` moved in the first scan and the scanner carried into
    the next. **Port bug on the evidence so far**: fix test-first, then re-check row 1439.
  - **Row 1863 (seed 4242)** `(?r)([^a]{2,4}(*SKIP)[a\d])((?:[^\d]++(*SKIP)\s|\ ))` on `b0 0
 A`:
    upstream `[(0,6),(0,5)]` but its own stateless `search(endpos=5)` and `match(endpos=5)` are `None`,
    so the second match is upstream's stateful scanner carrying a moved slice - the case-F mechanism.
    **Upstream bug, port right** unless a probe says otherwise; classify in `ExpectedDivergences` and
    add to the ledger's entry 5.
  - **Row 1439 (seed 7)**: the wave recorded upstream at 3 matches, but the recorder run on that row
    alone gives **1**, identical to the port. Upstream's answer depends on what ran before it in the
    same process - a `(*SKIP)`-moved bound surviving across independent calls through the compiled
    pattern's cached scanner state, or similar. Confirm by recording the row after its wave
    predecessors, name the mechanism, and record it as a ledger entry: an oracle row whose ground
    truth depends on call order is a recorder hazard for every later wave, so decide whether the
    recorder must isolate each row (fresh process or `cache_pattern=False`) from here on.
- **Ledger entry 7 (`İ` never reaches the full fold) is decided: it goes on Phase 6's fix list** (owner
  rule: fixed before 1.0; it needs the folding tables changed, which is a slice of its own there).
  Record that in the ROADMAP's Phase 6 opening and in the ledger; nothing else about it here.
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
  against the 7-slice estimate (10 once S33, S34 and S35 were added at the checkpoint); flag whether Phase 5's 5-8 still looks right. `CHANGELOG.md`
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
