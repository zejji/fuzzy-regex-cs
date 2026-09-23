---
slice: S89
phase: 7
title: The unjudged BESTMATCH, ENHANCEMATCH and (*SKIP) oracle rows are each judged, then fixed or pinned
delivers: []
---

# S89 - Six findings nobody has judged

`docs/plan/STATE.md` lists findings 1 and 3-6 under "Findings that need a slice". Each is an oracle
row where the port and upstream disagree and nobody has said who is right. The owner's rule
(2026-09-12) is that no known bug ships in 1.0, so each one ends this slice as one of: fixed here,
pinned as a port-right divergence (research, isolating probe, blind review, per the upstream-bug
rule), or handed to a named slice of its own in writing.

Several were found before S87's fix (an undone fuzzy section left a stale error total). S87 may
already explain them, so **re-run every row against main first**. That could shrink this slice to
a list of "fixed by S87" lines.

## Scope

1. For each finding, reproduce on main and in upstream (`regex` 2026.9.10, the same version flags
   as the port: V1 where the port runs V1). Record the three answers (upstream, port before, port
   now) in the notes file.
   - Finding 1: `(?b)(?fi)(f)(?:(?:\1)){e<=3}` fullmatch `fxf`, and row 67 of `fuzzy-overhang` at
     seed 20260923 (`(?b)(?fi)(f)(?:\d+a00(?:\1)){e<=3}`). Details in
     `docs/plan/slices/notes/S85-sittings.md`. Upstream matches the literal form `(?:f)`; say why
     the reference form differs there, from `_regex.c`, before calling either side wrong.
   - Finding 3: `(?b)(?e)(?fi)(?r)(?:fine){e<=7}` fullmatch `oelFin becf` (`fuzzy-literal` seed
     20260923, row 1611), and `(?b)(?r)(?:\L<phrases>){e<=3}`, phrases `['', 'amber lantern']`,
     fullmatch `znz`.
   - Finding 4: `(?b)(?r)\m(?:😀\d😀){e:[a-z]}` subf (`fuzzy-anchored` seed 20260923, row 5821).
   - Finding 5: S60b's reviewer saw 18 `(?b)`/`(?e)` divergences in 14,000 rows; the generator was
     not kept. Re-derive a generator that exercises BESTMATCH and ENHANCEMATCH over the
     `fuzzy-*` pattern shapes, run it at seeds 7, 4242 and 20260923, and judge what it finds.
   - Finding 6: oracle row 4957 at seed 99 (partial, `(*SKIP)`, like row 5185).
2. Group the rows that remain by cause. Minimise each group to one pattern.
3. Tests first, each seen red, pinned to upstream's answer (or to the port's, with a DIVERGENCES
   row and its COMPARISON section in the same commit, if upstream is shown wrong).
4. The smallest fix that matches upstream's mechanism. A shared-with-upstream bug gets a ledger
   entry and a draft report in `docs/plan/upstream-reports/`, as S83-S87 did. Nothing is filed.
5. Once finding 1 is settled, put `fuzzy-overhang` (and any generator this slice adds) on the
   default wave if it is green there.
6. Oracle at seeds 7, 4242 and 20260923. Update STATE.md's findings list.

If a group needs more than two sittings, hand it to a slice of its own and close the rest.

**Stop by 05:50 on 2026-09-24** with a green checkpoint if it cannot land. Commit every 30 minutes.
