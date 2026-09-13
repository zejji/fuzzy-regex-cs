---
slice: S44
phase: 6
title: Sync upstream to release 2026.9.10, re-record every pinned divergence, and prove the pin against the wheel
delivers: []
---

# S44 - Upstream sync to 2026.9.10

Phase 6 opens here and nothing else in the phase may run first: the bug sweep's verdicts are
recorded against the pinned release, so syncing after them invalidates the list (S43 handover,
DECISIONS 2026-09-12). Use the `sync-upstream` skill.

## Before launch (orchestrator, not the session)

The driver cannot `pip install`. The oracle interpreter (`python`) runs regex **2026.7.19** and the
submodule is pinned at **2026.8.12**, which already disagree; both move to 2026.9.10 here. The
orchestrator installs `regex==2026.9.10` into the oracle interpreter before launching; the session's
first check is `python -c "import regex; print(regex.__version__)"` printing `2026.9.10`.
`.venvs/regex-2026.9.10` keeps its copy for probes.

## Scope

- **Bump the submodule** to the `2026.9.10` tag (never head; on 2026-09-12 head and release were
  the same commit - re-check). Confirm `upstream/regex/` at the tag is byte-identical to the PyPI
  wheel's Python sources and `upstream/src/` to the sdist's (`pip download regex==2026.9.10
  --no-binary :all:`), which is amendment 7's requirement. Diff release..head; if head carries an
  unreleased engine or parser fix, port it too, test-first, recorded in PORTMAP as "ahead of
  release, from commit X".
- **Walk the changelog delta** 2026.8.12 -> 2026.9.10 one entry at a time. Known content: the four
  memory-safety fixes for issues 611-614 (`_regex.c`, 188 lines; `_regex_core.py`, three lines) and
  four Python-API error-propagation PRs (615-618) with nothing to port. Each entry: ported
  test-first with the issue's own test as ground truth, or recorded not-applicable with the reason,
  in PORTMAP. 614 is the group-call-in-lookbehind direction fix where this port was already right
  (S30): the port's answer must not change, and the entry `reverse-group-call-direction` is DELETED
  because upstream now agrees.
- **Re-record every `Example` row in `tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs`** at the
  new version and paste the results back. The staleness alarm compares this port against
  upstream-as-it-was and cannot see upstream's change, so this step is the sync's own test and it
  fails loudly if skipped: an entry whose row no longer diverges is deleted with the fixing upstream
  commit named in the ledger. Ledger entries marked fixed upstream (10, and 614's) are re-verified
  against 2026.9.10 and closed.
- **Re-run every wave**: default list at three seeds, 6000 rows a generator, plus `fuzzy` and
  `interactions` at 99991; all control suites at two seeds. Any new divergence gets the amendment
  16 treatment, not a quick entry.
- **Refresh the Unicode transliteration** only if `_regex_unicode.c` changed (the digest pins in
  `tools/transliterate-unicode.py` say so); record the Unicode version either way.

## Verification

- Interpreter and submodule both at 2026.9.10; wheel and sdist byte-identity quoted.
- Zero `Example` rows recorded against an older version; each remark names the version.
- Waves GREEN at three seeds and the fourth; ratchet GREEN.

## Done when

- [x] Submodule at 2026.9.10, byte-identity proven, changelog delta fully accounted for in PORTMAP.
- [x] Every divergence entry re-recorded; stale ones deleted with the fixing commit; ledger closed
      where upstream fixed it.
- [x] Waves GREEN at three seeds and 99991.
- [ ] **Controls re-run - PARTLY: 89 of the 102 in `controls.json`.** 58 in pass 1 before it
      aborted, 30 in pass 2, and S42-1B in sitting 2, which fired as a hang. The 13 left are the
      5 that no longer resolve, the 7 blocked by a locked build output this session cannot unlock,
      and 1 whose mutant does not compile. All recorded under "Controls" with the command that
      finishes the job.
- [x] Ratchet GREEN, blind review (hunt: an `Example` pasted from the old interpreter; a changelog
      entry marked not-applicable that touches `_regex.c`), commit.

---

## Closing notes (2026-09-13)

**The pin is 2026.9.10, commit `7dd71c1`, which is also upstream's head** - `git -C upstream diff
2026.9.10..origin/hg` is empty, so there was no unreleased engine or parser fix to take ahead of the
release. Byte-identity against PyPI is proven and the claim is narrower than "identical": the
**wheel** matches `upstream/regex/*.py` byte for byte with no normalisation at all, and the **sdist**
matches `upstream/src/*` and `upstream/regex/*.py` only after folding line endings, because the sdist
ships LF where the wheel and our Windows checkout ship CRLF. The raw wheel match is two CRLF sources
agreeing, not a stronger result, and a future sync reporting "raw identical" for both has measured
something wrong.

**2026.8.12 was never on PyPI**, so every "before" figure here is taken against **2026.7.19**, which
is that tag byte for byte under `src/` and `regex/` (the diff is one line, `__version__`). That also
settles retrospectively that the oracle interpreter running 2026.7.19 against a 2026.8.12 submodule
was a labelling disagreement and not a behavioural one.

### The changelog delta, entry by entry

Every entry is in PORTMAP's new **Upstream sync log** section with its disposition. In short:

- **611** (`LookAroundConditional.is_empty`'s boolean precedence) - **ported**, test-first. Six
  failing tests first, in `Gaps/Engine/BackrefAndConditionalTests.cs`, all six reproducing
  2026.7.19's answers exactly before the fix. One of them is upstream's crash reaching Python:
  `(x)(?(1)(?(?=a)(b)|)|)` over `'xa'` raises `MemoryError` on 2026.7.19 and answers no matches on
  2026.9.10. **A seventh test came out of the blind review**: the six were written as one per
  `_regex_core.py` call site that consults `is_empty`, and they were not - they reached `:585`,
  `:1045` and `:3164`, and missed `:2085`, `Atomic.optimise`, where an "empty" atomic group
  collapses to its subpattern and loses its atomicity. That site is live, not theoretical:
  `(?>(?(?=b)b*|))b` over `'bbb'` answers `None` on 2026.9.10 and `(0, 3)` with the old expression
  put back. `An_atomic_group_holding_a_lookaround_conditional_keeps_its_atomicity` pins it and was
  proven to fail against the pre-fix `IsEmpty` before being kept.
- **612** (`req_pos` stale after `do_best_fuzzy_match` narrows the slice) - **ported and INERT**, and
  said so in the code. `ReqPos` is only ever assigned -1 here because `locate_required_string` is the
  Phase 7 deferral, so the line cannot change an answer today. Ported anyway so the Phase 7 slice
  inherits the fix rather than re-introducing the bug.
- **613** (the `GREEDY_REPEAT_ONE` retreat clamps) - **ported**, and measured rather than asserted.
  See "What 613 actually changed here" below.
- **614** (`build_GROUP` direction) - **nothing to port; this port was already right** (S30, S36).
  Its effect was on the oracle: re-recording turned both example rows of the `group-call-direction`
  entry green, so the entry is deleted and its two gap tests are kept as plain regression tests.
- **PRs 615-618 and the four cibuildwheel entries** - **not applicable**, with the reason recorded.
  These are 178 of the range's 188 `_regex.c` lines, and four of the functions they touch ARE ported
  (`add_repeat_guards`, `use_nodes`, `discard_unused_nodes`, `optimise_pattern`), so every hunk was
  read rather than waved through. The only non-mechanical change is `add_repeat_guards`'s return type
  going from `RE_STATUS_T` to `BOOL`, and its single caller discarded the return value before the
  change - checked at `9398a6d:23841`.

**Unicode:** `_regex_unicode.c` and `.h` are unchanged across the range, and
`tools/transliterate-unicode.py` was run anyway rather than assumed - all four hand-ported digests
still match and 253 tables come out byte-identical. Unicode stays at **17.0.0**.

### What 613 actually changed here, because "ported" and "changed something" are different claims

Three throwaway probes in `Matcher.cs`, each answering one question:

1. **Is the branch live?** Yes - a probe throwing wherever the new clamp fires was hit by exactly one
   test of 5,875, `BacktrackingVerbTests.A_skip_inside_an_atomic_group_...`, at `pos=2, limit=4,
   sliceStart=4`.
2. **Could the unclamped retreat ever succeed below the limit?** Yes - it finds a tail match at
   `pos=1`. So the clamp is not decoration.
3. **Does any answer move?** No. The 1,296-call grid agrees with 2026.9.10 row for row both before
   and after, and so do the full suite and every wave.

So the clamps are here for the reason upstream added them - an unbounded retreat that reads off the
end of the buffer in C - and not because this port answered anything differently. A future sync that
sees an answer move on this shape should read it as news, not as this fix arriving late.

**The grid is reproducible from tracked code now.** S40a built the same 1,296-call grid in a
gitignored scratch script; the script is gone and only its hang count survived, which is exactly the
unreproducible-evidence failure this skill records for negative controls.
`tools/probes/upstream-skip-in-atomic-hang.py` gained `--oracle-rows`, so the whole sweep is two
tracked commands:

```
python tools/probes/upstream-skip-in-atomic-hang.py --oracle-rows > .scratch/grid.jsonl
pwsh -File tools/run-oracle.ps1 -Rows .scratch/grid.jsonl
```

Both runs: **agree 1296, diverge 0.**

### The finding: upstream REGRESSED in 2026.9.10, and issue 613's own fix did it

The three-seed 6000-row gate changed **exactly one row of 378,000** when the pin moved - row 98050 of
seed 20260913, `partial` generator. Minimised from a wave-sized pattern to three ASCII characters and
no flags:

```python
regex.compile(r'(a+)\1x(*SKIP)b').search('aax', partial=True)
# 2026.7.19  -> (0, 3) partial, group 1 == (0, 1)      <- this port's answer
# 2026.9.10  -> (3, 3) partial, group 1 unset
```

The lost match is plainly reachable: `(a+)` takes `'aa'`, `\1` cannot match `'aa'` at 2, the repeat
**retreats** to `'a'`, `\1` matches at 1, `'x'` matches at 2, and `'b'` runs off the end of the
subject - which is what a partial match is. `b77694a`'s clamp stops that one retreat step once a
`(*SKIP)` has moved `slice_start` above the repeat.

**Proven not to be this slice's doing** before anything else: the identical row replayed against the
engine at S43's HEAD gives the same `(0, 5)` this port gives now. Upstream's side moved.

Four pieces of evidence, to the amendment 16 standard, and the third is on its own decisive:

| evidence | answer |
|---|---|
| `(*PRUNE)` instead of `(*SKIP)` - same pruning, no bound moved | `(0, 3)` on **both** releases |
| the verb deleted | `(0, 3)` on **both** releases |
| **2026.9.10 on `'aaxb'`, where the match completes** | **`(0, 4)`, group 1 `(0, 1)` - the identical retreat, taken** |
| PCRE2 10.47, run not read | `PARTIAL (0, 3)`; `MATCH (0, 4) (0, 1)` on `'aaxb'` |

Upstream 2026.9.10 contradicts itself: it takes the retreat to finish a complete match and refuses it
to report a partial one, on the same pattern one character apart. Pinned as
`Gaps/Engine/PartialMatchingTests.A_skip_does_not_block_the_repeat_retreat_a_partial_needs` with all
four controls, classified as `skip-blocks-a-repeat-retreat-partial`, ledgered as **entry 15**, not
filed. Probe: `tools/probes/upstream-skip-blocks-a-repeat-retreat.py`, with `--pcre2`.

This port carries **both** of `b77694a`'s clamps and keeps the match anyway, because S40b already
restores both slice bounds before the partial pass. So the port is not diverging by omitting
upstream's fix; it has the fix and does not have the precondition.

### The divergence list

42 example rows re-recorded against 2026.9.10 - the file holds 42, not the 40 an earlier draft of
this note said, and the blind review is what counted them - of which 37 came back byte-identical.
**The post-slice file's 42 rows all re-record byte-identically**, which is the check the procedure
exists for. Three rows gained an `anchoredScan` field the recorder did not write
when they were first drawn (S40d widened it), and those were pasted back. Two rows stopped diverging
outright - both issue 614's - so `group-call-direction` is **deleted**, its now-orphaned predicate
`OnlyDifferenceIsACaptureUpstreamLeftEmpty` with it, and the header carries the note. One entry
**added**: `skip-blocks-a-repeat-retreat-partial`. Net 14 entries.

Ledger: **entry 10 CLOSED** (fixed upstream and both clamps now ported here), **entry 15 added**, and
no other entry changed status - which is not an assumption, it is what the 37 unchanged example rows
say. In particular entry 5 is unaffected by the 613 clamps landing, which the `ExpectedDivergences`
header warned could be wrongly assumed.

### Two things the sync broke that nobody had a gate for

**1. PORTMAP's `_regex.c` line numbers.** PRs 615-618 inserted ~65 lines mid-file, so every citation
after about `:18245` slid. `tools/check-symbols.py` caught the three ranges that broke badly enough to
change which functions they cover - the `RE_CheckStack`/`RE_NodeStack` row and the scanner/splitter
row - and those are re-anchored, verified by matching the text at each endpoint. **The rest is
measured and flagged rather than silently rewritten:** 44 of PORTMAP's 141 cited ranges and 175 of its
552 single line references have moved, and repo-wide 65 of the 151 unambiguous `_regex.c:NNNN`
references are stale (the bare `:NNNN` spelling is commoner, so the real figure is in the hundreds).
Not rewritten here because it is outside a slice scoped to four changelog entries, because a
several-hundred-line mechanical diff is one a blind review cannot usefully check, and because it needs
an owner decision first: historical documents (`slices/done/`, the dated notes) record what was true
then and must NOT be rewritten, while PORTMAP, `src/` and `tests/` comments are live and should be.
**Note `check-symbols.py` is not run by the ratchet or any hook**, so nothing would have caught even
the three at commit time.

**2. `tools/run-controls.py` mutates tracked source in place.** Reviewing this slice's own `git diff`
while the control suite ran in the background showed `stack.PushSize(repeatData.Count + 1)` in
`Matcher.cs` - S28-C's deliberate bug, caught mid-run. Nothing was committed, but a `git commit -a`
during a control run commits somebody's mutation, and a diff read during one is not the slice's diff.
Rule from here: **finish the controls before reviewing the diff or committing.**

### Controls

S44 added no negative control of its own: it ports upstream fixes and re-records a list, and the
one new divergence it pins carries four in-test controls instead (the `(*PRUNE)` row, the
verb-deleted row, the completing `'aaxb'` row and the unreachable `'aaxyz'` row, all in
`PartialMatchingTests.A_skip_does_not_block_the_repeat_retreat_a_partial_needs`). What the slice
owed was the **re-run of the existing suite at two seeds**, and that is only PARTLY discharged.
Everything below re-ran against the engine being committed: the two review passes changed tests,
docs and a probe, and no `src/` file, so no control result here was invalidated by a later edit.

| pass | command | outcome |
|---|---|---|
| 1 | `python tools/run-controls.py --seeds 2` | aborts at **S31-A** - see below |
| 2 | `python tools/run-controls.py --ids <39 survivors> --seeds 2` | 30 controls, S31-D..**S42-1A**, all fired; `.scratch/controls.log` (58 from pass 1), `.scratch/controls2.log` |
| 3 | `python tools/run-controls.py --ids S42-1B,S42-1C,S42-2A..2G --seeds 2` | sitting 2; results below |

**S42-1B (`bound-not-lowered`) does not diverge - it HANGS.** `TIMEOUT the consumer did not finish
within 240s` at seed 7 and again at seed 4242. That is a control firing as loudly as a control can,
and it is also the single most expensive fact in this slice's history: the mutation is
`state.MaxErrors = fewestErrors - 1` -> `state.MaxErrors = fewestErrors` in `DoBestFuzzyMatch`,
which is exactly the edit S43's second sitting was blamed for leaving behind "with no rationale
anywhere in its notes" (DECISIONS 2026-09-13). It was not a hand edit either time. It is this
control, left applied because `run-controls.py` restores in a `finally` that a **kill** never
reaches, and both sittings were killed inside S42-1B's eight-minute timeout window. See the new
DECISIONS entry.

**S42-1C, S42-2A, S42-2B, S42-2C, S42-2D, S42-2E, S42-2F: NOT MEASURED this slice.** All seven came
back `NO REPORT`, twice, on a build error:

> error MSB3027: Could not copy ... Exceeded retry count of 10. Failed. The file is locked by:
> "FuzzyRegex.OracleTests (34428), FuzzyRegex.OracleTests (26696)"

Those two processes are S42-1B's own timed-out consumers. `subprocess.run`'s timeout kills the
`dotnet test` child and not the test host it spawned - the runner's own comment says as much about
pipes - so a hung mutant leaves a spinning test host holding
`tests/FuzzyRegex.OracleTests/bin/Debug/net10.0/FuzzyRegex.dll`, and **every later control's build
fails**. Killing a process is outside this session's permissions, so the seven stay unmeasured and
the "Done when" box is left unticked. Re-run once the two processes are gone, which is one command:

```
python tools/run-controls.py --ids S42-1C,S42-2A,S42-2B,S42-2C,S42-2D,S42-2E,S42-2F --seeds 2
```

**S42-2G (`whole-match-cost-off-trailing-insertion-arm`) is a BROKEN CONTROL, and a new finding.**
Its mutant does not compile: dropping the `TotalCost(...) <= state.MaxCost` line takes the only
`innerNode!` out of the condition, and the nullable flow analysis then fails the next dereference.

> `src/FuzzyRegex/Engine/Matcher.cs(7965,49): error CS8602: Dereference of a possibly null reference.`

It is not S44's doing - the slice edits nothing near `:7965` - so it joins S31-A, S31-B, S31-C,
S32-B and S38-A on the owed list, as a **sixth** broken site and the only one broken in a different
way: the others no longer resolve, this one resolves and produces a mutant that cannot build.
A control that cannot build measures nothing, and the runner reports it identically to a control
that measured nothing, which is the second half of the same maintenance item.

### Review

**Two blind passes, seven findings raised, seven reproduced, seven fixed. Not one was a port
defect; all seven were false claims in the slice's own prose, tests and probe** - which is the
failure mode a sync slice has, because most of what it produces is assertions about what upstream
does rather than code.

**Pass 1** (the whole diff, hunting a stale `Example` row and a wrongly-dismissed changelog entry)
raised five. Each was reproduced in this session before anything was edited:

1. **A missing test, and a false coverage claim in three places.** `Nodes.cs`, `PORTMAP.md` and the
   probe all said the six new issue-611 tests covered one call site each. They reached `:585`,
   `:1045` and `:3164`, and missed `:2085`, `Atomic.optimise`. The site is live:
   `(?>(?(?=b)b*|))b` over `'bbb'` answers `None` as shipped and `(0, 3)` with the pre-`1c90270`
   expression monkeypatched back (`.scratch/atomic-611.py`, three rows, all three flipping).
   Fixed by adding `An_atomic_group_holding_a_lookaround_conditional_keeps_its_atomicity`, which
   was **proven to fail against the old `IsEmpty`** before being kept (1 of 1 failed with the `or`
   arm restored, 17 of 17 green with it gone), plus a seventh probe case and corrected counts.
2. **`GroupCallTests.cs:183` quoted the wrong new value.** The re-recording turned `spans('g')`
   from `(2, 1)` into `(2, 5)`, not `(2, 3)`; `(2, 3)` is that same span in the oracle row's
   index/length spelling, so two conventions had been mixed in one sentence.
   `regex.compile(r'(?r)(?<g>[ab]+)(?=(?&g))b').search('abbaa').spans('g')` -> `[(2, 5), (0, 2)]`.
3. **The probe cited a test that does not exist.** `upstream-skip-in-atomic-hang.py` said the
   `--answers` grid was pinned by `BacktrackingVerbTests.The_skip_in_an_atomic_group_grid_...`;
   `grep -rn` across the repo matches only the docstring itself. Nothing pins those answers - the
   oracle sweep compares them live - and the docstring now says that.
4. **The divergence-row counts were wrong and disagreed with each other.** The file holds **42**
   rows, not 40 (`grep -c '{"generator"'`, 42 at HEAD and 42 after). The true partition is 37
   byte-identical, 3 differing only by the `anchoredScan` field, 2 that stopped diverging. Fixed in
   the ledger and here.
5. **An off-by-one line citation**: entry 15 cited `do_match`'s `text_pos` restore as `:18160`; it
   is `:18161` at this ledger's commit and `:18170` at the new pin. Both now given.

Pass 1 also cleared, with the check quoted, every hunt the slice file asked for: all 42 example
rows re-record byte-identically against live 2026.9.10; the `9398a6d..7dd71c1` `_regex.c` diff is
error-propagation only and the one real behaviour change (`decode_partial` returning `-1`) is
unreachable through a `bool partial`; the 611 port is `1c90270` exactly; **the 613 clamps are
character-for-character `b77694a` in both directions**; and `NOTICE` and the submodule agree on
`7dd71c1`.

**Pass 2** was a first pass over the delta pass 1 never saw - the new test, the probe case and the
corrected numbers - because five of the six fixes were edits to the evidence itself, which is
exactly where an unreviewed correction can introduce a fresh false claim. It found one:

6. **The corrected count contradicted itself one paragraph later** - "37 came back byte-identical"
   against "the 38 unchanged example rows" further down. 37 is right
   (`git diff | grep -c '^-.*"generator"'` is 5: 2 deleted plus 3 modified, so 42 - 5 = 37).

No critique loop: each pass ran once, over a different body of code, and the findings were
reproduced before being acted on.

### For the next slice

- **S45 (the dotted-I fold, ledger entry 7) is next** and nothing in this slice blocks it.
- The **line-reference refresh** above is an owner decision with a measurement attached; it wants a
  tool and a rule about live versus historical references, not a hand edit.
- **The control suite has not been runnable end to end for some time, and S44 found out the hard
  way.** Five sites no longer resolve - S31-A, S31-B, S31-C, S32-B, S38-A, all "the 'before' text
  does not appear after the anchor" - and `run-controls.py` **aborts** on the first, so a full run
  dies at S31-A and everything from S31 to S43 never runs. That is most of the fuzzy controls. It
  predates S44 (identical on `75e3165`) and stayed invisible because sessions select subsets: S43's
  own command is `--slices S37,...,S43`, which steps over the broken ids by accident. S44 ran the
  suite in two passes, the second with an explicit `--ids` list of the 39 survivors. **Two things
  owed, neither in this slice's scope:** re-anchor the five, and make the runner report an
  unresolvable control and continue instead of aborting, so a broken anchor costs one control rather
  than the rest of the suite. This matters before S55-S56 lean on the control machinery.
- A **lock file in `run-controls.py`** would make hazard 2 above impossible rather than a rule people
  have to remember.
- **Two processes need killing before any oracle work runs again**, and this session could not do it:
  `FuzzyRegex.OracleTests` PIDs **34428** and **26696**, S42-1B's timed-out consumers, spinning since
  22:50 on 2026-09-13 and holding `tests/FuzzyRegex.OracleTests/bin/Debug/net10.0/FuzzyRegex.dll`.
  Nothing else is blocked - `src/`, `tests/FuzzyRegex.Tests`, the ratchet and the commit all build -
  but every oracle wave and the seven unmeasured controls are, until they are gone.
- **The maintenance item on `run-controls.py` is now three things, not one**, and it has cost two
  sittings: restore on a KILL and not only on an exception (a lock file, or a restore-on-start sweep
  that reverts any control whose `after` it finds in the tree); kill the consumer's whole process
  tree on timeout rather than just the `dotnet test` child; and report an unresolvable or
  non-compiling control and carry on instead of aborting the suite. S42-1B is the specific control
  that hangs, so it is also the specific control that must never be left applied.
