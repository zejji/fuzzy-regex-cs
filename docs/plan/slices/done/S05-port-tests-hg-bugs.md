---
slice: S05
phase: 1
title: Port upstream tests - hg_bugs and the tail
delivers: []
---

# S05 - Port upstream tests: regression corpus (lines 3084-4540)

Follow the `port-tests` skill. This is the last Phase 1 slice.

## Scope

`upstream/regex/tests/test_regex.py` lines 3084 to the end.

- `test_hg_bugs` (3084-4411, about 1,327 lines) - upstream's regression corpus, one assertion per
  historical bug. Repetitive and mechanical, which makes it the best candidate in the whole
  phase for a Sonnet subagent working in batches of a few hundred lines.
- `test_fuzzy_ext` (4411) - the extended fuzzy syntax, including per-error-type budgets and
  `{s<=2:[a-z]}` character-constrained errors. Not mechanical. Do this one yourself, and tag it
  with the same fuzzy vocabulary S04 established.
- `test_subscripted_captures`, `test_more_zerowidth`, `test_line_ending` - small and
  straightforward.
- `test_main` - the unittest runner entry point; not ported.

Suggested areas: `Regressions` for `test_hg_bugs` (keep the upstream bug identifier in the test
name where the source names one), then reuse the existing areas for the rest.

## Watch for

- **Batching `test_hg_bugs` needs a check, not trust.** Give each subagent a fixed line range,
  the `port-tests` skill and the tag vocabulary already in use, then spot-check its output
  against the source before accepting it. A subagent inventing a new capability tag or a wrong
  index is the likely failure, and both are cheap to catch and expensive to leave.
- Several `hg_bugs` assertions are about error *messages* and exception types for bad patterns.
  Port them against `FuzzyRegexParseException`, asserting the offset where upstream asserts a
  position.

## Done when

Same criteria as S02, plus the phase-closing checks:

- [ ] Every one of the 102 upstream test methods is now either ported or listed in PORTMAP.md as
      not ported, with a reason. Verify this by listing them, not by assuming.
- [ ] `docs/STATUS.md` shows the full ported-test count and the capability breakdown - this is
      the number Phase 2 planning works from.
- [ ] Closing notes record the measured tokens per slice for Phase 1 (from
      `docs/plan/slice-log.jsonl`), so `docs/plan/budget.json` can be recalibrated against
      evidence rather than the estimate it currently carries.

## Phase boundary

The driver stops after this slice: the pending queue is empty. The owner reviews
`docs/STATUS.md`, then authors the Phase 2 slices.


---

## Closing notes (2026-08-30)

### What landed

551 assertions read out of upstream lines 3084-4540; **527 ported**, 24 recorded in
`docs/PORTMAP.md` as deliberately not ported. 33 new test files, 517 new test cases, ratchet
GREEN at 2002 tests (34 passing, baseline 34, 0 failing).

| Upstream method | Assertions | Ported | Where |
|---|---:|---:|---|
| `test_hg_bugs` | 498 | 475 | `Ported/Regressions/` (29 files, new area) |
| `test_fuzzy_ext` | 40 | 40 | `Ported/Fuzzy/FuzzyExtendedSyntaxTests.cs` |
| `test_subscripted_captures` | 6 | 6 | `Ported/Format/SubscriptedCapturesTests.cs` |
| `test_more_zerowidth` | 5 | 5 | `Ported/ZeroWidth/MoreZeroWidthTests.cs` |
| `test_line_ending` | 2 | 1 | `Ported/Boundaries/LineEndingTests.cs` |

Plus two gap tests in `Gaps/Surrogates/`, which do not count towards parity.

Phase 1 is complete. All 102 upstream test methods are accounted for: 91 ported, 11 recorded as
not ported with a reason. Verified by listing the methods out of the Python source and matching
them against the provenance attributes present in `tests/`, not by assuming. Every one of the 527
ported assertion indices was checked to appear in a `[Property("Upstream", ...)]` attribute.

Five new capability tags: `backtracking-verbs` (34), `ascii-flag` (28), `define-groups` (16),
`posix-matching` (8), `keep-marker` (6).

### Surprises

- **The first non-BMP data in the whole port is here, and there are two sites, not one.**
  `test_hg_bugs` #373 (ZWJ family emoji) and #433-434 (U+1F63A). The scan method S03 and S04 used
  finds only the first: upstream writes the second as the name escape `\N{SMILING CAT FACE WITH
  OPEN MOUTH}`, which a search for astral literals and `\U` escapes does not see. It was caught by
  a porting agent reading its own assertion, not by the scan. Future range scans must resolve
  `\N{...}` through `unicodedata.lookup`.
- **`test_hg_bugs` #58 does not test what it looks like it tests.** `regex.WORD` is passed as
  `regex.sub`'s fourth positional argument, which is `count`, not `flags`.
- **An assertion's Python source is often not self-contained.** 57 ported assertions reference a
  variable assigned on an earlier line (`m`, `rx`, `seq`, `pattern`, `chars`). The extraction fed
  to the porting agents captured only the assertion line, so those packets were incomplete; the
  agents recovered the values from upstream themselves and flagged it. Verified afterwards that
  every recovered literal is byte-identical to upstream, including the 1343-character subject of
  Hg issue 300, whose `fuzzy_changes` assertion indexes position 1206 and so would break silently
  on a single dropped character. **Next extraction must carry the assignments into the packet.**

### What the blind review caught

Four findings, all reproduced against the oracle or the upstream source before any code changed,
all real - an unusually high survival rate for a review pass, and worth noting against the
"roughly four in five findings do not survive" figure in `docs/VERIFICATION.md`.

1. and 2. Two `(Index, Length)` tuples built by copying a Python `(start, end)` span. Both would
   have demanded wrong behaviour from a correct engine. See DECISIONS for the general rule.
3. `test_hg_bugs` #222 was given a `RightToLeft` option upstream does not pass; it is the forward
   control for #223.
4. Four control assertions tagged for the construct they are contrasted with rather than the one
   they use, which would have made the conditionals and define-groups slices un-skip tests that
   need lookahead.

No second review pass was run over the fixes: they touched only test files the reviewer had
already seen, added no public API and changed no tooling, so rule 4 of `docs/VERIFICATION.md`
does not apply. The ratchet was re-run after them and is still GREEN at 2002.

### For the next slice

- **Do not let parallel agents build the whole test project.** Nine agents shared one working
  tree; several built, saw compile errors from siblings' half-written files, and one renamed eight
  of its siblings' files to `*.bak` to get a clean build. Nothing was lost, but the coordinator
  should say up front that it fixes the build centrally. That central build then caught every
  analyzer failure the isolated builds had not: IDE0055, IDE0305, CA1826/1829/1859, S2971, S4144
  and S125 (which fires on a *comment line ending in a semicolon*).
- **`cat -A` is the only cheap way to see a backslash.** The Read tool renders `\\X` and `\X`
  identically, and the Edit tool decodes a `\uXXXX` escape to the character before it reaches
  disk. Write escape text with a Python script and check it with `cat -A`.
- U+0085, U+2028 and U+2029 are C# *source line terminators* and must be escaped inside string
  literals or the file does not compile.

### Phase 1 token cost

`docs/plan/slice-log.jsonl` is **empty**: every slice S01-S05 was run from an interactive session
rather than through `tools/run-slices.ps1`, and only the driver writes that file. So the
per-slice figure the done-criterion asks for does not exist, and no number is invented here.

What can be measured is the transcript record under
`~/.claude/projects/C--Users-<user>-source-repos-fuzzy-regex-cs/`, totalling every token
type including cache reads, which dominate:

| Session window (UTC) | Tokens | Covers |
|---|---:|---|
| 08-29 11:43-12:57 | 8.8M | pre-S01 planning |
| 08-29 12:58-15:10 | 68.7M | phase 0 / S01 |
| 08-29 14:55-16:23 | 101.6M | S01 |
| 08-29 15:34-16:21 | 23.2M | S01 |
| 08-29 16:22-17:43 | 56.6M | S01b and S02 |
| 08-29 20:38-21:52 | 51.9M | S03 |
| 08-29 22:00 - 08-30 06:45 | 73.7M | S04 |
| 08-30 06:45 onwards | this session | S05 |

Total across the project to the start of S05: **460M tokens**. Sessions do not map one-to-one onto
slices, so read this as an order of magnitude - roughly **50-100M tokens per slice** - not a
per-slice figure. `budget.json` currently rations by slice count (3/day, 12/week) with token caps
as a circuit breaker at 6e9/day; nothing here suggests those caps are wrong, and the slice caps
remain the real rationing. Recalibrating `maxSlicesPerDay` against a measured tokens-per-slice
number needs the driver to actually run a slice first.
