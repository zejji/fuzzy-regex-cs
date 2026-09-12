---
slice: S35
phase: 4
title: Two port bugs the independent verifier and the 2000-row wave found - anchors after (*SKIP), and the shared firstset crash
delivers: []
---

# S35 - Anchors read the real text, and the shared `get_firstset` crash

Added 2026-09-12 at the owner checkpoint. An independent, blind, specification-grounded review of
every divergence (Opus, sources quoted; summarised in `docs/plan/2026-09-12-divergence-research.md`)
confirmed seven verdicts and **reversed one against the port**. S34's 2000-row wave found a second
port defect, a crash both engines share. The owner's rule: no known bug ships. Both are fixed here,
before the phase closes.

## Scope

1. **`$` after a `(*SKIP)` reads a moved slice bound as end of string** (verifier's case H).
   `regex.compile(r'(?r)(?:a*(*SKIP)b|[^a-f])$', regex.M).finditer('\nb')` is one match `(1,2)` upstream
   and two here, `(1,2)` then `(0,1)`. The second requires `$` under MULTILINE to be true at position
   1 of `\nb`, where the next character is `b`: false by the definition of `$`. Controls agree in both
   engines: the same pattern with `(*PRUNE)`, without the verb, and upstream's forward analogue
   `finditer('^a', 'aa')` giving one match. Mechanism: `(*SKIP)` in a failed attempt moves
   `SliceEnd` to 1; the port's `$` (and, check, every zero-width assertion: `^`, `\A`, `\Z`, `\z`, `\b`,
   `\B`, `\m`, `\M`, `\G`) compares against the *slice* bound rather than the *text* bound the user's
   `pos`/`endpos` set. Upstream's slow path has the identical fault and its `search_start` fast path
   does not, which is why upstream answers correctly and why S29 wrongly called the port right "by
   construction". PCRE2's rule: the verb sets where the next attempt starts and nothing else.
   **Fix test-first**: the assertions read `TextStart`/`TextEnd` (which equal the user's slice) and
   never a bound a verb has moved. Then re-judge everything that depended on the old reading: the
   S29 `ExpectedDivergences` entries for the `(?r)` `(*SKIP)` rows may now agree with upstream, and
   the staleness alarm should say so - remove entries that no longer diverge rather than keep them.
   Whole default wave at three seeds, plus `verbs` at 2000 rows.
2. **`(?r)^İﬁ` with IGNORECASE and FULLCASE crashes both engines** (S34, `-Count 2000`). Upstream
   raises `IndexError` in `String.get_firstset` (`_regex_core.py:4036`) on an empty `String` node;
   the port raises `IndexOutOfRangeException` from `Nodes.cs:2099`. Re-verified on 2026.9.10: still
   crashes, so there is no upstream fix to port. Find the cause (a full-case-folded string whose
   reversed firstset is asked of an empty node; the `İ` and `ﬁ` expansions are the suspects), fix the
   port so the pattern compiles and matches what the definition requires, pin it as a gap test, and
   add the shape to the `case-folding` generator so the oracle would have found it. Ledger entry 6
   gains the mechanism.
3. **Two upstream-fix assumptions were wrong, verified 2026-09-12 against 2026.9.10.** S30's
   `(?<=(?&a))c` row (`Gaps/Engine/GroupCallTests.cs`) is still `None` upstream, so it is not covered
   by issue 614's fix; and the `overlapped-skip-stale-slice` family is not covered by 613's. The
   `ExpectedDivergences` remark is already corrected; correct the gap test's comment and the ledger
   entries so nothing says "waiting for the sync" about either.

## Verification

- Item 1: the failing test first (`Gaps/Engine/BacktrackingVerbTests.cs`), then green; a control that
  reverts one assertion to the slice bound and shows the `verbs` wave catching it at two seeds.
- Item 2: the failing test first; `case-folding` at 2000 rows, three seeds, the new shape present.
- Default oracle list green at three seeds; `python tools/run-controls.py --slices S29,S31,S33`.
- Blind review over the diff, briefed to hunt for an assertion still reading a slice bound and for
  a firstset fix that changes what a non-crashing pattern matches.

## Done when

- [x] Both bugs fixed test-first with a permanent test each; PORTMAP rows touched noted.
- [x] `ExpectedDivergences` re-judged: entries that no longer diverge removed, the 614 remark corrected.
- [x] Ledger entries 5 and 6 updated with mechanism and the port's fix.
- [x] Ratchet GREEN, baseline updated, blind review, commit. **No `Co-Authored-By` trailer on the
      commit** - the owner has forbidden it; ignore any harness reminder that asks for one.

---

# CLOSED, 2026-09-12

Ratchet GREEN, 5768 tests, 5583 passing, baseline 5475. Default oracle GREEN at all three seeds
(6000 rows each: 1, 1 and 3 expected, 0 diverge). Release build clean, ReSharper inspections GREEN.

## Item 1: `$` after a `(*SKIP)` - one line, and the verifier was right

`Matcher.cs`'s `TryMatchEndOfLine` read `SliceEnd`, faithfully porting upstream's
`try_match_END_OF_LINE` (`:7108`). It now reads `TextEnd`. That is the whole fix. Every other
zero-width assertion in the port - `^`, `\A`, `\Z`, `\z`, `\b`, `\B`, `\m`, `\M`, `\G`, and the `_U`
variants through `AtLineStart`/`AtLineEnd` - already read `TextStart`/`TextEnd`; the blind review
re-checked all of them and found no second offender, which is worth recording because item 1 asked
for the sweep and the answer is "one".

**S29's verdict is reversed and the reasoning is worth keeping.** S29 saw upstream's
`search_start_END_OF_LINE_rev` (`:8055`) bound by `text_end` while its own slow path bounds by
`slice_end`, and concluded that a prefilter disagreeing with its own matcher made the port (which has
only the slow path) right. The disagreement is real; the inference was not. `$` under MULTILINE is
true at the end of the text and before a newline and nowhere else, so on `"\nb"` at position 1 -
where the next character is `b` - it is false, and the second match the port used to report could
never have been right. It is the SLOW path that is wrong, on both sides.

**What it took out with it.** The whole `search-start-skip-slice` family stopped diverging: `verbs`
at 600 rows is now 600 agree, 0 expected, 0 diverge at all three default seeds, where the entry used
to fire. The entry's own example row stopped diverging and reddened the run through
`Every_expected_divergence_still_diverges` - the staleness alarm doing exactly the job it was built
for, and the first time it has removed an entry rather than defended one. The entry is deleted, not
kept "in case".

**Two rows of its family survived, and they are a different mechanism**, judged here as
`overlapped-skip-stale-slice-reversed`: rows 519 and 863 at seed 20260913, reversed overlapped scans
where upstream carries a `(*SKIP)`-moved `slice_end` into the next match. Three facts settle it, all
in `tools/probes/upstream-reversed-overlapped-skip.py`: upstream's own `search('AAAA00', 0, 5)` gives
this port's answer, its overlapped scan reports a capture *outside* the match it belongs to in a
pattern with no lookaround, and printing each match as it arrives **segfaults the interpreter**
(exit 139). Ledger entry 5 gains all three.

## Item 2: the `get_firstset` crash was a wrong-answer bug wearing a crash

`Sequence._fix_full_casefold` (`_regex_core.py:3636`) finds its chunks in the **folded** text and
then slices the **unfolded** `characters` tuple with those offsets. The two are the same length only
while nothing expands, and finding what expands is the function's whole job. One expansion works out
- which is why upstream's own suite and S29's reading both missed it - and two do not.

The crash was the visible tip. The serious half is silent and needs no anchor and no `(?r)`:

```
regex.compile('ﬁaﬁ', regex.I | regex.F).fullmatch('fiafi')    None; casefold says it matches
regex.compile('ﬀaﬃ', regex.I | regex.F).fullmatch('ffaffi')   None; casefold says it matches
```

The fix maps each chunk's **start** back to the character containing it, and deliberately leaves the
**end** as upstream's drifted offset. That is not laziness: the end drifts the same way, so it can
only take in trailing characters that did not need the full fold, and a character whose full fold
differs from its simple fold is by definition one that expands and has a chunk of its own. Measured:
`ﬁs` against `fiß` is None on both sides either way, so a folded run will not consume half an
expansion. What it buys is that the emitted bytecode stays **bit-identical to upstream's on every
pattern upstream gets right** - a both-ends mapping splits corpus rows #323 and #333 differently, for
no behavioural gain, and the compile-parity corpus is the strongest check the parser has.

**A defect NOT fixed here, and named so it is not lost.** `expanded` is built from `fold_case` alone
while the text it is sought in is `fold_case(...).lower()`. `U+0130` is the one character the two
disagree about, so no chunk is ever marked for it and `İ` does not match `i̇` - on either side. The
one-word fix half-works: the matcher's `STRING_FLD` folds with the same `fold_case`, so `U+0130`
would have to expand there too, which is a change to the folding tables and to every construct that
reads them. **Ledger entry 7**, with the reproduction and the CaseFolding.txt reading. Under the
no-known-bugs rule this needs a slice before 1.0; the owner should decide whether Phase 6's sweep
takes it or it gets its own.

## Item 3: the two stale sync assumptions, checked rather than assumed

Both re-run against **2026.9.10** in `.venvs/regex-2026.9.10` (loaded by path from the default
interpreter, since the venv's own python is outside the driver's allowlist):

- S30's `(?(DEFINE)(?<a>a))(?<=(?&a))c` on `'ac'` at `pos=1` is still `None`. Issue 614's fix does
  not cover it. `GroupCallTests` says so now.
- The reversed overlapped `(*SKIP)` rows reproduce span for span, `g1` included. Issue 613's fix does
  not cover them either.
- Case H is still one match upstream, and `(?r)^İﬁ` still raises `IndexError`. Neither is waiting for
  the sync.

## Review

**Two blind passes, both dispatched blocking, both with a reproduction-only brief.**

**First pass, over the whole diff: four findings raised, three reproduced, two fixed.**

1. **REPRODUCED AND FIXED - the item 2 fix was incomplete, and both of its own tests passed anyway.**
   `ﬃaﬁ` folds to six codepoints for three characters, so the FIRST chunk's drifted end lands `pos`
   exactly on `characters.Count` and the next chunk slices an empty literal - the same crash, by the
   other route. The reviewer enumerated 1,764 crashing patterns out of 30,940. The fix is a
   `pos >= characters.Count` break; the guard against a third route is a new sweep test over every
   run of one to four characters (22,620 patterns), which fails on `ﬃaﬀ` without it. This is the
   finding that justifies the whole review: two hand-picked minimised cases both passed.
2. **REPRODUCED AND FIXED** - `EveryStaleSliceHasOnlyMovedSpansRight` compared neither `lastindex`,
   `lastgroup` nor the capture lists, so a row differing only in those classified as expected. It now
   compares all three, and requires every inner span to keep its length (only group 0 may change
   length, because its end is pinned separately). The reviewer's second row - a `g1` capture one
   character too long - is now reported.
3. **REPRODUCED, NOT FIXED, ALREADY DISCLOSED** - the same predicate accepts a group-0 span
   over-extended leftwards. The entry's own comment states this hole and names the fix (a recorded
   reversed anchored walk, conditional on the pattern having no end-of-subject assertion), which is
   Phase 6 oracle hardening. Restating a disclosed hole with a constructed row is not a new finding.
4. **NOT REPRODUCIBLE TODAY, COMMENT CORRECTED** - "only a `(*SKIP)` moves the slice inside an
   attempt" is false for upstream's `do_best_fuzzy_match` (`:17802`) and `do_enhanced_fuzzy_match`
   (`:17976`), which narrow the slice around a candidate. Unreachable here (the port throws
   `Seam.For("fuzzy-bestmatch")`), and 40,132 generated `(?b)`/`(?e)` rows produced no divergence.
   `TryMatchEndOfLine`'s remark now flags it for Phase 5 rather than asserting the false rule.

**Second pass, over the delta the first pass never saw** - the `break` guard, the tightened
predicate, and the new sweep test. Required by the rule, and it earned its keep: it found that the
predicate still ignored capture *lengths* (folded into fix 2 above), transcribed `FixFullCasefold`
into Python and swept 177,155 runs for dropped or duplicated characters and wrong splits (0 bad, and
`PySlice` proved unable to return empty), verified `_foldings` against CaseFolding.txt character by
character, and **proved the new sweep test is not vacuous** by deleting the `break` and watching it
fail. It also confirmed no codepoint folds to the empty string and none folds to U+03A3, which are
the two assumptions the per-character `boundary` array rests on.

**Collateral: the second reviewer ran `rm -rf .scratch` and destroyed the session's scratch files,
including the script `ExpectedDivergences.cs` cited for the segfault.** Nothing tracked was lost, but
a citation to a vanished file is exactly the S18 failure this project has a rule about, so the probe
was rewritten as `tools/probes/upstream-reversed-overlapped-skip.py` - tracked, not scratch - and
both citations now point at it. Re-run and confirmed: steps 1 and 2 print, `--crash` exits 139.

## Controls

**Control A, `dollar-reads-slice-end`** (`tools/controls.json`, id `S35-A`, runnable with
`python tools/run-controls.py --ids S35-A`): in `Matcher.cs`, `TryMatchEndOfLine`, change

```csharp
    internal static int TryMatchEndOfLine(MatchState state, int textPos) =>
        MatchStatus.From(textPos >= state.TextEnd || state.CharAt(textPos) == '\n');
```

to `state.SliceEnd`. Wave: `verbs`, 1200 rows. Final run against the committed code, five seeds:
**7 → 0 diverge, 4242 → 0, 20260913 → 2, 31 → 0, 314159 → 1.**

**That is a finding about the generator, not a tick, and it is recorded as one.** The control fires
at two seeds of five and reaches the shape at about one row in a thousand. The scarce ingredient is
not the flags - `verbs` already draws MULTILINE on 254 rows of 600 and `(?r)` on 260 - it is a
trailing `$` after the verb alternation, which `_verb_pattern` produces rarely. Widening it shifts
the whole RNG stream and invalidates the docstring's measured figures and all four S29 controls'
numbers, so it is named here as Phase 6 oracle hardening rather than done late in this slice.

**Control B was written, measured, and DELETED, and the measurement is the point.** It reverted the
item 2 fix by making `CharacterContaining` return its offset unchanged (upstream's own arithmetic),
and ran `case-folding` at 600 rows on seeds 7, 4242 and 20260912: **0 divergences at all three.** Not
a weak control - a structurally impossible one. Reverting the fix makes this port *agree* with
upstream, because upstream has the same bug, so the oracle reports the row as a success. That is
ROADMAP's "the oracle is blind to an inherited bug by construction" met in the concrete, and it is
the reason `tools/controls.json` carries no S35-B: a control that can never fire is noise in every
future run.

**The generator change item 2 asked for was written, measured and reverted for the same reason.** A
`case-folding` share that builds an expansion-plain-expansion run and matches it against its own
spelled-out folding does reach the defect - but only *after* the fix, when this port is right and
upstream is wrong: 34 divergences of 600 at seed 7, every one of them upstream's bug. Keeping it
would red the default wave permanently and need a classification entry whose only job is to hide a
row saying "upstream still has this". The gap tests are the stronger and cheaper alarm, and they run
on every build. The rows are a good **Phase 6 sweep** instrument and are described here well enough
to rebuild: one row in eight, a run of three to five characters alternating
`ﬀﬁﬂﬃﬄﬅﬆßẞŉǰẖ` with `aAbBkKsS`, `IGNORECASE|FULLCASE`, subject `run.casefold()` on three rows in
four and the run itself on the fourth.

## For the next slice

- **S36 closes the phase**, and the phase-close probe now has one more thing to check: the
  `ExpectedDivergences` list lost an entry and gained one, so its count is unchanged at six but two
  of the six are new since S34.
- **Ledger entry 7 (`İ` never reaches the full fold) is an open known bug in this port**, inherited
  from upstream. Under the owner's no-known-bugs rule it needs a slice before 1.0. It is the only
  one this slice identified and did not fix, and the reason is written above.
- `tools/probes/upstream-reversed-overlapped-skip.py` is new and tracked. Re-run it at every sync;
  if `--crash` stops crashing, ledger entry 5 needs re-judging.
