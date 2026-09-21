Review C:/Users/gerard.howell/source/repos/fuzzy-regex-cs for DEFECTS only. Scope: everything
since commit 99d9294 - `git diff 99d9294 -- .` for the committed part, plus `git diff` and the
untracked files under `tools/probes/` for the uncommitted part (run `git status --porcelain`).
That is three sittings of slice S57b, which judges the twenty red rows of a 6000-row
differential-oracle gate against Python `regex 2026.9.10`.

CONTEXT
- `tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs` holds the pins. Each entry says why this
  port's answer differs from upstream's on named rows, and is keyed either on the row text
  (`Question(row)`) plus this port's exact `ours.Describe()` string, or on a recorded control
  outcome. Long prose `Reason` strings are deliberate and are the evidence record; do not report
  their length or style.
- New in sitting 5 (2026-09-21, uncommitted): the entry
  `reversed-body-change-lands-where-the-lookahead-reached` for row 72790 of the seed 20260921 gate,
  with two gap tests in `tests/FuzzyRegex.Tests/Gaps/Engine/FuzzyCountsAndChangesTests.cs` and
  ledger entry 26; and row 74947 added as the tenth row of the existing
  `bestmatch-loses-a-partial`, with a paragraph in its `Reason`, a gap test in
  `FuzzyBestMatchTests`, a paragraph in ledger entry 13 and the probe
  `tools/probes/s57b-row74947-two-doors.py`. Sitting 3 had proposed a separate
  `reversed-skip-missing-match` family for 74947; sitting 5 rejected that and the notes say why. So
  "the notes propose a family that the pin does not use" is expected, and a wrong reason for the
  change is a finding.
- New in sitting 3: entries `leaked-changes-beside-a-truncated-partial`,
  `skip-moved-slice-changes-the-match-set`, `reversed-anchor-cannot-compile-a-full-fold`, and an
  eighth row added to the existing `bestmatch-walk-truncated-by-a-skip`. Four new gap tests in
  `tests/FuzzyRegex.Tests/Gaps/Engine/`. Six new probes under `tools/probes/`. A paragraph added to
  ledger entry 6 in `docs/plan/upstream-reports/LEDGER.md`, and notes in
  `docs/plan/slices/notes/S57b-sittings.md`.
- ALSO NEW, and judged the same way: the gate was re-recorded at `-Count 6000` and its third default
  seed is the RUN DATE, so seed 20260921 drew 11 rows nobody had seen. Nine are members of families
  already pinned and were added as rows, each with a paragraph in its entry's `Reason` dated
  2026-09-21: rows 100927, 101194, 101236 and 105994 to `search-start-partial`; 104371 to
  `partial-retry-carried-slice-forward`; 119504 to `end-of-line-reads-a-skip-moved-slice`; 124755 to
  `bestmatch-loses-a-candidate`; 74201 to `posix-fuzzy-contradicts-its-own-flagless-answer`; 99993 to
  `reversed-anchor-cannot-compile-a-full-fold`. The batch instrument for all nine is
  `python tools/probes/gate-divergence-doors.py 20260921`, which reads `report-20260921.txt` and
  `wave-20260921.jsonl` under `TestResults/oracle/` and puts every judged family's own control to
  every diverging row. Sitting 5 re-recorded that seed green, so the bare-seed form now finds
  nothing to work on; pass the rows instead, `gate-divergence-doors.py --rows <file>`. Rows 72790 and 74947 were left unpinned by sitting 3 and are pinned by
  sitting 5, so the gate is now green at all three default seeds - re-run 2026-09-21, seeds 7, 4242
  and 20260921, 0 of 126,080 rows diverging at each.
- ALSO KNOWN, not a finding: `pwsh -File tools/run-oracle.ps1 -Count 6000 -Generator
  fuzzy,interactions -Seeds 99991,57057` is RED with four rows at 99991 and six at 57057. Those ten
  are a fresh unjudged batch recorded in `docs/plan/STATE.md` for the next sitting. Report a defect
  in how they are recorded, not their existence.
- DELIBERATE, do not report: this port diverging from upstream on these rows; the tests asserting
  this port's answer rather than upstream's; probe files living in `tools/probes/`; the absence of a
  `docs/DIVERGENCES.md` row (the blanket "Inherited upstream bugs are fixed here" row covers these
  families).

SOURCE OF TRUTH AND HOW TO RUN IT
- Upstream is the pinned submodule; the probes below print its answers. Run from the repo root:
    python tools/probes/s57b-leak-beside-truncation.py
    python tools/probes/s57b-skip-moved-slice-changes-the-match-set.py
    python tools/probes/s57b-bestmatch-walk-row76160.py
    python tools/probes/s57b-upstream-firstset-indexerror.py
    dotnet run tools/probes/s57b-port-firstset-indexerror.cs
    python tools/probes/s57b-posix-overcharges-a-named-list-section.py
    python tools/probes/s57b-row72790-changes-in-a-scan.py
    python tools/probes/s57b-row72790-change-order.py
    dotnet run tools/probes/s57b-row72790-port-changes-in-a-scan.cs
    python tools/probes/s57b-row74947-two-doors.py
    python tools/probes/gate-divergence-doors.py --rows tools/probes/s57b-gate-rows.jsonl
- The rows themselves are `tools/probes/s57b-gate-rows.jsonl`, keyed by `s57bRow`. A row's `flags`
  field is a `regex` module flag word (A=0x80 B=0x1000 E=0x8000 F=0x4000 I=0x2 M=0x8 P=0x10000
  R=0x400 S=0x10 U=0x20 V0=0x2000 V1=0x100 W=0x800 X=0x40).
- Tests: `dotnet run --project tests/FuzzyRegex.Tests` and
  `dotnet run --project tests/FuzzyRegex.OracleTests`. The oracle project's
  `Our_own_answers_never_contradict_themselves` needs a real recorded wave in
  `TestResults/oracle/wave.jsonl`; if the last thing run was a row replay it fails for that reason
  alone, which is not a finding.
- Row replay: `pwsh -File tools/run-oracle.ps1 -Rows tools/probes/bestmatch-walk-truncated-rows.jsonl`

RULES
- Part of the tree is UNCOMMITTED and there is NO commit to fall back to for it. NEVER
  run `git checkout`, `git restore`, `git stash`, `git reset` or `git clean`, on any path, for any
  reason, and do not edit any file - report, do not fix.
- Do NOT run `pwsh -File tools/run-oracle.ps1` without `-Rows` or `-SkipRecord`: a bare run
  re-records three 126,080-row waves and takes half an hour. `-SkipRecord` re-consumes
  `TestResults/oracle/wave.jsonl`, which currently holds the `fuzzy,interactions` wave at seed
  57057, not the default gate. To see one pin classify, replay its own row:
  `pwsh -File tools/run-oracle.ps1 -Rows <file>` with a one-row file cut from
  `TestResults/oracle/wave-20260921.jsonl` (the file has a header line, so row N is line N+1).
- A finding MUST come with a reproduction: the exact command and its exact output, or a failing
  test. No prose rationale, no speculation, no "consider whether".
- Style, naming and structure opinions are OUT OF SCOPE. Do not report them.
- Specifically hunt for: a quoted number, span, count or change list that the probe does not
  actually print; a pattern, subject or FLAG WORD in a test that differs from the row in
  `s57b-gate-rows.jsonl` it claims to be; an entry whose `Applies` predicate would swallow rows it
  should not, or that cannot match the row it names; an entry whose claimed mechanism cites an
  upstream line that says something else (`upstream/src/_regex.c`,
  `upstream/regex/_regex_core.py`); a claim in the ledger paragraph or the notes that its own probe
  contradicts; UTF-16 and codepoint positions mixed up (the recorder converts to UTF-16, raw Python
  prints codepoints).
- Verify the build and the test suites; report exact counts.

OUTPUT: numbered findings only. Each: `file:line` - one-line defect - exact reproduction command and
output. If nothing, say "No defects found." No preamble, no summary of the diff, no praise.
