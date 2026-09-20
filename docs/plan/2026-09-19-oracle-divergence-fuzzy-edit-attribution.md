# Open oracle divergence: which position gets the substitution and which the insertion

**Found** 2026-09-19 by S58's verification run, at seed `20260919`. **Not triaged, not pinned, not
fixed** - S58 is a measurement slice and is forbidden to touch `src/`. This file exists because the
oracle's own evidence (`TestResults/oracle/wave-20260919.jsonl`, `report-20260919.txt`) is
gitignored and would not survive the slice.

## It is not S58's doing

`git diff 2c1e747 b6e82db -- src` is empty: the engine is byte-identical to `S56b` (`2c1e747`,
2026-09-18 17:31), which is the last commit before S59 that touched `src/` at all
(`git log --oneline -1 b6e82db -- src`). Between the two there are 41 commits and not one of them
touches `src/`: `git rev-list --count 2c1e747..b6e82db` = 41 and
`git rev-list --count 2c1e747..b6e82db -- src` = 0.

**Every SHA here is an ancestor of `main`, checked with `git merge-base --is-ancestor`.** This
paragraph first cited `dfa8767` and `e30a4e8`, and neither is: they are pre-rebase duplicates, so
the `git diff` it quoted as empty in fact had two doc-comment lines in it and the "28 commits" was
a count over a branch nobody is on (the real figure on `main` is 41). Corrected 2026-09-19 during
S59, from the DECISIONS entry that recorded the `dfa8767` trap on the same day. The third default oracle seed is `Get-Date -Format 'yyyyMMdd'`
(`tools/run-oracle.ps1:256`), so every day generates a wave nobody has run before. This is a
pre-existing divergence that today's seed happened to reach, not a regression.

Seeds 7 and 4242 are GREEN on the same tree: `diverge 0` of 6380 rows each.

## The row

```
DIVERGE row 3655 (interactions) finditer-overlapped flags=0x2 version=V0
  pattern  '(?b)(?r)^(?:\\d{2}?\\s){e<=2:.}(?P<g1>[^a-f]{0}?)(?:(?(1)(?=(?&g1))[\\w\\s]|\\s))+'
  subject  'bb0\u000d\u000a0'
  upstream matches 1 | match 0:(0,5)[(0,5)] 1:(4,0)[(4,0)] last=1/g1 fuzzy=(1,1,0)[s:1][i:2][d:]
  port     matches 1 | match 0:(0,5)[(0,5)] 1:(4,0)[(4,0)] last=1/g1 fuzzy=(1,1,0)[s:2][i:1][d:]
```

Copied verbatim from `TestResults/oracle/report.txt`, which renders a backslash doubled - the
pattern itself has single ones. Corrected 2026-09-19 by S59's verifier, which compared a fresh
report against this block and found these five lines identical but for that escaping, the
transcription here having silently un-doubled them. Quote the report as it reads, so a later
reader can diff the two.

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
