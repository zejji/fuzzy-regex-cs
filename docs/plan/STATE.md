# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S50b IS CLOSED (2026-09-15, one sitting).** Suite 6,030 / 6,030 passing / **0 skipped**, ratchet
GREEN, baseline 5,922, oracle GREEN at seeds 7, 4242, 20260914 and 99991 with expected counts
5, 1, 1, 2 - **unchanged from S50**, which was the gate. **Next slice is S51** (timeouts and
cancellation).

**Version 1 is now the compile-time default.** Measured, not read: the only live V0/V1 differences
are nested sets, full case-folding, and - in neither upstream's README nor the slice file - **a
backreference to an OPEN group**, which V0 rejects and V1 and .NET both accept. `state->version_0`
is set and never read, upstream and here, so nothing at MATCH time depends on the version at all.

**The flip made a latent upstream bug live and it is FIXED here (ledger 22).** Under
`DEFAULT_VERSION = VERSION1` upstream disagrees with itself: `compile('a', V0)` folds simply and
`compile('(?V0)a')` folds fully, because `Info.__init__` writes `DEFAULT_FLAGS` into `global_flags`
and the `_UnscopedFlagSet` retry is seeded from it. `Parsing.Info` assigns `GlobalFlags` first; all
1,659 compile-parity rows unmoved is the proof it is a no-op under V0.

**The ported suite compiles through `Ported.Upstream`, which pins VERSION0** - 784 call sites in 140
files, rewritten before the flip so the unchanged ratchet proves it behaviour-neutral. A conventions
test scans the sources and fails on any bypass. **Control A says the pin reddens only 2 of 6,030
tests today**; its value is prospective, and the guard is what makes it hold.

**Owed, and the only thing this slice could not do: `.claude/skills/port-tests/SKILL.md` still
teaches `FuzzyRegex.Search(...)` and never mentions `Ported.Upstream`.** The edit was refused for
want of write permission under `.claude/skills/`. Next session with permission should fix it.

**Owed maintenance (unchanged from S50):** `tools/run-controls.py` cannot measure a control that
mutates the recorder, so **S42-2A is owed**; the two broken control sites S32-B and S38-A;
`FOLD_TURKIC`'s share of the `case-folding` rotation; S35-A and S29-A/D are thin; PORTMAP's
`_regex.c` line references stale after the sync; `record-oracle.py --self-check` exits 1 on a
pre-S46 message.
**Open for the owner:** `slice-log.jsonl` marks S26 `failed`; `origin/main` needs a push.
**Housekeeping:** the `s48b-baseline` and `pre-s48b` worktrees can now go.
