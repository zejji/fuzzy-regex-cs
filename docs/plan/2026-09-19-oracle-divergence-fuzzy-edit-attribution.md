# Open oracle divergence: which position gets the substitution and which the insertion

**Found** 2026-09-19 by S58's verification run, at seed `20260919`. **Not triaged, not pinned, not
fixed** - S58 is a measurement slice and is forbidden to touch `src/`. This file exists because the
oracle's own evidence (`TestResults/oracle/wave-20260919.jsonl`, `report-20260919.txt`) is
gitignored and would not survive the slice.

## It is not S58's doing

`git diff dfa8767 -- src` is empty: the engine is byte-identical to `S56b` (2026-09-18 17:31), which
is the last commit that touched `src/` at all - 28 commits back from this slice's first commit, and
not one of those 28 touched `src/` (`git rev-list --count dfa8767..e30a4e8` = 28,
`git log -n 2 --oneline -- src` reaches back past all of them). The third default oracle seed is `Get-Date -Format 'yyyyMMdd'`
(`tools/run-oracle.ps1:256`), so every day generates a wave nobody has run before. This is a
pre-existing divergence that today's seed happened to reach, not a regression.

Seeds 7 and 4242 are GREEN on the same tree: `diverge 0` of 6380 rows each.

## The row

```
DIVERGE row 3655 (interactions) finditer-overlapped flags=0x2 version=V0
  pattern  '(?b)(?r)^(?:\d{2}?\s){e<=2:.}(?P<g1>[^a-f]{0}?)(?:(?(1)(?=(?&g1))[\w\s]|\s))+'
  subject  'bb0\u000d\u000a0'
  upstream matches 1 | match 0:(0,5)[(0,5)] 1:(4,0)[(4,0)] last=1/g1 fuzzy=(1,1,0)[s:1][i:2][d:]
  port     matches 1 | match 0:(0,5)[(0,5)] 1:(4,0)[(4,0)] last=1/g1 fuzzy=(1,1,0)[s:2][i:1][d:]
```

**Everything agrees except the attribution.** Same number of matches, same match span `(0,5)`, same
group 1 span `(4,0)`, same `last`, and the same edit *totals* - `(1,1,0)`, one substitution and one
insertion. The disagreement is only which subject position each edit is recorded against: upstream
says substitute at 1 and insert at 2, the port says substitute at 2 and insert at 1. The two
positions are adjacent and the pattern is `{e<=2:.}` over `\d{2}?\s`, so both readings cost the
same two edits and the engines are picking different equal-cost edit scripts.

## Re-running it

```powershell
pwsh -File tools/run-oracle.ps1 -Seeds 20260919 -Generator interactions
```

Note the `-Seeds` plural: a single seed is right for *reproducing* a known row, and never right for
calling a wave green (`docs/VERIFICATION.md` rule 7a).

## What triage has to settle, before anything is changed

1. **Which engine is right, proved against a real upstream run** - not by reading this port's code.
   Upstream `regex 2026.9.10`, the pinned version, run directly on that pattern and subject.
2. **Whether an equal-cost edit script is specified at all.** If upstream documents no preference
   between two scripts of the same cost, the port is not wrong and this becomes a pinned expected
   difference with a `docs/DIVERGENCES.md` row, not a fix.
3. If it is a port bug, it is the kind the owner's 2026-09-12 rule covers: conclusively identified
   bugs get fixed before 1.0, inherited-from-upstream included.

Do not pin it as `expected` to get a green wave before (1) and (2) are answered - a pin recorded
without deciding correctness cements whichever answer happened to be there.
