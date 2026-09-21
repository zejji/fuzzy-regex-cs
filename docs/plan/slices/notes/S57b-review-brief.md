Review C:/Users/gerard.howell/source/repos/fuzzy-regex-cs for DEFECTS only. Scope: everything
since commit cc8ebb3, which is entirely UNCOMMITTED - `git diff` plus the untracked files under
`tools/probes/` (run `git status --porcelain`). That is sitting 6 of slice S57b, which judges the
twenty red rows of a 6000-row differential-oracle gate against Python `regex 2026.9.10`. Earlier
sittings are committed and already reviewed; do not review them.

WHAT SITTING 6 DID
The extra wave `pwsh -File tools/run-oracle.ps1 -Count 6000 -Generator fuzzy,interactions -Seeds
99991,57057` was red with ten rows, four at 99991 and six at 57057. All ten are now pinned as rows
of four EXISTING entries in `tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs`:
- `bestmatch-loses-a-candidate`, rows 22 and 23 (seed 57057 row 4067, seed 99991 row 683).
- `bestmatch-loses-a-partial`, row 11 (seed 57057 row 9163).
- `search-start-partial`, rows 24 and 25 (seed 57057 rows 6441 and 8825).
- `posix-fuzzy-contradicts-its-own-flagless-answer`, rows 11 to 15 (seed 57057 rows 8690 and 9182,
  seed 99991 rows 9204, 9720 and 9951).
Each entry gained a paragraph in its `Reason` and a provenance comment above the new `ours`
strings. One new gap test,
`FuzzyPosixTests.Posix_does_not_lengthen_a_group_inside_the_same_overall_match`, covers row 9720.
Two new probe files and one modified probe are the instruments.

CONTEXT
- `ExpectedDivergences.cs` holds the pins. Each entry says why this port's answer differs from
  upstream's on named rows, and is keyed either on the row text (`Question(row)`) plus this port's
  exact `ours.Describe()` string, or on a recorded control outcome. Long prose `Reason` strings are
  deliberate and are the evidence record; do not report their length or style.
- ONE NEW TEST FOR TEN ROWS IS A DELIBERATE JUDGEMENT, not an oversight: nine of the ten rows'
  mechanisms are already asserted by the tests each family's `PinnedBy` names, and only row 9720's
  shape (POSIX lengthening a GROUP inside an overall match whose bounds do not move) had none. If
  you can show by reproduction that a named row's mechanism is asserted by NO existing `PinnedBy`
  test, that is a finding. "There should be more tests" on its own is not.
- DELIBERATE, do not report: this port diverging from upstream on these rows; the tests asserting
  this port's answer rather than upstream's; probe files living in `tools/probes/`; the absence of a
  `docs/DIVERGENCES.md` row (the blanket "Inherited upstream bugs are fixed here" row covers these
  families).

WHAT SITTING 6 ALREADY RAN, so you need not
- `pwsh -File tools/run-oracle.ps1 -Count 6000 -Generator fuzzy,interactions -Seeds 99991,57057`:
  GREEN at both seeds, 2026-09-21.
- The default gate at `-Count 6000`, one seed per run, seeds 7, 4242 and 20260921: GREEN at each.
- `pwsh -File tools/check-ratchet.ps1`: GREEN, 6503 tests passing, 6395 distinct ids, baseline
  updated from 6394.

SOURCE OF TRUTH AND HOW TO RUN IT
- Upstream is the pinned submodule; the probes print its answers. From the repo root:
    python tools/probes/gate-divergence-doors.py --rows tools/probes/s57b-extra-wave-rows.jsonl
    python tools/probes/s57b-extra-wave-flag-ablations.py tools/probes/s57b-extra-wave-rows.jsonl
    python tools/probes/upstream-partial-anchor-reachability.py tools/probes/s57b-extra-wave-partial-search-rows.jsonl
    python tools/probes/upstream-partial-anchor-reachability.py
  The last form takes no argument and must still print exactly what it printed before the change;
  a difference there is a finding.
- The ten rows are `tools/probes/s57b-extra-wave-rows.jsonl`, each tagged `s57bSeed` and `s57bRow`,
  cut verbatim from `TestResults/oracle/wave-57057.jsonl` and `wave-99991.jsonl` (each file has a
  header line, so row N is line N+1). The `port` lines are in `report-57057.txt` and
  `report-99991.txt`. A row's `flags` field is a `regex` module flag word (A=0x80 B=0x1000 E=0x8000
  F=0x4000 I=0x2 M=0x8 P=0x10000 R=0x400 S=0x10 U=0x20 V0=0x2000 V1=0x100 W=0x800 X=0x40).
- Tests: `dotnet run --project tests/FuzzyRegex.Tests` and
  `dotnet run --project tests/FuzzyRegex.OracleTests`. The oracle project's
  `Our_own_answers_never_contradict_themselves` needs a real recorded wave in
  `TestResults/oracle/wave.jsonl`; if the last thing run was a row replay it fails for that reason
  alone, which is not a finding.
- Row replay: `pwsh -File tools/run-oracle.ps1 -Rows tools/probes/s57b-extra-wave-rows.jsonl`.

RULES
- The tree is UNCOMMITTED and there is NO commit to fall back to. NEVER run `git checkout`,
  `git restore`, `git stash`, `git reset` or `git clean`, on any path, for any reason, and do not
  edit any file - report, do not fix.
- Do NOT run `pwsh -File tools/run-oracle.ps1` without `-Rows` or `-SkipRecord`: a bare run
  re-records three 126,080-row waves and takes half an hour.
- A finding MUST come with a reproduction: the exact command and its exact output, or a failing
  test. No prose rationale, no speculation, no "consider whether".
- Style, naming and structure opinions are OUT OF SCOPE. Do not report them.
- Specifically hunt for: a quoted number, span, count or change list that the probe does not
  actually print; a row's JSON in the pin that is not byte for byte the wave file's line, or an
  `ours` string that is not the report's `port` line for that row; the pattern, subject or FLAG
  WORD in the new test differing from row 9720 of `wave-99991.jsonl`; an `Applies` predicate that
  would swallow rows it should not, or that cannot match a row it names; a claimed mechanism citing
  an upstream line that says something else (`upstream/README.rst`, `upstream/src/_regex.c`,
  `upstream/regex/_regex_core.py`); a superlative such as "the first of this arm" that the entry's
  own earlier rows contradict; UTF-16 and codepoint positions mixed up (the recorder converts to
  UTF-16, raw Python prints codepoints).
- Verify the build and the test suites; report exact counts.

OUTPUT: numbered findings only. Each: `file:line` - one-line defect - exact reproduction command and
output. If nothing, say "No defects found." No preamble, no summary of the diff, no praise.
