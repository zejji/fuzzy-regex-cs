# Roadmap

Phases, in order, from the design spec section 12. This file lists **what** each phase covers and
nothing about how far along it is: status lives in `docs/STATUS.md` (generated from the test
suite) and in `docs/plan/slices/` versus `docs/plan/slices/done/`. Duplicating status here would
just give it somewhere to rot.

Slice files are authored **one phase ahead only**. A detailed plan for phase 5 written today
would be wrong by the time phase 5 arrives, for the same reason a stale TODO list is.

| Phase | Content | Sessions (est.) | Main model |
|---|---|---:|---|
| 0 | Scaffolding: solution, build props, CI, skills, driver script, budget gate, slice files for phase 1 | 1-2 | Opus |
| 1 | Port the full upstream test suite, all skipped initially | 3-6 | Sonnet under Opus |
| 2 | Parser and compiler (`_regex_core.py`), public API | 5-8 | Opus |
| 3 | VM core: literals, classes, quantifiers, groups, backrefs, anchors | 10-15 | Opus |
| 4 | Advanced: lookaround, atomic and possessive, recursion, branch reset, named lists, POSIX, partial matching | 8-12 | Opus |
| 5 | Fuzzy matching, `BESTMATCH`, `ENHANCEMATCH` | 5-8 | Opus |
| 6 | Oracle hardening, Unicode generator, gap tests | 3-5 | Opus/Sonnet |
| 7 | Benchmarks and optimisation | 5-10 | Opus |
| 8 | Docs, packaging, NuGet, 1.0 | 2-3 | Sonnet/Opus |

Roughly 42-69 slice sessions in total, at plus or minus 50%. The generated status board makes the
real rate visible within the first two phases, which is when these numbers should be revised
against evidence rather than trusted.

Fuzzy matching - the reason this port exists - is usable at the end of phase 5, about two thirds
of the way through.

## Candidates parked for later

- **Mutation testing (Stryker.NET), phase 6, scoped and on demand.** It answers "do our tests
  actually pin this behaviour?", which is worth asking of a port. But it is the wrong tool for
  most of this codebase: the differential oracle answers the same question with real ground
  truth and also finds behaviour we never tested at all, whereas mutation testing only finds
  code paths existing tests fail to pin. Stryker reruns the suite per mutant, so a 30k-line
  engine would take hours to days per run - never a merge gate. Where it would earn its keep is
  the small, bounded parts the oracle cannot reach: the public API layer (option handling,
  argument validation, `MatchTimeout`, Span overloads) and the parse-error paths. Revisit in
  phase 6 with real code and real timings; any estimate made now would be a guess. It cannot
  test the PowerShell tooling at all.

## Phase boundaries are human checkpoints

`tools/run-slices.ps1` refuses to cross one. When the pending queue empties, the driver stops and
the owner reviews progress, then authors and approves the next phase's slices. See
`docs/plan/OPERATIONS.md`.

## Where the work queue actually is

- `docs/plan/slices/` - pending, lowest number first. Listing this directory **is** the todo list.
- `docs/plan/slices/done/` - completed, with closing notes appended.
- `docs/plan/STATE.md` - current slice, blockers, next action. Thirty lines maximum, rewritten
  each session.
- `docs/plan/DECISIONS.md` - append-only dated one-liners.
