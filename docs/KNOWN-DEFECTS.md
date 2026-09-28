# Known defects

This is the one list of every conclusively identified defect in the port, one row per root cause.
The owner's rule (2026-09-12) is that no known bug stays before 1.0, including those inherited from
upstream. When upstream is wrong and the port is right, the answer is pinned as an expected
divergence (`docs/DIVERGENCES.md`) with a ledger entry (`docs/plan/upstream-reports/LEDGER.md`). That
row then counts as completed here.

Update this file whenever something changes: a defect is found, work starts, or a fix merges. Every
merge into main is followed by an update to this file. The red tests for open defects are in
`tests/FuzzyRegex.Tests/OpenDefects/OpenDefectTests.cs`. They are `[Explicit]`, so the ratchet
skips them. Run them with:

    dotnet run --project tests/FuzzyRegex.Tests -- --treenode-filter "/*/*/OpenDefectTests/*"

## To do

| # | Defect | Repro (port vs expected) | Red test |
|---|---|---|---|
| D4 | The call guard blocks finite left recursion again when the attempt has already reached the whole text (see D3's design doc, section 6b) | `.*z\|(?:\|(?R)a)` fullmatch 'aa': None vs (0,2) | none yet |
| D5 | A conditional on a capture set inside a lookahead, with recursion, gives the wrong start (found in D3's design grids) | `.*z\|\1b\|(?(1)(?=(?<g>aa))\|(?=(?<g>a)))(?R)` search 'aab': (1,3) vs (0,3) | none yet |
| D7 | Full-fold fuzzy, subject side: an expanding subject character inside a run can't be edited as one character (the twin of D6; needs a STRING_FLD matcher change) | `(?fi)(?:ssx){s<=1}` over 'ǰsx': None in both engines | on D6's branch |
| D8 | A fuzzy insertion is never tried before a failing lookaround (S3-F2) | `(?:b(?=c)){i<=1}` over 'bxc': None vs (0,2) with 1 insertion | main |
| D9 | A section's minimum error count is met in the wrong order (F-D; upstream's END_FUZZY) | `(?:a){1<=e<=2}b` over 'aab': expected an insertion | main (3 rows) |
| D10 | A group call inside a failed lookaround leaves a capture behind. It is the same root cause as the leak that forces C1's lookaround exclusion | `(a)(?:(?!.(?1))\|.)+?b` over 'aaab': group 1 [0,1][2,1] vs [0,1] | main |
| D11 | Phantom partial matches at a boundary (F2; includes S3-F1 and oracle cluster A). The biggest item; design first. Owner rulings: Q1 = B, Q2 = B | see the 2026-09-26 handover | none |
| D12 | Exact matching: does a capture change count as progress in an empty iteration? Upstream says yes; re, PCRE2 and Perl say no. To be surveyed, then decided | `^(?:(?(1)c\|z)\|())*$` over 'c' | none |
| D13 | Timing tests depend on machine speed (TIMEOUT_SHAPES, demo checks, AOT smoke, FailedCallMemoTests) | FailedCallMemoTests went red under load on 2026-09-28 | none |
| D14 | Performance: FuzzyLiteralFilter searches an absent ASCII piece to the end of the subject on every Matches step, so the search is quadratic | found by D6's builder | none |
| D15 | Test tooling: 21 `tools/controls.json` entries no longer find their code site, and `run-controls.py` crashes on a cp1252 console | found by R3732's builder | none |
| D16 | Test harness: `_generate_matrix` (`tools/record-oracle.py` ~6762) draws `(?b)`/`(?e)` with weighted-cost constraints, which the other generators avoid | matrix row 7:2677 | none |

## In progress

| # | Defect | Repro (port vs expected) | Stage | Where |
|---|---|---|---|---|
| D1 | Fuzzy exact-deletion retry and failed-call memo (ledgers 42 and 44, C1), plus ruling A: an explicit empty alternative is an optional exit | see ledgers 42 and 44 | reviews clean; waiting on a quiet benchmark, then merge | `maint/fuzzy-exact-deletion-tidy` 54c6687; ruling in `docs/plan/2026-09-28-optional-vs-empty-alternative-ruling.md` |
| D2 | A fuzzy repeat of a section that only spends errors iterates until the stack runs out (queue items 4 and 5) | `(?:(?:a){d<=1})+?` fullmatch 'aaaaba': 1 GB stack exhausted vs None | fixed by D1's needed rule; merges with D1 | pinned on D1's branch. Delete the two matching OpenDefectTests when D1 merges |
| D3 | A same-position group call is refused when a capture read by a conditional or backreference has changed | `(?(a)(?(b)x\|(?<b>)(?R))\|(?<a>)(?R))` over 'x': None vs (0,1) | design reviewed; the build waits for D1 (it needs C1) | `maint/call-guard` 28a057e, `docs/plan/2026-09-28-capture-dependent-recursion-design.md`; red test on main |
| D6 | Full-fold fuzzy: editing an expanding pattern character (ß) inside a run costs two edits (ledger 49) | `(?fi)(?:ßx){s<=1}` over 'ax': None vs (0,2) with 1 substitution | fix reviewed clean; filter repair under review | `maint/fi-sharp-s` 85925d1 |

## Completed

| # | Defect | Merged |
|---|---|---|
| F1 | `\G` inside a fuzzy section threw NotImplementedException when backtracked over (ledger 48; upstream raises "invalid RE code") | main dea31f3 |
| F2 | The same-position call guard refused finite left recursion. It now uses PCRE2's reach rule (ledger 14, refined) | main 9a27677 |
| F3 | BESTMATCH fullmatch lost a candidate with trailing insertions. Upstream's END_FUZZY guard counts the errors twice (ledger 12; the port is right, pinned) | main 5e0a2cd |
| F4 | Oracle row 20260927:3732. Upstream's IGNORECASE first-set precheck refuses a cased letter with no case partner (ledger 35 H; the port is right, pinned) | main 2357e67 |

Defects fixed before 2026-09-27 are recorded in `docs/plan/upstream-reports/LEDGER.md` (upstream
bugs, entries 1-49) and `docs/DIVERGENCES.md`.

## After To do is empty

Run another sweep (`.claude/driver/sweep-brief-2026-09-25.md`) and add whatever it finds here.
