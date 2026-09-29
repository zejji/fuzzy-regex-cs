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
| D4 | The call guard blocks finite left recursion again when the attempt has already reached the whole text (see D3's design doc, section 6b) | design final 56a6bd1f. Chunk 2a-i 75247a6c (WIP on maint/d4-reach-rule; not reviewed): non-fuzzy patterns use the per-key count and return cut; 17 red rows green; ratchet and oracle green at 3 seeds. Two D3 polynomial rows now exceed 1M steps, red until C2 (do not merge before C2). Next: the grid/lb/A-B jobs, a review, then 2a-ii | `maint/d4-reach-rule` (design on `design/d4-reach-rule`) |
| D10 | A group call inside a failed lookaround leaves a capture behind. It is the same root cause as the leak that forces C1's lookaround exclusion | `(a)(?:(?!.(?1))\|.)+?b` over 'aaab': group 1 [0,1][2,1] vs [0,1] | main |
| D26 | Partial with fuzzy matching: a fuzzy insertion at the text's edge on a zero-width test is not treated as running out of text, so an undetermined answer never tries the insertion (found by D11 chunk 2's reviewer; it predates chunk 2). Goes with D11 chunk 8 | `(?:a$(?<=:)){i<=1}` match 'a' partial: None vs P(0,1) (witness ':'), also its `(?m:$)`, `{e<=1}` and `(?r)(?m:^)` forms. The `\Z` and reversed `^` forms were fixed by D11 chunk 3's RC5. Still open: an undetermined test on a fuzzy node never offers the insertion (Matcher.cs:9207) | fixed on maint/d11-partial-boundary by chunk 6's UndeterminedFuzzyRetry (b734b23a; confirmed by review over every listed form); moves to Completed when D11 merges
| D28 | Partial: an anchor that is not at the pattern's head is not failed at the edge, so patterns no extension can complete report a partial (inherited; the same answers at 11ccae7 and upstream). Found by D11 chunk 2's builder and reviewer; the head-of-pattern case inside w is fixed in chunk 2 | `(?r)b+$` search ' ': P(0,0), expected None. `(\A)x`, `(?>\A)x`, `(?=\A)x`, `(?:\A){0,1}\Ax` and `\Gx` search ':': P(1,1), expected None | none yet |
| D29 | Performance on main: WorkloadBenchmarks.FuzzyNoMatchLong is 1.23-1.25x slower than the 2026-09-27 baseline, and PatternCacheBenchmarks.UncachedFuzzyIsMatch allocates 1.18x more (11104 to about 13100 B). The cause is one of the merges since 2373e27 (D7, D14, D22, D24/D25, D13); bisect it | A/B 2026-09-29 on a quiet machine, main e67ef400+ | none yet |
| D32 | Test flakiness: AllocationTests.The_empty_iteration_record_allocates_only_for_the_states_it_keeps (F21) failed once in a full-suite run on D11's branch (961,600 bytes against a limit of 800,400) and passed on 2 isolated and 2 full reruns. It counts allocation per thread and retries on a GC pause, so something else on the thread (JIT or tiering?) allocates under load. Find the cause and make the bound machine-independent | D11 chunk 5 builder, 2026-09-29 | none yet |
| D33 | Performance: C2 and the failed-call memo are off for the whole pattern when a call, fuzzy section or \K sits in a lookaround, atomic group, possessive repeat or conditional test, or under (*PRUNE), (*SKIP), POSIX or partial matching. Once D4 lands, ambiguous and empty-cycle recursion is then 2^n where main is fast but wrong. Follow-up: decide UseCallMemo per call site (found by D4's design review) | `(?:|(?R)a|(?R)b)` plus `(?=(?R))` elsewhere, a^n: 2^n calls (model) | pinned by SHORTCUT work-counter tests when D4 lands |

## In progress

| # | Defect | Repro (port vs expected) | Stage | Where |
|---|---|---|---|---|
| D11 | Phantom partial matches at a boundary (F2; includes S3-F1 and oracle cluster A). The biggest item; design first. Owner rulings: Q1 = B, Q2 = B | see the 2026-09-26 handover | design final b243063. Chunks 1-6 CLEAN (chunk 6 b734b23a: lookaround cut rule, dedup, the run-out in a lookaround body; the guard rule not needed so far). Next: chunk 7 (conditions and NoBranch; the verb-in-condition SHORTCUT from chunk 5 goes then) | `maint/d11-partial-boundary`; design on `design/d11-partial-boundary` |
| D23 | A backtracking verb inside a lookbehind: the port's answer differs from upstream's and from PCRE2's (found by D11's design, ledger 53 draft on its branch). PCRE2 10.47 runs lookbehind bodies left to right, so `$` fails before `(*SKIP)` is reached and the empty alternative matches. That reading is principled, and it is the expected answer for every form. Research the verb model in lookbehinds | `(?m)(?<=$(*SKIP)\|a*)` over 'ab': port match None, search (2,2); upstream and PCRE2 (0,0). Without (?m): upstream None/(2,2), PCRE2 (0,0). More first-pass rows from D11's verb grid (the same at 11ccae7): `(?m)(?<=\M(*SKIP)|(*PRUNE)\B)*` match ':ab' gives None, the judge F(0,0); `(?m)(?>.\M|(*SKIP)(?!(*FAIL)a|\b(*SKIP))*$)?\W*?` over ':' gives None, the judge F(0,0) | design be1f62f, amended after review (partial at p, fuzzy width, captures order a,b,c, \K rows). About 250-300 lines. Builds after D11, because D11's model assumes right-to-left bodies | `design/d23-verb-lookbehind` |
| D18 | Partial matching checks each undetermined decision at the text's edge on its own, so contradictory assertions at one position give a phantom partial (D11's limit 1; SHORTCUT in D11) | `a\b\B` over 'a' search: P(1,1) vs None | design final f40f0623 (reviewed; cells cover every case form; 0 new misses over 5,976 judged rows on the extended grid). Build as slice D18a in chunks A1-A4 after D11 merges | `design/d19-runout-presumption` |
| D19 | Partial matching presumes the rest of the pattern can match once a character is taken past the edge (D11's limit 2; upstream issue 367). Decidable only without backreferences and without calls or recursion (regular-language emptiness, possibly exponential); calls make it context-free, and with lookarounds emptiness is undecidable. Research the split | `(?:a\B)+` fullmatch 'a': partial vs None | design final f40f0623: with D18a it removes 75% of phantoms on the extended grid (82% on D11's grid), 0 new misses. Build in chunks B1-B6 after D18a. Limits: commitments, negative lookarounds that read characters, captures set inside w, fuzzy groups (limit 8) | `design/d19-runout-presumption` |

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
| F15 | Fuzzy backreference retry opened a search with an insertion at the search anchor, ASCII included (it kept upstream's retry rule `new_folded_pos != folded_len`; it now uses PermitInsertionInFold, as D7 does) (was D25) | main e67ef400 |
| F16 | Full-fold fuzzy backreference, group side: an expanding character inside the captured group (ß) is edited as one character, in the literal ß's order of edit kinds, so a backreference costs what its literal costs. It also fixes an impossible match (`(?fi)(aß)-(?:){s<=2}` over 'aß-ß') and a read past the slice edge (extends ledger 52) (was D24) | main e67ef400 |
| F17 | Fuzzy exact-deletion retry and failed-call memo (ledgers 42 and 44, C1), plus ruling A: an explicit empty alternative is an optional exit. Compile-time ExactDeletionCeiling keeps FuzzyShort within noise of main (was D1) | main fa42f231 |
| F18 | A fuzzy repeat of a section that only spends errors iterated until the stack ran out; fixed by D1's needed rule, pinned in FuzzyNeededEmptyIterationTests (was D2) | main fa42f231 |
| F19 | A fuzzy insertion was never tried before a failing lookaround (S3-F2; upstream wrong, ledger 50, pinned). Failing fuzzy lookarounds search up to about 13% longer, because more candidates are valid (was D8) | main bdf87df6 |
| F20 | A section's minimum error count was met in the wrong order (F-D; upstream's END_FUZZY is wrong, ledger 51, pinned). It unblocked D7's minimum-check witness `(?fi)(?:st){1<=s<=1,i<=1,e<=1}` over 'ßt' (was D9) | main bdf87df6 |
| F21 | An empty iteration that flipped a tested group between spans at one position counted as progress for ever. Now a per-run record of repeat states (position, capped count, error counts clipped at the pattern's largest limit, tested-group spans) ends the loop; cleared at the end of each call. Removes a doubling-per-character slowdown on main (was D17) | main eb5124ad |
| F22 | A fuzzy repeat whose empty iterations only spent edits looped until the stack ran out; already fixed by F-A's needed rule (F18), pinned in EmptyIterationCycleTests (was D30) | main eb5124ad |
| F23 | A fuzzy repeat around empty-able group-setting iterations ran without bound (both engines); already fixed by F-A's needed rule (Matcher.EmptyIterationAdmitted). Pinned by 28 cases in FuzzyNeededEmptyIterationTests over a 5,144-row variant grid (was D27) | main 0922b568 |
| F24 | Not a defect: the port's None for the D31 row is pinned divergence ledger 47. A (*SKIP) or (*PRUNE) inside an unfinished atomic group fails the whole attempt, and an optional or repeat around the group does not rescue it. PCRE2 10.47 agrees on every sibling; upstream confines the verb to the group. Ten rows pinned in VerbScopeTests (was D31) | main e98b6f36 |
| F25 | A same-position group call was refused when a capture read by a conditional or backreference had changed. The call guard now keys on what readers see (a set bit, the text, or the span inside a repeat or fuzzy pattern), and the memo keys open calls as a set filtered by reach and set groups; CouldRefuseInside keeps it k·2^k. Read-group recursion +1-9%; the empty-groups a^n shape +80% as necessary work (was D3) | main ce904e6c |
| F26 | A conditional on a capture set inside a lookahead, with recursion, gave the wrong start; fixed by D3's capture key (was D5) | main ce904e6c |

Defects fixed before 2026-09-27 are recorded in `docs/plan/upstream-reports/LEDGER.md` (upstream
bugs, entries 1-49) and `docs/DIVERGENCES.md`.

## After To do is empty

Run another sweep (`.claude/driver/sweep-brief-2026-09-25.md`) and add whatever it finds here.
