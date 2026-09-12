# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Current slice:** none. S33 is closed. **Next:** S34, the three-seed sweep, authored 2026-09-12 at
the checkpoint - `pwsh -File tools/launch-slice.ps1 s34`. Then S35 closes the phase.

**Blockers:** none. The owner decision S33 asked for is taken: the three residual divergences get
their own slice (S34) before the close, because a phase cannot close on a default wave that is red at
two of three seeds. Rule from the owner, 2026-09-12: no known bug ships, inherited or not.

**Where the port stands:** ratchet GREEN, 5761 tests, 5576 passing, parity **90.6%**, tree clean.
`partial-sliced` is on the default oracle list and clean at three seeds; `verbs` rejoins in S34.
`tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs` classifies judged upstream-side rows and
`Every_expected_divergence_still_diverges` is the staleness alarm - read its remarks before adding.

**For S34's author, probed at the checkpoint (`tools/probes/upstream-overlapped-skip-scan.py`):** on
item 1, upstream's own `match` and `search` at every start position agree with the port; only its
stateful overlapped scanner disagrees, so start from `scanner_search_or_match` (`:20874`) and the
slice bound a `(*SKIP)` leaves behind between scans. Item 2 is a span with end before start - invalid
on its face; issue 614 (fixed upstream 2026-08-30) is the first suspect.

**Phase 6 now opens with the upstream sync and bug sweep** (ROADMAP, spec amendment 17): upstream is
at 2026.9.10, 21 commits and five releases past our pin, and head equals the release. Releases only,
for the pin and the oracle.

**Upstream report drafted, NOT filed:** `docs/plan/upstream-reports/2026-09-12-draft.md`, four
issues. The owner approves the text first. S34 may add two more.

**Oracle:** `pwsh -File tools/run-oracle.ps1` before committing any engine slice, at **three seeds**.
Controls: `python tools/run-controls.py --slices S29,S31,S33`. Delete `.scratch/control-waves/` after
a generator change.

**Still open for the owner:** `slice-log.jsonl` marks S26 and S29 `failed` though both commits are
real. Phase 4 so far: 7 slices closed, parity 76.5% to 90.6%.
