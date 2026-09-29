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

A defect moves to Completed only when a test in the ratchet pins the fix. The fix branch moves
the defect's red test out of `OpenDefectTests` into the normal suite. It adds a regression test
for every repro and sibling case it touches, and proves each one fails without the fix. A pinned
divergence is also pinned by its ExpectedDivergences entry and control. A Completed row with no
such test is a mistake: reopen it.

## To do

| # | Defect | Repro (port vs expected) | Red test |
|---|---|---|---|
| D4 | The call guard blocks finite left recursion again when the attempt has already reached the whole text (see D3's design doc, section 6b) | `.*z\|(?:\|(?R)a)` fullmatch 'aa': None vs (0,2) | none yet |
| D5 | A conditional on a capture set inside a lookahead, with recursion, gives the wrong start (found in D3's design grids) | `.*z\|\1b\|(?(1)(?=(?<g>aa))\|(?=(?<g>a)))(?R)` search 'aab': (1,3) vs (0,3) | none yet |
| D10 | A group call inside a failed lookaround leaves a capture behind. It is the same root cause as the leak that forces C1's lookaround exclusion | `(a)(?:(?!.(?1))\|.)+?b` over 'aaab': group 1 [0,1][2,1] vs [0,1] | main |
| D18 | Partial matching checks each undetermined decision at the text's edge on its own, so contradictory assertions at one position give a phantom partial (D11's limit 1; SHORTCUT in D11) | `a\b\B` over 'a' search: P(1,1) vs None | none yet (after D11) |
| D19 | Partial matching presumes the rest of the pattern can match once a character is taken past the edge (D11's limit 2; upstream issue 367). Decidable only without backreferences and without calls or recursion (regular-language emptiness, possibly exponential); calls make it context-free, and with lookarounds emptiness is undecidable. Research the split | `(?:a\B)+` fullmatch 'a': partial vs None | none yet |
| D24 | Full-fold fuzzy backreference, group side: an expanding character inside the captured group is still edited one folded piece at a time (found by D22's builder) | `(?fi)(ß)x(?:\1){s<=1}` fullmatch 'ßxa': None in both engines, while `(?fi)(?:ß){s<=1}` matches 'a' with (1,0,0) upstream (probed 2026-09-29) | none yet |
| D25 | A fuzzy backreference's retry path can open a search with an insertion at the search anchor, in plain ASCII too (found by D22's reviewer; it predates D22, and happens with whole-character edits off) | `(?i)(?=.*?(xtj))(?:\1){e<=1}` search 'axtj': (0,4) with 1 insertion vs (1,4) with 0 edits (upstream, probed 2026-09-29; the port's literal `(?:xtj){e<=1}` agrees with upstream). Needs e<=1; i<=1 alone is fine | none yet |

## In progress

| # | Defect | Repro (port vs expected) | Stage | Where |
|---|---|---|---|---|
| D1 | Fuzzy exact-deletion retry and failed-call memo (ledgers 42 and 44, C1), plus ruling A: an explicit empty alternative is an optional exit | see ledgers 42 and 44 | quiet benchmark failed on 2026-09-28: FuzzyShort is 12-14% slower than main (A/B, 2 rounds), and the compile path allocates about 5% more. Repair round 1 running (performance only; merges main in) | `maint/fuzzy-exact-deletion-tidy` 54c6687; ruling in `docs/plan/2026-09-28-optional-vs-empty-alternative-ruling.md` |
| D2 | A fuzzy repeat of a section that only spends errors iterates until the stack runs out (queue items 4 and 5) | `(?:(?:a){d<=1})+?` fullmatch 'aaaaba': 1 GB stack exhausted vs None | fixed by D1's needed rule; merges with D1 | pinned on D1's branch. Delete the two matching OpenDefectTests when D1 merges |
| D3 | A same-position group call is refused when a capture read by a conditional or backreference has changed | `(?(a)(?(b)x\|(?<b>)(?R))\|(?<a>)(?R))` over 'x': None vs (0,1) | design reviewed; the build waits for D1 (it needs C1) | `maint/call-guard` 28a057e, `docs/plan/2026-09-28-capture-dependent-recursion-design.md`; red test on main |
| D8 | A fuzzy insertion is never tried before a failing lookaround (S3-F2) | `(?:b(?=c)){i<=1}` over 'bxc': None vs (0,2) with 1 insertion | fixed (upstream wrong, ledger 50, pinned); blind review clean; merges right after D1. Failing fuzzy lookarounds search more (417 vs 259 ms at 4.8k chars), because more candidates are now valid | `maint/d8-insert-before-lookaround` 9fed032 (on D1's branch). At merge, delete its OpenDefectTests row |
| D11 | Phantom partial matches at a boundary (F2; includes S3-F1 and oracle cluster A). The biggest item; design first. Owner rulings: Q1 = B, Q2 = B | see the 2026-09-26 handover | design final b243063. Chunk 1 9edeb8e reviewed CLEAN. Chunk 2 5f3d184 (M1 on the anchors, plus M3's Success clause brought forward; refused 9/19 and reported 24/32 green; group captures of boundary partials moved) under review | `maint/d11-partial-boundary`; design on `design/d11-partial-boundary` |
| D9 | A section's minimum error count is met in the wrong order (F-D; upstream's END_FUZZY) | `(?:a){1<=e<=2}b` over 'aab': expected an insertion | fixed (upstream wrong, ledger 51, pinned); blind review clean; witness row pinned; waits for D1 and D8 | `maint/d9-minimum-errors` de86add (on D8's branch). At merge, delete its 3 OpenDefectTests rows |
| D23 | A backtracking verb inside a lookbehind: the port's answer differs from upstream's and from PCRE2's (found by D11's design, ledger 53 draft on its branch). PCRE2 10.47 runs lookbehind bodies left to right, so `$` fails before `(*SKIP)` is reached and the empty alternative matches. That reading is principled, and it is the expected answer for every form. Research the verb model in lookbehinds | `(?m)(?<=$(*SKIP)\|a*)` over 'ab': port match None, search (2,2); upstream and PCRE2 (0,0). Without (?m): upstream None/(2,2), PCRE2 (0,0) | design be1f62f, amended after review (partial at p, fuzzy width, captures order a,b,c, \K rows). About 250-300 lines. Builds after D11, because D11's model assumes right-to-left bodies | `design/d23-verb-lookbehind` |
| D17 | An empty iteration that flips a tested group between two spans at one position counts as progress for ever: upstream's rule has no cycle check (found by D12's survey). Needs a per-run record of group states, like the fuzzy repeat memo | `^(?:(?=(?P=g)b)(?=(?P<g>ab))\|(?=(?P<g>a)))*$` over 'ab': 1 GB stack exhausted vs None (upstream: MemoryError) | design and build started | `maint/d17-empty-iteration-cycle` |

## Completed

| # | Defect | Merged |
|---|---|---|
| F1 | `\G` inside a fuzzy section threw NotImplementedException when backtracked over (ledger 48; upstream raises "invalid RE code") | main dea31f3 |
| F2 | The same-position call guard refused finite left recursion. It now uses PCRE2's reach rule (ledger 14, refined) | main 9a27677 |
| F3 | BESTMATCH fullmatch lost a candidate with trailing insertions. Upstream's END_FUZZY guard counts the errors twice (ledger 12; the port is right, pinned) | main 5e0a2cd |
| F4 | Oracle row 20260927:3732. Upstream's IGNORECASE first-set precheck refuses a cased letter with no case partner (ledger 35 H; the port is right, pinned) | main 2357e67 |
| F5 | Full-fold fuzzy: editing an expanding pattern character (ß) inside a run cost two edits (ledger 49, was D6) | main 58e57e5 |
| F6 | Exact matching: an empty iteration that changes a tested group's span counts as progress. Kept upstream's rule; the port already followed it, now pinned by a 23-shape survey and 27 tests (was D12) | main 7d39d99 |
| F7 | Test tooling: 20 `tools/controls.json` entries had lost their code site; `run-controls.py` crashed on a cp1252 console and no longer parsed the oracle summary (was D15) | main 017f578 |
| F8 | Test harness: `_generate_matrix` drew `(?b)`/`(?e)` with weighted-cost constraints (was D16) | main 017f578 |
| F9 | Performance: FuzzyLiteralFilter searched an absent piece to the end of the subject on every Matches step (quadratic, forward and reverse). It now keeps a per-scan memory on MatchState. 1M characters: 2303 to 234 ms; absent branch 4951 to 30 ms. Its two timing tests move to D13's work counter when D13 merges (was D14) | main 1b76748 |
| F10 | Test tooling: `record-oracle.py` and `run-oracle.ps1` shared one wave file, so an ad hoc recording could replace a running oracle's wave. A multi-seed run also reused the first seed's file name (`$wavePath` clobbered `-WavePath`). Now every run has its own `-run-` scratch files, cleaned up in a finally block (was D20) | main 47ea792 |
| F11 | Test tooling: controls S22-D and S42-2G never fired. S22-D now names its 10 red tests; S42-2G fires through `Bestmatch_bounds_the_whole_match_cost_of_a_trailing_insertion` (`(?b)((?:a){1s+2i<=2})(?1)$` over 'bab') (was D21) | main 47ea792 |
| F12 | Timing tests depended on machine speed. The engine now has a Debug-only work counter (steps, characters walked, characters searched, states), and speed guards assert counts instead of times. Counter tests carry `[Category("WorkCounter")]`, check their answers in both builds, skip only the count in Release, and CI runs them in Debug. The demo and page tests use fake timers. Left: `checks.html` (driven by hand; `ticks > 10` of about 30), and FailedCallMemoTests moves to the counter when D8 merges (was D13) | main d80f0bb |
| F13 | Full-fold fuzzy, subject side: a subject character that folds to several characters (ǰ) is now edited as one character, by substitution or insertion (ledger 52; upstream is wrong, pinned). The first answer's mix of edits can differ from upstream's (3 rows pinned). Text dense with expanding characters searches 16-35% longer, because more candidates are valid (as for D8). The prune's minimum check gets its witness after D9 merges (was D7) | main 0bb8ebe |
| F14 | Full-fold fuzzy backreference, subject side: a subject character that folds to several characters is now edited as one character inside a backreference (extends ledger 52; upstream is wrong, pinned). It shares D7's whole-insertion helper. ASCII within noise; dense expanding text +19% from new valid matches (was D22) | main 894c495 |

Defects fixed before 2026-09-27 are recorded in `docs/plan/upstream-reports/LEDGER.md` (upstream
bugs, entries 1-49) and `docs/DIVERGENCES.md`.

## After To do is empty

Run another sweep (`.claude/driver/sweep-brief-2026-09-25.md`) and add whatever it finds here.
