# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S47c IS CLOSED** (2026-09-14, one sitting). Suite 5,953, ratchet GREEN, default oracle wave GREEN
at three seeds. No engine change. Next in the queue is **S48**.

**Ledger entry 13's mechanism is established to the line**, in a `/Od /Zi` MSVC build of the pinned
upstream, instrumented and run. `(*SKIP)` narrows `slice_start` at `_regex.c:14555` during the
NORMAL attempt; `do_match:18170` restores `text_pos` alone on the partial fallback; and
`do_best_fuzzy_match:17625`'s loop guard is then false for the caller's own start position, so the
body never runs and `status` keeps the `RE_ERROR_FAILURE` from `:17599`. **The partial is never
attempted.** `do_simple_fuzzy_match` gets the same leaked slice and answers anyway - no such guard -
which is why `(?b)` looks like a filter and is not one.

**Two fixes proven, upstream's own suite 101/0 under each. Fix A is what this port already does**
(`Matcher.cs:10098-10100`, S40b). Both give, under `(?b)`, this port's answer IN FULL on all five
judged rows - **77937 included**, which retires that row's "only the span agrees" caveat.

**Probe: `tools/probes/upstream-bestmatch-lost-candidate.py`** - plain (no compiler), `--trace`
(instrumented build, plus which SKIP arm each pinned row fires), `--fix` (stock / A / B side by
side). Builds go in `.scratch/`; `upstream/` is never written to. Needs MSVC; `REGEX_VCVARS`
overrides the search. setuptools is NOT installed for this interpreter - the probe drives `cl.exe`.

**Pin widened by one row**, `bestmatch-loses-a-partial` 6 -> 7: the minimised REVERSED shape, the
`slice_end` arm's only small witness. The whole block is now re-recordable -
`python tools/record-oracle.py --rows tools/probes/bestmatch-loses-a-partial-rows.jsonl`.
**Seed 20260914 row 76345 is explained but NOT pinned:** its recorded question is gone (the wave file
was overwritten; a single-generator re-run does not redraw it) and a guessed key would be fabricated.

**The blind pass raised 5 and ALL 5 reproduced** - two false scope claims, a probe that did not
measure what three documents said it measured, a wrong "unconditionally", an off-by-one line. All
fixed. An investigation slice gives a reviewer real purchase; budget for findings.

**Untriaged (unchanged):** the 6000-row everything gate at 99991 RED at 4 of 126,000; the three-seed
gate's 19; the POSIX `(?e)` count bug; ledger 11 mechanisms C and D; 5's remaining door (S48); the
issue sweep (S49, S50); the promoted fold sweep's 30 `fold_case(FULL)` mismatches, NOT measured.

**Watch:** `FuzzyRecursionTests.The_stack_bound_is_still_what_catches_a_blowup_the_guard_cannot_see`
went RED once for the reviewer under load and passed idle. Not this slice's doing.

**Owed maintenance (unchanged):** `FOLD_TURKIC`'s share of the `case-folding` rotation; five broken
control sites; PORTMAP's `_regex.c` line references stale after the sync; `record-oracle.py
--self-check` exits 1 on a pre-S46 message. **Still open for the owner:** `slice-log.jsonl` marks S26
`failed`; `origin/main` needs a push.
