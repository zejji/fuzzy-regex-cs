---
slice: S04
phase: 1
title: Port upstream tests - test_various, captures, named lists, fuzzy
delivers: []
---

# S04 - Port upstream tests: the feature body (lines 1741-3084)

Follow the `port-tests` skill.

## Scope

`upstream/regex/tests/test_regex.py` lines 1741 to 3084.

Notable methods in range: `test_various` (1741, about 735 lines and by far the densest method in
the file - a grab bag of patterns and expected spans), `test_replacement`, `test_common_prefix`,
`test_captures`, `test_guards`, `test_turkic`, `test_named_lists`, `test_fuzzy` (2612 - the
headline feature), `test_recursive`, `test_format`, `test_fullmatch`, `test_issue_18468`,
`test_partial`.

Suggested areas: `Various`, `Replacement`, `Captures`, `NamedLists`, `Fuzzy`, `Recursion`,
`Format`, `FullMatch`, `Partial`, `Turkic`.

## Watch for

- **`test_various` is a table, not a narrative.** It is mostly (pattern, subject, expected span)
  triples. Port it as `[Arguments(...)]` rows on a handful of tests rather than 700 individual
  methods, keeping the upstream assertion index range in the `Upstream` property. Judgement
  call: split where the *operation* differs, use arguments where only the data differs.
- **`test_fuzzy` is the reason this project exists.** Port it meticulously and do not batch it to
  a subagent. Give fuzzy tests fine-grained capability tags (`fuzzy-substitution`,
  `fuzzy-insertion`, `fuzzy-deletion`, `fuzzy-budget`, `fuzzy-bestmatch`, `fuzzy-enhancematch`,
  `fuzzy-charset`) so Phase 5 can slice them sensibly.
- **`test_copy`** (2876) is Python's copy protocol - not ported; record it in PORTMAP.md.

## Done when

Same criteria as S02, plus:

- [ ] Fuzzy tests are tagged finely enough that `docs/STATUS.md` shows a usable breakdown of
      fuzzy work rather than one large `needs:fuzzy` bucket.

---

## Closing notes (2026-08-30)

**What landed.** 40 new test files, 720 new test cases (ported upstream tests 731 -> 1451). Suite 765 -> 1485, ratchet GREEN, baseline
unchanged at 34 (correct: every new test is skipped). All 13 applicable upstream methods in lines
1741-3083 are ported; `test_copy` is not portable and every assertion dropped from inside a ported
method is listed in `docs/PORTMAP.md` with a reason. 17 new capability tags, vocabulary 32 -> 49.
Eight new areas: `Various` (524), `Fuzzy` (72), `Recursion` (32), `PartialMatching` (18),
`FullMatch` (12), `NamedLists` (10), `Captures` (8), `Format` (5), plus rows added to eight
existing areas.

**The public API was widened, by owner approval.** `test_named_lists` and the `\L<name>`
assertions inside `test_fuzzy` and `test_partial` could not be *written* against the S01 stub -
upstream passes named lists as Python keyword arguments and no parameter could carry them, so 41
assertions would not have compiled. Counting them first turned "is the stub wide enough?" into a
number, as it did in S01. See DECISIONS.md.

**`test_various` was generated, not transcribed.** It is a 524-row data table, not a narrative, so
the rows were extracted from the upstream source with `ast`, re-run through the local oracle to get
ground truth, classified by capability, and emitted as C#. Then all 524 rows were read back out of
the compiled `FuzzyRegex.Tests.dll` by reflection and compared to the oracle's output - pattern,
subject, group spec and expected values - with zero missing and zero extra. **Hand-transcribing 524
rows would have been the single largest error source in the slice**; generating them removed the
error class entirely, and the read-back is what proves the C# escaping survived. The same technique
is worth reaching for in S05 if `test_hg_bugs` turns out to be table-shaped.

**The slice's headline risk did not exist, for the second slice running.** S03's closing notes
predicted the first real surrogate data would appear here. Lines 1741-3083 contain no code point
above U+FFFF at all, measured before anything was ported. Every index copied across unchanged.
S05 is the last chance for that prediction to come true; measure the range before planning around
it.

**Three defects found by central integration that no agent saw.** Widening `Matches` broke two
existing `<see cref="...">` references in `Ported/FindAll/`, which only a whole-project build
surfaces; Sonar S4144 rejected twelve identical parse-error methods; CA1716 rejected the namespace
`...Ported.Partial` because `partial` is a reserved keyword. **Central build stays non-negotiable**
- this is the third slice in a row where it caught something agents could not.

**Two assertions were nearly lost to my own delegation brief.** The group B brief listed the API
members the agent could call and omitted `Replace` and `Groups.Count`, so the agent correctly
reported `test_issue_18468` #1 and #23 as unportable. They are portable; both are now ported. **A
delegation brief that enumerates the allowed API is a brief that can silently narrow the port** -
next time, point the agent at the source file and name the exclusions instead.

**One defect I introduced myself**, found on re-reading before review: `test_partial` #19 was
ported through the *static* `MatchAtStart`, which has no `partial` parameter, so it asserted a
non-partial match. The expected result is the same either way, which is exactly why it would have
survived. All eight call sites in the file were then audited.

**For S05.** Reuse the 49-tag vocabulary. The `test_various` generator and the DLL read-back check
are in the session scratchpad pattern, not committed - re-derive them if `test_hg_bugs` needs them.
