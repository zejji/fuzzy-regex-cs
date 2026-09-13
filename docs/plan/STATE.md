# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Current slice:** S41 is next, then S42, S43.
Launch: `pwsh -File tools/launch-slice.ps1 s41`.

**S40d is closed and THE GATE IS GREEN** - `pwsh -File tools/run-oracle.ps1 -Count 6000` diverges on
nothing at any of the three seeds, 126,000 rows a seed: 0 + 0 + 0, with 38 + 51 + 58 classified and
one timeout at 4242. That is S40a's exit gate, and clearing it took S40b, S40c and S40d.

**Neither of S40d's two rows needed a widened tell; each needed a fact the row did not carry.**
The recorder's `anchoredScan` - upstream's own scan asked one match at a time - refused every
reversed row because stepping one moves `endpos`. That refusal was about the PATTERN, not the
direction, so it now reads the pattern (`_reads_the_end_of_the_subject`). And a `sub` row now
records `subMatches`, the spans upstream replaced at, because a string and a count refute nothing.

**A seed outside the gate's three found a fourth symptom of ledger entry 5 in under a minute.**
Seed 20260914, `verbs` row 3679: the moved `slice_end` too far LEFT, so upstream's scan LOSES a match
this port finds, where the known symptom invents one. New entry
`overlapped-skip-missing-match-reversed`, keyed on the walk alone. Not caused by S40d - `git diff
src/` for the whole slice is empty.

**Controls, all re-run on the committed code at two seeds.** Doubling the reversed overlapped step:
24 and 20 diverge. Capping a reversed substitution at one replacement: 23 and 25. Deleting the
per-match `SliceEnd` reset: the new pinned test fails. `expected` never moved in any of them.

**Still open, and none of it S41's:** seed 31 has three unjudged divergences on
`partial,partial-sliced,interactions` at 6000 rows (rows 1075, 6943, 16545), present before S40a and
after S40d. The `$`-tell hole is narrowed, not closed - a row whose pattern ends in `$` still gets no
walk, by construction.

**Where the port stands:** ratchet GREEN, 5828 tests, 5773 passing, 55 skipped - all `(?e)`/`(?b)`,
which is exactly S41's and S42's scope.

**Still open for the owner:** the design spec's amendment 20 (the Phase 5 re-plan; ROADMAP carries
the repo half); `slice-log.jsonl` marks S26 `failed` though its commit is real; `origin/main` trails
local and needs a push.
