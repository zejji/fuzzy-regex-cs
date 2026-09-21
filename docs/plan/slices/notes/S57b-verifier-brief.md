You are the independent verifier of spec amendment 16 limb (d) for slice S57b, sitting 3, in
C:/Users/gerard.howell/source/repos/fuzzy-regex-cs. You did not see the verdicts being reached and
you are not reviewing them. Your job is to RE-RUN the claims, from the files as they stand, and
report each quoted number as CONFIRMED, DIFFERENT (with the value you got) or COULD NOT RUN (with
why).

You are working on a tree with UNCOMMITTED changes and there is NO commit to fall back to.
NEVER run `git checkout`, `git restore`, `git stash`, `git reset` or `git clean`, on any
path, for any reason. To revert a control you applied, re-edit exactly what you edited, or
use the slice's own revert script if it names one. Finish by showing `git status --porcelain`
and `git diff --stat`.

WHAT TO VERIFY, PART 2. The same treatment for the paragraphs dated 2026-09-21 that this sitting
added to six OTHER entries, each recording rows the seed-20260921 gate drew: the four rows added to
`search-start-partial`, and one each to `partial-retry-carried-slice-forward`,
`end-of-line-reads-a-skip-moved-slice`, `bestmatch-loses-a-candidate`,
`posix-fuzzy-contradicts-its-own-flagless-answer` and `reversed-anchor-cannot-compile-a-full-fold`.
Every span, count, split, deletion list and flag word those paragraphs quote is a claim to re-run.
Their instrument is `python tools/probes/gate-divergence-doors.py 20260921`, plus
`python tools/probes/s57b-posix-overcharges-a-named-list-section.py` for the POSIX row. Check as
well that each added row's JSON is the row `TestResults/oracle/wave-20260921.jsonl` holds at that
line number (the file's header line makes file line N the row numbered N) and that each added
"ours" string is the `port` line `TestResults/oracle/report-20260921.txt` records for it.

WHAT TO VERIFY, PART 1. Four entries in `tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs`:
`leaked-changes-beside-a-truncated-partial`, `skip-moved-slice-changes-the-match-set`,
`reversed-anchor-cannot-compile-a-full-fold`, and row 8 of `bestmatch-walk-truncated-by-a-skip`.
Read each entry's `Reason` and its `<remarks>` block, then check EVERY number, span, count, change
list and split it quotes, plus the same claims where they are repeated in
`tests/FuzzyRegex.Tests/Gaps/Engine/{FuzzyCountsAndChangesTests,BacktrackingVerbTests,CaseFoldingTests,FuzzyBestMatchTests}.cs`,
in the paragraph added to ledger entry 6 of `docs/plan/upstream-reports/LEDGER.md`, and in the
"Sitting 3" section of `docs/plan/slices/notes/S57b-sittings.md`.

HOW TO RUN THEM. From the repo root:
    python tools/probes/s57b-leak-beside-truncation.py
    python tools/probes/s57b-skip-moved-slice-changes-the-match-set.py
    python tools/probes/s57b-bestmatch-walk-row76160.py
    python tools/probes/s57b-upstream-firstset-indexerror.py
    dotnet run tools/probes/s57b-port-firstset-indexerror.cs
    dotnet run --project tests/FuzzyRegex.Tests
    pwsh -File tools/run-oracle.ps1 -Rows tools/probes/bestmatch-walk-truncated-rows.jsonl
The last one rewrites `TestResults/oracle/`; run it LAST and only once.
`python` is the pinned upstream `regex 2026.9.10`. The rows live in
`tools/probes/s57b-gate-rows.jsonl`, keyed by `s57bRow`; a row's `flags` is a `regex` flag word
(A=0x80 B=0x1000 E=0x8000 F=0x4000 I=0x2 M=0x8 P=0x10000 R=0x400 S=0x10 U=0x20 V0=0x2000 V1=0x100
W=0x800 X=0x40). Check that each test's pattern, subject and OPTIONS are the row it names.
Positions: the oracle recorder converts to UTF-16, raw Python prints codepoints; say which one a
number is in when they disagree.

DO NOT run `pwsh -File tools/run-oracle.ps1` without `-Rows` or `-SkipRecord`, and do not edit any
file. A bare run re-records three 126,080-row waves and takes half an hour; `-SkipRecord`
re-consumes `TestResults/oracle/wave.jsonl`, which holds seed 20260921, and should report
`diverge 2` - rows 72790 and 74947, which this sitting deliberately did not pin.

OUTPUT: one line per claim - the file:line, the claim in a few words, and CONFIRMED / DIFFERENT
(value) / COULD NOT RUN (why). Group by entry. No preamble, no narration of what you did, no
restating of the task, no advice. End with `git status --porcelain` and `git diff --stat`.
