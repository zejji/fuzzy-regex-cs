Review C:/Users/gerard.howell/source/repos/fuzzy-regex-cs for DEFECTS only.

SCOPE: slice S57d, which is `git diff 68e693c a1b0ce1` plus the uncommitted changes on top of it
(`git status --porcelain`, then `git diff`). Nothing else. Commits 29294f9, 05220d5 and 1a36c53 are
driver and DECISIONS maintenance by someone else - out of scope - and adeb0aa is a STATE.md
checkpoint. The files in scope are:

    src/FuzzyRegex/Engine/MatchState.cs
    src/FuzzyRegex/Engine/Matcher.cs
    tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs
    tests/FuzzyRegex.OracleTests/OracleWaveTests.cs
    tests/FuzzyRegex.Tests/Gaps/Engine/PartialMatchingTests.cs
    tests/FuzzyRegex.Tests/Gaps/UpstreamIssues/InheritedIssueTests.cs
    tools/probes/pcre2-hitend-partial-span.py
    tools/probes/upstream-partial-needs-text-exhaustion.py
    tools/controls.json
    docs/COMPARISON.md, docs/DIVERGENCES.md, docs/plan/slices/notes/S57d-sittings.md

WHAT THE SLICE DID

Under `partial`, a word or grapheme boundary (`\b`, `\B`, `\m`, `\M`, `\X`) decided at the end of
the available text now makes a partial match where upstream answers no match at all. That is
upstream issue 589 / ledger entry 21, and the port takes PCRE2's soft model:

1. `MatchState.HitEnd` and `MatchState.HitEndMatchPos`. The seven boundary predicates in
   `Matcher.cs` call `NoteBoundaryAtTruncationPoint` (`Matcher.cs:1838`), which sets the flag when
   asked about a position at the truncation point - whichever way the boundary then answers - and
   returns the ordinary verdict. Nothing about the verdict changes.
2. `Matcher.DoMatch` (`:10837`) is the only reader: when the partial pass has FAILED and the flag is
   set, the answer becomes a partial spanning `HitEndMatchPos` to the end of the available text, with
   `ClearGroups()` called so no capture group is reported.
3. Two narrowings, both deliberate: the attempt must have CONSUMED something (a departure from
   PCRE2, which escalates `\b` over `''`), and the pre-existing `(?r)\b$` over `''` pin stays None -
   re-judged and upheld in `PartialMatchingTests.A_reverse_search_for_a_boundary_at_the_end_of_an_
   empty_subject_finds_no_partial_here`, whose comment was rewritten because two of its three
   original reasons were measured and do not hold.
4. New pins: five tests in `PartialMatchingTests` (`A_boundary_decided_at_the_end_of_the_text_
   reports_a_partial_upstream_denies`, `A_partial_found_the_ordinary_way_is_kept_in_place_of_the_
   boundary_one`, `A_boundary_partial_reports_no_groups`, `A_complete_match_beats_a_boundary_partial`,
   `A_boundary_that_consumed_nothing_reports_no_partial`, plus two oracle-row tests and two reversed
   ones), the inherited pin inverted in `InheritedIssueTests` (renamed from
   `A_partial_fullmatch_still_denies_...` to `A_partial_fullmatch_reports_...`), a
   `docs/DIVERGENCES.md` row, a `docs/COMPARISON.md` section, and the
   `boundary-at-the-end-of-the-text` entry in `ExpectedDivergences.cs`.

DELIBERATE, do not report:

- This port diverging from upstream on these rows, and the tests asserting THIS PORT's answer.
- The long prose `Reason` string in `ExpectedDivergences.cs`: it is the evidence record. Its LENGTH
  and STYLE are not findings. A number or span in it that the probes do not print IS.
- The `(?r)\b$` over `''` pin staying None while the rest escalates. It is argued in the test's own
  comment; a defect is a reason in that comment that the probes contradict, not the verdict.
- Probe files living in `tools/probes/`; the `SHORTCUT`-free absence of a `suite` mode in
  `tools/run-controls.py`.
- `HasABoundaryEscape` counting `[\b]` (a backspace). The comment says so and says why.

WHAT HAS ALREADY BEEN RUN, so you need not:

- `pwsh -File tools/check-ratchet.ps1`: GREEN on the working tree, 6528 tests passing, 6420
  distinct ids.
- The default oracle wave at three seeds (7, 4242, 20260921): GREEN at a1b0ce1.
- The `-Count 6000` gate, seeds 7, 4242 and 20260921: GREEN at each, 0 diverging of 126,080 rows a
  seed, run against this code in this sitting.
- `python tools/run-controls.py --ids S57d-A`: 14 diverging rows at each of seeds 7, 4242 and
  20260921, and 12 at the unused seed 31337.
- Control B, by hand: with `state.ClearGroups()` in `Matcher.DoMatch` replaced by `_ = state;`,
  `A_boundary_partial_reports_no_groups((a)(*SKIP)(b)\B)` fails on `m.Groups[1].Success` and the
  `(a)(b)\B` row still passes.
- An independent verifier re-ran every quoted upstream and PCRE2 answer in the tests, the
  `Reason` string and both docs pages: 54 confirmed, 1 different. The one it found is fixed in the
  working tree (PCRE2 has no default partial option), along with two citation corrections it
  prompted, so those three comment edits are UNREVIEWED and are in scope for you.

SOURCE OF TRUTH AND HOW TO RUN IT

- Upstream is the pinned submodule (`regex 2026.9.10`). PCRE2 is `import pcre2` (10.47).
    python tools/probes/upstream-partial-needs-text-exhaustion.py
    python tools/probes/pcre2-partial-truncation-assertions.py
    python tools/probes/pcre2-hitend-partial-span.py
  Every number, span and verdict quoted in the code comments, the test comments, `DIVERGENCES.md`,
  `COMPARISON.md` and the `Reason` string must be what these print. A mismatch is a finding, quoted.
- Tests: `dotnet run --project tests/FuzzyRegex.Tests -c Release -- --treenode-filter
  "/*/*/PartialMatchingTests/*"`, the same for `InheritedIssueTests` and `ReverseMatchingTests`, and
  `dotnet run --project tests/FuzzyRegex.OracleTests` for the pins.
  `Our_own_answers_never_contradict_themselves` needs a real recorded wave in
  `TestResults/oracle/wave.jsonl`; a failure for that reason alone is not a finding.
- A row's `flags` field is a `regex` module flag word (A=0x80 B=0x1000 E=0x8000 F=0x4000 I=0x2
  M=0x8 P=0x10000 R=0x400 S=0x10 U=0x20 V0=0x2000 V1=0x100 W=0x800 X=0x40).

RULES

- Report, do not fix. Do not edit any file. Never run `git checkout`, `git restore`, `git stash`,
  `git reset` or `git clean`, on any path, for any reason.
- Do NOT run `pwsh -File tools/run-oracle.ps1` without `-Rows` or `-SkipRecord`: a bare run records
  three 126,080-row waves and takes half an hour. A wave IS running in this repo right now, so do
  not delete or overwrite anything under `TestResults/oracle/`.
- A finding MUST come with a reproduction: the exact command and its exact output, or a failing
  test. No prose rationale, no speculation, no "consider whether".
- Style, naming and structure opinions are OUT OF SCOPE.

SPECIFICALLY HUNT FOR

- A predicate that ENDS the match instead of only flagging it - the defect that reverted S50. The
  two rows it broke are `search(r'(\.+?)\1\b', '..', partial=True)` group 1 = (0, 2) and
  `'....'` group 1 = (0, 3), pinned in `InheritedIssueTests`.
- A STALE `HitEnd` between matches: one scan reuses one `MatchState` (`finditer`, `sub`, `split`,
  `Matches`). `DoMatch` clears the flag at `:10764` - show a sequence where a later match inherits a
  flag from an earlier one, or where the clear happens on the wrong side of a retry.
- `HitEndMatchPos` being the WRONG end under `(?r)`: a reversed match runs out of text at its start,
  the far end is `Reverse ? SliceStart : SliceEnd` (`:10872`). A reported span whose start exceeds
  its end, or a span that is not the leftmost end-reaching attempt's, is a finding.
- A ZERO-WIDTH partial at the truncation point reaching a caller by any route.
- The `boundary-at-the-end-of-the-text` `Applies` predicate swallowing rows it should not, or being
  unable to match a row it names. Its four rows are named in the `Reason`; the two in `Example` must
  be byte for byte a wave file's line.
- A gap-test expectation with NO provenance - every asserted answer needs an upstream or PCRE2 run
  named beside it, or a `DIVERGENCES.md` row.
- `COMPARISON.md`'s new C# samples printing something other than what the comments claim.
- `tools/controls.json`'s new `S57d-A` entry: its `before` text must be the file's exact current
  text (`python tools/run-controls.py --check` resolves it), and removing the narrowing must be what
  the entry claims it removes.

OUTPUT: numbered findings only. Each: `file:line` - one-line defect - exact reproduction command and
its output. If nothing, say "No defects found." No preamble, no diff summary, no praise.
