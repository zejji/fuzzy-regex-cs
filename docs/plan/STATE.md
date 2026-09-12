# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Current slice:** none. S32 is closed. **Next:** S33, added 2026-09-12 at the owner checkpoint -
`pwsh -File tools/launch-slice.ps1 s33`. Then S34 closes the phase.

**Blockers:** none.

**Where the port stands:** ratchet GREEN, 5760 tests, 5575 passing, parity **90.6%**, tree clean.
Every non-fuzzy capability tag is delivered. Oracle GREEN over eighteen default generators;
`verbs` and `partial-sliced` are off the list until S33 puts them back.

**Why S33 exists.** The owner asked how sure we were about the divergences S29, S31 and S32 had
"pinned and parked", and the answer was: not sure enough, and one was called backwards. They were
re-judged against PCRE2 run directly (`tools/probes/pcre2-partial-and-skip.py`), Perl, Boost's and
PCRE2's definitions and upstream's issue history - `docs/plan/2026-09-12-divergence-research.md`.
Result: the port is right on every `(*SKIP)` case, on `ba??x`/`baa` partial and on `(?r)\b$`;
**wrong on the reversed empty-slice partial**; and `(?r)(ab)+` fullmatch on a slice is still open.
S33 fixes the bug, settles the open case, re-marks every inverted in Phase 7 test as permanent, puts
both generators back on the default list, and drafts (does not file) the upstream report.

**Owner rule, 2026-09-12 (DECISIONS, spec amendment 16):** no known bug ships, inherited or not.
Phase 7 ports upstream's prefilters without importing their answers (ROADMAP).

**For S33's author:** upstream's `RE_PARTIAL_LEFT` arms all compare `text_start`, never
`slice_start` (grep `_regex.c`), yet upstream answers a partial on an empty reversed slice - so the
partial comes from somewhere other than those arms. Find where before touching ours.

**Oracle:** `pwsh -File tools/run-oracle.ps1` before committing any engine slice. Controls:
`python tools/run-controls.py --slices S31`. Delete `.scratch/control-waves/` after a generator
change. Research subagents derive from manuals; run the binary instead when one is reachable.

**Still open for the owner:** `slice-log.jsonl` marks S26 and S29 `failed` though both commits are
real. Phase 4 so far: 6 slices, about 290M Opus tokens, parity 76.5% to 90.6%.
