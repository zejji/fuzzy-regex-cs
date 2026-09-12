# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Current slice:** S38 is next - the fuzzy spine, and the first slice of Phase 5 that touches the
engine. Launch: `pwsh -File tools/launch-slice.ps1 s38`. S37 closed Phase 4's last unfinished item:
`interactions` is GREEN at 6000 rows on four seeds and the default wave is GREEN on three, so S43 can
widen that generator with fuzzy and tell a new divergence from an old one.

**What S37 found, in one line each.** S36's thirteen rows were three families, not the three it
named: five are upstream losing a match when a group call sits inside a lookaround running the other
way (new entry `group-call-loses-the-match`, ledger entry 8, NOT fixed by issue 614 and unchanged on
2026.9.10); three are `search_start`'s partial arms in the shape where this port answers its OWN
partial elsewhere (`search-start-partial` widened); one is a reversed overlapped `(*SKIP)` extra
match that a too-wide clause kept out of its own entry (a NEGATIVE lookaround cannot leave a
capture, so only `(?=`, `(?<=` and `\K` are excluded now). **No port bug**: the one candidate, a
`lastindex` difference on row 4182, is two different patterns and both engines agree on each.

**Read before re-deriving it:** minimising family A **failed**, deliberately recorded. Every shrink
that kept upstream self-contradictory landed on a pattern this port answers exactly as upstream does,
so a call through an opposite-direction lookaround is necessary and not sufficient - and **inlining a
called group is not semantics-preserving**, which kills the obvious recorder field. The rows are
listed whole in `ExpectedDivergences.cs`.

**Blockers:** none.

**Where the port stands:** ratchet GREEN, 5772 tests, 5589 passing, parity **90.7%**, 28 areas at
100%. Every remaining skipped test (183) is fuzzy; the 27 `Seam.For(Opcode.Fuzzy)` sites in
`Matcher.cs` and the three entry-point seams at `Matcher.cs:6582-6592` are Phase 5's map.

**Oracle:** `pwsh -File tools/run-oracle.ps1` (three seeds). Rows per generator is **`-Count`**;
`-Rows` is a path to a JSONL file. Controls: `python tools/run-controls.py --slices S37 --seeds 3`.
Upstream 2026.9.10 for probes is in `.venvs/regex-2026.9.10` (git-ignored), loaded by path.

**Upstream is a ledger, not a queue** (`docs/plan/upstream-reports/LEDGER.md`, eight entries):
nothing filed until everything else in the plan is done. Entry 7 is on Phase 6's fix list.

**Still open for the owner:** `slice-log.jsonl` marks S26 `failed` though its commit is real;
`origin/main` trails local and needs a push.
