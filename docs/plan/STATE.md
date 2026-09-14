# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S45 IS DONE** (sitting 3, 2026-09-14). Ledger entry 7 was wrong in cause AND fix: upstream merges
`CaseFolding.txt`'s Turkic-only `T` rows into both default folding tables, so the blast radius is the
four codepoints U+0049, U+0069, U+0130, U+0131, not U+0130 alone. New `Unicode/TurkicDefaults.cs`
carries the default case data; no `.g.cs` touched; `Sequence.FixFullCasefold` needed no code change.
PCRE2 10.47, Perl 5.42.2 and .NET 10.0.10 all agree with the fix and only `regex` does not. Ratchet
GREEN at 5,945 tests, 5,945 passing, 0 skipped. Oracle GREEN at three seeds on the full 6300-row
default wave and on `case-folding` at 2000. Ledger entry 7, ROADMAP and PORTMAP all rewritten.

**The two stale `FuzzyRegex.OracleTests` PIDs are gone** - `tasklist` found none at the start of this
sitting, so the seven controls S44 could not measure are unblocked:
`python tools/run-controls.py --ids S42-1C,S42-2A,S42-2B,S42-2C,S42-2D,S42-2E,S42-2F --seeds 2`.
The post-S44 maintenance held: no control mutation survived any run this sitting.

**Next: S46** (BESTMATCH family - ledger 12, 13 and 9's port half). Nothing in S45 blocks it.
**S49** still needs a `gh` snapshot of the tracker from the orchestrator before it launches.

**Blockers:** none. **Known bugs in this port**: ledger 12, 13, 9's port half (S46); 11, 14 (S47);
5's remaining door (S48); the issue sweep (S49, S50). Entry 7 is CLOSED.

**Oracle:** `pwsh -File tools/run-oracle.ps1` (three seeds; `-Count 6000` is the gate).
Ledger: `docs/plan/upstream-reports/LEDGER.md`, 15 entries, nothing filed.

**Owed maintenance, now four items.** (1) `FOLD_TURKIC`'s share of the `case-folding` rotation is too
small: the generator draws ZERO Turkic rows at the default 300 and 4/11/3 at 2000, so the family is
invisible to the default wave through its own generator. (2) `turkic-default-folding`'s predicate has
one false positive no predicate can close - see its remarks; the exact test needs a selectable Turkic
case mode. (3) Six broken control sites (five unresolvable, S42-2G's mutant will not compile).
(4) PORTMAP's `_regex.c` line references are stale after the sync and need an owner decision first.

**Still open for the owner:** `slice-log.jsonl` marks S26 `failed` though its commit is real;
`origin/main` needs a push (nothing since Phase 4's close).
