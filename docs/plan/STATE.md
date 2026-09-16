# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S52 IS CLOSED (2026-09-16, sitting 19), and the slice file is in `done/`. Ratchet GREEN,
6127 / 6127 / 0 skipped, 6019 distinct ids, baseline 6019.** `src/` untouched: this sitting added
pins, tests and one instrument, no engine change.

**ALL 37 SWEEP ROWS ARE JUDGED AND PINNED.**
`pwsh -File tools/run-oracle.ps1 -Rows tools/probes/sweep-divergence-rows.jsonl` gives
**expected 37, diverge 0**, where sitting 18 left `expected 15, diverge 22`. The default wave is
GREEN at all three seeds, the third of which is the DATE - so a fresh seed every day.

**THE METHOD THAT DID IT, and the owner asked for it: batch the ablations, classify by family.**
`tools/probes/sweep-ablation-matrix.py` takes every unjudged row and writes ONE rows file holding
every single-construct ablation of it - each flag, the fuzzy constraints, the verb, the atomic cut,
the prefilter, the Turkic swap, and the doors. One `run-oracle.ps1 -Rows` over it answers both
halves of amendment 16 at once, because the harness asks upstream AND this port the identical
questions. The table is committed at `tools/probes/sweep-ablation-matrix.txt`. Twenty-two rows
became five families in one reading; three new entries, five widened, four new gap tests.

**THE SLICE FILE IS NOW A SPEC.** Every per-sitting note lives in
`docs/plan/slices/notes/S52-sittings.md` (about 3,640 lines); `docs/plan/slices/done/S52-*.md` is
61. Do the same for any slice whose notes outgrow it.

**NEXT: S52b (thread safety)**, then S52c/S52d, which the owner's ruling handed the gate's row
104366. Nothing is blocked.

**Carried** (full list in sitting 10's notes): `upstream-bestmatch-free-answer.py`'s unguarded
`fuzzy_changes` read - **now urgent, because sweep row 18 carries POSIX and is in a pin**; the
`_regex.c` citation reconciliation; `port-tests/SKILL.md`'s stale `FuzzyRegex.Search(...)`;
`record-oracle.py --self-check`; `run-controls.py`; control sites S32-B/S38-A/S35-A/S29-A/D;
PORTMAP lines; `quantifiers-long`'s filler margin; `oracle.yml`'s weekly sweep verdict rule; the
`pos`/`endpos`-versus-`codepointSlice` fix. **Open for the owner:** `slice-log.jsonl` marks S26
`failed`; `origin/main` needs a push.
