# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S48b IS A CHECKPOINT, NOT CLOSED** (2026-09-14, sitting 1). The slice file stays in
`docs/plan/slices/` and carries a full "Progress, sitting 1" section. Suite 5,959 (+4), ratchet
GREEN, default wave GREEN at three seeds.

**All three scoped items are FIXED and each mechanism was measured, not hypothesised.** Ledger 9's
port bug was ONE stale field: `RestoreBestMatch` restored `FuzzyCounts`/`FuzzyChanges` and left
`TotalErrors`/`TotalCost` holding the LOSING candidate's, so the `(?e)` walk cut itself off with
`3 >= 3` while holding the right two-error counts. Ledger 11 C and D took ONE edit, not nineteen:
`PushFuzzyCounts` pushes the change list's LENGTH, `PopFuzzyCounts` truncates back to it, and the
sites split eight **restore** / three **merge** by which buffer the pop writes into. **The ranking
rule is untouched**, as the slice's guard requires.

**Two of the three items were one bug.** C came in two shapes - list too LONG (fixable by
truncation) and too SHORT (not). The short one was ledger 9's stale totals, because it carries
`(?b)` too. Truncation deliberately never GROWS the list.

**TWO NEW UPSTREAM FINDINGS, both with upstream contradicting itself.** Ledger entry 16 (new): a
POSIX overlapped scan of a BESTMATCH fuzzy pattern **drops its LONGEST match** - three other doors
and upstream's own `fullmatch` at the same flags answer `(0,9)`, only POSIX+BESTMATCH starts at
`(0,8)`. And entry 11 gains an **ATOMIC GROUP door** that S47's anchored `leakFreeFuzzy` cannot see,
settled instead by upstream's own `(?>` -> `(?:` control.

**WHAT IS LEFT, and it is the whole reason this is a checkpoint: the 6000-row three-seed gate is
9+4+11 = 24 against S48's 6+4+9 = 19.** The five new rows were named by REPLAY through a worktree at
HEAD, not by arithmetic (both baselines reproduce S48's figures exactly): seed 7 rows 73463, 73895,
76983 and seed 20260914 rows 74033, 76101. **On all five this port answers what upstream's own
control answers** - none is a regression. Classifying them needs a NEW recorded control per family,
because neither existing discriminator reaches them (`leakFreeFuzzy` removes an earlier attempt's
leak, not an atomic group's; `bestmatchFreeOutcome` needs a `(?b)`, and 76983 is `(?e)`+POSIX), then
two `ExpectedDivergences` entries, then a re-run of the gate.

**Owed maintenance (unchanged from S48):** `FOLD_TURKIC`'s share of the `case-folding` rotation; two
broken control sites; S35-A and S29-A/D are thin; PORTMAP's `_regex.c` line references stale after
the sync; `record-oracle.py --self-check` exits 1 on a pre-S46 message. **Still open for the owner:**
`slice-log.jsonl` marks S26 `failed`; `origin/main` needs a push. **Housekeeping:** delete the
`.claude/worktrees/s48b-baseline` worktree once the classification work no longer needs it.
