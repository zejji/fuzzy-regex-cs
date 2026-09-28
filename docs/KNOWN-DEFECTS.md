# Known defects

Every conclusively identified defect in the port, one row per root cause, with its status. The rule
(owner, 2026-09-12): no known bug stays before 1.0, including those inherited from upstream. When
upstream is wrong and the port is right, the answer is pinned as an expected divergence
(`docs/DIVERGENCES.md`) with a ledger entry (`docs/plan/upstream-reports/LEDGER.md`). That row then
counts as fixed here.

Update this file on every change of status: when a defect is found, when a branch starts, when it
merges. The red tests for open defects live in
`tests/FuzzyRegex.Tests/OpenDefects/OpenDefectTests.cs`. They are `[Explicit]`, so the ratchet skips
them. Run them with:

    dotnet run --project tests/FuzzyRegex.Tests -- --treenode-filter "/*/*/OpenDefectTests/*"

Status values:
- **open**: no work started.
- **design**: a design note exists; nothing is built yet.
- **branch**: built on a branch, not yet merged.
- **fixed**: merged into main, with the commit given.

## Open and in flight

| # | Defect | Repro (port vs expected) | Status | Where |
|---|---|---|---|---|
| D1 | Fuzzy exact-deletion retry and failed-call memo (ledgers 42, 44, C1); includes ruling A (an explicit empty alternative is an optional exit) | see ledger 42, 44 | branch: review clean, waiting on a quiet benchmark, then merge | `maint/fuzzy-exact-deletion-tidy` 54c6687; ruling in `docs/plan/2026-09-28-optional-vs-empty-alternative-ruling.md` |
| D2 | A fuzzy repeat of a section that only spends errors iterates until the stack runs out (queue items 4, 5) | `(?:(?:a){d<=1})+?` fullmatch 'aaaaba': 1 GB stack exhausted vs None | branch: fixed by D1's needed rule | pinned on D1's branch; delete the two matching OpenDefectTests at D1's merge |
| D3 | A same-position group call is refused when a capture read by a conditional or backreference has changed | `(?(a)(?(b)x\|(?<b>)(?R))\|(?<a>)(?R))` over 'x': None vs (0,1) | design, reviewed; the build waits for D1 (needs C1) | `maint/call-guard` 28a057e, `docs/plan/2026-09-28-capture-dependent-recursion-design.md`; red test on main |
| D4 | The call guard blocks finite left recursion again when the attempt has already reached the whole text | `.*z\|(?:\|(?R)a)` fullmatch 'aa': None vs (0,2) | open (design doc section 6b) | no red test yet |
| D5 | A conditional on a capture set inside a lookahead, with recursion, gives the wrong start | `.*z\|\1b\|(?(1)(?=(?<g>aa))\|(?=(?<g>a)))(?R)` search 'aab': (1,3) vs (0,3) | open (found in D3's design grids) | no red test yet |
| D6 | Full-fold fuzzy: editing an expanding pattern character (ß) inside a run costs two edits (ledger 49) | `(?fi)(?:ßx){s<=1}` over 'ax': None vs (0,2) with 1 substitution | branch: review clean; filter repair under review | `maint/fi-sharp-s` 85925d1 |
| D7 | Full-fold fuzzy, subject side: an expanding subject character inside a run can't be edited as one | `(?fi)(?:ssx){s<=1}` over 'ǰsx': None (both engines) | open (needs a STRING_FLD matcher change) | red test on D6's branch |
| D8 | A fuzzy insertion is never tried before a failing lookaround (S3-F2) | `(?:b(?=c)){i<=1}` over 'bxc': None vs (0,2) with 1 insertion | open | red test on main |
| D9 | A section's minimum error count is met in the wrong order (F-D, upstream END_FUZZY) | `(?:a){1<=e<=2}b` over 'aab': expected an insertion | open | red test on main (3 rows) |
| D10 | A group call inside a failed lookaround leaves a capture behind; the same root cause as the leak that forces C1's lookaround exclusion | `(a)(?:(?!.(?1))\|.)+?b` over 'aaab': group 1 [0,1][2,1] vs [0,1] | open | red test on main |
| D11 | Phantom partial matches at a boundary (F2; includes S3-F1 and oracle cluster A) | see the 2026-09-26 handover | open: the biggest item; design first | owner rulings Q1 = B, Q2 = B |
| D12 | Exact matching: does a capture change count as progress in an empty iteration? Upstream says yes; re, PCRE2 and Perl say no | `^(?:(?(1)c\|z)\|())*$` over 'c' | open: survey, then decide (delegated to me 2026-09-28) | none |
| D13 | Timing tests depend on machine speed (TIMEOUT_SHAPES, demo checks, AOT smoke, FailedCallMemoTests) | FailedCallMemoTests went red under load on 2026-09-28 | open | none |
| D14 | Performance: FuzzyLiteralFilter searches an absent ASCII piece to the end of the subject on every Matches step, so the search is quadratic | found by D6's builder | open | none |
| D15 | Test tooling: 21 `tools/controls.json` entries no longer find their code site, and `run-controls.py` crashes on a cp1252 console | found by R3732's builder | open | none |
| D16 | Test harness: `_generate_matrix` draws `(?b)`/`(?e)` on weighted-cost constraints, which the other generators avoid | matrix row 7:2677 | open | `tools/record-oracle.py` ~6762 |

## Fixed since 2026-09-27

| # | Defect | Fix |
|---|---|---|
| F1 | `\G` inside a fuzzy section threw NotImplementedException when backtracked over (ledger 48; upstream raises "invalid RE code") | main dea31f3 |
| F2 | The same-position call guard refused finite left recursion; it now uses PCRE2's reach rule (ledger 14 refined) | main 9a27677 |
| F3 | BESTMATCH fullmatch lost a candidate with trailing insertions: upstream's END_FUZZY guard counts the errors twice (ledger 12; port right, pinned) | main 5e0a2cd |
| F4 | Oracle row 20260927:3732: upstream's IGNORECASE first-set precheck refuses a cased letter with no case partner (ledger 35 H; port right, pinned) | main 2357e67 |

Before 2026-09-27, see `docs/plan/upstream-reports/LEDGER.md` (upstream bugs, entries 1-49) and
`docs/DIVERGENCES.md`.

## After the list is empty

Run another sweep (`.claude/driver/sweep-brief-2026-09-25.md`) and add whatever it finds here.
