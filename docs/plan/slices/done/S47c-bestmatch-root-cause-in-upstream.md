---
slice: S47c
phase: 6
title: Ledger entry 13 traced to the line in upstream's C - a debug build of _regex.c, stepped, with the mechanism proven
delivers: []
---

# S47c - Where upstream's BESTMATCH loses the match

Ledger entry 13 is the one inherited defect whose mechanism is not established to the line. The
owner asked (2026-09-14) for the upstream code to be gone through step by step, installing whatever
compiler that needs, until the cause is identified and PROVEN. This slice does that and nothing else.
Its output is knowledge and evidence, not engine code: the port already answers correctly.

## Before launch (orchestrator)

A C toolchain that can build CPython extensions on this machine: Visual Studio Build Tools with the
C++ workload (`winget install Microsoft.VisualStudio.2022.BuildTools` with
`--add Microsoft.VisualStudio.Workload.VCTools`), or confirm `cl.exe` is already on the path. The
session may not install system software; it says so in STATE.md and stops if the compiler is absent.

**Done 2026-09-14 09:25 (orchestrator; the owner ran the elevated installer):** Visual Studio 2022
Build Tools at `C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools`, MSVC 14.44.35207.
`vcvars64.bat` then `cl /nologo hello.c` compiled, and the program printed `cl ok`. Python 3.14.6
headers are at `C:\Python314\Include`. Build from a shell that has called
`"C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat"`,
or let `pip`/`setuptools` find MSVC itself (it locates Build Tools through `vswhere`).

## Scope

1. **Build upstream from source, debug.** `upstream/` is the pinned 2026.9.10 checkout. Build it
   into a throwaway venv (`.venvs/regex-debug`) with `pip install -e upstream/` or the `setup.py`
   path, `-O0` / `/Od` and debug symbols, and prove the built module is the one imported (its
   `__file__`, and one deliberately added `fprintf` that appears). Record the exact commands.
2. **Instrument, then step.** Reproduce entry 13's minimised shape
   `(?b)(?:ab){e<=1}(?:\S(*SKIP)\w|\W)` over `ab.` with `partial=True`, and at least two of the five
   wave rows. Instrument `do_best_fuzzy_match` and the paths it calls (`_regex.c` around the
   `best_match` / `max_cost` / `fewest_errors` logic that S41/S42 ported) so the candidate list, each
   candidate's errors and cost, and every place a candidate is discarded print with a line number.
   Then find the discard that loses the match. Use a debugger (`windbg`/`cdb` or Visual Studio's,
   attached to `python.exe`) where prints are not enough. The finding is a line number, the
   condition at that line, and the values that made it true on this input.
3. **Prove it two ways.** (a) A one-line change at that spot in the debug build makes upstream return
   the match, with no other test in `upstream/regex/tests/test_regex.py` changing outcome (run the
   upstream suite before and after). (b) A reasoned account of why the port does not take that path,
   pointing at the ported line in `Matcher.cs`. If the one-line fix is not enough, the mechanism is
   not yet found: say so, do not declare victory.
4. **Record.** Ledger entry 13 rewritten: cause to the line, the proof, `Reproduce:` naming a
   `tools/probes/upstream-bestmatch-lost-candidate.py` that prints the instrumented trace when run
   against the debug build and the plain contradiction against a normal install. Then the pin from
   S47b is widened ONLY to the rows the mechanism explains, each named. A `docs/plan/upstream-reports/`
   draft report is prepared but NOT filed (owner rule: nothing filed until Phase 8).
5. **Time.** Two sittings are budgeted. Checkpoint with the trace so far in STATE.md; a sitting that
   ends with "not found yet, here is what was excluded" is a good sitting.

## Verification

- The debug-build reproduction and the one-line fix are both in the notes with their output.
- Upstream's own test suite result before and after the one-line fix.
- Blind review (hunt: a "cause" that is a correlate, an instrumented print misread as a discard, a fix
  that also changes another upstream test) and the verifier pass re-running the probe.

## Done when

- [x] Debug build imported and proven; commands recorded.
- [x] Discarding line found; condition and values on the minimised input quoted.
- [x] One-line fix proven against upstream's suite; port's path explained.
- [x] Ledger 13 rewritten; probe committed; pin widened only as far as the mechanism reaches.
- [x] Blind review, verifier, ratchet GREEN, commit.

---

# Closing notes (2026-09-14, one sitting)

**The mechanism is a LEAK, not a ranking rule, and it is three lines in two functions.** `(*SKIP)`
narrows `state->slice_start` at `_regex.c:14555` during the NORMAL attempt; `do_match:18170` restores
`text_pos` alone when it falls back to the partial attempt; and `do_best_fuzzy_match`'s scan loop at
`:17625` is guarded by `state->slice_start <= start_pos && start_pos <= state->slice_end`, which with
`slice_start=3` and `start_pos=0` is false, so the body never runs once and `status` keeps the
`RE_ERROR_FAILURE` it was given at `:17599`. **The partial is never attempted.** The entry's old
hypothesis named the right guard for the wrong reason - it blamed the retry's
`start_pos = state->match_pos`, and the loop is refused on its FIRST iteration, before any retry
exists.

**What makes `(?b)` look like the culprit.** `do_simple_fuzzy_match` is handed the SAME leaked
`slice=[3,3]` - the trace prints it - and answers anyway, because it has no such guard. The flag does
not filter the match out; it routes the retry through the one entry point the leaked bound can stop.
`do_enhanced_fuzzy_match` restores the slice before every return that is not a hard error (`:18003`;
the `goto error` at `:18001` skips it, and that path aborts the match anyway), which is upstream
saying in its own code that the slice is per-attempt state; `do_best_fuzzy_match` restores it only
inside `found_match && fewest_errors > 0` (`:17848`).

**This port has carried the fix since S40b and nobody knew what it was fixing.** `Matcher.cs:10098-10100`
restores `SliceStart`/`SliceEnd` beside `TextPos`, a deliberate departure from upstream's `:18170`
chosen on self-refutation grounds. That IS candidate fix A. So the reason this port never reproduced
entry 13 is now a fact with a line number rather than a coincidence.

## How the builds were made

setuptools is not installed for this interpreter, so `pip install -e upstream/` is not the path -
cl.exe is driven directly. `upstream/` is never written to; every tree is a copy under `.scratch/`,
which is gitignored.

```
python tools/probes/upstream-bestmatch-lost-candidate.py           # plain contradiction, no compiler
python tools/probes/upstream-bestmatch-lost-candidate.py --trace   # instrumented build + trace
python tools/probes/upstream-bestmatch-lost-candidate.py --fix     # stock / fix A / fix B, side by side
```

The probe copies `upstream/` to `.scratch/regex-lostcand-*`, patches `src/_regex.c` (every anchor
asserted to appear exactly once, so an upstream that has moved stops the probe instead of printing a
stale story), and builds with
`cl /nologo /Od /Zi /FS /MD /W3 /I <python include> ... /LD /Fe:<tree>\regex\_regex.cp314-win_amd64.pyd
/link /LIBPATH:<python libs> /DEBUG`, from a shell that has called
`"C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat"`.
`REGEX_VCVARS` overrides the search. `sys.path.insert(0, tree)` is what makes the built module the
one imported, and every run prints `regex._regex.__file__` to prove it. MSVC 14.44.35207, CPython
3.14.6.

Three notes for whoever does this next. `cl` prints `'vswhere.exe' is not recognized` to stderr and
exits 0 anyway - it is noise from vcvars, not a failure. `subprocess.run(["cmd", "/c", <string with
quoted paths>])` mangles the quoting; write a `.bat` and run that. And `RE_ERROR_PARTIAL` is **-13**,
so a partial returns through `if (status < 0) goto error;` - the `error:` label is the normal exit
for a partial, which reads alarmingly in the trace until you know it.

## THE CONTROL IS THE FIX EXPERIMENT, and both arms were run

No engine or generator changed, so there is no mutation control to run. The equivalent evidence is
stock-versus-fixed, and it is stronger, because it measures the actual proposed change rather than a
deliberate fault:

> **Control: stock vs fix A vs fix B**, all three built from `upstream/` at 2026.9.10 by
> `python tools/probes/upstream-bestmatch-lost-candidate.py --fix`.
> **Fix A** - in `.scratch/<tree>/src/_regex.c`, `do_match`: declare `Py_ssize_t slice_start;` and
> `Py_ssize_t slice_end;` beside `Py_ssize_t text_pos;`, save both after `text_pos = state->text_pos;`,
> and restore both after `state->text_pos = text_pos;` in the `if (status == RE_ERROR_FAILURE)` arm.
> **Fix B** - in `do_best_fuzzy_match`: declare `entry_slice_start`/`entry_slice_end`, save them
> before `init_best_list(&best_list);`, restore them before both `return status;` statements.
> **Result.** Stock: the minimised shape, its reversed twin and all five judged wave rows answer
> `None` under `(?b)`. Fix A and fix B: every one of them answers, and on all five wave rows the
> answer equals this port's answer in full - span, groups and fuzzy counts. Upstream's own
> `regex/tests/test_regex.py` is **101 run / 0 failed / 0 errors** under stock, fix A and fix B alike.
> **The one difference between the fixes:** A also reaches the non-BESTMATCH retry, so row 77937's
> FLAGLESS answer moves from `(0,7)P fuzzy=(0,0,0)` with no groups to `(0,7)P fuzzy=(1,1,1)` with
> group 2 at (0,3). B leaves the flagless path alone.

**That retires a caveat this entry has carried since S43.** It said this port's answer was upstream's
flagless answer in full on four of five rows and on 77937 only the span agreed. The flagless answer
was only ever a stand-in for "what upstream would say without the defect", and there is now a build
without the defect: under either fix, upstream WITH the flag gives this port's whole answer on all
five, 77937 included. The yardstick was wrong for that row, not the port.

## The pin was widened by exactly one row, and the row that was not added is named

`bestmatch-loses-a-partial` goes from six example rows to seven. **Row 7 is the minimised shape
REVERSED** - `(?b)(?r)(?:ab){e<=1}(?:\S(*SKIP)\w|\W)` over `'.ab'`. `RE_OP_SKIP` writes `slice_start`
going forwards (`:14555`) and `slice_end` going backwards (`:14553`), and row 7 is the MINIMISED
witness of the reversed arm, there for the same reason row 6 sits beside the forward wave rows.
Stock upstream answers `None`; both fixes answer `(0, 3)` partial with the substitution at 1, which
is this port's answer (`match 0:(0,3)[(0,3)] last=-1/- partial fuzzy=(1,0,0)[s:1][i:][d:]`).

**A first draft justified row 7 by claiming rows 1-6 were forward-only, and the blind review proved
that false.** Wave rows 1 and 2 are the two `(?r)` rows and they fire `:14553`; only rows 3-6 are
forward. `--trace` now prints which arm every pinned row fires, so the claim is measured rather than
asserted. CONDENSED below - the probe prints one `[LC]` line per write and an eighth section for row
76345 (`:14555 slice_start 0 -> 1`); run it for the literal output:

```
=== seed 7 row 74938 ===   SKIP :14553 slice_end 3 -> 1
=== seed 7 row 77937 ===   SKIP :14553 slice_end 7 -> 6 / 6 -> 5 / 5 -> 2 / 2 -> 0
=== seed 4242 row 76251 ===   SKIP :14555 slice_start 0 -> 1
=== seed 4242 row 76681 ===   SKIP :14555 slice_start 0 -> 4
=== seed 20260913 row 76593 ===   SKIP :14555 slice_start 0 -> 4
=== the minimised shape ===   SKIP :14555 slice_start 0 -> 3
=== the minimised shape reversed (pin row 7) ===   SKIP :14553 slice_end 3 -> 2 / 2 -> 1
```

The widening still stands, on the honest ground: the reversed arm had no SMALL witness, and rows 1
and 2 are several hundred characters of generated pattern each.

**Seed 20260914 row 76345 was NOT added, and the reason is recorded rather than glossed.** The
mechanism does explain it - measured, not assumed:
`(?b)(?e)\b(?:\p{Ll}(*SKIP)[^\d]|\W)(?=(?:(\p{ASCII}+)([^\d]*)a){e<=2,s<=1})` over `'aaa'`, asked the
row's own `search` from 0, is `None` on stock and the `(0, 3)` partial this port answers on both
fixed builds. **It is NOT None from every door, and a first draft said it was**: the blind review
swept all three operations by `pos` and by `endpos` and found `search` from 1, 2 or 3 answering the
empty partial at (3, 3) on stock. But the pin is keyed on the recorded question, and **that row's
recorded question is not on disk**: `TestResults/oracle/wave-20260914.jsonl` has since been
overwritten by a `case-folding` wave, and re-running `interactions` alone at seed 20260914 does NOT
redraw it (checked - 6000 rows, the pattern is not there), because the generators share one RNG
stream. Recovering it needs the full 21-generator 6000-row gate at that seed. Adding a guessed
`flags` would have been a fabricated key that classifies nothing, so it was not done. It will red a
wave that draws it again, which is the owner's 2026-09-14 ruling working as intended.

The whole seven-row block is now the recorder's own output and reproduces:
`python tools/record-oracle.py --rows tools/probes/bestmatch-loses-a-partial-rows.jsonl`. That rows
file is new and tracked - before this slice, entry 13's example rows were hand-assembled and only the
sibling entry had one.

## Numbers

Suite **5,953 passing**, 5,845 distinct ids, ratchet GREEN. Default oracle wave GREEN at all three
seeds. The seven-row file GREEN (`pwsh -File tools/run-oracle.ps1 -Rows tools/probes/bestmatch-loses-a-partial-rows.jsonl`).
No engine file changed; the diff is a probe, a rows file, the oracle pin's data and prose, the ledger
and a draft report.

## Review

**One blind pass, five findings raised, FIVE reproduced, five fixed.** That is an unusually high
survival rate - the standing figure is about one in five - and it is because this slice's output is
almost entirely CLAIMS ABOUT MEASUREMENTS, where a reviewer with the same builds can check each one
directly. The lesson is worth keeping: an investigation slice gives a blind pass more purchase than
an engine slice does, so budget for the findings rather than hoping for none.

1. **"Rows 1-6 exercised the forward arm only" was false.** `python .scratch/arms.py` against the
   `--trace` build shows rows 74938 and 77937 firing `:14553`. That was the stated justification for
   adding row 7. Fixed: the justification is now "the reversed arm had no minimised witness", the
   probe prints which arm every row fires, and the correction is recorded above and in the pin.
2. **"Stock upstream answers None at every door" for row 76345 was false.** Sweeping all three
   operations by `pos` and `endpos` gives `search pos=1..3 -> (3, 3) partial` on stock. Fixed: the
   claim is now the row's own `search` from 0, which is what was actually measured.
3. **The probe measured neither row 7 nor row 76345, while three documents cited `--fix` as where
   they were measured.** Both were measured in scratch scripts that were about to be deleted - the
   exact failure mode the verifier step exists to catch. Fixed: `EXTRA_ROWS` in the probe, printed by
   `--fix` beside the five wave rows.
4. **"`do_enhanced_fuzzy_match` restores the slice unconditionally" was false**: the `goto error` at
   `:18001` bypasses the restore at `:18003`. Fixed in all four places it was said. The argument is
   unaffected - that path aborts the whole match - but the word was wrong.
5. **`:18158` was off by one** for "saves beside `text_pos`"; `:18158` is the `partial_side` save and
   `:18159` is `text_pos`. Fixed to `:18155-18159`.

No second blind pass over the fixes: they changed prose, three line references and the probe's input
lists, all of which the verifier re-ran end to end.

**Independent verifier: 11 of 11 CONFIRMED**, re-running every probe, every quoted trace, every line
number, the suite figures and the pin's byte-identity from the commit-ready files. Two presentation
notes, both applied: the arms block above is condensed rather than literal (said so), and
`do_enhanced_fuzzy_match` has a SECOND `goto error` at `:17949` that also bypasses the restore -
it fires only when `save_fuzzy_changes` runs out of memory, so "every return that is not a hard
error" still holds. **The reviewer also reported a flaky ratchet** -
`FuzzyRecursionTests.The_stack_bound_is_still_what_catches_a_blowup_the_guard_cannot_see` went RED on
a run concurrent with an MSVC compile and passed idle. Not caused by this slice, and worth watching:
a stack-bound test is exactly the kind to be load-sensitive.
