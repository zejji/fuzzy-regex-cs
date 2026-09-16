# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S52c IS CLOSED (sitting 3, 2026-09-16).** Ratchet GREEN 6144 / 6144 / 0 - nothing in `src/`
changed. Oracle GREEN at all three default seeds, `diverge 0` of 6,380 each. Notes:
`docs/plan/slices/notes/S52c-sittings.md`; the list and its results: `docs/ORACLE-INVARIANTS.md`.

**THE CHECKER SHIPS AND THE CALIBRATION CAME BACK.** Seven invariants at ZERO extra upstream calls -
four read off the row, three off the ablation twins `_CONTROLS` already records - writing
`selfContradiction` per row in `tools/record-oracle.py`; the four structural ones also run on the
PORT in `SelfConsistency.cs`, swept by the renamed
`OracleWaveTests.Our_own_answers_never_contradict_themselves`. Over **126,240 rows at three seeds:
10 firings.** Ledger 11 (6 rows) and ledger 12's shape (2) were re-found automatically on rows
nobody had seen. No NEW ledger entry - every firing belongs to one already open. Four of the six
ledger-11 rows are PARTIAL matches, which none of its seven hand-found doors pointed at.

**Gate row 104366:** upstream breaks `greedy-lazy-existence-agree` on 2 of 6 cells, this port on
0 of 6, so the port is the self-consistent engine. **Ledger 24's `slice_start`-versus-`text_start`
ruling is untouched and the row is NOT pinned on this** - still the owner's call and S52d's.

**TRE is real but narrow, measured not assumed.** `tools/probes/tre-fuzzy-check.py` self-tests 4/4
and CONFIRMS 10 of 10 comparable wave rows; it can answer NONE of the 10 violation rows, each with
its reason. On the rows the invariants are for, the second engine is not available.

**Review: 4 findings, 4 reproduced, 4 fixed, no second pass** (it covered sitting 1's unreviewed
documents, so that debt is paid). Worst: `posix-chooses-among-flagless-answers` compared costs at
the same START where ledger 9 means the same SPAN. **Verifier: 11 of 12 CONFIRMED** - including an
independent re-record of seed 4242 - and claim 12, the three-seed oracle, was COULD NOT RUN on time
and was then re-run by the slice itself, GREEN.

**NEXT: S52d** (ledger 24's ruling, open for the owner). Then S53.

**Carried:** `record-oracle.py --self-check` is RED on ONE PRE-EXISTING guard (a 2,000-deep
nested-group recursion limit Python 3.14 no longer raises) - identical at HEAD, not this slice's.
Plus S52 sitting 10's list: `upstream-bestmatch-free-answer.py`'s unguarded `fuzzy_changes` read;
the `_regex.c` citation reconciliation; `port-tests/SKILL.md`'s stale `FuzzyRegex.Search(...)`;
`run-controls.py`; control sites S32-B/S38-A/S35-A/S29-A/D; PORTMAP lines; `quantifiers-long`'s
filler margin; `oracle.yml`'s weekly sweep verdict rule. **Open for the owner:** `slice-log.jsonl`
marks S26 `failed`; `origin/main` needs a push.
