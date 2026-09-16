# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S52d IS CLOSED (one sitting, 2026-09-16).** Ratchet GREEN 6153 / 6153 / 0, baseline updated with
one accepted removal (a rename). Default oracle wave GREEN at seeds 7, 4242 and 20260916,
`diverge 0` of 6,380 each. Closing notes are in the slice file, `docs/plan/slices/done/`.

**LEDGER 24 IS RULED AND CLOSED.** A reversed match with `partial: true` now runs out of text at the
SLICE START, one rule, asked in one place - `Matcher.RanOutOnTheLeft`, plus `SteppedPastTheLeft` for
the two sites whose position has already been moved. Owner's Option B, spec amendment 16 outcome (c),
not filed until Phase 8. `docs/DIVERGENCES.md` has the SHIPPED behaviour row.

**Fifteen sites, not the ledger's nine**, and the six extra were the same question in a different
SPELLING (`TryMatchAny*Rev`, `TryMatchOneRev`, `IsTailPartial`). Grep the PREDICATE (`PartialLeft`),
never the field. Three sites that already read `SliceStart` were routed through the same helper. The
18 remaining `TextStart` reads all serve `^`, `\A`, `(?m)^`, `(?m)$`, `\b`, `\B`, the grapheme walks
or the lookaround widening, and are listed in ledger 24.

**The fix also REMOVED four pre-existing divergences** - rows where upstream answered a partial and
this port did not - so it moves the port into agreement with upstream wherever upstream uses its own
Rule B. 44 rows now diverge the other way and all 44 fit one narrow pin,
`reversed-partial-runs-out-at-the-slice-start`. Gate row 104366's invariant still holds 0 of 6 here
against upstream's 2 of 6, with the port now on upstream's self-consistent arm.

**Review: 2 findings, 2 reproduced, 1 fixed, 1 out of scope, no second pass.** The fix: a
`beginning` that SPLITS a surrogate pair made `<=` fire where upstream's `==` does not; the slice
moves the bound and not the comparison. **Verifier: 10 of 12 CONFIRMED**, both DIFFERENTs being the
claim's wording rather than the code.

**NEXT: S53** (the AOT dynamic gate).

**Carried:** two unjudged oracle rows, both proven PRE-EXISTING by the negative control and neither
S52d's - seed 99991 row 3825 and seed 31415 row 3756, both `interactions`. Plus S52c's carried list:
`record-oracle.py --self-check` RED on one pre-existing guard; `upstream-bestmatch-free-answer.py`'s
unguarded `fuzzy_changes` read; the `_regex.c` citation reconciliation; `port-tests/SKILL.md`'s stale
`FuzzyRegex.Search(...)`; `run-controls.py`; control sites S32-B/S38-A/S35-A/S29-A/D; PORTMAP lines;
`quantifiers-long`'s filler margin; `oracle.yml`'s weekly sweep verdict rule. **Open for the owner:**
`slice-log.jsonl` marks S26 `failed`; `origin/main` needs a push.
