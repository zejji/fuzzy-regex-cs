---
slice: S19
phase: 3
title: Quantifiers - greedy, lazy, the ONE fast paths, and repeat guards
delivers: [quantifiers]
---

# S19 - Quantifiers: greedy, lazy, the ONE fast paths, and repeat guards

Needs S16-S18. The biggest single behaviour of the engine: 179 tests wait on `quantifiers`, and
the guard machinery it lands is what keeps pathological patterns from exponential re-entry.

## Scope

All line references are `upstream/src/_regex.c`.

- **Main-switch cases**: `GREEDY_REPEAT` (`:13176`), `LAZY_REPEAT` (`:13557`),
  `GREEDY_REPEAT_ONE` (`:13313`), `LAZY_REPEAT_ONE` (`:13693`), `END_GREEDY_REPEAT` (`:12525`),
  `END_LAZY_REPEAT` (`:12760`).
- **Backtrack cases**: the shared `GREEDY_REPEAT_ONE`/`LAZY_REPEAT_ONE` guard block (`:15707`),
  `GREEDY_REPEAT` / `LAZY_REPEAT` (`:15778`), `GREEDY_REPEAT_ONE` (`:15815`) with its
  per-character retreat sub-switch (`:15907-16342`), `LAZY_REPEAT_ONE` (`:16445`) with its
  advance sub-switch (`:16533-17107`), `BODY_END` (`:15282`), `BODY_START` (`:15312`),
  `MATCH_BODY` (`:17177`), `MATCH_TAIL` (`:17223`), `TAIL_START` (`:17377`).
- **Guards**: `RE_GuardList` (`:320`), `insert_guard_span` (`:9296`), `delete_guard_span`
  (`:9328`), `is_guarded` (`:9340`), `guard` (`:9378`), `guard_repeat` (`:9446`), `guard_range`
  (`:9464`), `guard_repeat_range` (`:9534`), `is_repeat_guarded` (`:9559`), `reset_guard_list`
  (`:3363`), the real bodies of `push_guard_data` / `pop_guard_data` and `push_repeat_data` /
  `pop_repeat_data` / `push_repeats` / `pop_repeats` (`:2541-2589`, `:2713-2763`),
  `dealloc_repeats` (`:18630`). S15 already computed `RE_STATUS_BODY`/`RE_STATUS_TAIL` statuses
  in `add_repeat_guards`; this is where they are consulted.
- **Bulk stepping**: the `match_many_*` family for the opcodes landed so far - ANY (`:3537`),
  CHARACTER (`:3803`), PROPERTY (`:4045`), RANGE (`:4485`), SET (`:4737`) - which the
  `*_REPEAT_ONE` cases call; `node_matches_one_character` (`:3433`) and `locate_test_start`
  (`:3476`) as far as REPEAT_ONE construction needs them.
- **Timeout with teeth**: a gap test that a known-catastrophic pattern (e.g. nested quantifiers
  over a non-matching subject) raises the timeout instead of hanging - the S16 plumbing's first
  real exercise.

## Verification

- **Un-skip `needs:quantifiers`** (179 tests), reading each skip's prose first; stragglers
  retag with prose.
- **Oracle wave**: generated quantifiers - `* + ? {m,n} {m,}` in greedy and lazy forms, nested,
  over classes and groups and literals, including empty-body repeats (`(a?)*`-shaped, where the
  guards are what terminates) and repeats of capturing groups (comparing all captures, which is
  where S18's save/restore either holds or breaks). Zero divergences; negative control.
- **The prefilter contingency, named**: `locate_required_string` and the string-search family are
  deferred to Phase 7 (DECISIONS 2026-08-31). If any test un-skipped in this slice times out
  rather than fails, that deferral is the first suspect - the sanctioned response is to port
  `string_search`/`locate_required_string` (`:5231-6918`, `:11082`) into this slice and say so in
  the closing notes, not to skip the test.

## Done when

- [x] `quantifiers` delivered or stragglers retagged; counts in closing notes.
- [x] Oracle wave green including empty-body and captured-repeat probes; counts quoted.
- [x] Catastrophic-pattern timeout gap test in and proven to fail with the timeout removed.
- [x] `docs/PORTMAP.md` updated.
- [x] Ratchet GREEN, baseline updated, blind review (hunt: a guard keyed on the wrong position
      (body vs tail), `{m,n}` counts compared with the wrong inclusivity at the boundaries,
      a lazy repeat that advances past `max_count`, backtrack pops mismatched with pushes in the
      REPEAT_ONE sub-switches), commit.

## Closing notes (2026-08-31)

**What landed.** All six repeat opcodes and all eight backtrack markers, plus the guard machinery
they rest on: `Engine/GuardList.cs` is new (`RE_GuardSpan`, `RE_GuardList` and the five functions
over it, `RE_RepeatData`), `MatchState` gained `Repeats`, `ResetGuards`, `GuardRepeat`,
`GuardRepeatRange` and `IsRepeatGuarded`, and `Matcher` gained `CountOne`, `MatchesMany`,
`MatchOne`, `TryMatchOne`, `AtEnd`, `StepBy`, `CountBetween` and four push/pop pairs for upstream's
repeat state structs. The `FAILURE` backtrack case's two seams are gone: it now skips repeated
leading characters and resets the guards, as upstream does. `Seam.Tag` lost its `quantifiers` arm,
because the tag is delivered.

**Counts.** 94 `[Skip("needs:quantifiers …")]` attributes removed; 13 tests then failed, every one at
another slice's seam and none on an assertion, and all 13 were retagged with prose - 11
`needs:find-all` (they call `FuzzyRegex.Matches`, which does not exist yet) and 2 `needs:lookaround`
(`(?=abc){3}abc` and `\d{4}(\s*\w)?\W*((?!\d)\w){2}`). No fan-out method needed splitting, and
`git show HEAD:<path>` confirms no `[Arguments]` row was altered. Suite: **4239 passing, 0 failing,
1299 skipped of 5538** (from 3987/5526). Overall parity **21.8% → 34.0%**; `Quantifiers` 0% → 98.1%,
`Various` 31.7% → 54.6%, `Groups` 30.8% → 69.2%, `Possessive` 0% → 50%, `Captures` 0% → 75%.

**Oracle.** A `quantifiers` generator was added to `tools/record-oracle.py` and to
`tools/run-oracle.ps1`'s default list: greedy and lazy `* + ? {m} {m,} {m,n}` over atoms, capture
groups, non-capturing groups, nested repeats and the `(a?)*` empty-body shape, over four subject
alphabets one of which is astral. Measured at 300 rows of seed 1: 174 match, 126 no-match, 58 with an
astral subject, no parse errors, slowest row 0.05ms upstream. Full wave **agree 9000, unsupported 0,
diverge 0** across all six generators at 1500 rows each. Three negative controls fired: refusing to
retreat a greedy `*_REPEAT_ONE` to exactly its minimum, **148 of 600**; counting a lazy one up to its
maximum instead of its minimum, **54 of 600**; stepping one code unit per character in `CountOne`,
**1 of 600**. Two planned controls did *not* fire and were replaced - see DECISIONS; the short
version is that upstream's `count > max_count` check in the `LAZY_REPEAT_ONE` backtrack case is dead
in practice, and a control that hangs is not a countable one.

**Three things the next slice should know.**

1. **The slice file was wrong about `push_repeats` and the guard-data pushes**, exactly as S18's was
   wrong about `save_captures`. All ten call sites are Phase 4's (`CONDITIONAL`, `END_CONDITIONAL`,
   `GROUP_CALL`, `GROUP_RETURN`); the repeat opcodes park their own state instead. One grep settled
   it. Two slice files in a row have now claimed a Phase 4/5 helper on the strength of its name.
2. **The Phase 7 prefilters are not only a speed matter.** The guard gap test hung on `(a|a)*b`,
   where upstream had measured 0.00ms, and the port looked to have a guard bug. It has not: upstream
   is exponential on that pattern too - 23.3 *seconds* at n=26 - and its apparent speed was
   `locate_required_string` rejecting a subject with no `'b'` before the engine ran. Re-measured
   like for like, the two engines have the same curve. On the strength of the wrong diagnosis
   `try_match`'s test-node arm was ported in full and then reverted, because it fixed nothing
   measurable. DECISIONS has the numbers.
3. **A repeat count is a count of characters and our positions are code units.** `StepBy` and
   `CountBetween` are the only two places that conversion lives, and they are not upstream
   functions. Anything that reads `node.Values[1]` or `[2]` as an offset is a bug.

**Analyzers.** Nothing was disapplied and nothing was suppressed; the build is clean at
`TreatWarningsAsErrors`. One formatting rule bit once during a negative control - IDE0054 on
`pos = pos + 1` - and the control was rewritten as `++pos`, which is the rule being right.

**Review.** One blind pass over the whole diff, briefed per `docs/VERIFICATION.md` (defects only,
reproduction required, and the four failure modes this slice's own "Done when" line names). It raised
**zero findings**, so none were reproduced and none were fixed, and there was therefore no second
pass - nothing changed after the review. The reviewer independently re-ran the build, the full suite
(4239/0/1299) and three oracle waves at seeds this session had not used, then added 2,715
hand-written adversarial rows through the oracle's `-Rows` path - astral repeats, guard-heavy shapes
such as `(|a)*`, `(a??)*` and `((a*)*)*b`, and a `{m,n}` boundary matrix over lengths 0-8 - all
agreeing, and 40,000 generator rows across 200 seeds with no pattern upstream rejects and none over
5ms. It recorded one coverage limit, not a defect: a lone surrogate cannot be sent through the oracle
at all, because the recorder reads rows as strict UTF-8, and no public entry point can currently
start a match mid-pair.
**How to re-run this slice's negative controls.** Back-filled 2026-08-31 from the session's scratch
files, under the rule the `port-slice` skill now carries: a control figure nobody can reproduce is
not evidence. All three ran against the same wave - generator `quantifiers`, 600 rows, **seed 7**,
`regex` 2026.7.19 - recorded with `tools/run-oracle.ps1 -Generator quantifiers -Count 600 -Seed 7`
and replayed against the mutated build with `-SkipRecord`. Each mutation is a single edit to
`src/FuzzyRegex/Engine/Matcher.cs`; unmutated, that wave is 600 agree, 0 diverge.

| Control | Edit | Result |
|---|---|---|
| `greedy-min` - a greedy `REPEAT_ONE` that will not retreat all the way to its minimum | in the `GreedyRepeatOne` retreat loop, `if (count < node.Values[1])` becomes `if (count <= node.Values[1])` | 452 agree, **148 diverge** |
| `lazy-greedy` - a lazy `REPEAT_ONE` that counts up to its maximum instead of its minimum | in the `LazyRepeatOne` case, the `CountOne` call's `node.Values[1]` argument becomes `node.Values[2]` | 546 agree, **54 diverge** |
| `astral-step` - a repeat that counts one UTF-16 code unit per character | in `CountOne`'s loop, `pos = state.NextPos(pos);` becomes `++pos;` | 599 agree, **1 diverge** |

The `greedy-min` figure was independently reproduced by the orchestrator on 2026-08-31, at 452/148
exactly. Two earlier reconstructions from the English description alone gave 3 and 8 divergences,
which is why the four values above are now recorded rather than the number alone.
