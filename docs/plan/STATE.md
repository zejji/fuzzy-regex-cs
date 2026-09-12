# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Current slice:** none. S33 is closed. **Next:** S34, the phase close - but read the blocker first.

**Blocker, and it is an owner decision.** S33's blind review found that **a default oracle wave is
not reliably green, and was not before S33 either** - every slice so far ran one seed and that seed
happened to be clean. Three unjudged divergences, none caused by S33 (each reproduces on `fb4e705`):

1. `regex.finditer(r'(?:[^\d](*SKIP)){2,3}', '\r\naabb ', regex.M, overlapped=True)` - upstream
   `(0,3)(1,4)(2,4)(3,4)(4,7)(5,7)`, this port has `(2,5)` and `(3,6)` in the middle. Forward
   `(*SKIP)` in a bounded repeat; not the prefilter (prefilter-free upstream answers the same). **The
   port matches MORE than upstream, so this is the likeliest of the three to be a port bug.**
   `tools/run-oracle.ps1 -Generator verbs -Count 2000 -Seed 7`
2. `regex.compile(r'(?r)(?<g>[ab]+)(?=(?&g))b').search('>abbaa\r<').spans('g')` is
   `[(3, 2), (1, 3)]` - upstream records a capture whose end is before its start. This port records
   `(3, 3)`. `-Generator recursion -Count 2000 -Seed 4242`
3. The bounded-lazy-repeat partial family (`ba??x`) reaches a wave after all: `-Generator
   partial,partial-sliced -Count 2000 -Seed 314159`, two rows. Judged and port-right already, but no
   safe classifier predicate was found, so it is a known red.

Each needs S33's treatment: research, an isolating probe, a blind review of the verdict, then a fix
or a permanent test. The no-known-bugs rule makes item 1 non-optional before 1.0. **Author a slice
for these before S34, or fold them into S34 - that is yours to decide.**

**Where the port stands:** ratchet GREEN, 5761 tests, 5576 passing, parity **90.6%**, tree clean.
`partial-sliced` is on the default oracle list and clean at seeds 7, 31 and 4242. `verbs` is not,
for reason 1 above; its judged rows are classified in
`tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs`, which is new - read its remarks before adding
an entry, and write the control first (S33's own control caught the list eating a mutation).

**Rule learned, worth more than the slice:** run every generator at **three** seeds before believing
a wave. Fifteen seconds a generator, and it found all three of the above.

**Oracle:** `pwsh -File tools/run-oracle.ps1` before committing any engine slice. Controls:
`python tools/run-controls.py --slices S29,S31,S33`. Delete `.scratch/control-waves/` after a
generator change. Upstream report drafted, NOT filed:
`docs/plan/upstream-reports/2026-09-12-draft.md` - the owner approves the text first.

**Still open for the owner:** `slice-log.jsonl` marks S26 and S29 `failed` though both commits are
real. Phase 4 so far: 7 slices, parity 76.5% to 90.6%.
