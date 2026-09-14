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

- [ ] Debug build imported and proven; commands recorded.
- [ ] Discarding line found; condition and values on the minimised input quoted.
- [ ] One-line fix proven against upstream's suite; port's path explained.
- [ ] Ledger 13 rewritten; probe committed; pin widened only as far as the mechanism reaches.
- [ ] Blind review, verifier, ratchet GREEN, commit.
