# Complete matrix, Part B draft: triage of the full run (2026-09-30)

Input: the full run of `matrix/harness` f05f8ff9 (12,457 rows, `.claude/worktrees/matrix-2/.scratch/matrix/results/full/`,
read only) and the C7 run beside it (`results/c7/`, 70 failures). This branch re-judged a copy in
`.scratch/matrix/results/triage/` after fixing the judges below. `src/` is untouched and equal to
main 3def5eba, so every "port" answer here is main's.

Authorities used: PCRE2 10.47 (pip `pcre2`, which wraps it) and Perl 5.42.3, run for each witness
below; Part A of `matrix/answer-key` 3e255f4e; the owner's rulings of 2026-09-30 (D51 = option (a),
OPEN-1 = a verb in a call ends the whole attempt, OPEN-2 = D54, OPEN-3 = option 3).

## 1. The judges, validated

Each check was tested for rows it judges wrongly before any count was trusted. Where a judge was
wrong it was fixed under `tools/`, that check was re-run over every row, and the validation
controls (`tools/matrix/controls.py`, now 19 rows) were re-run: all OK.

| Check | Raw failures | Tool artefacts | Left | What was wrong with the judge |
|---|---|---|---|---|
| C6 | 548 | 548 | 0 | The reference matcher read any unknown escape as a literal (`\G\Ab` matched the text "GAb"), read `a?+` as two repeats, dropped the errors of a fuzzy section inside a lookaround, tried lookbehind starts in an order no engine uses (verbs and captures there), never let an insertion pass a failing `^`/`$` (its own docstring says upstream does), ended the attempt on a verb inside a NEGATIVE lookahead (PCRE2, Perl and upstream make the assertion true), and ran in the "perl" empty-iteration mode that ledger 44 rejected. Fixed in `tools/probes/fuzzy-reference-matcher.py` (reject what it cannot model; model anchors and negative lookaheads) and `tools/matrix/pyworker.py` (mode "needed"). Applicable rows fell from 2,212 to 875. |
| C1x | 27 | 25 | 2 | A raw string compare, so pinned divergences counted as failures. It now accounts as C1 does: port-runner answers each row under the oracle's fuzzy ablations (entries 42, 44, 51, 50, anchor pin, default boundary, SKIP timing, verb scope, call features, doubled insertion guard) and a row one of them takes away is EXPECTED; upstream's leaked `fuzzy_changes` are re-asked anchored, as the recorder does; the reversed partial at a slice start is ledger 24. 23 were entries 42/44/51, 1 leak, 1 ledger 24. |
| C2 | 501 | 0 | 501 | The written-out form was checked for group numbering, `\G`, `(?1)` before its group and flags. The orchestrator's point holds in principle (a called group keeps its DEFINITION's flags), but no C2 row has a scoped flag, so no row is affected; marked `SHORTCUT:` in `gen.py`. |
| C4 | 12 + 114 phantoms | 3 + 11 | 9 + 99 | (a) Both judges report a match/fullmatch partial as P(0,n); with a `\K` passed the start is the `\K` position, as upstream's own partial is (`(?:ba)*+.\K\w.` match 'baab' partial is P(3,4) upstream and here). Fixed in `run.py`. (b) The 3-character bound: a deeper judge (`.scratch/triage/deepjudge.py`: 5 characters over the judge alphabet, then 6-8 over a/b/x/space, 60 s per row, upstream non-partial answers) completes 11 phantoms with 4-6 characters, so they are not phantoms; 4 more it cannot judge (upstream MemoryError or cap). The other 99 phantoms have no completion within that bound: "no completion up to 5 characters (8 over a, b, x, space)", not proven impossible. |
| C1 | 24 | 7 (key gaps) | 17 | The oracle's own judge. 5 rows are pinned divergences (42, 44 or 51) that come out right under the ablation except for upstream's leaked change positions, and 2 are DIVERGENCES "BestMatch and EnhanceMatch keep a fit that ends in trailing insertions" (port-runner's `xdoubled` reproduces upstream's None exactly). ExpectedDivergences' keys are narrower than those entries; nothing was changed in the oracle. |
| C7 | 70 | 0 | 70 | Read as delivered; its survey copy's `(?1)` parse bug was already fixed. |

## 2. Root causes

Main shares every answer below (port-probe, Release, main's src). "Upstream" is regex 2026.9.10.

| Root cause | Mechanism (file:line) | Minimal witness: main / correct | Authority | Checks and rows | Upstream | Register | Confidence |
|---|---|---|---|---|---|---|---|
| Captures made inside a call are kept in the history | Captures pushed inside a called copy are not popped on return (the span is restored, the history is not) | `(?P<g1>a)(?1)` fullmatch 'aa': c[(0,1)(1,2)] / c[(0,1)] | D51 ruling; PCRE2 and Perl give g1 = (0,1) | C2 492, C7 67 | shares | D51 | High: C2 454 rows equal the capturing written-out form exactly (copies keep their captures under the group's name), 38 more (numbering shifts, depth-bound recursion) meet the weaker predicate "spans and every group equal, only extra history"; all 67 C7 rows equal PCRE2 and Perl in span and every group |
| Call guard blocks finite left recursion | D4's reach rule, unfixed on main | `(?P<g1>(?:a*(?1)b\|.))` (?fwi) match 'aaabbbb' partial: (0,5) / (0,7); `(?P<g1>[ab](*SKIP)a*(?1)?)` (?r) finditer 'aabaaabb': (6,8) / none (OPEN-1) | written-out form, depths 3-6 stable; OPEN-1 | C2 7 (12140, 3803, 1163, 11830, 3280, 11936, 12126) | MemoryError | D4 (fixed on `maint/d4-reach-rule` 5992368c: all 7 answer as the written-out form there) | High |
| A conditional's lookaround TEST is matched fuzzily | upstream `_regex_core.py:3266` compiles the test with the section's `fuzzy`; a plain lookaround compiles its body exact (`:~3180`); port `Nodes.cs:4401` | `(?:(?(?=a)ab\|b)){s<=1}` over 'b': None / (0,1) | answer key A3 fuzzy+conditional; ablation below | C1 5 (528, 1535, 1539, 2485, 8878), C1x 1 (11769) | shares | D52, cause now traced | High: patching the test to compile exact turns all three D52 witnesses right and changes every listed row to its exact-test answer |
| A NEGATIVE conditional loses the alternatives of a branch at the start of its yes-branch | upstream `_regex.c:23203-23208` skips a CONDITIONAL's true node past a BRANCH when `next->nonstring.true_node` is NULL, which is always true for a BRANCH; it means `next_2`. Port `Optimiser.cs:194-200` (`next.TrueNode is null`) | `(?(?!a)(?:x\|))x` over 'x': None / (0,1); `(?(?<!x)(?:ab\|a))c` over 'ac': None / (0,2) | PCRE2 and Perl (0,1), (0,2); `(?(?=x)(?:x\|))x` is (0,1) everywhere | C7 3 (8301, 11941, 11948) | shares | NEW-1 (inherited) | High: every variant fits (positive test fine; a leading `x?` fine; `(?:xz\|x)` fine because it is factored to `x(?:z\|)`) |
| BESTMATCH after a `(*SKIP)` keeps a costlier match | not traced: upstream's best-match pass does not reach the exact fit once the verb has run | `(?b)x(*SKIP)(?:ba+){e<=1}` match 'xbaabbbx': main (0,4) exact, upstream (0,5) one substitution | owner ruling "lowest-cost match"; without the verb upstream gives (0,4) | C1 3 (5849, 8589, 8595) | upstream wrong, port right | NEW-2 (port right; needs a pin and a draft report) | Medium |
| ENHANCEMATCH under POSIX | upstream does not improve the fit when POSIX is set; the port does | `(?pew)(?:b?){d<=1}` over 'a': main (0,0) no errors, upstream one deletion; upstream `(?ew)` gives no errors | README: ENHANCEMATCH "attempt[s] to improve the fit" | C1 2 (11574, 11578) | differs | NEW-3 (owner: is the port right?) | Low |
| ENHANCEMATCH reaches a lower-error fit with a different span | not traced | `(?e)(?P<g1>(?:a+(?:ab\|a)){d<=1}){1,2}` over 'xaaabaxa': main (1,5) exact, upstream (1,6) two deletions (both (1,6) without (?e)) | as above | C1 2 (7619, 11464) | differs | NEW-4 (owner: is the port right?) | Low |
| A partial search after `(*SKIP)` skips a live partial start | upstream moves the next start to the SKIP position although the attempt has not been backtracked past the verb | `aa(*SKIP)x` search partial 'axaa': main P(2,4), upstream P(4,4) | answer key A2 partial ("least start from which some continuation matches"; 'axaax' matches at 2) | C1x 1 (12240) | upstream wrong, port right | NEW-5, unless an existing pin covers it | Medium |
| Partial phantoms: edge decisions taken one at a time, or the rest of the pattern presumed matchable | D18, D19 (designs on `design/d19-runout-presumption`); D28 for anchors off the head; D53 for verbs | `(?!a)a(?:ab\|a)` (?e) search 'xb': P(2,2) / None; `\Gx` (?p) search 'bxx': P(3,3) / None | brute judge, bounded as above | C4 phantoms 89, C1 3 (3914, 4191, 4395), C2 1 (3576, the written-out form) | shares | D18/D19 (+ D28, D53) | Low per row: grouped by the zero-width construct each reads (`.scratch/triage/phgroup.py`), not traced one by one |
| Partial answers `maint/d11-partial-boundary` already changes | D11 chunks, D41 | `(?:abx){1<=i<=2}` (?e) search 'abx' partial: main P(1,3), branch and judges P(0,3) | brute judge | C4 5 fails (1556, 10947, 1922, 733, 1551), 10 phantoms | varies | D11 / D41 (branch f7da26d4) | Medium: the branch's answers were read, not reviewed |

Unsettled rows (not counted as root causes):

- C4 5168, 5888, 7088: BESTMATCH or ENHANCEMATCH partial searches. The judges take the least start
  of `search(t + w)`, but under (?b) and (?e) `search` does not return the leftmost match, so the
  partial rule has no defined answer here. Owner question.
- C4 2677 is a bound artefact (the deep judge gives the port's P(0,2) with 'baaa').
- C2 3396 (a call inside a lookbehind picks g1 (1,3) where the written-out form picks (0,3)), C1 11777
  (call plus group-test conditional, captures and one count differ) and C1 471 (change positions
  only, suspected leak inside one attempt): low confidence, not traced.
- C7's 79 other rows (22 engine splits, 57 single-engine) were read as leads only; none was found to
  be a new mechanism.

## 3. Counts

Distinct root causes: 10. Existing register rows: 5 (D51, D4, D52, D18/D19 family, D11/D41).
NEW: 5 (NEW-1 inherited bug; NEW-2 and NEW-5 port-right divergences to pin; NEW-3 and NEW-4 need
an owner ruling). Numbers are for the orchestrator to assign.

Top rows by count: D51 559; D18/D19 phantoms 93; D11/D41 15; D4 7; D52 6; C1 pinned-divergence key
gaps 7 (not a defect); NEW-1 3; NEW-2 3; NEW-3 2; NEW-4 2.

## 4. Repro commands and outputs (2026-09-30)

Port: `git show 8dda4d9:tools/probes/port-probe.cs > .scratch/probes/port-probe.cs`, then
`dotnet run -c Release .scratch/probes/port-probe.cs -- ROWS.jsonl` (flags written inline). Upstream
and PCRE2: `.scratch/triage/iso.py ROWS.jsonl`. Perl: `.scratch/triage/perlcheck.pl`. Ablations
monkeypatch upstream in memory and touch no file: `ablate_test.py` (D52), `ablate_lac.py`.

- NEW-1: `(?(?!a)(?:x|))x` / 'x': main None, upstream None, PCRE2 (0,1), Perl (0,1).
  `(?(?<!x)(?:ab|a))c` / 'ac': main None, upstream None, PCRE2 (0,2), Perl (0,2).
  `(?(?<!x)(?:b(*F)|b))` / 'b': main None, upstream None, PCRE2 (0,1), Perl (0,1).
  Control `(?(?=x)(?:x|))x` / 'x': (0,1) in all four.
- D52: `(?:(?(?=a)ab|b)){s<=1}` / 'b': main None, upstream None; test compiled exact (ablation)
  (0,1) with no errors. `(?:(?(?<![ab])baab|ab)){1<=e<=2}` / 'a baax': main and upstream (0,2) one
  substitution (the no-branch ran); exact test (0,3), one substitution and one deletion.
- D4: C2 residue rows on main and on `maint/d4-reach-rule` (`.scratch/triage/c2r.*`), e.g.
  `(?fwi)(?P<g1>(?:a*(?1)b|.))` match 'aaabbbb': main (0,5), branch (0,7), written-out (0,7).
- NEW-2: `(?b)x(*SKIP)(?:ba+){e<=1}` match 'xbaabbbx': main (0,4) 0 errors; upstream (0,5) 1 sub;
  upstream without the verb (0,4).
- NEW-3: `(?pew)(?:b?){d<=1}` / 'a': main (0,0) 0 errors, upstream (0,0) 1 deletion; `(?ew)`
  upstream 0 errors.
- NEW-4: `(?e)(?P<g1>(?:a+(?:ab|a)){d<=1}){1,2}` / 'xaaabaxa': main (1,5) 0 errors, upstream (1,6)
  2 deletions.
- NEW-5: `aa(*SKIP)x` search partial 'axaa': main P(2,4), upstream P(4,4); `aax` gives P(2,4) in both.
- D51: `(?P<g1>a)(?1)` fullmatch 'aa': main g1 (0,1) with captures (0,1),(1,2); PCRE2 and Perl g1 (0,1).

PCRE2 and Perl cannot express fuzzy constraints, BESTMATCH, ENHANCEMATCH, POSIX or REVERSE, so the
D52, NEW-2 to NEW-4 and phantom rows rest on upstream's own answers, the answer key's rules and the
ablations above.
