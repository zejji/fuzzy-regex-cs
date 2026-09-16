# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S52c IS OPEN - this is a CHECKPOINT after sitting 1 (2026-09-16), a short sitting the
orchestrator scoped to two scope items and told not to start the checker, a wave or a review.**
Ratchet GREEN, 6144 / 6144 / 0 skipped, unchanged: nothing in `src/` or `tests/` was touched. This
commit is four documents. Per-sitting notes: `docs/plan/slices/notes/S52c-sittings.md`.

**DONE: scope item 1, the invariant list - `docs/ORACLE-INVARIANTS.md`, 22 invariants in eight
groups.** Each carries an ID (the string the recorder writes into `selfContradiction`), a statement
in terms of calls the recorder can make, a ground, a cost tier and a **calibration**: the numbered
`LEDGER.md` entries it would have caught automatically. The list was derived by walking all 24
ledger entries, so it is checkable against them; 16 are reached and the 8 that are not are named at
the foot of the file. Four of the slice's starting bullets were narrowed or rejected with reasons -
`group-spans-inside-match` is false unnarrowed because of `\K`, `fuzzy-budget-monotone-cost` is
false outside `BESTMATCH`, the V0/V1 item has an open-ended exception list, and
`reverse-mirrors-forward` is deferred in favour of `greedy-lazy-existence-agree`, which is also the
invariant that handles gate row 104366.

**DONE: scope item 5, the fuzzy second engine - NONE IS REACHABLE, and the wall is permissions.**
`agrep` and Perl `String::Approx` are genuinely ABSENT. `fuzzysearch` is merely BLOCKED: `pip`,
`winget` and `choco` are all installed and all outside the driver's allowlist, so a non-interactive
session cannot even query them. Do not record that as "fails on Windows" - it is untested.
`docs/plan/OPERATIONS.md` has the table and the one command the owner runs to settle it.

**NEXT SITTING, in this order:** scope item 2, the checker in `tools/record-oracle.py`, taking the
five `FREE`-tier invariants first (no extra upstream calls, and one of them reaches ledger 11's
seven doors); then scope item 4, the same checker in `OracleComparer`; then item 3's three-seed
2000-row wave and its triage; then item 7's second half and item 6's VERIFICATION paragraph.

**THE REVIEW DEBT IS REAL AND MUST BE PAID BY THE NEXT SITTING.** No blind review and no verifier
ran here, by instruction. These four documents have had no pass over them, so the checker sitting's
review covers this delta as well as its own.

**Carried** (unchanged by this sitting; full list in S52 sitting 10's notes):
`upstream-bestmatch-free-answer.py`'s unguarded `fuzzy_changes` read; the `_regex.c` citation
reconciliation; `port-tests/SKILL.md`'s stale `FuzzyRegex.Search(...)`; `record-oracle.py
--self-check`; `run-controls.py`; control sites S32-B/S38-A/S35-A/S29-A/D; PORTMAP lines;
`quantifiers-long`'s filler margin; `oracle.yml`'s weekly sweep verdict rule; the
`pos`/`endpos`-versus-`codepointSlice` fix. **Open for the owner:** ledger 24's `slice_start` versus
`text_start` ruling (S52d); `slice-log.jsonl` marks S26 `failed`; `origin/main` needs a push.
