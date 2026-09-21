You are the independent verifier of spec amendment 16 limb (d) for slice S57b, sitting 6 - the ten
rows judged since commit cc8ebb3, all of them UNCOMMITTED - in
C:/Users/gerard.howell/source/repos/fuzzy-regex-cs. You did not see the verdicts being reached and
you are not reviewing them. Your job is to RE-RUN the claims, from the files as they stand, and
report each quoted number as CONFIRMED, DIFFERENT (with the value you got) or COULD NOT RUN (with
why).

You are working on a tree with UNCOMMITTED changes and there is NO commit to fall back to.
NEVER run `git checkout`, `git restore`, `git stash`, `git reset` or `git clean`, on any
path, for any reason, and do not edit any file. Finish by showing `git status --porcelain` and
`git diff --stat`.

WHAT TO VERIFY. Ten rows were added to four EXISTING entries in
`tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs`. For each entry, read the paragraph dated
2026-09-21 in its `Reason`, the provenance comment above the new `ours` strings, and the matching
"Sitting 6" section of `docs/plan/slices/notes/S57b-sittings.md`. Every span, count, change list,
split, match count and flag word they quote is a claim to re-run.

- `bestmatch-loses-a-candidate`, rows 22 and 23: seed 57057 row 4067, seed 99991 row 683.
- `bestmatch-loses-a-partial`, row 11: seed 57057 row 9163.
- `search-start-partial`, rows 24 and 25: seed 57057 rows 6441 and 8825. The claim to check
  hardest is that upstream's own search answer (1, 3) is NOT reachable by any anchored call in the
  searched region, while this port's answer IS, and that the `(*PRUNE)` door answers upstream's
  span rather than this port's - the reverse of this entry's earlier rows.
- `posix-fuzzy-contradicts-its-own-flagless-answer`, rows 11 to 15: seed 57057 rows 8690 and 9182,
  seed 99991 rows 9204, 9720 and 9951. Row 9720's claims are repeated in the new test
  `FuzzyPosixTests.Posix_does_not_lengthen_a_group_inside_the_same_overall_match`; check its
  pattern, subject and OPTIONS against the row, remembering that the row's pattern carries U+10400
  itself and not the six-character escape text.

Check for every row that the JSON added to the entry's rows constant is byte for byte the line the
wave file holds (`TestResults/oracle/wave-57057.jsonl` and `wave-99991.jsonl`; the header line
makes row N line N+1) and that the added `ours` string is the `port` line
`report-57057.txt` / `report-99991.txt` records for that row.

Two entries cite upstream's README as the contract they judge against:
`upstream/README.rst` lines 175, 177, 590 and 592. Read those lines and report whether they say
what the entry says they say.

HOW TO RUN THEM. From the repo root:
    python tools/probes/gate-divergence-doors.py --rows tools/probes/s57b-extra-wave-rows.jsonl
    python tools/probes/s57b-extra-wave-flag-ablations.py tools/probes/s57b-extra-wave-rows.jsonl
    python tools/probes/upstream-partial-anchor-reachability.py tools/probes/s57b-extra-wave-partial-search-rows.jsonl
    dotnet run --project tests/FuzzyRegex.Tests
    pwsh -File tools/run-oracle.ps1 -Rows tools/probes/s57b-extra-wave-rows.jsonl
The last one rewrites `TestResults/oracle/wave.jsonl` and `report.txt`; it does NOT touch the
per-seed files. Run it last and only once.

`python` is the pinned upstream `regex 2026.9.10`. A row's `flags` is a `regex` flag word (A=0x80
B=0x1000 E=0x8000 F=0x4000 I=0x2 M=0x8 P=0x10000 R=0x400 S=0x10 U=0x20 V0=0x2000 V1=0x100 W=0x800
X=0x40). Positions: the oracle recorder converts to UTF-16, raw Python prints codepoints, and
several of these subjects are astral, so say which one a number is in when they disagree.

DO NOT run `pwsh -File tools/run-oracle.ps1` without `-Rows`: a bare run re-records three
126,080-row waves and takes half an hour.

OUTPUT: one line per claim - the file:line, the claim in a few words, and CONFIRMED / DIFFERENT
(value) / COULD NOT RUN (why). Group by entry. No preamble, no narration of what you did, no
restating of the task, no advice. End with `git status --porcelain` and `git diff --stat`.
