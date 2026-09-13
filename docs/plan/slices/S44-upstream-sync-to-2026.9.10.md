---
slice: S44
phase: 6
title: Sync upstream to release 2026.9.10, re-record every pinned divergence, and prove the pin against the wheel
delivers: []
---

# S44 - Upstream sync to 2026.9.10

Phase 6 opens here and nothing else in the phase may run first: the bug sweep's verdicts are
recorded against the pinned release, so syncing after them invalidates the list (S43 handover,
DECISIONS 2026-09-12). Use the `sync-upstream` skill.

## Before launch (orchestrator, not the session)

The driver cannot `pip install`. The oracle interpreter (`python`) runs regex **2026.7.19** and the
submodule is pinned at **2026.8.12**, which already disagree; both move to 2026.9.10 here. The
orchestrator installs `regex==2026.9.10` into the oracle interpreter before launching; the session's
first check is `python -c "import regex; print(regex.__version__)"` printing `2026.9.10`.
`.venvs/regex-2026.9.10` keeps its copy for probes.

## Scope

- **Bump the submodule** to the `2026.9.10` tag (never head; on 2026-09-12 head and release were
  the same commit - re-check). Confirm `upstream/regex/` at the tag is byte-identical to the PyPI
  wheel's Python sources and `upstream/src/` to the sdist's (`pip download regex==2026.9.10
  --no-binary :all:`), which is amendment 7's requirement. Diff release..head; if head carries an
  unreleased engine or parser fix, port it too, test-first, recorded in PORTMAP as "ahead of
  release, from commit X".
- **Walk the changelog delta** 2026.8.12 -> 2026.9.10 one entry at a time. Known content: the four
  memory-safety fixes for issues 611-614 (`_regex.c`, 188 lines; `_regex_core.py`, three lines) and
  four Python-API error-propagation PRs (615-618) with nothing to port. Each entry: ported
  test-first with the issue's own test as ground truth, or recorded not-applicable with the reason,
  in PORTMAP. 614 is the group-call-in-lookbehind direction fix where this port was already right
  (S30): the port's answer must not change, and the entry `reverse-group-call-direction` is DELETED
  because upstream now agrees.
- **Re-record every `Example` row in `tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs`** at the
  new version and paste the results back. The staleness alarm compares this port against
  upstream-as-it-was and cannot see upstream's change, so this step is the sync's own test and it
  fails loudly if skipped: an entry whose row no longer diverges is deleted with the fixing upstream
  commit named in the ledger. Ledger entries marked fixed upstream (10, and 614's) are re-verified
  against 2026.9.10 and closed.
- **Re-run every wave**: default list at three seeds, 6000 rows a generator, plus `fuzzy` and
  `interactions` at 99991; all control suites at two seeds. Any new divergence gets the amendment
  16 treatment, not a quick entry.
- **Refresh the Unicode transliteration** only if `_regex_unicode.c` changed (the digest pins in
  `tools/transliterate-unicode.py` say so); record the Unicode version either way.

## Verification

- Interpreter and submodule both at 2026.9.10; wheel and sdist byte-identity quoted.
- Zero `Example` rows recorded against an older version; each remark names the version.
- Waves GREEN at three seeds and the fourth; ratchet GREEN.

## Done when

- [ ] Submodule at 2026.9.10, byte-identity proven, changelog delta fully accounted for in PORTMAP.
- [ ] Every divergence entry re-recorded; stale ones deleted with the fixing commit; ledger closed
      where upstream fixed it.
- [ ] Waves GREEN at three seeds and 99991; controls re-run.
- [ ] Ratchet GREEN, blind review (hunt: an `Example` pasted from the old interpreter; a changelog
      entry marked not-applicable that touches `_regex.c`), commit.
